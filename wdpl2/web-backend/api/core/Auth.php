<?php
declare(strict_types=1);

/**
 * Two separate authentication realms that never overlap.
 *
 *   Role::Admin   - the league secretary (desktop app, later a browser admin).
 *   Role::Captain - one team captain, scoped to their own team only. Added in M3.
 *   Role::Public  - anonymous read of published data.
 *
 * Holding a captain session must never grant admin access, and vice versa.
 */
final class Role
{
    public const Public  = 'public';
    public const Captain = 'captain';
    public const Admin   = 'admin';
}

final class Auth
{
    private const SESSION_KEY = 'wdpl_admin';

    /** @var bool|null */
    private static $admin = null;

    public static function gate(string $role): void
    {
        switch ($role) {
            case Role::Public:
                return;
            case Role::Admin:
                Http::requireSecure();
                if (!self::isAdmin()) {
                    throw new ApiError(401, 'admin_required', 'Administrator sign-in required.');
                }
                return;
            case Role::Captain:
                Http::requireSecure();
                if (!Captain::isSignedIn()) {
                    throw new ApiError(401, 'captain_required', 'Captain sign-in required.');
                }
                return;
            default:
                throw new LogicException("Unknown role: {$role}");
        }
    }

    /**
     * Admin is accepted either from an established session (browser) or from
     * Basic credentials on the request (the desktop app, which is stateless).
     */
    public static function isAdmin(): bool
    {
        if (self::$admin !== null) {
            return self::$admin;
        }

        self::startSession();
        if (($_SESSION[self::SESSION_KEY] ?? false) === true) {
            return self::$admin = true;
        }

        $user = (string)($_SERVER['PHP_AUTH_USER'] ?? '');
        $pass = (string)($_SERVER['PHP_AUTH_PW'] ?? '');
        if ($user === '' && $pass === '') {
            [$user, $pass] = self::basicFromHeader();
        }
        if ($user === '' || $pass === '') {
            // No credentials offered: not an attempt, so nothing to throttle.
            return self::$admin = false;
        }

        // Credentials were offered, so this IS an authentication attempt and
        // must be throttled exactly like the session login. The desktop app
        // authenticates with Basic on every call, so leaving this path
        // unlimited would make it an unthrottled brute-force channel.
        RateLimit::check('admin-basic', 20, 900);

        $ok = self::verifyPassword($user, $pass);
        if (!$ok) {
            RateLimit::record('admin-basic');
        }

        return self::$admin = $ok;
    }

    /** Establishes a browser session after verifying credentials. */
    public static function signIn(string $user, string $pass): void
    {
        Http::requireSecure();
        RateLimit::check('admin-login', 10, 900);

        if (!self::verifyPassword($user, $pass)) {
            RateLimit::record('admin-login');
            throw new ApiError(401, 'bad_credentials', 'Incorrect username or password.');
        }

        self::startSession();
        session_regenerate_id(true);
        $_SESSION[self::SESSION_KEY] = true;
        self::$admin = true;
    }

    public static function signOut(): void
    {
        self::startSession();
        unset($_SESSION[self::SESSION_KEY]);
        session_regenerate_id(true);
        self::$admin = false;
    }

    private static function verifyPassword(string $user, string $pass): bool
    {
        $expectedUser = (string)Config::get('admin_user', '');
        $hash         = (string)Config::get('admin_password_hash', '');
        if ($expectedUser === '' || $hash === '') {
            throw new ApiError(
                503,
                'not_configured',
                'No administrator account is configured. Deploy configuration from the app.'
            );
        }
        // Compare both, always, so timing does not reveal which half was wrong.
        $userOk = hash_equals($expectedUser, $user);
        $passOk = self::verifyHash($pass, $hash);
        return $userOk && $passOk;
    }

    /**
     * Verifies "pbkdf2-sha256$<iterations>$<base64 salt>$<base64 hash>".
     *
     * PBKDF2 rather than password_hash() because the desktop app writes this
     * value, and PBKDF2-SHA256 is available natively in .NET and PHP alike -
     * bcrypt would mean pulling a third-party library into the app purely to
     * produce a string.
     */
    private static function verifyHash(string $password, string $stored): bool
    {
        $parts = explode('$', $stored);
        if (count($parts) !== 4 || $parts[0] !== 'pbkdf2-sha256') {
            return false;
        }

        $iterations = (int)$parts[1];
        $salt       = base64_decode($parts[2], true);
        $expected   = base64_decode($parts[3], true);

        if ($iterations < 1000 || $salt === false || $expected === false || $expected === '') {
            return false;
        }

        $actual = hash_pbkdf2('sha256', $password, $salt, $iterations, strlen($expected), true);
        return hash_equals($expected, $actual);
    }

    private static function basicFromHeader(): array
    {
        $header = (string)($_SERVER['HTTP_AUTHORIZATION'] ?? $_SERVER['REDIRECT_HTTP_AUTHORIZATION'] ?? '');
        if (stripos($header, 'Basic ') !== 0) {
            return ['', ''];
        }
        $decoded = base64_decode(substr($header, 6), true);
        if ($decoded === false || strpos($decoded, ':') === false) {
            return ['', ''];
        }
        [$user, $pass] = explode(':', $decoded, 2);
        return [$user, $pass];
    }

    public static function startSession(): void
    {
        if (session_status() === PHP_SESSION_ACTIVE) {
            return;
        }
        session_set_cookie_params([
            'lifetime' => 0,
            'path'     => '/',
            'secure'   => Http::isSecure(),
            'httponly' => true,
            'samesite' => 'Strict',
        ]);
        session_name('wdplsid');
        session_start();
    }
}

/**
 * Small database-backed attempt counter. Shared hosting has no shared memory,
 * so this lives in a table; IPs are hashed with the configured pepper rather
 * than stored raw.
 */
final class RateLimit
{
    public static function check(string $bucket, int $limit, int $windowSeconds): void
    {
        self::prune();
        $count = (int)Db::value(
            'SELECT COUNT(*) FROM wdpl_auth_attempts WHERE bucket = ? AND client = ? AND attempted_at > (UTC_TIMESTAMP() - INTERVAL ? SECOND)',
            [$bucket, self::client(), $windowSeconds]
        );
        if ($count >= $limit) {
            throw new ApiError(429, 'too_many_attempts', 'Too many attempts. Wait a few minutes and try again.');
        }
    }

    public static function record(string $bucket): void
    {
        Db::query(
            'INSERT INTO wdpl_auth_attempts (bucket, client, attempted_at) VALUES (?, ?, UTC_TIMESTAMP())',
            [$bucket, self::client()]
        );
    }

    public static function clear(string $bucket): void
    {
        Db::query('DELETE FROM wdpl_auth_attempts WHERE bucket = ? AND client = ?', [$bucket, self::client()]);
    }

    private static function prune(): void
    {
        Db::query('DELETE FROM wdpl_auth_attempts WHERE attempted_at < (UTC_TIMESTAMP() - INTERVAL 1 DAY)');
    }

    /**
     * Forwarded-IP headers are not trusted - they are trivially spoofed, and
     * trusting them would let an attacker sidestep the limit entirely.
     */
    private static function client(): string
    {
        $ip     = (string)($_SERVER['REMOTE_ADDR'] ?? 'unknown');
        $pepper = (string)Config::get('pepper', 'wdpl');
        return hash_hmac('sha256', $ip, $pepper);
    }
}
