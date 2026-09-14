<?php
declare(strict_types=1);

/**
 * Reads the deployment sidecar (api/config.php).
 *
 * The sidecar holds database credentials, the admin password hash and the
 * session pepper. It is written once by the app's deploy step and is never part
 * of the repository, so redeploying code cannot clobber working credentials.
 */
final class Config
{
    private static ?array $values = null;

    public static function load(): void
    {
        if (self::$values !== null) {
            return;
        }
        $path = dirname(__DIR__) . '/config.php';
        if (!is_file($path)) {
            throw new ApiError(
                503,
                'not_configured',
                'Backend is not configured: api/config.php is missing. Deploy configuration from the app.'
            );
        }
        /** @noinspection PhpIncludeInspection */
        $values = require $path;
        if (!is_array($values)) {
            throw new ApiError(503, 'not_configured', 'api/config.php must return an array.');
        }
        self::$values = $values;
    }

    public static function get(string $key, $default = null)
    {
        // Header setup runs before the sidecar is required, so a missing file
        // must not be fatal here - the router reports it properly later.
        if (self::$values === null) {
            try {
                self::load();
            } catch (ApiError $ignored) {
                return $default;
            }
        }
        return self::$values[$key] ?? $default;
    }

    public static function require(string $key)
    {
        self::load();
        $value = self::$values[$key] ?? null;
        if ($value === null || $value === '') {
            throw new ApiError(503, 'not_configured', "Missing configuration value: {$key}.");
        }
        return $value;
    }
}
