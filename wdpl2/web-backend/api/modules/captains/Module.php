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

    public static function schemaVersion(): int { return 2; }

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

            // Team control: a captain runs their own squad directly.
            'squad'      => ['role' => Role::Captain, 'fn' => [self::class, 'squad']],
            'addPlayer'  => ['role' => Role::Captain, 'fn' => [self::class, 'addPlayer']],
            'setPlayer'  => ['role' => Role::Captain, 'fn' => [self::class, 'setPlayer']],
            'setPin'     => ['role' => Role::Captain, 'fn' => [self::class, 'setPin']],

            // What the app needs to catch up with them.
            'uncollected' => ['role' => Role::Admin, 'fn' => [self::class, 'uncollected']],
            'markCollected' => ['role' => Role::Admin, 'fn' => [self::class, 'markCollected']],
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

    // ------------------------------------------------------------ team control

    /** The captain's own squad, newest additions flagged. */
    public static function squad()
    {
        $teamId = Captain::requireTeamId();
        return Db::all(
            'SELECT id, name, is_active, added_by_captain, collected_by_app, updated_at
             FROM wdpl_players WHERE team_id = ? ORDER BY is_active DESC, name',
            [$teamId]
        );
    }

    /**
     * Adds a player to the captain's own squad.
     *
     * Someone who already exists on the squad is reactivated rather than added
     * again. Two rows for one person splits their frames between them and
     * quietly corrupts the season's statistics, which is far harder to notice
     * and undo than a duplicate name in a list.
     */
    public static function addPlayer()
    {
        $teamId = Captain::requireTeamId();
        $name   = trim((string)Http::requireField('name'));

        if ($name === '' || mb_strlen($name) > 190) {
            throw new ApiError(400, 'bad_name', 'Enter a name of up to 190 characters.');
        }
        if (strcasecmp($name, 'VOID') === 0) {
            throw new ApiError(400, 'reserved_name', 'VOID is reserved for a conceded frame.');
        }

        $team = Db::one('SELECT season_id FROM wdpl_teams WHERE id = ?', [$teamId]);
        if ($team === null) {
            throw new ApiError(404, 'no_team', 'That team is no longer published.');
        }

        return Db::transaction(function () use ($teamId, $team, $name) {
            $existing = Db::one(
                'SELECT id, is_active FROM wdpl_players WHERE team_id = ? AND LOWER(name) = LOWER(?) LIMIT 1',
                [$teamId, $name]
            );

            if ($existing !== null) {
                if ((int)$existing['is_active'] === 1) {
                    throw new ApiError(409, 'already_on_squad', $name . ' is already on your squad.');
                }
                Db::query(
                    'UPDATE wdpl_players SET is_active = 1, updated_at = UTC_TIMESTAMP() WHERE id = ?',
                    [$existing['id']]
                );
                return ['id' => $existing['id'], 'name' => $name, 'reactivated' => true];
            }

            $id = self::newUuid();
            Db::query(
                'INSERT INTO wdpl_players
                    (id, season_id, team_id, name, is_active, added_by_captain, collected_by_app, updated_at)
                 VALUES (?, ?, ?, ?, 1, 1, 0, UTC_TIMESTAMP())',
                [$id, $team['season_id'], $teamId, $name]
            );

            return ['id' => $id, 'name' => $name, 'reactivated' => false];
        });
    }

    /**
     * Renames or retires a player on the captain's own squad.
     *
     * Renaming is limited to players the captain added. A player who came from
     * the league's own records is the league's to name - a captain correcting a
     * spelling there would silently disagree with years of history.
     */
    public static function setPlayer()
    {
        $teamId   = Captain::requireTeamId();
        $playerId = self::uuid(Http::requireField('playerId'), 'playerId');

        $player = Db::one(
            'SELECT id, name, is_active, added_by_captain FROM wdpl_players WHERE id = ? AND team_id = ?',
            [$playerId, $teamId]
        );
        if ($player === null) {
            throw new ApiError(404, 'not_your_player', 'That player is not on your squad.');
        }

        $changes = [];
        $params  = [];

        if (Http::field('name') !== null) {
            if ((int)$player['added_by_captain'] !== 1) {
                throw new ApiError(403, 'league_player',
                    'This player comes from the league records, so their name has to be changed by the league.');
            }
            $name = trim((string)Http::field('name'));
            if ($name === '' || mb_strlen($name) > 190) {
                throw new ApiError(400, 'bad_name', 'Enter a name of up to 190 characters.');
            }
            $changes[] = 'name = ?';
            $params[]  = $name;
        }

        if (Http::field('isActive') !== null) {
            $changes[] = 'is_active = ?';
            $params[]  = Http::field('isActive') ? 1 : 0;
        }

        if (count($changes) === 0) {
            throw new ApiError(400, 'nothing_to_do', 'Send a name or an active flag.');
        }

        $changes[] = 'updated_at = UTC_TIMESTAMP()';
        $params[]  = $playerId;
        $params[]  = $teamId;

        Db::query(
            'UPDATE wdpl_players SET ' . implode(', ', $changes) . ' WHERE id = ? AND team_id = ?',
            $params
        );

        return Db::one('SELECT id, name, is_active, added_by_captain FROM wdpl_players WHERE id = ?', [$playerId]);
    }

    /**
     * Lets a captain choose their own PIN.
     *
     * The current PIN is required, so someone who picks up an unlocked phone
     * cannot lock the captain out of their own team. Hashed here because this
     * is the one case the app never sees the plaintext.
     */
    public static function setPin()
    {
        Http::requireSecure();

        $teamId  = Captain::requireTeamId();
        $current = (string)Http::requireField('currentPin');
        $next    = (string)Http::requireField('newPin');

        if (mb_strlen($next) < 4 || mb_strlen($next) > 32) {
            throw new ApiError(400, 'bad_pin', 'A PIN needs between 4 and 32 characters.');
        }
        if ($next === $current) {
            throw new ApiError(400, 'same_pin', 'That is already your PIN.');
        }

        RateLimit::check('captain-setpin', 10, 900, $teamId);

        $row = Db::one('SELECT pin_hash FROM wdpl_captain_pins WHERE team_id = ?', [$teamId]);
        if ($row === null || !Passwords::verify($current, (string)$row['pin_hash'])) {
            RateLimit::record('captain-setpin', $teamId);
            throw new ApiError(401, 'bad_pin', 'That is not your current PIN.');
        }
        RateLimit::clear('captain-setpin', $teamId);

        Db::query(
            'UPDATE wdpl_captain_pins SET pin_hash = ?, updated_at = UTC_TIMESTAMP() WHERE team_id = ?',
            [Passwords::hash($next), $teamId]
        );

        // The league sets PINs from the app, and publishing replaces the whole
        // set. Say so plainly rather than let a captain be surprised later.
        return [
            'changed' => true,
            'note'    => 'Keep this PIN safe. If the league publishes PINs again it will be replaced.',
        ];
    }

    // ------------------------------------------------- what the app collects

    /** Players captains have added that the app has not taken in yet. */
    public static function uncollected()
    {
        return Db::all(
            'SELECT p.id, p.name, p.is_active, p.updated_at, p.team_id, p.season_id,
                    t.name AS team_name
             FROM wdpl_players p
             LEFT JOIN wdpl_teams t ON t.id = p.team_id
             WHERE p.added_by_captain = 1 AND p.collected_by_app = 0
             ORDER BY t.name, p.name'
        );
    }

    /**
     * Marks players as taken into the app.
     *
     * Called after the app has created them locally. They stay flagged as
     * captain-added for the record, but the next publish now owns them.
     */
    public static function markCollected()
    {
        $ids = Http::field('playerIds', []);
        if (!is_array($ids) || count($ids) === 0) {
            throw new ApiError(400, 'no_players', 'Send the player ids that were collected.');
        }

        $collected = 0;
        Db::transaction(function () use ($ids, &$collected) {
            foreach ($ids as $raw) {
                $id = self::uuid($raw, 'playerIds[]');
                Db::query(
                    'UPDATE wdpl_players SET collected_by_app = 1, updated_at = UTC_TIMESTAMP()
                      WHERE id = ? AND added_by_captain = 1',
                    [$id]
                );
                $collected++;
            }
        });

        return ['collected' => $collected];
    }

    /** A v4 UUID, so captain-added players look like every other player. */
    private static function newUuid(): string
    {
        $bytes = random_bytes(16);
        $bytes[6] = chr((ord($bytes[6]) & 0x0f) | 0x40);
        $bytes[8] = chr((ord($bytes[8]) & 0x3f) | 0x80);
        return strtolower(vsprintf('%s%s-%s-%s-%s-%s%s%s', str_split(bin2hex($bytes), 4)));
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
