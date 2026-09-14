<?php
declare(strict_types=1);

/**
 * The one place a stored secret is verified.
 *
 * Both realms - the administrator password and captain PINs - use this, so
 * there is a single hashing scheme to reason about and no chance of one being
 * quietly weaker than the other. The app writes these values with
 * PasswordHash.Create; PasswordHashTests pins the format from the .NET side and
 * backend.rules.test.php pins it from here.
 *
 * Format: pbkdf2-sha256$<iterations>$<base64 salt>$<base64 hash>
 */
final class Passwords
{
    /** Anything below this is treated as a malformed hash, not a weak one. */
    const MIN_ITERATIONS = 1000;

    public static function verify(string $password, string $stored): bool
    {
        $parts = explode('$', $stored);
        if (count($parts) !== 4 || $parts[0] !== 'pbkdf2-sha256') {
            return false;
        }

        $iterations = (int)$parts[1];
        $salt       = base64_decode($parts[2], true);
        $expected   = base64_decode($parts[3], true);

        if ($iterations < self::MIN_ITERATIONS || $salt === false || $expected === false || $expected === '') {
            return false;
        }

        $actual = hash_pbkdf2('sha256', $password, $salt, $iterations, strlen($expected), true);

        // Constant-time: a timing difference here leaks how much of a PIN was right.
        return hash_equals($expected, $actual);
    }

    /** True if the value at least looks like something verify() could accept. */
    public static function isHash(string $stored): bool
    {
        $parts = explode('$', $stored);
        return count($parts) === 4
            && $parts[0] === 'pbkdf2-sha256'
            && (int)$parts[1] >= self::MIN_ITERATIONS
            && base64_decode($parts[2], true) !== false
            && base64_decode($parts[3], true) !== false;
    }
}
