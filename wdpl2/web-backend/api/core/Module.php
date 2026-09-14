<?php
declare(strict_types=1);

/**
 * The whole extension surface of this backend.
 *
 * A feature is one folder under api/modules/ containing a class implementing
 * this interface. Adding a feature never edits the core; the router, auth and
 * schema installer all work off this contract.
 */
interface Module
{
    /** Stable lowercase id, matching the folder name. Used in routing. */
    public static function id(): string;

    /** Human label for the app's Web Control tab. */
    public static function title(): string;

    /** Bump when tables() changes so the installer knows to re-apply. */
    public static function schemaVersion(): int;

    /**
     * DDL statements, each safe to run repeatedly (CREATE TABLE IF NOT EXISTS).
     *
     * @return string[]
     */
    public static function tables(): array;

    /**
     * Action name => ['role' => Role::*, 'fn' => callable(): mixed].
     *
     * The role is enforced by the router before the callable runs, so a handler
     * can assume its caller is already authorised.
     *
     * @return array<string, array{role: string, fn: callable}>
     */
    public static function actions(): array;
}
