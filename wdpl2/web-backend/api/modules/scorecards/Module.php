<?php
declare(strict_types=1);

/**
 * Live scorecards.
 *
 * This is the feature that sank the previous backend. There, the desktop and
 * the server could both edit a card, so it needed an after-the-fact
 * reconciliation engine - and a lost HTTP response could leave a card in a
 * state nothing could recover.
 *
 * Here a card has exactly one owner at any instant, and ownership moves only by
 * explicit handoff:
 *
 *   (no card) --open--> live --finalise--> finalised --claim--> claimed
 *       ^                                                          |
 *       +-------------------- open again ---------------------------+
 *
 *   live       the server owns it. Only the two captains write. The app will
 *              not touch this fixture.
 *   finalised  nobody writes. It is waiting for the app to collect it.
 *   claimed    the app owns it again. The server copy is frozen and is kept
 *              only so a repeated claim can return the same answer.
 *
 * There is never a moment when both sides may write the same row, so there is
 * nothing to merge. Every failure is a refusal with a clear reason, not a
 * silent divergence.
 *
 * Within the live window two captains genuinely can write at once. That is
 * handled by a version compare-and-swap under SELECT ... FOR UPDATE - one row,
 * one lock, no merge logic.
 */
final class ScorecardsModule implements Module
{
    const STATE_LIVE      = 'live';
    const STATE_FINALISED = 'finalised';
    const STATE_CLAIMED   = 'claimed';

    public static function id(): string { return 'scorecards'; }

    public static function title(): string { return 'Live scorecards'; }

    public static function schemaVersion(): int { return 1; }

    public static function tables(): array
    {
        return [
            "CREATE TABLE IF NOT EXISTS wdpl_scorecards (
                fixture_id   CHAR(36)    NOT NULL,
                season_id    CHAR(36)    NOT NULL,
                state        VARCHAR(16) NOT NULL,
                version      INT         NOT NULL DEFAULT 1,
                frames_total INT         NOT NULL DEFAULT 0,
                opened_at    DATETIME    NOT NULL,
                finalised_at DATETIME    NULL,
                claimed_at   DATETIME    NULL,
                PRIMARY KEY (fixture_id),
                KEY idx_card_state (state),
                KEY idx_card_season (season_id, state)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

            "CREATE TABLE IF NOT EXISTS wdpl_scorecard_frames (
                fixture_id     CHAR(36)    NOT NULL,
                frame_no       INT         NOT NULL,
                is_doubles     TINYINT(1)  NOT NULL DEFAULT 0,
                home_player_id CHAR(36)    NULL,
                away_player_id CHAR(36)    NULL,
                winner         VARCHAR(8)  NOT NULL DEFAULT 'none',
                eight_ball     TINYINT(1)  NOT NULL DEFAULT 0,
                updated_at     DATETIME    NULL,
                PRIMARY KEY (fixture_id, frame_no)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
        ];
    }

    public static function actions(): array
    {
        return [
            // The app: hands cards out and takes them back.
            'open'   => ['role' => Role::Admin,   'fn' => [self::class, 'open']],
            'claim'  => ['role' => Role::Admin,   'fn' => [self::class, 'claim']],
            'reopen' => ['role' => Role::Admin,   'fn' => [self::class, 'reopen']],
            'state'  => ['role' => Role::Admin,   'fn' => [self::class, 'state']],

            // Captains: write only while the card is live, only their own match.
            'mine'     => ['role' => Role::Captain, 'fn' => [self::class, 'mine']],
            'card'     => ['role' => Role::Captain, 'fn' => [self::class, 'card']],
            'setFrame' => ['role' => Role::Captain, 'fn' => [self::class, 'setFrame']],
            'finalise' => ['role' => Role::Captain, 'fn' => [self::class, 'finalise']],
        ];
    }

    // -------------------------------------------------------------- the app

    /**
     * Hands a fixture to the server for live scoring.
     *
     * Refuses when someone else already owns the card. Re-opening a claimed
     * card is allowed - at that point the app owns it again, so there is no
     * second writer to conflict with - and starts a fresh card.
     */
    public static function open()
    {
        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');
        $frames    = Http::field('frames', []);
        if (!is_array($frames) || count($frames) === 0) {
            throw new ApiError(400, 'no_frames', 'Opening a card needs its frame list.');
        }

        $fixture = Db::one('SELECT id, season_id FROM wdpl_fixtures WHERE id = ?', [$fixtureId]);
        if ($fixture === null) {
            throw new ApiError(404, 'unknown_fixture', 'That fixture has not been published to the website.');
        }

        return Db::transaction(function () use ($fixtureId, $fixture, $frames) {
            $existing = Db::lockRow('wdpl_scorecards', 'fixture_id', $fixtureId);

            if ($existing !== null && $existing['state'] === self::STATE_LIVE) {
                throw new ApiError(409, 'already_live', 'This card is already open for live scoring.');
            }
            if ($existing !== null && $existing['state'] === self::STATE_FINALISED) {
                throw new ApiError(409, 'awaiting_claim',
                    'This card has been finalised and is waiting to be collected. Collect it before reopening.');
            }

            Db::query('DELETE FROM wdpl_scorecard_frames WHERE fixture_id = ?', [$fixtureId]);

            Db::query(
                'INSERT INTO wdpl_scorecards (fixture_id, season_id, state, version, frames_total, opened_at,
                                              finalised_at, claimed_at)
                 VALUES (?, ?, ?, 1, ?, UTC_TIMESTAMP(), NULL, NULL)
                 ON DUPLICATE KEY UPDATE state = VALUES(state), version = 1, frames_total = VALUES(frames_total),
                     opened_at = VALUES(opened_at), finalised_at = NULL, claimed_at = NULL',
                [$fixtureId, $fixture['season_id'], self::STATE_LIVE, count($frames)]
            );

            $frameNo = 0;
            foreach ($frames as $frame) {
                $frameNo++;
                Db::query(
                    'INSERT INTO wdpl_scorecard_frames (fixture_id, frame_no, is_doubles, winner)
                     VALUES (?, ?, ?, ?)',
                    [
                        $fixtureId,
                        isset($frame['frameNo']) && is_numeric($frame['frameNo']) ? (int)$frame['frameNo'] : $frameNo,
                        empty($frame['isDoubles']) ? 0 : 1,
                        'none',
                    ]
                );
            }

            return self::readCard($fixtureId);
        });
    }

    /**
     * Collects a finalised card and returns ownership to the app.
     *
     * Only a finalised card can be claimed. Claiming again returns the same
     * answer rather than failing, so a lost response is safe to retry - that is
     * the exact recovery gap the previous backend never closed.
     */
    public static function claim()
    {
        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');

        return Db::transaction(function () use ($fixtureId) {
            $card = Db::lockRow('wdpl_scorecards', 'fixture_id', $fixtureId);
            if ($card === null) {
                throw new ApiError(404, 'no_card', 'There is no card for that fixture.');
            }

            if ($card['state'] === self::STATE_LIVE) {
                throw new ApiError(409, 'still_live',
                    'This match is still being scored. It must be finalised before it can be collected.');
            }

            // Already claimed: return the same card again. Idempotent by design.
            if ($card['state'] === self::STATE_CLAIMED) {
                $result = self::readCard($fixtureId);
                $result['alreadyClaimed'] = true;
                return $result;
            }

            Db::query(
                'UPDATE wdpl_scorecards SET state = ?, claimed_at = UTC_TIMESTAMP(), version = version + 1
                 WHERE fixture_id = ?',
                [self::STATE_CLAIMED, $fixtureId]
            );

            $result = self::readCard($fixtureId);
            $result['alreadyClaimed'] = false;
            return $result;
        });
    }

    /** Puts a finalised-but-not-yet-collected card back into play. */
    public static function reopen()
    {
        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');

        return Db::transaction(function () use ($fixtureId) {
            $card = Db::lockRow('wdpl_scorecards', 'fixture_id', $fixtureId);
            if ($card === null) {
                throw new ApiError(404, 'no_card', 'There is no card for that fixture.');
            }
            if ($card['state'] === self::STATE_CLAIMED) {
                throw new ApiError(409, 'already_claimed',
                    'This card has already been collected. Correct it in the app and publish again.');
            }
            if ($card['state'] === self::STATE_LIVE) {
                return self::readCard($fixtureId);
            }

            Db::query(
                'UPDATE wdpl_scorecards SET state = ?, finalised_at = NULL, version = version + 1
                 WHERE fixture_id = ?',
                [self::STATE_LIVE, $fixtureId]
            );

            return self::readCard($fixtureId);
        });
    }

    /** Every card and who owns it, for the app's Scorecards page. */
    public static function state()
    {
        return Db::all(
            "SELECT c.fixture_id, c.state, c.version, c.frames_total,
                    c.opened_at, c.finalised_at, c.claimed_at,
                    h.name AS home_team_name, a.name AS away_team_name,
                    f.match_date,
                    SUM(CASE WHEN fr.winner = 'home' THEN 1 ELSE 0 END) AS home_score,
                    SUM(CASE WHEN fr.winner = 'away' THEN 1 ELSE 0 END) AS away_score,
                    SUM(CASE WHEN fr.winner <> 'none' THEN 1 ELSE 0 END) AS frames_played
             FROM wdpl_scorecards c
             LEFT JOIN wdpl_fixtures f ON f.id = c.fixture_id
             LEFT JOIN wdpl_teams    h ON h.id = f.home_team_id
             LEFT JOIN wdpl_teams    a ON a.id = f.away_team_id
             LEFT JOIN wdpl_scorecard_frames fr ON fr.fixture_id = c.fixture_id
             GROUP BY c.fixture_id
             ORDER BY f.match_date DESC"
        );
    }

    // ------------------------------------------------------------- captains

    /** The signed-in captain's currently live match, if any. */
    public static function mine()
    {
        $teamId = Captain::requireTeamId();

        $row = Db::one(
            'SELECT c.fixture_id
             FROM wdpl_scorecards c
             JOIN wdpl_fixtures f ON f.id = c.fixture_id
             WHERE c.state = ? AND (f.home_team_id = ? OR f.away_team_id = ?)
             ORDER BY f.match_date DESC LIMIT 1',
            [self::STATE_LIVE, $teamId, $teamId]
        );

        if ($row === null) {
            return ['fixtureId' => null];
        }
        return self::readCard((string)$row['fixture_id']);
    }

    public static function card()
    {
        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');
        self::requireCaptainPlaysIn($fixtureId);
        return self::readCard($fixtureId);
    }

    /**
     * Records one frame.
     *
     * The caller sends the version it last saw. If the card has moved on since,
     * the write is refused and the current card is returned so the captain sees
     * what actually happened rather than silently overwriting a colleague.
     */
    public static function setFrame()
    {
        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');
        $frameNo   = (int)Http::requireField('frameNo');
        $expected  = Http::field('version', null);

        if ($expected === null || !is_numeric($expected)) {
            throw new ApiError(400, 'version_required',
                'Send the version you last read, so a simultaneous edit cannot be overwritten.');
        }

        $winner = (string)Http::field('winner', 'none');
        if (!in_array($winner, ['home', 'away', 'none'], true)) {
            throw new ApiError(400, 'bad_winner', 'Winner must be home, away or none.');
        }

        $teamId = self::requireCaptainPlaysIn($fixtureId);

        return Db::transaction(function () use ($fixtureId, $frameNo, $expected, $winner, $teamId) {
            $card = Db::lockRow('wdpl_scorecards', 'fixture_id', $fixtureId);
            if ($card === null) {
                throw new ApiError(404, 'no_card', 'There is no card for that fixture.');
            }
            if ($card['state'] !== self::STATE_LIVE) {
                throw new ApiError(409, 'not_live', $card['state'] === self::STATE_CLAIMED
                    ? 'This match has been collected by the league and can no longer be changed.'
                    : 'This match has been finalised. Ask the league to reopen it if it needs changing.');
            }

            if ((int)$card['version'] !== (int)$expected) {
                $current = self::readCard($fixtureId);
                $current['conflict'] = true;
                throw new ApiConflict($current);
            }

            $frame = Db::one(
                'SELECT frame_no FROM wdpl_scorecard_frames WHERE fixture_id = ? AND frame_no = ?',
                [$fixtureId, $frameNo]
            );
            if ($frame === null) {
                throw new ApiError(404, 'no_frame', 'That frame is not part of this card.');
            }

            Db::query(
                'UPDATE wdpl_scorecard_frames
                 SET winner = ?, home_player_id = ?, away_player_id = ?, eight_ball = ?, updated_at = UTC_TIMESTAMP()
                 WHERE fixture_id = ? AND frame_no = ?',
                [
                    $winner,
                    self::optionalUuid(Http::field('homePlayerId')),
                    self::optionalUuid(Http::field('awayPlayerId')),
                    Http::field('eightBall') ? 1 : 0,
                    $fixtureId,
                    $frameNo,
                ]
            );

            Db::query('UPDATE wdpl_scorecards SET version = version + 1 WHERE fixture_id = ?', [$fixtureId]);

            return self::readCard($fixtureId);
        });
    }

    /** Ends live scoring. After this only the league can act on the card. */
    public static function finalise()
    {
        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');
        self::requireCaptainPlaysIn($fixtureId);

        return Db::transaction(function () use ($fixtureId) {
            $card = Db::lockRow('wdpl_scorecards', 'fixture_id', $fixtureId);
            if ($card === null) {
                throw new ApiError(404, 'no_card', 'There is no card for that fixture.');
            }
            if ($card['state'] === self::STATE_CLAIMED) {
                throw new ApiError(409, 'already_claimed', 'This match has already been collected by the league.');
            }
            if ($card['state'] === self::STATE_FINALISED) {
                return self::readCard($fixtureId);   // idempotent
            }

            Db::query(
                'UPDATE wdpl_scorecards SET state = ?, finalised_at = UTC_TIMESTAMP(), version = version + 1
                 WHERE fixture_id = ?',
                [self::STATE_FINALISED, $fixtureId]
            );

            return self::readCard($fixtureId);
        });
    }

    // -------------------------------------------------------------- helpers

    /**
     * Confirms the signed-in captain's team actually plays in this fixture.
     *
     * The team comes from the session, never the request, so a captain cannot
     * reach another match by sending its id.
     */
    private static function requireCaptainPlaysIn(string $fixtureId): string
    {
        $teamId = Captain::requireTeamId();

        $row = Db::one(
            'SELECT id FROM wdpl_fixtures WHERE id = ? AND (home_team_id = ? OR away_team_id = ?)',
            [$fixtureId, $teamId, $teamId]
        );

        if ($row === null) {
            // Deliberately the same answer as a fixture that does not exist:
            // a captain should not be able to probe which fixtures are live.
            throw new ApiError(404, 'not_your_match', 'That match is not one of yours.');
        }

        return $teamId;
    }

    /** @return array */
    public static function readCard(string $fixtureId): array
    {
        $card = Db::one(
            'SELECT c.fixture_id, c.state, c.version, c.frames_total, c.opened_at, c.finalised_at, c.claimed_at,
                    f.match_date, f.home_team_id, f.away_team_id,
                    h.name AS home_team_name, a.name AS away_team_name, v.name AS venue_name
             FROM wdpl_scorecards c
             LEFT JOIN wdpl_fixtures f ON f.id = c.fixture_id
             LEFT JOIN wdpl_teams    h ON h.id = f.home_team_id
             LEFT JOIN wdpl_teams    a ON a.id = f.away_team_id
             LEFT JOIN wdpl_venues   v ON v.id = f.venue_id
             WHERE c.fixture_id = ?',
            [$fixtureId]
        );

        if ($card === null) {
            throw new ApiError(404, 'no_card', 'There is no card for that fixture.');
        }

        $frames = Db::all(
            'SELECT fr.frame_no, fr.is_doubles, fr.winner, fr.eight_ball,
                    fr.home_player_id, fr.away_player_id,
                    hp.name AS home_player_name, ap.name AS away_player_name
             FROM wdpl_scorecard_frames fr
             LEFT JOIN wdpl_players hp ON hp.id = fr.home_player_id
             LEFT JOIN wdpl_players ap ON ap.id = fr.away_player_id
             WHERE fr.fixture_id = ?
             ORDER BY fr.frame_no',
            [$fixtureId]
        );

        $home = 0; $away = 0; $played = 0;
        foreach ($frames as $frame) {
            if ($frame['winner'] === 'home') { $home++; $played++; }
            elseif ($frame['winner'] === 'away') { $away++; $played++; }
        }

        $card['home_score']    = $home;
        $card['away_score']    = $away;
        $card['frames_played'] = $played;
        $card['frames']        = $frames;

        return $card;
    }

    private static function uuid($value, string $what): string
    {
        $text = is_string($value) ? trim($value) : '';
        if (!preg_match('/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/', $text)) {
            throw new ApiError(400, 'bad_id', $what . ' is not a valid identifier.');
        }
        return strtolower($text);
    }

    private static function optionalUuid($value)
    {
        if ($value === null || $value === '') {
            return null;
        }
        return self::uuid($value, 'playerId');
    }
}
