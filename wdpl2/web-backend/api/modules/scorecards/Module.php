<?php
declare(strict_types=1);

require_once __DIR__ . '/Rules.php';

/**
 * Live scorecards, playing by the league's actual rules.
 *
 * Ownership (unchanged, and the reason this rebuild exists): a card has exactly
 * one owner at a time and ownership moves only by explicit handoff.
 *
 *   (none) --open--> live --both captains finalise--> finalised --claim--> claimed
 *
 * Inside the live window the WDPL playing rules apply. They live in Rules.php,
 * tested on their own; this file is the plumbing that enforces them - who may
 * write what, under which lock, at which version.
 *
 * Writes are side-scoped: the captain's side comes from their session, never
 * from the request, so neither captain can edit the other's picks.
 */
final class ScorecardsModule implements Module
{
    const STATE_LIVE      = 'live';
    const STATE_FINALISED = 'finalised';
    const STATE_CLAIMED   = 'claimed';

    const DEFAULT_FRAMES          = 15;
    const DEFAULT_MAX_PER_PLAYER  = 3;

    public static function id(): string { return 'scorecards'; }

    public static function title(): string { return 'Live scorecards'; }

    public static function schemaVersion(): int { return 2; }

    public static function tables(): array
    {
        return [
            "CREATE TABLE IF NOT EXISTS wdpl_scorecards (
                fixture_id   CHAR(36)    NOT NULL,
                season_id    CHAR(36)    NOT NULL,
                state        VARCHAR(16) NOT NULL,
                version      INT         NOT NULL DEFAULT 1,
                frames_total INT         NOT NULL DEFAULT 0,
                max_per_player INT       NOT NULL DEFAULT 3,
                notes        TEXT        NULL,
                opened_at    DATETIME    NOT NULL,
                home_finalised_at      DATETIME NULL,
                home_finalised_version INT      NULL,
                away_finalised_at      DATETIME NULL,
                away_finalised_version INT      NULL,
                finalised_at DATETIME    NULL,
                claimed_at   DATETIME    NULL,
                PRIMARY KEY (fixture_id),
                KEY idx_card_state (state)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

            "CREATE TABLE IF NOT EXISTS wdpl_scorecard_frames (
                fixture_id         CHAR(36)     NOT NULL,
                frame_no           INT          NOT NULL,
                is_doubles         TINYINT(1)   NOT NULL DEFAULT 0,
                home_player_id     CHAR(36)     NULL,
                home_player_name   VARCHAR(190) NULL,
                home_player2_id    CHAR(36)     NULL,
                home_player2_name  VARCHAR(190) NULL,
                away_player_id     CHAR(36)     NULL,
                away_player_name   VARCHAR(190) NULL,
                away_player2_id    CHAR(36)     NULL,
                away_player2_name  VARCHAR(190) NULL,
                winner             VARCHAR(8)   NOT NULL DEFAULT 'none',
                eight_ball         TINYINT(1)   NOT NULL DEFAULT 0,
                pending_eight_by    VARCHAR(8)  NULL,
                pending_eight_value TINYINT(1)  NULL,
                eight_declined_by    VARCHAR(8) NULL,
                eight_declined_value TINYINT(1) NULL,
                eight_declined_at    DATETIME   NULL,
                updated_at         DATETIME     NULL,
                PRIMARY KEY (fixture_id, frame_no)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

            // Upgrade path from schema 1. MariaDB's IF NOT EXISTS makes each of
            // these idempotent, so a fresh install runs them harmlessly and an
            // existing deployment gains exactly the columns it lacks.
            "ALTER TABLE wdpl_scorecards
                ADD COLUMN IF NOT EXISTS max_per_player INT NOT NULL DEFAULT 3,
                ADD COLUMN IF NOT EXISTS notes TEXT NULL,
                ADD COLUMN IF NOT EXISTS home_finalised_at DATETIME NULL,
                ADD COLUMN IF NOT EXISTS home_finalised_version INT NULL,
                ADD COLUMN IF NOT EXISTS away_finalised_at DATETIME NULL,
                ADD COLUMN IF NOT EXISTS away_finalised_version INT NULL",

            "ALTER TABLE wdpl_scorecard_frames
                ADD COLUMN IF NOT EXISTS home_player_name VARCHAR(190) NULL,
                ADD COLUMN IF NOT EXISTS home_player2_id CHAR(36) NULL,
                ADD COLUMN IF NOT EXISTS home_player2_name VARCHAR(190) NULL,
                ADD COLUMN IF NOT EXISTS away_player_name VARCHAR(190) NULL,
                ADD COLUMN IF NOT EXISTS away_player2_id CHAR(36) NULL,
                ADD COLUMN IF NOT EXISTS away_player2_name VARCHAR(190) NULL,
                ADD COLUMN IF NOT EXISTS pending_eight_by VARCHAR(8) NULL,
                ADD COLUMN IF NOT EXISTS pending_eight_value TINYINT(1) NULL,
                ADD COLUMN IF NOT EXISTS eight_declined_by VARCHAR(8) NULL,
                ADD COLUMN IF NOT EXISTS eight_declined_value TINYINT(1) NULL,
                ADD COLUMN IF NOT EXISTS eight_declined_at DATETIME NULL",
        ];
    }

    public static function actions(): array
    {
        return [
            'open'   => ['role' => Role::Admin,   'fn' => [self::class, 'open']],
            'claim'  => ['role' => Role::Admin,   'fn' => [self::class, 'claim']],
            'reopen' => ['role' => Role::Admin,   'fn' => [self::class, 'reopen']],
            'state'  => ['role' => Role::Admin,   'fn' => [self::class, 'state']],

            'mine'     => ['role' => Role::Captain, 'fn' => [self::class, 'mine']],
            'card'     => ['role' => Role::Captain, 'fn' => [self::class, 'card']],
            'roster'   => ['role' => Role::Captain, 'fn' => [self::class, 'roster']],
            'apply'    => ['role' => Role::Captain, 'fn' => [self::class, 'apply']],
            'finalise' => ['role' => Role::Captain, 'fn' => [self::class, 'finalise']],
            'solo'     => ['role' => Role::Captain, 'fn' => [self::class, 'solo']],
        ];
    }

    // ---------------------------------------------------------------- the app

    public static function open()
    {
        $fixtureId    = self::uuid(Http::requireField('fixtureId'), 'fixtureId');
        $framesTotal  = (int)Http::field('framesTotal', self::DEFAULT_FRAMES);
        $maxPerPlayer = (int)Http::field('maxPerPlayer', self::DEFAULT_MAX_PER_PLAYER);
        $doubles      = Http::field('doublesFrames', []);

        if ($framesTotal < 1 || $framesTotal > 50) {
            throw new ApiError(400, 'bad_frames', 'A match must have between 1 and 50 frames.');
        }

        $fixture = Db::one('SELECT id, season_id FROM wdpl_fixtures WHERE id = ?', [$fixtureId]);
        if ($fixture === null) {
            throw new ApiError(404, 'unknown_fixture', 'That fixture has not been published to the website.');
        }

        $doublesSet = [];
        if (is_array($doubles)) {
            foreach ($doubles as $n) {
                if (is_numeric($n)) { $doublesSet[(int)$n] = true; }
            }
        }

        return Db::transaction(function () use ($fixtureId, $fixture, $framesTotal, $maxPerPlayer, $doublesSet) {
            $existing = Db::lockRow('wdpl_scorecards', 'fixture_id', $fixtureId);

            if ($existing !== null && $existing['state'] === self::STATE_LIVE) {
                throw new ApiError(409, 'already_live', 'This card is already open for live scoring.');
            }
            if ($existing !== null && $existing['state'] === self::STATE_FINALISED) {
                throw new ApiError(409, 'awaiting_claim',
                    'Both captains have signed this card off and it is waiting to be collected.');
            }

            Db::query('DELETE FROM wdpl_scorecard_frames WHERE fixture_id = ?', [$fixtureId]);

            Db::query(
                'INSERT INTO wdpl_scorecards
                    (fixture_id, season_id, state, version, frames_total, max_per_player, notes, opened_at,
                     home_finalised_at, home_finalised_version, away_finalised_at, away_finalised_version,
                     finalised_at, claimed_at)
                 VALUES (?, ?, ?, 1, ?, ?, NULL, UTC_TIMESTAMP(), NULL, NULL, NULL, NULL, NULL, NULL)
                 ON DUPLICATE KEY UPDATE state = VALUES(state), version = 1, frames_total = VALUES(frames_total),
                     max_per_player = VALUES(max_per_player), notes = NULL, opened_at = VALUES(opened_at),
                     home_finalised_at = NULL, home_finalised_version = NULL,
                     away_finalised_at = NULL, away_finalised_version = NULL,
                     finalised_at = NULL, claimed_at = NULL',
                [$fixtureId, $fixture['season_id'], self::STATE_LIVE, $framesTotal,
                 $maxPerPlayer > 0 ? $maxPerPlayer : self::DEFAULT_MAX_PER_PLAYER]
            );

            for ($n = 1; $n <= $framesTotal; $n++) {
                Db::query(
                    'INSERT INTO wdpl_scorecard_frames (fixture_id, frame_no, is_doubles, winner)
                     VALUES (?, ?, ?, ?)',
                    [$fixtureId, $n, isset($doublesSet[$n]) ? 1 : 0, 'none']
                );
            }

            return self::readCard($fixtureId, null);
        });
    }

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
                    'This match is still being scored. Both captains must sign it off first.');
            }

            if ($card['state'] === self::STATE_CLAIMED) {
                $result = self::readCard($fixtureId, null);
                $result['alreadyClaimed'] = true;
                return $result;
            }

            Db::query(
                'UPDATE wdpl_scorecards SET state = ?, claimed_at = UTC_TIMESTAMP(), version = version + 1
                 WHERE fixture_id = ?',
                [self::STATE_CLAIMED, $fixtureId]
            );

            $result = self::readCard($fixtureId, null);
            $result['alreadyClaimed'] = false;
            return $result;
        });
    }

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
                return self::readCard($fixtureId, null);
            }

            // Reopening undoes both sign-offs: the captains agreed to the card
            // as it stood, and it is about to change.
            Db::query(
                'UPDATE wdpl_scorecards
                    SET state = ?, finalised_at = NULL, version = version + 1,
                        home_finalised_at = NULL, home_finalised_version = NULL,
                        away_finalised_at = NULL, away_finalised_version = NULL
                  WHERE fixture_id = ?',
                [self::STATE_LIVE, $fixtureId]
            );

            return self::readCard($fixtureId, null);
        });
    }

    public static function state()
    {
        return Db::all(
            "SELECT c.fixture_id, c.state, c.version, c.frames_total,
                    c.opened_at, c.finalised_at, c.claimed_at,
                    c.home_finalised_at, c.away_finalised_at,
                    h.name AS home_team_name, a.name AS away_team_name, f.match_date,
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

    // --------------------------------------------------------------- captains

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
            return ['fixture_id' => null];
        }
        return self::readCard((string)$row['fixture_id'], self::sideFor((string)$row['fixture_id'], $teamId));
    }

    public static function card()
    {
        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');
        $side = self::requireSide($fixtureId);
        return self::readCard($fixtureId, $side);
    }

    /**
     * Both teams' players for a match, grouped by side.
     *
     * Both are returned, not just the caller's own, because solo mode has one
     * captain recording the other team's players too. A team sheet is not a
     * secret - the opposing captain watches each pick appear anyway - and this
     * is scoped to a fixture the caller actually plays in.
     */
    public static function roster()
    {
        $fixtureId = Http::field('fixtureId', null);

        if ($fixtureId === null || $fixtureId === '') {
            // No fixture named: just the caller's own squad.
            $teamId = Captain::requireTeamId();
            return [
                'yourSide' => null,
                'home'     => self::playersOf($teamId),
                'away'     => [],
            ];
        }

        $fixtureId = self::uuid($fixtureId, 'fixtureId');
        $side = self::requireSide($fixtureId);

        $fixture = Db::one(
            'SELECT home_team_id, away_team_id FROM wdpl_fixtures WHERE id = ?',
            [$fixtureId]
        );

        return [
            'yourSide' => $side,
            'home'     => self::playersOf((string)$fixture['home_team_id']),
            'away'     => self::playersOf((string)$fixture['away_team_id']),
        ];
    }

    private static function playersOf(string $teamId): array
    {
        return Db::all(
            'SELECT id, name FROM wdpl_players WHERE team_id = ? AND is_active = 1 ORDER BY name',
            [$teamId]
        );
    }

    /**
     * Applies a batch of edits.
     *
     * Batched because a captain on a weak pub connection may queue several taps
     * before anything reaches the server, and because one version check should
     * cover the whole batch rather than each tap racing separately.
     *
     * Individual edits that break a playing rule are refused and reported in
     * `rejections`; the rest of the batch still applies. A captain who picks an
     * ineligible player should not lose the frame result they tapped first.
     */
    public static function apply()
    {
        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');
        $expected  = Http::field('version', null);
        $ops       = Http::field('ops', []);

        if ($expected === null || !is_numeric($expected)) {
            throw new ApiError(400, 'version_required',
                'Send the version you last read, so a simultaneous edit cannot be overwritten.');
        }
        if (!is_array($ops)) {
            throw new ApiError(400, 'bad_ops', 'ops must be a list.');
        }

        $side = self::requireSide($fixtureId);

        return Db::transaction(function () use ($fixtureId, $side, $expected, $ops) {
            $card = Db::lockRow('wdpl_scorecards', 'fixture_id', $fixtureId);
            if ($card === null) {
                throw new ApiError(404, 'no_card', 'There is no card for that fixture.');
            }
            if ($card['state'] !== self::STATE_LIVE) {
                throw new ApiError(409, 'not_live', $card['state'] === self::STATE_CLAIMED
                    ? 'This match has been collected by the league and can no longer be changed.'
                    : 'Both captains have signed this card off. Ask the league to reopen it.');
            }
            if ((int)$card['version'] !== (int)$expected) {
                $current = self::readCard($fixtureId, $side);
                throw new ApiConflict($current, 'Someone else updated this card first.');
            }

            $frames = self::loadFrames($fixtureId);
            $maxPerPlayer = (int)$card['max_per_player'];
            $rejections = [];
            $changed = false;
            $notes = $card['notes'];

            foreach ($ops as $index => $op) {
                if (!is_array($op) || empty($op['kind'])) {
                    continue;
                }
                $result = self::applyOne($op, $side, $frames, $notes, $maxPerPlayer);

                if ($result['rejected'] !== null) {
                    $rejections[] = ['op' => $index, 'reason' => $result['rejected']]
                                  + (isset($result['frame']) ? ['frame' => $result['frame']] : []);
                    continue;
                }
                if ($result['changed']) {
                    $changed = true;
                }
            }

            if ($changed) {
                self::saveFrames($fixtureId, $frames);
                // Any change invalidates both sign-offs: the captains agreed to
                // a card that no longer exists, so both must look again.
                Db::query(
                    'UPDATE wdpl_scorecards
                        SET version = version + 1, notes = ?,
                            home_finalised_at = NULL, home_finalised_version = NULL,
                            away_finalised_at = NULL, away_finalised_version = NULL
                      WHERE fixture_id = ?',
                    [$notes, $fixtureId]
                );
            }

            $current = self::readCard($fixtureId, $side);
            $current['rejections'] = $rejections;
            return $current;
        });
    }

    /**
     * Applies one operation to the in-memory frame list.
     *
     * @return array{rejected: string|null, changed: bool, frame?: int}
     */
    private static function applyOne(array $op, string $side, array &$frames, &$notes, int $maxPerPlayer): array
    {
        $kind = (string)$op['kind'];

        if ($kind === 'set_notes') {
            $notes = isset($op['value']) ? (string)$op['value'] : '';
            return ['rejected' => null, 'changed' => true];
        }

        $index = isset($op['frame']) ? (int)$op['frame'] : -1;
        if ($index < 0 || $index >= count($frames)) {
            return ['rejected' => null, 'changed' => false];
        }
        $frameNo = (int)$frames[$index]['frame_no'];

        switch ($kind) {
            case 'set_player':
                $slot = isset($op['slot']) ? (string)$op['slot'] : '';
                if (!in_array($slot, ScorecardRules::slotsFor($side), true)) {
                    return ['rejected' => 'You can only pick players for your own team.',
                            'changed' => false, 'frame' => $frameNo];
                }

                $playerId   = isset($op['playerId']) && $op['playerId'] !== '' ? (string)$op['playerId'] : null;
                $playerName = isset($op['playerName']) && $op['playerName'] !== '' ? (string)$op['playerName'] : null;
                $clearing   = ($playerId === null && $playerName === null);

                if (!$clearing && $side === 'away' && ScorecardRules::awaySlotLocked($frames, $index)) {
                    return ['rejected' => ScorecardRules::awayLockReason($frames, $index),
                            'changed' => false, 'frame' => $frameNo];
                }

                $reason = ScorecardRules::rejectPick($frames, $index, $slot, $playerId, $playerName, $maxPerPlayer);
                if ($reason !== null) {
                    return ['rejected' => $reason, 'changed' => false, 'frame' => $frameNo];
                }

                $prefix = ScorecardRules::slotPrefix($slot);
                $frames[$index][$prefix . '_id']   = $playerId;
                $frames[$index][$prefix . '_name'] = $playerName;
                return ['rejected' => null, 'changed' => true];

            case 'set_doubles':
                // Either captain may set this: it changes how both sides pick.
                $on = !empty($op['value']);
                $frames[$index]['is_doubles'] = $on ? 1 : 0;
                if (!$on) {
                    foreach (['home_player2', 'away_player2'] as $prefix) {
                        $frames[$index][$prefix . '_id'] = null;
                        $frames[$index][$prefix . '_name'] = null;
                    }
                }
                return ['rejected' => null, 'changed' => true];

            case 'set_winner':
                $value = isset($op['value']) ? $op['value'] : null;
                if ($value !== 'home' && $value !== 'away' && $value !== null) {
                    return ['rejected' => 'Winner must be home, away or none.',
                            'changed' => false, 'frame' => $frameNo];
                }
                // Tapping the current winner again clears the frame, which is
                // how a captain undoes a mis-tap.
                $clear = ($value === null) || ($frames[$index]['winner'] === $value);

                // A result needs the players it belongs to. Clearing is always
                // allowed - a captain must be able to undo a mis-tap even on a
                // frame that is not fully filled in yet.
                if (!$clear && !ScorecardRules::frameHasPlayers($frames[$index])) {
                    $missing = ScorecardRules::missingPlayers($frames[$index]);
                    return ['rejected' => 'Pick the players first - still to name: '
                                        . implode(' and ', $missing) . '.',
                            'changed' => false, 'frame' => $frameNo];
                }
                $frames[$index]['winner'] = $clear ? 'none' : $value;
                if ($clear) {
                    $frames[$index]['eight_ball'] = 0;
                }
                if ($clear || $frames[$index]['winner'] === 'none') {
                    self::clearEightNegotiation($frames[$index]);
                }
                return ['rejected' => null, 'changed' => true];

            case 'propose_eight':
                if ($frames[$index]['winner'] === 'none') {
                    return ['rejected' => 'Record who won the frame before claiming an 8-ball.',
                            'changed' => false, 'frame' => $frameNo];
                }
                $value = !empty($op['value']);
                if ($value === (bool)$frames[$index]['eight_ball']) {
                    return ['rejected' => null, 'changed' => false];   // already agreed
                }
                $frames[$index]['pending_eight_by']    = $side;
                $frames[$index]['pending_eight_value'] = $value ? 1 : 0;
                $frames[$index]['eight_declined_by']    = null;
                $frames[$index]['eight_declined_value'] = null;
                $frames[$index]['eight_declined_at']    = null;
                return ['rejected' => null, 'changed' => true];

            case 'withdraw_eight':
                // Only the captain who made the claim can take it back. The
                // previous scorecard had no way to do this - it tried to fake
                // one by re-proposing, which the server treats as a no-op, so a
                // mistaken claim just sat there until the other captain acted.
                if ($frames[$index]['pending_eight_by'] !== $side) {
                    return ['rejected' => null, 'changed' => false];
                }
                self::clearEightNegotiation($frames[$index]);
                return ['rejected' => null, 'changed' => true];

            case 'agree_eight':
                $by = $frames[$index]['pending_eight_by'];
                if ($by === null || $by === $side) {
                    // Only the other captain can confirm - that is the point.
                    return ['rejected' => null, 'changed' => false];
                }
                $frames[$index]['eight_ball'] = !empty($frames[$index]['pending_eight_value']) ? 1 : 0;
                self::clearEightNegotiation($frames[$index]);
                return ['rejected' => null, 'changed' => true];

            case 'decline_eight':
                $by = $frames[$index]['pending_eight_by'];
                if ($by === null || $by === $side) {
                    return ['rejected' => null, 'changed' => false];
                }
                // Recorded so the proposing captain sees the disagreement. The
                // score is unaffected either way - an 8-ball is a statistic.
                $frames[$index]['eight_declined_by']    = $side;
                $frames[$index]['eight_declined_value'] = $frames[$index]['pending_eight_value'];
                $frames[$index]['eight_declined_at']    = gmdate('Y-m-d H:i:s');
                $frames[$index]['pending_eight_by']     = null;
                $frames[$index]['pending_eight_value']  = null;
                return ['rejected' => null, 'changed' => true];
        }

        return ['rejected' => null, 'changed' => false];
    }

    private static function clearEightNegotiation(array &$frame): void
    {
        $frame['pending_eight_by']     = null;
        $frame['pending_eight_value']  = null;
        $frame['eight_declined_by']    = null;
        $frame['eight_declined_value'] = null;
        $frame['eight_declined_at']    = null;
    }

    /**
     * Signs the card off for this captain's side.
     *
     * Both sides must sign before the card leaves the captains' hands. That is
     * the online equivalent of both captains signing the paper card, and it
     * means a disagreement surfaces on the night rather than weeks later.
     */
    public static function finalise()
    {
        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');
        $side = self::requireSide($fixtureId);

        return Db::transaction(function () use ($fixtureId, $side) {
            $card = Db::lockRow('wdpl_scorecards', 'fixture_id', $fixtureId);
            if ($card === null) {
                throw new ApiError(404, 'no_card', 'There is no card for that fixture.');
            }
            if ($card['state'] === self::STATE_CLAIMED) {
                throw new ApiError(409, 'already_claimed', 'This match has already been collected by the league.');
            }

            $frames = self::loadFrames($fixtureId);
            if (!ScorecardRules::allFramesScored($frames)) {
                $missing = ScorecardRules::unscoredFrames($frames);
                throw new ApiError(400, 'not_all_scored',
                    'Every frame needs a result first. Still to score: ' . implode(', ', $missing) . '.');
            }

            $column = $side === 'home' ? 'home' : 'away';
            Db::query(
                'UPDATE wdpl_scorecards
                    SET ' . $column . '_finalised_at = UTC_TIMESTAMP(),
                        ' . $column . '_finalised_version = version
                  WHERE fixture_id = ?',
                [$fixtureId]
            );

            // Once both captains have signed, the card is frozen and waits for
            // the league to collect it.
            $after = Db::one('SELECT home_finalised_version, away_finalised_version FROM wdpl_scorecards WHERE fixture_id = ?', [$fixtureId]);
            $bothSigned = $after !== null
                && $after['home_finalised_version'] !== null
                && $after['away_finalised_version'] !== null;

            if ($bothSigned) {
                Db::query(
                    'UPDATE wdpl_scorecards SET state = ?, finalised_at = UTC_TIMESTAMP(), version = version + 1
                      WHERE fixture_id = ?',
                    [self::STATE_FINALISED, $fixtureId]
                );
            }

            $result = self::readCard($fixtureId, $side);
            $result['bothSigned'] = $bothSigned;
            return $result;
        });
    }

    /**
     * Submits a whole card captured on one phone, covering both sides.
     *
     * Venues with no usable signal are why this exists: rather than two
     * captains failing to sync, one fills the card in with the other watching,
     * and uploads it once as an agreed result.
     *
     * Which rules still apply, and why:
     *
     *  - Selection rules DO apply - max frames per player, no repeat pairings,
     *    nobody on both sides of a frame. Those are competition rules and hold
     *    however the card was captured.
     *  - Nomination order does NOT. Its whole purpose is stopping each captain
     *    seeing the other's picks before committing to their own, and one
     *    person filling both sides has already set that aside by agreement.
     *
     * Both sides are marked signed, because the submitting captain asserts the
     * other agreed. Which captain submitted it is recorded in the notes, so the
     * league can see this was a solo capture and not two independent sign-offs.
     */
    public static function solo()
    {
        Http::setMaxBytes(512 * 1024);

        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');
        $incoming  = Http::field('frames', []);
        $notes     = (string)Http::field('notes', '');

        if (!is_array($incoming) || count($incoming) === 0) {
            throw new ApiError(400, 'no_frames', 'A solo submission needs the whole card.');
        }

        $side = self::requireSide($fixtureId);

        return Db::transaction(function () use ($fixtureId, $side, $incoming, $notes) {
            $card = Db::lockRow('wdpl_scorecards', 'fixture_id', $fixtureId);
            if ($card === null) {
                throw new ApiError(404, 'no_card', 'There is no card for that fixture.');
            }
            if ($card['state'] === self::STATE_CLAIMED) {
                throw new ApiError(409, 'already_claimed', 'This match has already been collected by the league.');
            }

            $frames = self::loadFrames($fixtureId);
            $byNumber = array();
            foreach ($frames as $i => $frame) {
                $byNumber[(int)$frame['frame_no']] = $i;
            }

            // Apply every incoming frame first, then validate the finished card
            // as a whole: a half-applied swap can look invalid in mid-flight.
            foreach ($incoming as $row) {
                if (!is_array($row) || !isset($row['frame_no'])) {
                    continue;
                }
                $number = (int)$row['frame_no'];
                if (!isset($byNumber[$number])) {
                    continue;
                }
                $i = $byNumber[$number];

                $winner = isset($row['winner']) ? (string)$row['winner'] : 'none';
                if (!in_array($winner, array('home', 'away', 'none'), true)) {
                    $winner = 'none';
                }

                $frames[$i]['is_doubles'] = empty($row['is_doubles']) ? 0 : 1;
                $frames[$i]['winner']     = $winner;
                $frames[$i]['eight_ball'] = ($winner !== 'none' && !empty($row['eight_ball'])) ? 1 : 0;

                foreach (array('home', 'home2', 'away', 'away2') as $slot) {
                    $prefix = ScorecardRules::slotPrefix($slot);
                    $idKey   = $prefix . '_id';
                    $nameKey = $prefix . '_name';
                    $frames[$i][$idKey]   = (isset($row[$idKey])   && $row[$idKey]   !== '') ? (string)$row[$idKey]   : null;
                    $frames[$i][$nameKey] = (isset($row[$nameKey]) && $row[$nameKey] !== '') ? (string)$row[$nameKey] : null;
                }

                self::clearEightNegotiation($frames[$i]);
            }

            // Competition rules, checked against the completed card.
            $maxPerPlayer = (int)$card['max_per_player'];
            $problems = array();
            foreach ($frames as $i => $frame) {
                foreach (array('home', 'home2', 'away', 'away2') as $slot) {
                    $prefix = ScorecardRules::slotPrefix($slot);
                    $id   = $frame[$prefix . '_id'];
                    $name = $frame[$prefix . '_name'];
                    if ($id === null && $name === null) {
                        continue;
                    }
                    $reason = ScorecardRules::rejectPick($frames, $i, $slot, $id, $name, $maxPerPlayer);
                    if ($reason !== null) {
                        $problems[] = 'Frame ' . $frame['frame_no'] . ': ' . $reason;
                    }
                }
            }
            if (count($problems) > 0) {
                $extra = count($problems) > 3 ? ' (and ' . (count($problems) - 3) . ' more)' : '';
                throw new ApiError(409, 'rule_violation',
                    'This card breaks the match rules, so nothing was saved. '
                    . implode(' ', array_slice($problems, 0, 3)) . $extra);
            }

            if (!ScorecardRules::allFramesScored($frames)) {
                $missing = ScorecardRules::unscoredFrames($frames);
                throw new ApiError(400, 'not_all_scored',
                    'Every frame needs a result. Still to score: ' . implode(', ', $missing) . '.');
            }

            self::saveFrames($fixtureId, $frames);

            $stamp = ($notes === '' ? '' : $notes . "\n")
                   . '[Submitted from one device by the ' . $side . ' captain.]';

            Db::query(
                'UPDATE wdpl_scorecards
                    SET state = ?, notes = ?, version = version + 1,
                        finalised_at = UTC_TIMESTAMP(),
                        home_finalised_at = UTC_TIMESTAMP(), home_finalised_version = version + 1,
                        away_finalised_at = UTC_TIMESTAMP(), away_finalised_version = version + 1
                  WHERE fixture_id = ?',
                array(self::STATE_FINALISED, $stamp, $fixtureId)
            );

            $result = self::readCard($fixtureId, $side);
            $result['solo'] = true;
            return $result;
        });
    }

    // ---------------------------------------------------------------- reading

    public static function readCard(string $fixtureId, $side): array
    {
        $card = Db::one(
            'SELECT c.fixture_id, c.state, c.version, c.frames_total, c.max_per_player, c.notes,
                    c.opened_at, c.finalised_at, c.claimed_at,
                    c.home_finalised_at, c.away_finalised_at,
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

        $frames = self::loadFrames($fixtureId);
        $score  = ScorecardRules::score($frames);

        $card['frames']        = $frames;
        $card['home_score']    = $score['home'];
        $card['away_score']    = $score['away'];
        $card['frames_played'] = $score['played'];
        $card['your_side']     = $side;
        $card['home_signed']   = $card['home_finalised_at'] !== null;
        $card['away_signed']   = $card['away_finalised_at'] !== null;
        $card['away_locked']   = ScorecardRules::blindWindowLocked($frames);
        $card['all_scored']    = ScorecardRules::allFramesScored($frames);
        $card['unscored']      = ScorecardRules::unscoredFrames($frames);

        return $card;
    }

    private static function loadFrames(string $fixtureId): array
    {
        return Db::all(
            'SELECT frame_no, is_doubles, winner, eight_ball,
                    home_player_id, home_player_name, home_player2_id, home_player2_name,
                    away_player_id, away_player_name, away_player2_id, away_player2_name,
                    pending_eight_by, pending_eight_value,
                    eight_declined_by, eight_declined_value, eight_declined_at
             FROM wdpl_scorecard_frames WHERE fixture_id = ? ORDER BY frame_no',
            [$fixtureId]
        );
    }

    private static function saveFrames(string $fixtureId, array $frames): void
    {
        foreach ($frames as $frame) {
            Db::query(
                'UPDATE wdpl_scorecard_frames
                    SET is_doubles = ?, winner = ?, eight_ball = ?,
                        home_player_id = ?, home_player_name = ?,
                        home_player2_id = ?, home_player2_name = ?,
                        away_player_id = ?, away_player_name = ?,
                        away_player2_id = ?, away_player2_name = ?,
                        pending_eight_by = ?, pending_eight_value = ?,
                        eight_declined_by = ?, eight_declined_value = ?, eight_declined_at = ?,
                        updated_at = UTC_TIMESTAMP()
                  WHERE fixture_id = ? AND frame_no = ?',
                [
                    (int)$frame['is_doubles'], $frame['winner'], (int)$frame['eight_ball'],
                    $frame['home_player_id'], $frame['home_player_name'],
                    $frame['home_player2_id'], $frame['home_player2_name'],
                    $frame['away_player_id'], $frame['away_player_name'],
                    $frame['away_player2_id'], $frame['away_player2_name'],
                    $frame['pending_eight_by'], $frame['pending_eight_value'],
                    $frame['eight_declined_by'], $frame['eight_declined_value'], $frame['eight_declined_at'],
                    $fixtureId, (int)$frame['frame_no'],
                ]
            );
        }
    }

    // ---------------------------------------------------------------- helpers

    /**
     * Which side the signed-in captain is on for this fixture.
     *
     * Derived from the session and the fixture, never from the request, so a
     * captain cannot claim to be the other side.
     */
    private static function requireSide(string $fixtureId): string
    {
        $teamId = Captain::requireTeamId();
        $side = self::sideFor($fixtureId, $teamId);
        if ($side === null) {
            // The same answer as a fixture that does not exist, so a captain
            // cannot probe which matches are live.
            throw new ApiError(404, 'not_your_match', 'That match is not one of yours.');
        }
        return $side;
    }

    /** @return string|null */
    private static function sideFor(string $fixtureId, string $teamId)
    {
        $row = Db::one(
            'SELECT home_team_id, away_team_id FROM wdpl_fixtures WHERE id = ?',
            [$fixtureId]
        );
        if ($row === null) {
            return null;
        }
        if ($row['home_team_id'] === $teamId) return 'home';
        if ($row['away_team_id'] === $teamId) return 'away';
        return null;
    }

    private static function uuid($value, string $what): string
    {
        $text = is_string($value) ? trim($value) : '';
        if (!preg_match('/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/', $text)) {
            throw new ApiError(400, 'bad_id', $what . ' is not a valid identifier.');
        }
        return strtolower($text);
    }
}
