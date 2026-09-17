<?php
declare(strict_types=1);

require_once __DIR__ . '/Rules.php';
require_once __DIR__ . '/CupRules.php';

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

    public static function schemaVersion(): int { return 4; }

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
                solo_by      VARCHAR(8)  NULL,
                solo_since   DATETIME    NULL,
                card_kind    VARCHAR(8)  NOT NULL DEFAULT 'league',
                toss_won_by  VARCHAR(8)  NULL,
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
                ADD COLUMN IF NOT EXISTS away_finalised_version INT NULL,
                ADD COLUMN IF NOT EXISTS solo_by VARCHAR(8) NULL,
                ADD COLUMN IF NOT EXISTS solo_since DATETIME NULL",

            // A cup card is scored like a league night but filled in
            // differently, and which side counts as home is decided on the
            // night by a toss rather than by the fixture.
            "ALTER TABLE wdpl_scorecards
                ADD COLUMN IF NOT EXISTS card_kind VARCHAR(8) NOT NULL DEFAULT 'league',
                ADD COLUMN IF NOT EXISTS toss_won_by VARCHAR(8) NULL",

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
            'close'  => ['role' => Role::Admin,   'fn' => [self::class, 'close']],
            'state'  => ['role' => Role::Admin,   'fn' => [self::class, 'state']],
            'view'      => ['role' => Role::Admin, 'fn' => [self::class, 'view']],
            'soloClear' => ['role' => Role::Admin, 'fn' => [self::class, 'soloClear']],

            'mine'     => ['role' => Role::Captain, 'fn' => [self::class, 'mine']],
            'card'     => ['role' => Role::Captain, 'fn' => [self::class, 'card']],
            'roster'   => ['role' => Role::Captain, 'fn' => [self::class, 'roster']],
            'apply'    => ['role' => Role::Captain, 'fn' => [self::class, 'apply']],
            'finalise' => ['role' => Role::Captain, 'fn' => [self::class, 'finalise']],
            'solo'     => ['role' => Role::Captain, 'fn' => [self::class, 'solo']],
            'soloStart' => ['role' => Role::Captain, 'fn' => [self::class, 'soloStart']],
            'soloStop'  => ['role' => Role::Captain, 'fn' => [self::class, 'soloStop']],
            'toss'      => ['role' => Role::Captain, 'fn' => [self::class, 'toss']],
        ];
    }

    // ---------------------------------------------------------------- the app

    public static function open()
    {
        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');
        $doubles   = Http::field('doublesFrames', []);

        $fixture = Db::one('SELECT id, season_id, kind FROM wdpl_fixtures WHERE id = ?', [$fixtureId]);
        if ($fixture === null) {
            throw new ApiError(404, 'unknown_fixture', 'That fixture has not been published to the website.');
        }

        // The app states the format when it opens a card. The admin portal does
        // not know it, so the season's published format answers for it - which
        // is the app's own setting, pushed with the league, not a second copy.
        // Whether this is a cup tie is the fixture's own business, not the
        // caller's. The admin portal can open one without knowing, and the app
        // cannot open a cup tie as a league card by mistake.
        $kind = (isset($fixture['kind']) && $fixture['kind'] === 'cup') ? 'cup' : 'league';

        $format = self::seasonFormat((string)$fixture['season_id']);

        $framesTotal  = (int)Http::field('framesTotal', $format['frames']);
        $maxPerPlayer = (int)Http::field('maxPerPlayer', $format['maxPerPlayer']);

        if ($framesTotal < 1 || $framesTotal > 50) {
            throw new ApiError(400, 'bad_frames', 'A match must have between 1 and 50 frames.');
        }

        $doublesSet = [];
        if (is_array($doubles)) {
            foreach ($doubles as $n) {
                if (is_numeric($n)) { $doublesSet[(int)$n] = true; }
            }
        }

        return Db::transaction(function () use ($fixtureId, $fixture, $framesTotal, $maxPerPlayer, $doublesSet, $kind) {
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
                     card_kind, toss_won_by,
                     home_finalised_at, home_finalised_version, away_finalised_at, away_finalised_version,
                     finalised_at, claimed_at)
                 VALUES (?, ?, ?, 1, ?, ?, NULL, UTC_TIMESTAMP(), ?, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
                 ON DUPLICATE KEY UPDATE state = VALUES(state), version = 1, frames_total = VALUES(frames_total),
                     max_per_player = VALUES(max_per_player), notes = NULL, opened_at = VALUES(opened_at),
                     card_kind = VALUES(card_kind), toss_won_by = NULL,
                     home_finalised_at = NULL, home_finalised_version = NULL,
                     away_finalised_at = NULL, away_finalised_version = NULL,
                     finalised_at = NULL, claimed_at = NULL',
                [$fixtureId, $fixture['season_id'], self::STATE_LIVE, $framesTotal,
                 $maxPerPlayer > 0 ? $maxPerPlayer : self::DEFAULT_MAX_PER_PLAYER,
                 $kind]
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
                'UPDATE wdpl_scorecards
                    SET state = ?, claimed_at = UTC_TIMESTAMP(), version = version + 1,
                        solo_by = NULL, solo_since = NULL
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

    /**
     * Abandons a card and hands the fixture back to the app.
     *
     * Without this, a card opened by mistake is stuck: it cannot be reopened
     * (it is already live), cannot be collected (it is not finished), and
     * cannot be opened again (one is already open). The only way out was
     * editing the database.
     *
     * The card is deleted rather than parked in some cancelled state. Anything
     * the captains entered is discarded, which is the point - the league is
     * saying this card should not exist. What was on it is reported back so the
     * app can warn before it happens.
     *
     * A collected card is refused: it has already been written into the season,
     * so there is nothing here to abandon.
     */
    public static function close()
    {
        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');

        return Db::transaction(function () use ($fixtureId) {
            $card = Db::lockRow('wdpl_scorecards', 'fixture_id', $fixtureId);
            if ($card === null) {
                throw new ApiError(404, 'no_card', 'There is no card for that fixture.');
            }
            if ($card['state'] === self::STATE_CLAIMED) {
                throw new ApiError(409, 'already_claimed',
                    'This card has already been collected, so there is nothing to close.');
            }

            $frames = self::loadFrames($fixtureId);
            $score  = ScorecardRules::score($frames);

            Db::query('DELETE FROM wdpl_scorecard_frames WHERE fixture_id = ?', [$fixtureId]);
            Db::query('DELETE FROM wdpl_scorecards WHERE fixture_id = ?', [$fixtureId]);

            return [
                'fixture_id'    => $fixtureId,
                'closed'        => true,
                'was_state'     => $card['state'],
                'frames_played' => $score['played'],
                'frames_total'  => (int)$card['frames_total'],
                'home_score'    => $score['home'],
                'away_score'    => $score['away'],
            ];
        });
    }

    public static function state()
    {
        $rows = Db::all(
            "SELECT c.fixture_id, c.state, c.version, c.frames_total,
                    c.opened_at, c.finalised_at, c.claimed_at, c.card_kind, c.toss_won_by,
                    c.home_finalised_at, c.away_finalised_at, c.solo_by,
                    f.home_team_id, f.away_team_id,
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

        // A cup tie's two teams change places when the coin lands, and the
        // scoreline above is already counted in the card's own columns - so the
        // names have to follow, or the league would read the result backwards.
        foreach ($rows as &$row) {
            $row['is_cup'] = ($row['card_kind'] ?? 'league') === 'cup';

            if ($row['is_cup'] && ($row['toss_won_by'] ?? null) === 'away') {
                foreach ([['home_team_id', 'away_team_id'], ['home_team_name', 'away_team_name']] as $pair) {
                    $was = $row[$pair[0]];
                    $row[$pair[0]] = $row[$pair[1]];
                    $row[$pair[1]] = $was;
                }
            }
        }
        unset($row);

        return $rows;
    }

    /** The match format a season was published with. */
    private static function seasonFormat(string $seasonId): array
    {
        $row = Db::one(
            'SELECT frames_total, max_per_player FROM wdpl_seasons WHERE id = ?',
            [$seasonId]
        );

        $frames = ($row !== null && (int)$row['frames_total'] > 0)
            ? (int)$row['frames_total'] : self::DEFAULT_FRAMES;

        $max = ($row !== null && (int)$row['max_per_player'] > 0)
            ? (int)$row['max_per_player'] : self::DEFAULT_MAX_PER_PLAYER;

        return ['frames' => $frames, 'maxPerPlayer' => $max];
    }

    /**
     * The whole card, for the league to watch without touching it.
     *
     * Read-only on purpose. The admin portal shows a match as it is being
     * scored; editing it there would put a second writer on a card the captains
     * own, which is the thing this design exists to prevent.
     */
    public static function view()
    {
        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');

        $card = Db::one('SELECT fixture_id FROM wdpl_scorecards WHERE fixture_id = ?', [$fixtureId]);
        if ($card === null) {
            throw new ApiError(404, 'no_card', 'There is no card for that fixture.');
        }

        return self::readCard($fixtureId, null);
    }

    /**
     * Hands a solo captain's borrowed side back, on their behalf.
     *
     * Solo mode is given up by the captain who took it, normally once the other
     * captain's phone is working again. If the night ends with the card still
     * in their name, nobody else can pick for that side - so the league can
     * hand it back for them.
     */
    public static function soloClear()
    {
        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');

        return Db::transaction(function () use ($fixtureId) {
            $card = Db::lockRow('wdpl_scorecards', 'fixture_id', $fixtureId);
            if ($card === null) {
                throw new ApiError(404, 'no_card', 'There is no card for that fixture.');
            }
            if ($card['solo_by'] === null) {
                return self::readCard($fixtureId, null);
            }

            Db::query(
                'UPDATE wdpl_scorecards
                    SET solo_by = NULL, solo_since = NULL, version = version + 1
                  WHERE fixture_id = ?',
                [$fixtureId]
            );

            return self::readCard($fixtureId, null);
        });
    }

    /**
     * Records who won the toss, and therefore who fills the card in as home.
     *
     * A cup tie has no home team until the coin lands. Either captain may enter
     * it - they are both standing there watching - but only once, because the
     * whole card hangs off it: who is home, who names the first player, and
     * which half of the scoreline belongs to which team.
     */
    public static function toss()
    {
        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');
        $won       = Http::requireField('wonBy') === 'away' ? 'away' : 'home';

        $side = self::requireSide($fixtureId);

        return Db::transaction(function () use ($fixtureId, $won, $side) {
            $card = Db::lockRow('wdpl_scorecards', 'fixture_id', $fixtureId);
            if ($card === null) {
                throw new ApiError(404, 'no_card', 'There is no card for that fixture.');
            }
            if (($card['card_kind'] ?? 'league') !== 'cup') {
                throw new ApiError(409, 'not_a_cup', 'Only a cup tie is decided on a toss.');
            }
            if ($card['state'] !== self::STATE_LIVE) {
                throw new ApiError(409, 'not_live', 'This card is no longer being scored.');
            }
            if ($card['toss_won_by'] !== null) {
                throw new ApiError(409, 'already_tossed',
                    'The toss has already been recorded. Ask the league if it is wrong.');
            }

            Db::query(
                'UPDATE wdpl_scorecards SET toss_won_by = ?, version = version + 1
                  WHERE fixture_id = ?',
                [$won, $fixtureId]
            );

            // The caller's side is asked again: the coin may have just moved it.
            return self::readCard($fixtureId, self::requireSide($fixtureId));
        });
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

        // The team ids travel with the squads so a captain adding someone who
        // has turned up can say which side they are turning out for - which in
        // solo mode is not always their own.
        return [
            'yourSide'    => $side,
            'homeTeamId'  => (string)$fixture['home_team_id'],
            'awayTeamId'  => (string)$fixture['away_team_id'],
            'home'        => self::playersOf((string)$fixture['home_team_id']),
            'away'        => self::playersOf((string)$fixture['away_team_id']),
        ];
    }

    /**
     * Replaces any draw op with the individual picks it stands for.
     *
     * The pool is the side's own squad. VOID is never drawn - conceding a frame
     * is a decision, not something to leave to chance.
     */
    private static function expandDraws(
        array $ops,
        string $fixtureId,
        string $side,
        array $frames,
        int $maxPerPlayer,
        bool $driving
    ): array {
        $expanded = [];

        foreach ($ops as $op) {
            if (!is_array($op) || !isset($op['kind']) || $op['kind'] !== 'draw') {
                $expanded[] = $op;
                continue;
            }

            $for = isset($op['side']) && $op['side'] === 'away' ? 'away' : 'home';
            if (!$driving) {
                $for = $side;   // only a captain driving both sides may draw for the other
            }

            $raw   = isset($op['count']) ? $op['count'] : null;
            $limit = ($raw === null || $raw === 'all') ? null : max(1, (int)$raw);

            $picks = ScorecardRules::draw(
                $frames,
                $for,
                self::drawPool($fixtureId, $for),
                $maxPerPlayer,
                $driving,
                $limit
            );

            foreach ($picks as $pick) {
                $expanded[] = [
                    'kind'       => 'set_player',
                    'frame'      => $pick['frame'],
                    'slot'       => $pick['slot'],
                    'playerId'   => $pick['playerId'],
                    'playerName' => $pick['playerName'],
                ];
            }
        }

        return $expanded;
    }

    /** The squad a draw picks from, for one side of a fixture. */
    private static function drawPool(string $fixtureId, string $side): array
    {
        $sides = self::sides($fixtureId);
        if ($sides === null) {
            return [];
        }

        return self::playersOf($sides[$side === 'home' ? 'home' : 'away']);
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

            // While this captain is driving both sides, they may fill either
            // team's slots and the nomination order does not apply - one person
            // entering both line-ups has already set that aside by agreement.
            $driving = ($card['solo_by'] !== null && $card['solo_by'] === $side);

            // A cup card is filled in turns rather than one side following the
            // other, so both captains have to be told to wait - not just away.
            $cup = ($card['card_kind'] ?? 'league') === 'cup';

            if ($cup && $card['toss_won_by'] === null) {
                throw new ApiError(409, 'no_toss',
                    'Toss for it first. The winner of the toss is home.');
            }

            // A draw is shorthand for the picks it makes. Expanding it here
            // means it goes through exactly the same rule checks, rejections
            // and version bump as a captain tapping each slot by hand.
            $ops = self::expandDraws($ops, $fixtureId, $side, $frames, $maxPerPlayer, $driving);

            foreach ($ops as $index => $op) {
                if (!is_array($op) || empty($op['kind'])) {
                    continue;
                }
                $result = self::applyOne($op, $side, $frames, $notes, $maxPerPlayer, $driving, $cup);

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
    private static function applyOne(
        array $op, string $side, array &$frames, &$notes, int $maxPerPlayer, bool $driving = false,
        bool $cup = false
    ): array {
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
                $allowed = $driving
                    ? ['home', 'home2', 'away', 'away2']
                    : ScorecardRules::slotsFor($side);
                if (!in_array($slot, $allowed, true)) {
                    return ['rejected' => 'You can only pick players for your own team.',
                            'changed' => false, 'frame' => $frameNo];
                }

                $playerId   = isset($op['playerId']) && $op['playerId'] !== '' ? (string)$op['playerId'] : null;
                $playerName = isset($op['playerName']) && $op['playerName'] !== '' ? (string)$op['playerName'] : null;
                $clearing   = ($playerId === null && $playerName === null);

                if (!$clearing && !$driving) {
                    if ($cup) {
                        // Both sides take turns on a cup card, so both are
                        // asked. Home here is the side that won the toss.
                        if (CupRules::slotLocked($frames, $index, $side)) {
                            return ['rejected' => CupRules::lockReason($frames, $index, $side),
                                    'changed' => false, 'frame' => $frameNo];
                        }
                    } elseif ($side === 'away' && ScorecardRules::awaySlotLocked($frames, $index)) {
                        return ['rejected' => ScorecardRules::awayLockReason($frames, $index),
                                'changed' => false, 'frame' => $frameNo];
                    }
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
                if ($driving) {
                    // No one to agree with - the captain driving is recording
                    // what both sides already accepted at the table.
                    if ($frames[$index]['winner'] === 'none') {
                        return ['rejected' => 'Record who won the frame before claiming an 8-ball.',
                                'changed' => false, 'frame' => $frameNo];
                    }
                    $frames[$index]['eight_ball'] = !empty($op['value']) ? 1 : 0;
                    self::clearEightNegotiation($frames[$index]);
                    return ['rejected' => null, 'changed' => true];
                }
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
            if (!self::readyToSign($card, $frames)) {
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
     * Takes control of both sides of a live card on one device.
     *
     * The venue has one usable phone between them, so one captain enters
     * everything with the other watching. Unlike the offline submission below,
     * this stays LIVE - every tap reaches the server as normal, so the public
     * scoreboard keeps updating and the league can see the match progressing.
     *
     * Recorded on the card rather than kept in the browser, for three reasons:
     * the server has to know to accept the other side's slots from this
     * captain; the other captain's device can say what is happening instead of
     * silently refusing their taps; and the league can see afterwards that one
     * person entered the whole card.
     */
    public static function soloStart()
    {
        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');
        $side = self::requireSide($fixtureId);

        return Db::transaction(function () use ($fixtureId, $side) {
            $card = Db::lockRow('wdpl_scorecards', 'fixture_id', $fixtureId);
            if ($card === null) {
                throw new ApiError(404, 'no_card', 'There is no card for that fixture.');
            }
            if ($card['state'] !== self::STATE_LIVE) {
                throw new ApiError(409, 'not_live', 'This card is no longer being scored.');
            }
            if ($card['solo_by'] !== null && $card['solo_by'] !== $side) {
                throw new ApiError(409, 'solo_taken',
                    'The other captain is already entering this card on their device.');
            }

            Db::query(
                'UPDATE wdpl_scorecards SET solo_by = ?, solo_since = UTC_TIMESTAMP(), version = version + 1
                  WHERE fixture_id = ?',
                [$side, $fixtureId]
            );

            return self::readCard($fixtureId, $side);
        });
    }

    /** Hands the other captain their own side back. */
    public static function soloStop()
    {
        $fixtureId = self::uuid(Http::requireField('fixtureId'), 'fixtureId');
        $side = self::requireSide($fixtureId);

        return Db::transaction(function () use ($fixtureId, $side) {
            $card = Db::lockRow('wdpl_scorecards', 'fixture_id', $fixtureId);
            if ($card === null) {
                throw new ApiError(404, 'no_card', 'There is no card for that fixture.');
            }
            // Either captain may end it: the one driving because they are done,
            // the other because their signal came back and they want their side.
            Db::query(
                'UPDATE wdpl_scorecards SET solo_by = NULL, solo_since = NULL, version = version + 1
                  WHERE fixture_id = ?',
                [$fixtureId]
            );

            return self::readCard($fixtureId, $side);
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

            if (!self::readyToSign($card, $frames)) {
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
                    c.opened_at, c.finalised_at, c.claimed_at, c.card_kind, c.toss_won_by,
                    c.home_finalised_at, c.away_finalised_at, c.solo_by, c.solo_since,
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
        $cup = ($card['card_kind'] ?? 'league') === 'cup';

        $toss = $card['toss_won_by'];

        // The coin decides who is home, so the two teams change places on the
        // card rather than the scoreline being translated afterwards. Doing it
        // here means everything downstream - the captain's page, the league's
        // view, the collected result - reads one consistent card.
        if ($cup && $toss === 'away') {
            foreach ([['home_team_id', 'away_team_id'], ['home_team_name', 'away_team_name']] as $pair) {
                $was = $card[$pair[0]];
                $card[$pair[0]] = $card[$pair[1]];
                $card[$pair[1]] = $was;
            }
        }

        $card['is_cup']      = $cup;

        // The blind window is a league idea: away picks catch up behind home's.
        // A cup card has no such window - both sides are held by the turn.
        $card['away_locked'] = $cup ? false : ScorecardRules::blindWindowLocked($frames);

        // Whose turn it is, which a league card never has to say because the
        // answer is always "whoever has not filled this frame in yet".
        $card['turn'] = null;
        $card['your_turn'] = null;

        if ($cup && $toss !== null) {
            $card['turn'] = CupRules::turn($frames);
            $card['your_turn'] = $side !== null && $card['turn']['side'] === $side;
        }

        // A cup tie is first to eight, not fifteen played out, so the card has
        // to say when it is already won - otherwise the captains sit waiting
        // for frames that no longer decide anything.
        $card['frames_to_win'] = $cup ? CupRules::target((int)$card['frames_total']) : null;
        $card['decided_by']    = $cup ? CupRules::decided($frames, $card['frames_to_win']) : null;
        $card['solo_driver']   = $card['solo_by'];
        $card['you_are_driving'] = ($side !== null && $card['solo_by'] === $side);
        $card['all_scored']    = ScorecardRules::allFramesScored($frames);
        $card['unscored']      = ScorecardRules::unscoredFrames($frames);

        return $card;
    }

    /**
     * Whether a card may be signed off.
     *
     * A league night is every frame. A cup tie is the first to eight: once one
     * side has that, the rest decide nothing, so the captains may sign there
     * and then - or play the remaining frames out first and sign afterwards.
     */
    private static function readyToSign(array $card, array $frames): bool
    {
        if (ScorecardRules::allFramesScored($frames)) {
            return true;
        }

        if (($card['card_kind'] ?? 'league') !== 'cup') {
            return false;
        }

        return CupRules::decided($frames, CupRules::target((int)$card['frames_total'])) !== null;
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
        $sides = self::sides($fixtureId);
        if ($sides === null) {
            return null;
        }
        if ($sides['home'] === $teamId) return 'home';
        if ($sides['away'] === $teamId) return 'away';
        return null;
    }

    /**
     * Which team is on which side of this card.
     *
     * A league fixture answers this itself. A cup tie does not: the two teams
     * are listed in the order the draw made them, and the coin decides which of
     * them is actually home. Everything that asks - the captain's own side, the
     * squad a draw picks from, the scoreline - has to get the same answer, so
     * they all come through here.
     *
     * @return array|null ['home' => teamId, 'away' => teamId]
     */
    private static function sides(string $fixtureId)
    {
        $row = Db::one(
            'SELECT f.home_team_id, f.away_team_id, c.card_kind, c.toss_won_by
               FROM wdpl_fixtures f
               LEFT JOIN wdpl_scorecards c ON c.fixture_id = f.id
              WHERE f.id = ?',
            [$fixtureId]
        );

        if ($row === null) {
            return null;
        }

        $home = (string)$row['home_team_id'];
        $away = (string)$row['away_team_id'];

        if (($row['card_kind'] ?? 'league') === 'cup' && ($row['toss_won_by'] ?? null) === 'away') {
            return ['home' => $away, 'away' => $home];
        }

        return ['home' => $home, 'away' => $away];
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
