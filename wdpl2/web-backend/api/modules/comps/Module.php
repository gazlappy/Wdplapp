<?php
declare(strict_types=1);

require_once __DIR__ . '/Standings.php';
require_once __DIR__ . '/Draw.php';

/**
 * Competition nights, run at the venue by one of the players.
 *
 * A competition night is not run from the secretary's desk. Groups play at
 * different pubs at the same time, and in each one a nominated player organises
 * it: who turned up, what order they play in, and who won.
 *
 * The runnable unit is a SESSION - one group of a group stage, or one round of
 * a knockout. The app publishes a session with its players and matches; the
 * nominated player signs in with a PIN that opens that session and nothing else.
 *
 * Ownership follows the same rule as the scorecards: while a session is open
 * the website owns those results, and the app collects them back afterwards.
 * Nothing here edits the competition itself - the app remains the record.
 */
final class CompsModule implements Module
{
    const STATE_OPEN    = 'open';
    const STATE_CLOSED  = 'closed';

    public static function id(): string { return 'comps'; }

    public static function title(): string { return 'Competition nights'; }

    public static function schemaVersion(): int { return 4; }

    public static function tables(): array
    {
        return [
            "CREATE TABLE IF NOT EXISTS wdpl_comp_sessions (
                id             CHAR(36)     NOT NULL,
                competition_id CHAR(36)     NOT NULL,
                season_id      CHAR(36)     NOT NULL,
                kind           VARCHAR(8)   NOT NULL,
                ref_id         CHAR(36)     NOT NULL,
                competition    VARCHAR(190) NOT NULL,
                name           VARCHAR(190) NOT NULL,
                venue_name     VARCHAR(190) NULL,
                table_label    VARCHAR(60)  NULL,
                organiser_name VARCHAR(190) NULL,
                best_of        INT          NOT NULL DEFAULT 0,
                frames_to_win  INT          NOT NULL DEFAULT 0,
                allow_order    TINYINT(1)   NOT NULL DEFAULT 1,
                pin_hash       VARCHAR(255) NOT NULL,
                state          VARCHAR(8)   NOT NULL,
                version        INT          NOT NULL DEFAULT 1,
                collected_at   DATETIME     NULL,
                drawn          TINYINT(1)   NOT NULL DEFAULT 0,
                bracket_size   INT          NOT NULL DEFAULT 0,
                places         INT          NOT NULL DEFAULT 1,
                finished_at    DATETIME     NULL,
                updated_at     DATETIME     NOT NULL,
                PRIMARY KEY (id),
                KEY idx_comp_session_comp (competition_id),
                KEY idx_comp_session_state (state)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

            "CREATE TABLE IF NOT EXISTS wdpl_comp_players (
                session_id     CHAR(36)     NOT NULL,
                participant_id CHAR(36)     NOT NULL,
                name           VARCHAR(190) NOT NULL,
                sort_order     INT          NOT NULL DEFAULT 0,
                present        TINYINT(1)   NULL,
                updated_at     DATETIME     NULL,
                PRIMARY KEY (session_id, participant_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

            "CREATE TABLE IF NOT EXISTS wdpl_comp_matches (
                session_id  CHAR(36)   NOT NULL,
                match_id    CHAR(36)   NOT NULL,
                order_no    INT        NOT NULL DEFAULT 0,
                p1_id       CHAR(36)   NULL,
                p2_id       CHAR(36)   NULL,
                p1_score    INT        NOT NULL DEFAULT 0,
                p2_score    INT        NOT NULL DEFAULT 0,
                winner_id   CHAR(36)   NULL,
                is_complete TINYINT(1) NOT NULL DEFAULT 0,
                updated_at  DATETIME   NULL,
                PRIMARY KEY (session_id, match_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

            // A group is drawn out and played as a knockout, so a match belongs
            // to a round and a place in it. Sites installed before the draw
            // existed keep their matches as a single first round.
            "ALTER TABLE wdpl_comp_sessions
                ADD COLUMN IF NOT EXISTS drawn TINYINT(1) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS bracket_size INT NOT NULL DEFAULT 0",

            // How many go through. A group is played only as far as it has to
            // be, so this decides how many rounds the draw produces.
            "ALTER TABLE wdpl_comp_sessions
                ADD COLUMN IF NOT EXISTS places INT NOT NULL DEFAULT 1",

            // When the player running it says the group is done. The league has
            // not taken the results yet, so it is a handover rather than an end.
            "ALTER TABLE wdpl_comp_sessions
                ADD COLUMN IF NOT EXISTS finished_at DATETIME NULL",

            "ALTER TABLE wdpl_comp_players
                ADD COLUMN IF NOT EXISTS draw_no INT NULL",

            "ALTER TABLE wdpl_comp_matches
                ADD COLUMN IF NOT EXISTS round_no INT NOT NULL DEFAULT 1,
                ADD COLUMN IF NOT EXISTS slot INT NOT NULL DEFAULT 0",
        ];
    }

    public static function actions(): array
    {
        return [
            // The app
            'push'    => ['role' => Role::Admin, 'fn' => [self::class, 'push']],
            'state'   => ['role' => Role::Admin, 'fn' => [self::class, 'state']],
            'collect' => ['role' => Role::Admin, 'fn' => [self::class, 'collect']],
            'close'   => ['role' => Role::Admin, 'fn' => [self::class, 'close']],

            // Signing in at the venue
            'sessions' => ['role' => Role::Public, 'fn' => [self::class, 'sessions']],
            'login'    => ['role' => Role::Public, 'fn' => [self::class, 'login']],
            'logout'   => ['role' => Role::Public, 'fn' => [self::class, 'logout']],

            // The player running it
            'mine'   => ['role' => Role::Runner, 'fn' => [self::class, 'mine']],
            'apply'  => ['role' => Role::Runner, 'fn' => [self::class, 'apply']],
            'finish' => ['role' => Role::Runner, 'fn' => [self::class, 'finish']],
            'reopen' => ['role' => Role::Runner, 'fn' => [self::class, 'reopen']],
        ];
    }

    // ---------------------------------------------------------------- the app

    /**
     * Publishes the sessions for one competition, replacing what was there.
     *
     * A session already collected is left alone: its results are in the app,
     * and re-publishing over it would offer the runner a night that is done.
     */
    public static function push()
    {
        $competitionId = self::uuid(Http::requireField('competitionId'), 'competitionId');
        $seasonId      = self::uuid(Http::requireField('seasonId'), 'seasonId');
        $competition   = self::text(Http::field('competition', ''), 190);
        $sessions      = Http::field('sessions', []);

        if (!is_array($sessions)) {
            throw new ApiError(400, 'bad_sessions', 'sessions must be a list.');
        }

        return Db::transaction(function () use ($competitionId, $seasonId, $competition, $sessions) {
            $kept = Db::all(
                'SELECT id FROM wdpl_comp_sessions WHERE competition_id = ? AND collected_at IS NOT NULL',
                [$competitionId]
            );
            $protected = [];
            foreach ($kept as $row) { $protected[(string)$row['id']] = true; }

            $wanted = [];
            foreach ($sessions as $entry) {
                if (!is_array($entry)) continue;
                $id = self::uuid(isset($entry['id']) ? $entry['id'] : null, 'sessions[].id');
                $wanted[$id] = $entry;
            }

            // Anything no longer published, and not already collected, goes.
            $existing = Db::all('SELECT id FROM wdpl_comp_sessions WHERE competition_id = ?', [$competitionId]);
            foreach ($existing as $row) {
                $id = (string)$row['id'];
                if (isset($protected[$id]) || isset($wanted[$id])) continue;
                self::deleteSession($id);
            }

            $written = 0;
            foreach ($wanted as $id => $entry) {
                if (isset($protected[$id])) continue;

                $hash = isset($entry['pinHash']) && is_string($entry['pinHash']) ? trim($entry['pinHash']) : '';
                if (!Passwords::isHash($hash)) {
                    throw new ApiError(400, 'bad_pin_hash',
                        'A PIN was not sent in the expected hashed form.');
                }

                Db::query(
                    "INSERT INTO wdpl_comp_sessions
                        (id, competition_id, season_id, kind, ref_id, competition, name,
                         venue_name, table_label, organiser_name, best_of, frames_to_win,
                         allow_order, places, pin_hash, state, version, updated_at)
                     VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 1, UTC_TIMESTAMP())
                     ON DUPLICATE KEY UPDATE
                        season_id = VALUES(season_id), kind = VALUES(kind), ref_id = VALUES(ref_id),
                        competition = VALUES(competition), name = VALUES(name),
                        venue_name = VALUES(venue_name), table_label = VALUES(table_label),
                        organiser_name = VALUES(organiser_name), best_of = VALUES(best_of),
                        frames_to_win = VALUES(frames_to_win), allow_order = VALUES(allow_order),
                        places = VALUES(places),
                        pin_hash = VALUES(pin_hash), updated_at = UTC_TIMESTAMP()",
                    [
                        $id, $competitionId, $seasonId,
                        self::kind(isset($entry['kind']) ? $entry['kind'] : 'group'),
                        self::uuid(isset($entry['refId']) ? $entry['refId'] : null, 'sessions[].refId'),
                        $competition,
                        self::text(isset($entry['name']) ? $entry['name'] : '', 190),
                        self::nullableText(isset($entry['venueName']) ? $entry['venueName'] : null, 190),
                        self::nullableText(isset($entry['tableLabel']) ? $entry['tableLabel'] : null, 60),
                        self::nullableText(isset($entry['organiserName']) ? $entry['organiserName'] : null, 190),
                        (int)(isset($entry['bestOf']) ? $entry['bestOf'] : 0),
                        (int)(isset($entry['framesToWin']) ? $entry['framesToWin'] : 0),
                        !empty($entry['allowOrder']) ? 1 : 0,
                        max(1, (int)(isset($entry['places']) ? $entry['places'] : 1)),
                        $hash,
                        self::STATE_OPEN,
                    ]
                );

                self::writePlayers($id, isset($entry['players']) ? $entry['players'] : []);
                self::writeMatches($id, isset($entry['matches']) ? $entry['matches'] : []);
                $written++;
            }

            return ['competitionId' => $competitionId, 'sessions' => $written,
                    'keptCollected' => count($protected)];
        });
    }

    /**
     * Writes the players of a session, keeping any attendance already recorded.
     *
     * The runner may have ticked people in before the app pushed again; that is
     * their answer about the room they are standing in, so a republish must not
     * quietly discard it.
     */
    private static function writePlayers(string $sessionId, $players): void
    {
        if (!is_array($players)) $players = [];

        $was = [];
        foreach (Db::all('SELECT participant_id, present FROM wdpl_comp_players WHERE session_id = ?',
                         [$sessionId]) as $row) {
            if ($row['present'] !== null) {
                $was[(string)$row['participant_id']] = (int)$row['present'];
            }
        }

        Db::query('DELETE FROM wdpl_comp_players WHERE session_id = ?', [$sessionId]);

        $order = 0;
        foreach ($players as $player) {
            if (!is_array($player)) continue;
            $id = self::uuid(isset($player['id']) ? $player['id'] : null, 'players[].id');

            Db::query(
                'INSERT INTO wdpl_comp_players
                    (session_id, participant_id, name, sort_order, present, updated_at)
                 VALUES (?, ?, ?, ?, ?, UTC_TIMESTAMP())',
                [
                    $sessionId, $id,
                    self::text(isset($player['name']) ? $player['name'] : '', 190),
                    $order++,
                    isset($was[$id]) ? $was[$id] : null,
                ]
            );
        }
    }

    /** Writes the matches of a session, keeping any scores already entered. */
    private static function writeMatches(string $sessionId, $matches): void
    {
        if (!is_array($matches)) $matches = [];

        $was = [];
        foreach (Db::all(
            'SELECT match_id, order_no, p1_score, p2_score, winner_id, is_complete
               FROM wdpl_comp_matches WHERE session_id = ?', [$sessionId]) as $row) {
            $was[(string)$row['match_id']] = $row;
        }

        Db::query('DELETE FROM wdpl_comp_matches WHERE session_id = ?', [$sessionId]);

        $order = 0;
        foreach ($matches as $match) {
            if (!is_array($match)) continue;
            $id = self::uuid(isset($match['id']) ? $match['id'] : null, 'matches[].id');
            $old = isset($was[$id]) ? $was[$id] : null;

            Db::query(
                'INSERT INTO wdpl_comp_matches
                    (session_id, match_id, order_no, p1_id, p2_id,
                     p1_score, p2_score, winner_id, is_complete, updated_at)
                 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, UTC_TIMESTAMP())',
                [
                    $sessionId, $id,
                    $old !== null ? (int)$old['order_no'] : $order,
                    self::optionalUuid(isset($match['p1Id']) ? $match['p1Id'] : null),
                    self::optionalUuid(isset($match['p2Id']) ? $match['p2Id'] : null),
                    $old !== null ? (int)$old['p1_score'] : 0,
                    $old !== null ? (int)$old['p2_score'] : 0,
                    $old !== null ? $old['winner_id'] : null,
                    $old !== null ? (int)$old['is_complete'] : 0,
                ]
            );
            $order++;
        }
    }

    /** Every session, for the app's Web Control view. */
    public static function state()
    {
        return Db::all(
            "SELECT s.id, s.competition_id, s.competition, s.name, s.kind, s.ref_id,
                    s.venue_name, s.table_label, s.organiser_name, s.state, s.version,
                    s.finished_at, s.collected_at, s.updated_at,
                    (SELECT COUNT(*) FROM wdpl_comp_matches m WHERE m.session_id = s.id) AS matches,
                    (SELECT COUNT(*) FROM wdpl_comp_matches m
                      WHERE m.session_id = s.id AND m.is_complete = 1) AS played,
                    (SELECT COUNT(*) FROM wdpl_comp_players p
                      WHERE p.session_id = s.id AND p.present = 1) AS present
             FROM wdpl_comp_sessions s
             ORDER BY s.competition, s.name"
        );
    }

    /** Hands one session's results back to the app and freezes it. */
    public static function collect()
    {
        $sessionId = self::uuid(Http::requireField('sessionId'), 'sessionId');

        return Db::transaction(function () use ($sessionId) {
            $session = Db::lockRow('wdpl_comp_sessions', 'id', $sessionId);
            if ($session === null) {
                throw new ApiError(404, 'no_session', 'There is no such competition session.');
            }

            Db::query(
                'UPDATE wdpl_comp_sessions
                    SET state = ?, collected_at = UTC_TIMESTAMP(), version = version + 1
                  WHERE id = ?',
                [self::STATE_CLOSED, $sessionId]
            );

            return self::read($sessionId);
        });
    }

    /** Abandons a session without taking its results. */
    public static function close()
    {
        $sessionId = self::uuid(Http::requireField('sessionId'), 'sessionId');

        $session = Db::one('SELECT id FROM wdpl_comp_sessions WHERE id = ?', [$sessionId]);
        if ($session === null) {
            throw new ApiError(404, 'no_session', 'There is no such competition session.');
        }

        self::deleteSession($sessionId);
        return ['sessionId' => $sessionId, 'closed' => true];
    }

    private static function deleteSession(string $id): void
    {
        Db::query('DELETE FROM wdpl_comp_players WHERE session_id = ?', [$id]);
        Db::query('DELETE FROM wdpl_comp_matches WHERE session_id = ?', [$id]);
        Db::query('DELETE FROM wdpl_comp_sessions WHERE id = ?', [$id]);
    }

    // ------------------------------------------------------------- signing in

    /**
     * The sessions a runner could be signing in to.
     *
     * Deliberately only what is needed to choose one: the competition, the
     * group and where it is playing. No PIN, and nothing about the players.
     */
    public static function sessions()
    {
        return Db::all(
            "SELECT id, competition, name, venue_name, table_label, organiser_name
             FROM wdpl_comp_sessions
             WHERE state = ?
             ORDER BY competition, name",
            [self::STATE_OPEN]
        );
    }

    public static function login()
    {
        Http::requireSecure();

        $sessionId = self::uuid(Http::requireField('sessionId'), 'sessionId');
        $pin       = (string)Http::requireField('pin');

        // Two limits, as for captains: one stops a single machine grinding,
        // the other stops one group's PIN being guessed from many addresses.
        RateLimit::check('runner-login', 20, 900);
        RateLimit::check('runner-session', 10, 900, $sessionId);

        $session = Db::one(
            'SELECT id, pin_hash, state FROM wdpl_comp_sessions WHERE id = ?',
            [$sessionId]
        );

        if ($session === null || !Passwords::verify($pin, (string)$session['pin_hash'])) {
            RateLimit::record('runner-login');
            RateLimit::record('runner-session', $sessionId);
            throw new ApiError(401, 'bad_pin', 'That PIN was not accepted for this group.');
        }

        if ($session['state'] !== self::STATE_OPEN) {
            throw new ApiError(409, 'session_closed',
                'That group has been closed by the league. Nothing more can be entered.');
        }

        RateLimit::clear('runner-login');
        RateLimit::clear('runner-session', $sessionId);

        Runner::signIn($sessionId);
        return self::read($sessionId);
    }

    public static function logout()
    {
        Runner::signOut();
        return ['signedOut' => true];
    }

    // ------------------------------------------------------- running the night

    public static function mine()
    {
        return self::read(Runner::requireSessionId());
    }

    /**
     * Applies a batch of edits to the session the runner holds.
     *
     * Batched and version-checked for the same reasons the scorecards are: a
     * pub connection drops taps, and two people should not be able to overwrite
     * each other silently.
     */
    public static function apply()
    {
        $sessionId = Runner::requireSessionId();
        $expected  = Http::field('version', null);
        $ops       = Http::field('ops', []);

        if ($expected === null || !is_numeric($expected)) {
            throw new ApiError(400, 'version_required',
                'Send the version you last read, so a simultaneous edit cannot be overwritten.');
        }
        if (!is_array($ops)) {
            throw new ApiError(400, 'bad_ops', 'ops must be a list.');
        }

        return Db::transaction(function () use ($sessionId, $expected, $ops) {
            $session = Db::lockRow('wdpl_comp_sessions', 'id', $sessionId);
            if ($session === null) {
                throw new ApiError(404, 'no_session', 'This group is no longer published.');
            }
            if ($session['state'] !== self::STATE_OPEN) {
                throw new ApiError(409, 'session_closed',
                    'The league has closed this group. Nothing more can be entered.');
            }
            if ($session['finished_at'] !== null) {
                throw new ApiError(409, 'session_finished',
                    'This group has been sent to the league. Reopen it to change anything.');
            }
            if ((int)$session['version'] !== (int)$expected) {
                throw new ApiConflict(self::read($sessionId),
                    'Someone else updated this group first.');
            }

            $framesToWin = (int)$session['frames_to_win'];
            $bestOf      = (int)$session['best_of'];
            $allowOrder  = (int)$session['allow_order'] === 1;

            $rejections = [];
            $changed = false;

            foreach ($ops as $index => $op) {
                if (!is_array($op) || empty($op['kind'])) continue;

                $reason = self::applyOne($op, $sessionId, $framesToWin, $bestOf, $allowOrder, $changed);
                if ($reason !== null) {
                    $rejections[] = ['op' => $index, 'reason' => $reason];
                }
            }

            if ($changed) {
                Db::query(
                    'UPDATE wdpl_comp_sessions SET version = version + 1, updated_at = UTC_TIMESTAMP()
                      WHERE id = ?',
                    [$sessionId]
                );
            }

            $current = self::read($sessionId);
            $current['rejections'] = $rejections;
            return $current;
        });
    }

    /** @return string|null the reason it was refused, or null if it applied */
    private static function applyOne(
        array $op, string $sessionId, int $framesToWin, int $bestOf, bool $allowOrder, bool &$changed
    ) {
        switch ($op['kind']) {

            case 'set_present':
                $participant = self::optionalUuid(isset($op['playerId']) ? $op['playerId'] : null);
                if ($participant === null) return 'No player was named.';

                $value = array_key_exists('value', $op) ? $op['value'] : null;
                Db::query(
                    'UPDATE wdpl_comp_players SET present = ?, updated_at = UTC_TIMESTAMP()
                      WHERE session_id = ? AND participant_id = ?',
                    [$value === null ? null : (!empty($value) ? 1 : 0), $sessionId, $participant]
                );
                $changed = true;
                return null;

            case 'set_score':
                $matchId = self::optionalUuid(isset($op['matchId']) ? $op['matchId'] : null);
                if ($matchId === null) return 'No match was named.';

                $row = Db::one(
                    'SELECT p1_id, p2_id FROM wdpl_comp_matches WHERE session_id = ? AND match_id = ?',
                    [$sessionId, $matchId]
                );
                if ($row === null) return 'That match is not in this group.';

                if ($row['p1_id'] === null || $row['p2_id'] === null) {
                    return 'That match does not have two players yet.';
                }

                $p1 = max(0, (int)(isset($op['p1']) ? $op['p1'] : 0));
                $p2 = max(0, (int)(isset($op['p2']) ? $op['p2'] : 0));

                if ($bestOf > 0 && $p1 + $p2 > $bestOf) {
                    return 'That is more frames than the match is played over.';
                }

                $winner = null;
                $complete = 0;

                if ($framesToWin > 0) {
                    if ($p1 >= $framesToWin)      { $winner = $row['p1_id']; $complete = 1; }
                    elseif ($p2 >= $framesToWin)  { $winner = $row['p2_id']; $complete = 1; }
                } else {
                    if ($p1 > $p2)     { $winner = $row['p1_id']; $complete = 1; }
                    elseif ($p2 > $p1) { $winner = $row['p2_id']; $complete = 1; }
                }

                Db::query(
                    'UPDATE wdpl_comp_matches
                        SET p1_score = ?, p2_score = ?, winner_id = ?, is_complete = ?,
                            updated_at = UTC_TIMESTAMP()
                      WHERE session_id = ? AND match_id = ?',
                    [$p1, $p2, $winner, $complete, $sessionId, $matchId]
                );

                self::advance($sessionId, $matchId);
                $changed = true;
                return null;

            case 'set_order':
                if (!$allowOrder) return 'The order of play is set by the league for this group.';

                $order = isset($op['matchIds']) ? $op['matchIds'] : null;
                if (!is_array($order) || count($order) === 0) return 'No order was sent.';

                $n = 0;
                foreach ($order as $raw) {
                    $matchId = self::optionalUuid($raw);
                    if ($matchId === null) continue;
                    Db::query(
                        'UPDATE wdpl_comp_matches SET order_no = ?, updated_at = UTC_TIMESTAMP()
                          WHERE session_id = ? AND match_id = ?',
                        [$n++, $sessionId, $matchId]
                    );
                }
                $changed = true;
                return null;

            case 'draw':
                return self::makeDraw($sessionId, !empty($op['redraw']), $changed);

            default:
                return 'That is not something this page can do.';
        }
    }

    /**
     * Draws the group out and builds the knockout tree.
     *
     * Only players marked present are drawn. Nobody has said who is here on a
     * fresh sheet, so in that case everyone is in - which is the sensible
     * reading of "the draw" before anyone has ticked anything.
     */
    private static function makeDraw(string $sessionId, bool $redraw, bool &$changed)
    {
        $session = Db::one('SELECT drawn, places FROM wdpl_comp_sessions WHERE id = ?', [$sessionId]);
        if ($session === null) return 'This group is no longer published.';

        if ((int)$session['drawn'] === 1 && !$redraw) {
            return 'This group has already been drawn.';
        }

        $players = Db::all(
            'SELECT participant_id, present FROM wdpl_comp_players
              WHERE session_id = ? ORDER BY sort_order, name',
            [$sessionId]
        );

        // Only the people standing there go in the bag. Somebody who has not
        // turned up cannot play their tie, and drawing them in would hand their
        // opponent a walkover the league never awarded.
        $field = [];
        foreach ($players as $player) {
            if ((int)$player['present'] === 1) {
                $field[] = (string)$player['participant_id'];
            }
        }

        if (count($field) === 0) {
            return 'Nobody is marked as here yet. Tick who has turned up, then draw.';
        }

        if (count($field) < 2) {
            return 'Only one player is marked as here, so there is nothing to draw.';
        }

        // Out of the bag: the order decides the sheet, so this is the only
        // place chance comes into it.
        shuffle($field);

        $places = max(1, (int)$session['places']);

        $slots = CompDraw::place($field);
        $tree  = CompDraw::tree($slots, $places);
        $size  = count($slots);

        if (count($tree) === 0) {
            return 'There are already few enough players here that nobody needs to play.';
        }

        Db::query('DELETE FROM wdpl_comp_matches WHERE session_id = ?', [$sessionId]);
        Db::query('UPDATE wdpl_comp_players SET draw_no = NULL WHERE session_id = ?', [$sessionId]);

        foreach ($field as $index => $participantId) {
            Db::query(
                'UPDATE wdpl_comp_players SET draw_no = ?, updated_at = UTC_TIMESTAMP()
                  WHERE session_id = ? AND participant_id = ?',
                [$index + 1, $sessionId, $participantId]
            );
        }

        foreach ($tree as $roundIndex => $round) {
            foreach ($round as $matchIndex => $match) {
                Db::query(
                    'INSERT INTO wdpl_comp_matches
                        (session_id, match_id, round_no, slot, order_no,
                         p1_id, p2_id, p1_score, p2_score, winner_id, is_complete, updated_at)
                     VALUES (?, ?, ?, ?, ?, ?, ?, 0, 0, ?, ?, UTC_TIMESTAMP())',
                    [
                        $sessionId,
                        self::matchId($sessionId, $roundIndex, $matchIndex),
                        $roundIndex + 1,
                        $matchIndex,
                        $matchIndex,
                        $match['p1'],
                        $match['p2'],
                        $match['winner'],
                        $match['complete'] ? 1 : 0,
                    ]
                );
            }
        }

        Db::query(
            'UPDATE wdpl_comp_sessions SET drawn = 1, bracket_size = ?, updated_at = UTC_TIMESTAMP()
              WHERE id = ?',
            [$size, $sessionId]
        );

        $changed = true;
        return null;
    }

    /**
     * A match's id, derived from where it sits in the tree.
     *
     * Derived rather than random so a redraw writes over the same rows, and so
     * the app recognises the same match across two collects.
     */
    private static function matchId(string $sessionId, int $roundIndex, int $matchIndex): string
    {
        $hash = md5($sessionId . ':' . $roundIndex . ':' . $matchIndex);

        return substr($hash, 0, 8) . '-' . substr($hash, 8, 4) . '-4' . substr($hash, 13, 3)
             . '-8' . substr($hash, 17, 3) . '-' . substr($hash, 20, 12);
    }

    /**
     * Carries a decided match's winner into the next round.
     *
     * A bye in the next round settles itself the moment the other side arrives,
     * so a player can walk through two rounds without anybody pressing anything
     * - which is exactly what happens on paper.
     */
    private static function advance(string $sessionId, string $matchId): void
    {
        $match = Db::one(
            'SELECT round_no, slot, winner_id, is_complete
               FROM wdpl_comp_matches WHERE session_id = ? AND match_id = ?',
            [$sessionId, $matchId]
        );
        if ($match === null) return;

        $top = Db::one('SELECT MAX(round_no) AS n FROM wdpl_comp_matches WHERE session_id = ?',
                       [$sessionId]);
        $rounds = $top === null ? 0 : (int)$top['n'];

        $roundIndex = (int)$match['round_no'] - 1;
        $matchIndex = (int)$match['slot'];

        $next = CompDraw::nextSlot($roundIndex, $matchIndex, $rounds);
        if ($next === null) return;

        $side = CompDraw::nextSide($matchIndex) === 'p1' ? 'p1_id' : 'p2_id';
        $winner = (int)$match['is_complete'] === 1 ? $match['winner_id'] : null;

        Db::query(
            "UPDATE wdpl_comp_matches
                SET {$side} = ?, updated_at = UTC_TIMESTAMP()
              WHERE session_id = ? AND round_no = ? AND slot = ?",
            [$winner, $sessionId, $next[0] + 1, $next[1]]
        );

        // Taking a result back can leave the next round holding somebody who is
        // no longer through, so it is re-settled rather than left as it was.
        self::settleBye($sessionId, $next[0] + 1, $next[1]);
    }

    /** Settles, or unsettles, a match that has become a bye. */
    private static function settleBye(string $sessionId, int $roundNo, int $slot): void
    {
        $match = Db::one(
            'SELECT match_id, p1_id, p2_id, p1_score, p2_score
               FROM wdpl_comp_matches WHERE session_id = ? AND round_no = ? AND slot = ?',
            [$sessionId, $roundNo, $slot]
        );
        if ($match === null) return;

        // A match somebody has actually scored is theirs, not ours to decide.
        if ((int)$match['p1_score'] > 0 || (int)$match['p2_score'] > 0) return;

        $p1 = $match['p1_id'];
        $p2 = $match['p2_id'];

        $winner = null;

        // Only the first round can hold a real bye. Later on, an empty side
        // means the match feeding it has not been played yet.
        if ($roundNo === 1) {
            if ($p1 !== null && $p2 === null)     $winner = $p1;
            elseif ($p2 !== null && $p1 === null) $winner = $p2;
        }

        Db::query(
            'UPDATE wdpl_comp_matches SET winner_id = ?, is_complete = ?, updated_at = UTC_TIMESTAMP()
              WHERE session_id = ? AND match_id = ?',
            [$winner, $winner === null ? 0 : 1, $sessionId, $match['match_id']]
        );

        if ($winner !== null) {
            self::advance($sessionId, (string)$match['match_id']);
        }
    }

    /**
     * The player running it says the group is done.
     *
     * An end to the night rather than a state change nobody sees: the card
     * locks, the league is told it is ready, and the room gets an answer
     * instead of a page that looks the same as it did an hour ago.
     *
     * Ties still to play are refused. A group sent half-finished reads as
     * complete to everyone downstream, and the only person who knows otherwise
     * has gone home.
     */
    public static function finish()
    {
        $sessionId = Runner::requireSessionId();

        return Db::transaction(function () use ($sessionId) {
            $session = Db::lockRow('wdpl_comp_sessions', 'id', $sessionId);
            if ($session === null) {
                throw new ApiError(404, 'no_session', 'This group is no longer published.');
            }
            if ($session['state'] !== self::STATE_OPEN) {
                throw new ApiError(409, 'session_closed', 'The league has closed this group.');
            }
            if ((int)$session['drawn'] !== 1) {
                throw new ApiError(409, 'not_drawn', 'The group has not been drawn yet.');
            }

            $unplayed = Db::one(
                'SELECT COUNT(*) AS n FROM wdpl_comp_matches
                  WHERE session_id = ? AND is_complete = 0',
                [$sessionId]
            );

            if ($unplayed !== null && (int)$unplayed['n'] > 0) {
                throw new ApiError(409, 'ties_left',
                    (int)$unplayed['n'] . ' tie(s) still have no result. Finish those first.');
            }

            if ($session['finished_at'] === null) {
                Db::query(
                    'UPDATE wdpl_comp_sessions
                        SET finished_at = UTC_TIMESTAMP(), version = version + 1,
                            updated_at = UTC_TIMESTAMP()
                      WHERE id = ?',
                    [$sessionId]
                );
            }

            return self::read($sessionId);
        });
    }

    /** Takes a sent group back, while the league has still not collected it. */
    public static function reopen()
    {
        $sessionId = Runner::requireSessionId();

        return Db::transaction(function () use ($sessionId) {
            $session = Db::lockRow('wdpl_comp_sessions', 'id', $sessionId);
            if ($session === null) {
                throw new ApiError(404, 'no_session', 'This group is no longer published.');
            }
            if ($session['state'] !== self::STATE_OPEN) {
                throw new ApiError(409, 'session_closed',
                    'The league has already taken this group.');
            }

            if ($session['finished_at'] !== null) {
                Db::query(
                    'UPDATE wdpl_comp_sessions
                        SET finished_at = NULL, version = version + 1, updated_at = UTC_TIMESTAMP()
                      WHERE id = ?',
                    [$sessionId]
                );
            }

            return self::read($sessionId);
        });
    }

    // ----------------------------------------------------------------- reading

    private static function read(string $sessionId): array
    {
        $session = Db::one(
            'SELECT id, competition_id, competition, name, kind, ref_id, venue_name, table_label,
                    organiser_name, best_of, frames_to_win, allow_order, places, state, version,
                    drawn, bracket_size, finished_at, collected_at, updated_at
             FROM wdpl_comp_sessions WHERE id = ?',
            [$sessionId]
        );

        if ($session === null) {
            throw new ApiError(404, 'no_session', 'There is no such competition session.');
        }

        $session['players'] = Db::all(
            'SELECT participant_id, name, sort_order, present, draw_no
             FROM wdpl_comp_players WHERE session_id = ? ORDER BY sort_order, name',
            [$sessionId]
        );

        $session['matches'] = Db::all(
            'SELECT match_id, round_no, slot, order_no, p1_id, p2_id,
                    p1_score, p2_score, winner_id, is_complete
             FROM wdpl_comp_matches WHERE session_id = ? ORDER BY round_no, slot',
            [$sessionId]
        );

        // Grouped into rounds as well, so the page can draw the tree without
        // having to work out the shape for itself.
        $rounds = [];
        foreach ($session['matches'] as $match) {
            $rounds[(int)$match['round_no']][] = $match;
        }

        $total = count($rounds);
        $session['rounds'] = [];

        foreach ($rounds as $number => $matches) {
            $session['rounds'][] = [
                'round'   => $number,
                'name'    => CompDraw::roundName($number, $total, (int)$session['places']),
                'matches' => $matches,
            ];
        }

        $session['standings'] = CompStandings::table($session['players'], $session['matches']);

        return $session;
    }

    // ------------------------------------------------------------- coercions

    private static function kind($value): string
    {
        return $value === 'round' ? 'round' : 'group';
    }

    private static function text($value, int $max): string
    {
        $text = is_string($value) ? trim($value) : '';
        return mb_substr($text, 0, $max);
    }

    private static function nullableText($value, int $max)
    {
        $text = self::text($value, $max);
        return $text === '' ? null : $text;
    }

    private static function uuid($value, string $field): string
    {
        $id = self::optionalUuid($value);
        if ($id === null) {
            throw new ApiError(400, 'bad_uuid', "{$field} must be a UUID.");
        }
        return $id;
    }

    private static function optionalUuid($value)
    {
        if (!is_string($value)) return null;
        $trimmed = strtolower(trim($value));
        return preg_match('/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/', $trimmed)
            ? $trimmed : null;
    }
}
