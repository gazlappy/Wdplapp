<?php
declare(strict_types=1);

/**
 * Captain access: PIN sign-in scoped to exactly one team.
 *
 * Replaces the previous approach, which published an unsalted SHA-256 of each
 * PIN into a public JSON file and checked it in the browser. A four digit PIN
 * hashed that way is ten thousand guesses - the gate was decorative.
 *
 * Here the PIN never leaves the server, is stored as PBKDF2 (the same scheme as
 * the administrator password), and every sign-in attempt is rate limited both
 * per caller and per team, so guessing one team's PIN from many addresses still
 * trips the limit.
 *
 * Every captain-scoped query filters on Captain::requireTeamId() - the session -
 * and never on a team id taken from the request. A captain can only ever see
 * their own team.
 */
final class CaptainsModule implements Module
{
    public static function id(): string { return 'captains'; }

    public static function title(): string { return 'Captains'; }

    public static function schemaVersion(): int { return 1; }

    public static function tables(): array
    {
        return [
            "CREATE TABLE IF NOT EXISTS wdpl_captain_pins (
                team_id    CHAR(36)     NOT NULL,
                season_id  CHAR(36)     NOT NULL,
                pin_hash   VARCHAR(255) NOT NULL,
                updated_at DATETIME     NOT NULL,
                PRIMARY KEY (team_id),
                KEY idx_pin_season (season_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
        ];
    }

    public static function actions(): array
    {
        return [
            // Admin
            'push'     => ['role' => Role::Admin,   'fn' => [self::class, 'push']],
            'status'   => ['role' => Role::Admin,   'fn' => [self::class, 'status']],

            // Anonymous: the sign-in flow
            'teams'    => ['role' => Role::Public,  'fn' => [self::class, 'teams']],
            'login'    => ['role' => Role::Public,  'fn' => [self::class, 'login']],
            'logout'   => ['role' => Role::Public,  'fn' => [self::class, 'logout']],

            // Signed-in captain, own team only
            'me'       => ['role' => Role::Captain, 'fn' => [self::class, 'me']],
            'fixtures' => ['role' => Role::Captain, 'fn' => [self::class, 'fixtures']],
            'roster'   => ['role' => Role::Captain, 'fn' => [self::class, 'roster']],
            'contacts' => ['role' => Role::Captain, 'fn' => [self::class, 'contacts']],
        ];
    }

    // ------------------------------------------------------------------ admin

    /**
     * Replaces the PIN set for one season.
     *
     * The app sends already-hashed PINs; plaintext never crosses the wire and is
     * never stored here. Teams omitted from the payload lose their access,
     * which is how a captain is removed.
     */
    public static function push()
    {
        Http::setMaxBytes(1024 * 1024);

        $seasonId = self::uuid(Http::requireField('seasonId'), 'seasonId');
        $pins     = Http::field('pins', []);
        if (!is_array($pins)) {
            throw new ApiError(400, 'bad_payload', 'Field pins must be a list.');
        }

        $clean = [];
        foreach ($pins as $entry) {
            if (!is_array($entry)) {
                continue;
            }
            $teamId = self::uuid(isset($entry['teamId']) ? $entry['teamId'] : null, 'pins[].teamId');
            $hash   = isset($entry['pinHash']) && is_string($entry['pinHash']) ? trim($entry['pinHash']) : '';

            // Refuse anything that is not the expected hash format outright: a
            // plaintext PIN arriving here would otherwise be stored as though
            // it were a hash and would never match.
            if (!Passwords::isHash($hash)) {
                throw new ApiError(400, 'bad_pin_hash', 'A PIN was not sent in the expected hashed form.');
            }
            $clean[$teamId] = $hash;
        }

        Db::transaction(function () use ($seasonId, $clean) {
            Db::query('DELETE FROM wdpl_captain_pins WHERE season_id = ?', [$seasonId]);
            foreach ($clean as $teamId => $hash) {
                Db::query(
                    'INSERT INTO wdpl_captain_pins (team_id, season_id, pin_hash, updated_at)
                     VALUES (?, ?, ?, UTC_TIMESTAMP())
                     ON DUPLICATE KEY UPDATE season_id = VALUES(season_id), pin_hash = VALUES(pin_hash),
                         updated_at = VALUES(updated_at)',
                    [$teamId, $seasonId, $hash]
                );
            }
        });

        return ['seasonId' => $seasonId, 'teams' => count($clean)];
    }

    /** Which teams can sign in. Never returns a hash. */
    public static function status()
    {
        return Db::all(
            'SELECT p.team_id, t.name AS team_name, p.updated_at
             FROM wdpl_captain_pins p
             LEFT JOIN wdpl_teams t ON t.id = p.team_id
             ORDER BY t.name'
        );
    }

    // -------------------------------------------------------------- sign-in

    /**
     * Teams offered on the sign-in screen.
     *
     * Deliberately only id and name. Listing which teams have access is not
     * sensitive; anything else about them is, and is behind the gate.
     */
    public static function teams()
    {
        return Db::all(
            'SELECT t.id, t.name, d.name AS division_name
             FROM wdpl_captain_pins p
             JOIN wdpl_teams t      ON t.id = p.team_id
             LEFT JOIN wdpl_divisions d ON d.id = t.division_id
             ORDER BY d.sort_order, d.name, t.name'
        );
    }

    public static function login()
    {
        Http::requireSecure();

        $teamId = self::uuid(Http::requireField('teamId'), 'teamId');
        $pin    = (string)Http::requireField('pin');

        // Two independent limits. Per caller stops one machine grinding away;
        // per team stops the same PIN being guessed from many addresses.
        RateLimit::check('captain-login', 20, 900);
        RateLimit::check('captain-team', 10, 900, $teamId);

        $row = Db::one('SELECT pin_hash FROM wdpl_captain_pins WHERE team_id = ?', [$teamId]);

        // Verify even when the team is unknown, against a throwaway hash, so the
        // response time does not reveal which teams have access configured.
        $stored = $row === null
            ? 'pbkdf2-sha256$210000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA='
            : (string)$row['pin_hash'];

        $ok = Passwords::verify($pin, $stored) && $row !== null;

        if (!$ok) {
            RateLimit::record('captain-login');
            RateLimit::record('captain-team', $teamId);
            throw new ApiError(401, 'bad_pin', 'That PIN was not recognised for this team.');
        }

        RateLimit::clear('captain-login');
        RateLimit::clear('captain-team', $teamId);

        Captain::signIn($teamId);

        return self::me();
    }

    public static function logout()
    {
        Captain::signOut();
        return ['signedIn' => false];
    }

    // --------------------------------------------------- signed-in captain

    public static function me()
    {
        $teamId = Captain::requireTeamId();

        $row = Db::one(
            'SELECT t.id, t.name, t.captain_name, d.name AS division_name, v.name AS venue_name, t.season_id
             FROM wdpl_teams t
             LEFT JOIN wdpl_divisions d ON d.id = t.division_id
             LEFT JOIN wdpl_venues    v ON v.id = t.venue_id
             WHERE t.id = ?',
            [$teamId]
        );

        if ($row === null) {
            // The season was republished without this team. The session is
            // meaningless now, so end it rather than leave a captain signed in
            // to something that no longer exists.
            Captain::signOut();
            throw new ApiError(404, 'team_gone', 'That team is no longer published. Please sign in again.');
        }

        $row['signedIn'] = true;
        return $row;
    }

    public static function fixtures()
    {
        $teamId = Captain::requireTeamId();

        return Db::all(
            'SELECT f.id, f.match_date, f.week_no, f.played,
                    f.home_team_id = ? AS is_home,
                    CASE WHEN f.home_team_id = ? THEN f.home_score ELSE f.away_score END AS team_score,
                    CASE WHEN f.home_team_id = ? THEN f.away_score ELSE f.home_score END AS opponent_score,
                    CASE WHEN f.home_team_id = ? THEN a.name ELSE h.name END AS opponent_name,
                    v.name AS venue_name
             FROM wdpl_fixtures f
             LEFT JOIN wdpl_teams  h ON h.id = f.home_team_id
             LEFT JOIN wdpl_teams  a ON a.id = f.away_team_id
             LEFT JOIN wdpl_venues v ON v.id = f.venue_id
             WHERE f.home_team_id = ? OR f.away_team_id = ?
             ORDER BY f.match_date',
            [$teamId, $teamId, $teamId, $teamId, $teamId, $teamId]
        );
    }

    public static function roster()
    {
        $teamId = Captain::requireTeamId();

        return Db::all(
            'SELECT id, name, is_active FROM wdpl_players WHERE team_id = ? ORDER BY name',
            [$teamId]
        );
    }

    /**
     * Other captains' contact details.
     *
     * This is the personal data the old public JSON exposed. It is available
     * only to a signed-in captain, and only for teams in the same season.
     */
    public static function contacts()
    {
        $teamId = Captain::requireTeamId();

        return Db::all(
            'SELECT t.id, t.name, t.captain_name, d.name AS division_name, v.name AS venue_name
             FROM wdpl_teams t
             LEFT JOIN wdpl_divisions d ON d.id = t.division_id
             LEFT JOIN wdpl_venues    v ON v.id = t.venue_id
             WHERE t.season_id = (SELECT season_id FROM wdpl_teams WHERE id = ?)
             ORDER BY d.sort_order, d.name, t.name',
            [$teamId]
        );
    }

    // ---------------------------------------------------------------- helper

    private static function uuid($value, string $what): string
    {
        $text = is_string($value) ? trim($value) : '';
        if (!preg_match('/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/', $text)) {
            throw new ApiError(400, 'bad_id', $what . ' is not a valid identifier.');
        }
        return strtolower($text);
    }
}
