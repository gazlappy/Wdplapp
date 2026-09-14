<?php
declare(strict_types=1);

/**
 * Connectivity, sign-in and schema installation.
 *
 * This module is what the app's Connection & Deploy page talks to, and it is
 * the first exercise of the module contract: everything here goes through the
 * same router, roles and error shape as every later feature.
 */
final class SystemModule implements Module
{
    public static function id(): string { return 'system'; }

    public static function title(): string { return 'System'; }

    public static function schemaVersion(): int { return 1; }

    /** Core tables are owned by Schema::installCore(). */
    public static function tables(): array { return []; }

    public static function actions(): array
    {
        return [
            // Unauthenticated: proves the front controller is reachable at all.
            'ping' => [
                'role' => Role::Public,
                'fn'   => static function () {
                    return [
                        'backend' => 'wdpl',
                        'time'    => gmdate('c'),
                        'secure'  => Http::isSecure(),
                        'php'     => PHP_VERSION,
                    ];
                },
            ],

            'login' => [
                'role' => Role::Public,
                'fn'   => static function () {
                    Auth::signIn(
                        (string)Http::requireField('user'),
                        (string)Http::requireField('password')
                    );
                    return ['signedIn' => true];
                },
            ],

            'logout' => [
                'role' => Role::Public,
                'fn'   => static function () {
                    Auth::signOut();
                    return ['signedIn' => false];
                },
            ],

            'whoami' => [
                'role' => Role::Public,
                'fn'   => static function () {
                    return [
                        'admin'         => Auth::isAdmin(),
                        'captainTeamId' => Captain::teamId(),
                    ];
                },
            ],

            // Everything the app's status view needs, in one round trip.
            'health' => [
                'role' => Role::Admin,
                'fn'   => static function () {
                    return [
                        'php'      => PHP_VERSION,
                        'mysql'    => Db::serverVersion(),
                        'secure'   => Http::isSecure(),
                        'modules'  => Registry::manifest(),
                        'schema'   => Schema::status(),
                        'time'     => gmdate('c'),
                    ];
                },
            ],

            'install' => [
                'role' => Role::Admin,
                'fn'   => static function () {
                    return ['applied' => Schema::installAll()];
                },
            ],
        ];
    }
}
