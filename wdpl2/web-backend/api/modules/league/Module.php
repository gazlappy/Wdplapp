<?php
declare(strict_types=1);

/**
 * League data: the read-only public face of what the desktop app knows.
 *
 * Ownership (see wdpl2/Docs/WebPlatform.md): every row here is DESKTOP-OWNED.
 * The app pushes a complete snapshot for one season and the server never edits
 * it. That is why push is a wholesale replace rather than a merge - there is no
 * second writer to reconcile with, so there is nothing to merge.
 *
 * Standings are pushed rather than computed here on purpose. The app's
 * StandingsCalculator handles points deductions and the league's sort rules;
 * reimplementing that in SQL would guarantee the website eventually disagrees
 * with the app.
 */
final class LeagueModule implements Module
{
    public static function id(): string { return 'league'; }

    public static function title(): string { return 'League data'; }

    public static function schemaVersion(): int { return 2; }

    public static function tables(): array
    {
        return [
            "CREATE TABLE IF NOT EXISTS wdpl_seasons (
                id          CHAR(36)     NOT NULL,
                name        VARCHAR(190) NOT NULL,
                start_date  DATE         NULL,
                end_date    DATE         NULL,
                is_current  TINYINT(1)   NOT NULL DEFAULT 0,
                updated_at  DATETIME     NOT NULL,
                PRIMARY KEY (id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

            "CREATE TABLE IF NOT EXISTS wdpl_divisions (
                id         CHAR(36)     NOT NULL,
                season_id  CHAR(36)     NOT NULL,
                name       VARCHAR(190) NOT NULL,
                sort_order INT          NOT NULL DEFAULT 0,
                PRIMARY KEY (id),
                KEY idx_div_season (season_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

            "CREATE TABLE IF NOT EXISTS wdpl_venues (
                id        CHAR(36)     NOT NULL,
                season_id CHAR(36)     NOT NULL,
                name      VARCHAR(190) NOT NULL,
                address   VARCHAR(500) NULL,
                PRIMARY KEY (id),
                KEY idx_venue_season (season_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

            "CREATE TABLE IF NOT EXISTS wdpl_teams (
                id           CHAR(36)     NOT NULL,
                season_id    CHAR(36)     NOT NULL,
                division_id  CHAR(36)     NULL,
                venue_id     CHAR(36)     NULL,
                name         VARCHAR(190) NOT NULL,
                captain_name VARCHAR(190) NULL,
                PRIMARY KEY (id),
                KEY idx_team_season (season_id),
                KEY idx_team_division (division_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

            "CREATE TABLE IF NOT EXISTS wdpl_players (
                id        CHAR(36)     NOT NULL,
                season_id CHAR(36)     NOT NULL,
                team_id   CHAR(36)     NULL,
                name      VARCHAR(190) NOT NULL,
                is_active TINYINT(1)   NOT NULL DEFAULT 1,
                added_by_captain TINYINT(1) NOT NULL DEFAULT 0,
                collected_by_app TINYINT(1) NOT NULL DEFAULT 0,
                updated_at DATETIME    NULL,
                PRIMARY KEY (id),
                KEY idx_player_season (season_id),
                KEY idx_player_team (team_id),
                KEY idx_player_added (season_id, added_by_captain, collected_by_app)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

            "ALTER TABLE wdpl_players
                ADD COLUMN IF NOT EXISTS added_by_captain TINYINT(1) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS collected_by_app TINYINT(1) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS updated_at DATETIME NULL",

            "CREATE TABLE IF NOT EXISTS wdpl_fixtures (
                id           CHAR(36)   NOT NULL,
                season_id    CHAR(36)   NOT NULL,
                division_id  CHAR(36)   NULL,
                home_team_id CHAR(36)   NULL,
                away_team_id CHAR(36)   NULL,
                venue_id     CHAR(36)   NULL,
                match_date   DATE       NULL,
                week_no      INT        NULL,
                home_score   INT        NOT NULL DEFAULT 0,
                away_score   INT        NOT NULL DEFAULT 0,
                frames_total INT        NOT NULL DEFAULT 0,
                played       TINYINT(1) NOT NULL DEFAULT 0,
                PRIMARY KEY (id),
                KEY idx_fx_season (season_id),
                KEY idx_fx_date (season_id, match_date)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

            "CREATE TABLE IF NOT EXISTS wdpl_standings (
                season_id      CHAR(36) NOT NULL,
                division_id    CHAR(36) NOT NULL,
                team_id        CHAR(36) NOT NULL,
                position       INT      NOT NULL DEFAULT 0,
                played         INT      NOT NULL DEFAULT 0,
                won            INT      NOT NULL DEFAULT 0,
                drawn          INT      NOT NULL DEFAULT 0,
                lost           INT      NOT NULL DEFAULT 0,
                frames_for     INT      NOT NULL DEFAULT 0,
                frames_against INT      NOT NULL DEFAULT 0,
                deducted       INT      NOT NULL DEFAULT 0,
                points         INT      NOT NULL DEFAULT 0,
                PRIMARY KEY (season_id, division_id, team_id),
                KEY idx_st_division (division_id, position)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
        ];
    }

    public static function actions(): array
    {
        return [
            'push'      => ['role' => Role::Admin,  'fn' => [self::class, 'push']],
            'seasons'   => ['role' => Role::Public, 'fn' => [self::class, 'seasons']],
            'teams'     => ['role' => Role::Public, 'fn' => [self::class, 'teams']],
            'fixtures'  => ['role' => Role::Public, 'fn' => [self::class, 'fixtures']],
            'results'   => ['role' => Role::Public, 'fn' => [self::class, 'results']],
            'standings' => ['role' => Role::Public, 'fn' => [self::class, 'standings']],
            'live'      => ['role' => Role::Public, 'fn' => [self::class, 'live']],
        ];
    }

    // ------------------------------------------------------------------- push

    /**
     * Replaces everything the app owns for ONE season, atomically.
     *
     * Scoped to a single season so publishing the current one can never disturb
     * historical seasons, and wrapped in a transaction so a failure part-way
     * leaves the website showing the previous complete snapshot rather than a
     * half-deleted one.
     */
    public static function push()
    {
        // A whole season of fixtures and players exceeds the public body limit,
        // which exists to constrain anonymous callers - not the authenticated
        // secretary.
        Http::setMaxBytes(8 * 1024 * 1024);

        $season = Http::requireField('season');
        if (!is_array($season) || empty($season['id'])) {
            throw new ApiError(400, 'bad_season', 'The payload needs a season with an id.');
        }
        $seasonId = self::uuid($season['id'], 'season.id');

        $divisions = self::rows('divisions');
        $venues    = self::rows('venues');
        $teams     = self::rows('teams');
        $players   = self::rows('players');
        $fixtures  = self::rows('fixtures');
        $standings = self::rows('standings');

        try {
            self::replaceSeason($seasonId, $season, $divisions, $venues, $teams, $players, $fixtures, $standings);
        } catch (PDOException $e) {
            // A duplicate key here means the payload claims an id that already
            // belongs to a different season. Reported plainly, because "the
            // server failed" gives the secretary nothing to act on, and the
            // transaction has already rolled the whole push back.
            if (strpos($e->getMessage(), '1062') !== false) {
                throw new ApiError(
                    409,
                    'duplicate_id',
                    'This season references an id that already belongs to another season. Nothing was changed.'
                );
            }
            throw $e;
        }

        return [
            'seasonId'  => $seasonId,
            'divisions' => count($divisions),
            'venues'    => count($venues),
            'teams'     => count($teams),
            'players'   => count($players),
            'fixtures'  => count($fixtures),
            'standings' => count($standings),
        ];
    }

    /** The actual replace, wrapped in one transaction. */
    private static function replaceSeason(
        string $seasonId, array $season, array $divisions, array $venues,
        array $teams, array $players, array $fixtures, array $standings
    ): void {
        Db::transaction(function () use ($seasonId, $season, $divisions, $venues, $teams, $players, $fixtures, $standings) {
            $owned = ['wdpl_standings', 'wdpl_fixtures', 'wdpl_teams', 'wdpl_venues', 'wdpl_divisions'];
            foreach ($owned as $table) {
                Db::query('DELETE FROM ' . Db::identifier($table) . ' WHERE season_id = ?', [$seasonId]);
            }

            // Players are the one exception to "publishing replaces the season".
            // A captain can add someone on a match night, and that player must
            // survive the next publish or their frames would point at nobody.
            // Only players the app knows about are cleared; a captain's
            // addition stays until the app has collected it, at which point it
            // arrives in the payload like any other player.
            Db::query(
                'DELETE FROM wdpl_players
                  WHERE season_id = ? AND (added_by_captain = 0 OR collected_by_app = 1)',
                [$seasonId]
            );

            $isCurrent = self::flag($season, 'isCurrent');

            Db::query(
                'INSERT INTO wdpl_seasons (id, name, start_date, end_date, is_current, updated_at)
                 VALUES (?, ?, ?, ?, ?, UTC_TIMESTAMP())
                 ON DUPLICATE KEY UPDATE name = VALUES(name), start_date = VALUES(start_date),
                     end_date = VALUES(end_date), is_current = VALUES(is_current), updated_at = VALUES(updated_at)',
                [
                    $seasonId,
                    self::text($season, 'name', 190),
                    self::date($season, 'startDate'),
                    self::date($season, 'endDate'),
                    $isCurrent,
                ]
            );

            // Exactly one season may be the current one.
            if ($isCurrent === 1) {
                Db::query('UPDATE wdpl_seasons SET is_current = 0 WHERE id <> ?', [$seasonId]);
            }

            foreach ($divisions as $d) {
                Db::query(
                    'INSERT INTO wdpl_divisions (id, season_id, name, sort_order) VALUES (?, ?, ?, ?)',
                    [self::uuid(isset($d['id']) ? $d['id'] : null, 'division.id'), $seasonId, self::text($d, 'name', 190), self::int($d, 'sortOrder')]
                );
            }

            foreach ($venues as $v) {
                Db::query(
                    'INSERT INTO wdpl_venues (id, season_id, name, address) VALUES (?, ?, ?, ?)',
                    [self::uuid(isset($v['id']) ? $v['id'] : null, 'venue.id'), $seasonId, self::text($v, 'name', 190), self::text($v, 'address', 500, true)]
                );
            }

            foreach ($teams as $t) {
                Db::query(
                    'INSERT INTO wdpl_teams (id, season_id, division_id, venue_id, name, captain_name) VALUES (?, ?, ?, ?, ?, ?)',
                    [
                        self::uuid(isset($t['id']) ? $t['id'] : null, 'team.id'), $seasonId,
                        self::optionalUuid($t, 'divisionId'), self::optionalUuid($t, 'venueId'),
                        self::text($t, 'name', 190), self::text($t, 'captainName', 190, true),
                    ]
                );
            }

            foreach ($players as $p) {
                // ON DUPLICATE rather than INSERT: a player the app has
                // collected still exists here as the captain's row, and this
                // is the app taking ownership of it.
                Db::query(
                    'INSERT INTO wdpl_players (id, season_id, team_id, name, is_active,
                                               added_by_captain, collected_by_app, updated_at)
                     VALUES (?, ?, ?, ?, ?, 0, 0, UTC_TIMESTAMP())
                     ON DUPLICATE KEY UPDATE season_id = VALUES(season_id), team_id = VALUES(team_id),
                         name = VALUES(name), is_active = VALUES(is_active),
                         added_by_captain = 0, collected_by_app = 0, updated_at = VALUES(updated_at)',
                    [
                        self::uuid(isset($p['id']) ? $p['id'] : null, 'player.id'), $seasonId,
                        self::optionalUuid($p, 'teamId'), self::text($p, 'name', 190), self::flag($p, 'isActive'),
                    ]
                );
            }

            foreach ($fixtures as $f) {
                Db::query(
                    'INSERT INTO wdpl_fixtures
                        (id, season_id, division_id, home_team_id, away_team_id, venue_id,
                         match_date, week_no, home_score, away_score, frames_total, played)
                     VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)',
                    [
                        self::uuid(isset($f['id']) ? $f['id'] : null, 'fixture.id'), $seasonId,
                        self::optionalUuid($f, 'divisionId'),
                        self::optionalUuid($f, 'homeTeamId'), self::optionalUuid($f, 'awayTeamId'),
                        self::optionalUuid($f, 'venueId'),
                        self::date($f, 'date'), self::int($f, 'weekNo'),
                        self::int($f, 'homeScore'), self::int($f, 'awayScore'),
                        self::int($f, 'framesTotal'), self::flag($f, 'played'),
                    ]
                );
            }

            foreach ($standings as $s) {
                Db::query(
                    'INSERT INTO wdpl_standings
                        (season_id, division_id, team_id, position, played, won, drawn, lost,
                         frames_for, frames_against, deducted, points)
                     VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)',
                    [
                        $seasonId,
                        self::uuid(isset($s['divisionId']) ? $s['divisionId'] : null, 'standing.divisionId'),
                        self::uuid(isset($s['teamId']) ? $s['teamId'] : null, 'standing.teamId'),
                        self::int($s, 'position'), self::int($s, 'played'), self::int($s, 'won'),
                        self::int($s, 'drawn'), self::int($s, 'lost'), self::int($s, 'framesFor'),
                        self::int($s, 'framesAgainst'), self::int($s, 'deducted'), self::int($s, 'points'),
                    ]
                );
            }
        });
    }

    // ------------------------------------------------------------ public reads

    public static function seasons()
    {
        return Db::all(
            'SELECT id, name, start_date, end_date, is_current FROM wdpl_seasons ORDER BY start_date DESC, name'
        );
    }

    public static function teams()
    {
        return Db::all(
            'SELECT t.id, t.name, t.captain_name, t.division_id, d.name AS division_name, v.name AS venue_name
             FROM wdpl_teams t
             LEFT JOIN wdpl_divisions d ON d.id = t.division_id
             LEFT JOIN wdpl_venues    v ON v.id = t.venue_id
             WHERE t.season_id = ?
             ORDER BY d.sort_order, d.name, t.name',
            [self::requestedSeason()]
        );
    }

    public static function fixtures()
    {
        return self::fixtureRows(self::requestedSeason(), false);
    }

    public static function results()
    {
        return self::fixtureRows(self::requestedSeason(), true);
    }

    public static function standings()
    {
        return Db::all(
            'SELECT s.division_id, d.name AS division_name, s.team_id, t.name AS team_name,
                    s.position, s.played, s.won, s.drawn, s.lost,
                    s.frames_for, s.frames_against, s.deducted, s.points
             FROM wdpl_standings s
             LEFT JOIN wdpl_divisions d ON d.id = s.division_id
             LEFT JOIN wdpl_teams     t ON t.id = s.team_id
             WHERE s.season_id = ?
             ORDER BY d.sort_order, d.name, s.position',
            [self::requestedSeason()]
        );
    }

    /**
     * Matches currently being scored.
     *
     * Deliberately empty until live scorecards exist (M4). The public page polls
     * this endpoint, so it must answer correctly - with no live matches - rather
     * than 404 and leave the page showing a permanent error.
     */
    public static function live()
    {
        // Never 404 here. The public scoreboard polls this endpoint on a timer,
        // so a failure is not a one-off error message - it is a page stuck
        // showing "could not load" until someone publishes a season. An empty
        // board is the honest answer to "what is being played right now".
        // Two ways this legitimately has no answer yet: no season published
        // (ApiError), or the tables not installed at all (PDOException) on a
        // site where the website was generated before the backend was set up.
        try {
            $seasonId = self::currentSeasonId();
        } catch (ApiError $noSeasonYet) {
            return self::emptyBoard();
        } catch (PDOException $notInstalledYet) {
            return self::emptyBoard();
        }

        // Only live cards. A finalised card is no longer "in progress", and a
        // claimed one belongs to the app again - neither should sit on the
        // public board pretending a match is still being played.
        //
        // Deliberately NOT filtered by season. A card is only ever live because
        // someone opened it deliberately, so season is not a useful filter here
        // - and scoping by "current season" would silently hide a live match
        // whenever that flag was wrong, which is the worst possible time to
        // have an empty scoreboard.
        $items = [];
        try {
            $items = Db::all(
                "SELECT h.name AS home_team_name, a.name AS away_team_name, v.name AS venue_name,
                        c.frames_total,
                        SUM(CASE WHEN fr.winner = 'home' THEN 1 ELSE 0 END) AS home_score,
                        SUM(CASE WHEN fr.winner = 'away' THEN 1 ELSE 0 END) AS away_score,
                        SUM(CASE WHEN fr.winner <> 'none' THEN 1 ELSE 0 END) AS frames_played
                 FROM wdpl_scorecards c
                 JOIN wdpl_fixtures f ON f.id = c.fixture_id
                 LEFT JOIN wdpl_teams  h ON h.id = f.home_team_id
                 LEFT JOIN wdpl_teams  a ON a.id = f.away_team_id
                 LEFT JOIN wdpl_venues v ON v.id = f.venue_id
                 LEFT JOIN wdpl_scorecard_frames fr ON fr.fixture_id = c.fixture_id
                 WHERE c.state = 'live'
                 GROUP BY c.fixture_id
                 ORDER BY f.match_date, h.name"
            );
        } catch (PDOException $noScorecards) {
            // The scorecards module may not be installed yet. The public board
            // polls this endpoint constantly, so answer "no live matches"
            // rather than erroring on every request.
            $items = [];
        }

        // The board shows whole numbers; MySQL returns SUM() as a string.
        foreach ($items as $index => $item) {
            $items[$index]['home_score']    = (int)$item['home_score'];
            $items[$index]['away_score']    = (int)$item['away_score'];
            $items[$index]['frames_played'] = (int)$item['frames_played'];
            $items[$index]['frames_total']  = (int)$item['frames_total'];
        }

        return [
            'seasonId'     => $seasonId,
            'generatedUtc' => gmdate('c'),
            'items'        => $items,
        ];
    }

    /** What the scoreboard shows when there is genuinely nothing to show. */
    private static function emptyBoard(): array
    {
        return ['seasonId' => null, 'generatedUtc' => gmdate('c'), 'items' => []];
    }

    // ----------------------------------------------------------------- helpers

    private static function fixtureRows(string $seasonId, bool $playedOnly): array
    {
        $sql =
            'SELECT f.id, f.match_date, f.week_no, f.home_score, f.away_score, f.frames_total, f.played,
                    f.division_id, d.name AS division_name,
                    h.name AS home_team_name, a.name AS away_team_name, v.name AS venue_name
             FROM wdpl_fixtures f
             LEFT JOIN wdpl_divisions d ON d.id = f.division_id
             LEFT JOIN wdpl_teams     h ON h.id = f.home_team_id
             LEFT JOIN wdpl_teams     a ON a.id = f.away_team_id
             LEFT JOIN wdpl_venues    v ON v.id = f.venue_id
             WHERE f.season_id = ?' . ($playedOnly ? ' AND f.played = 1' : '') .
            ' ORDER BY f.match_date' . ($playedOnly ? ' DESC' : '') . ', d.sort_order, h.name';

        return Db::all($sql, [$seasonId]);
    }

    /** The season asked for by query string, or the current one. */
    private static function requestedSeason(): string
    {
        $requested = Http::query('season');
        if ($requested !== '') {
            return self::uuid($requested, 'season');
        }
        return self::currentSeasonId();
    }

    private static function currentSeasonId(): string
    {
        $id = Db::value('SELECT id FROM wdpl_seasons WHERE is_current = 1 ORDER BY start_date DESC LIMIT 1');
        if ($id === null) {
            $id = Db::value('SELECT id FROM wdpl_seasons ORDER BY start_date DESC LIMIT 1');
        }
        if ($id === null) {
            throw new ApiError(404, 'no_season', 'No season has been published yet.');
        }
        return (string)$id;
    }

    /** @return array<int, array> */
    private static function rows(string $field): array
    {
        $value = Http::field($field, []);
        if (!is_array($value)) {
            throw new ApiError(400, 'bad_payload', "Field {$field} must be a list.");
        }
        return array_values($value);
    }

    /**
     * Identifiers must be real UUIDs. They are the join keys between the app and
     * the website, so a malformed one is a corrupt push - not something to
     * silently coerce into a row nothing will ever match.
     */
    private static function uuid($value, string $what): string
    {
        $text = is_string($value) ? trim($value) : '';
        if (!preg_match('/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/', $text)) {
            throw new ApiError(400, 'bad_id', $what . ' is not a valid identifier.');
        }
        return strtolower($text);
    }

    private static function optionalUuid(array $row, string $key)
    {
        $value = isset($row[$key]) ? $row[$key] : null;
        if ($value === null || $value === '') {
            return null;
        }
        return self::uuid($value, $key);
    }

    private static function text(array $row, string $key, int $max, bool $nullable = false)
    {
        $value = isset($row[$key]) && is_scalar($row[$key]) ? trim((string)$row[$key]) : '';
        if ($value === '') {
            return $nullable ? null : '';
        }
        return function_exists('mb_substr') ? mb_substr($value, 0, $max) : substr($value, 0, $max);
    }

    private static function int(array $row, string $key): int
    {
        return isset($row[$key]) && is_numeric($row[$key]) ? (int)$row[$key] : 0;
    }

    private static function flag(array $row, string $key): int
    {
        return empty($row[$key]) ? 0 : 1;
    }

    private static function date(array $row, string $key)
    {
        $value = isset($row[$key]) && is_string($row[$key]) ? trim($row[$key]) : '';
        if ($value === '') {
            return null;
        }
        // Accepts a full ISO timestamp or a bare date; only the date part is kept.
        $stamp = strtotime($value);
        return $stamp === false ? null : date('Y-m-d', $stamp);
    }
}
