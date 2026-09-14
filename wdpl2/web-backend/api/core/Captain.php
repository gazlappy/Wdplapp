<?php
declare(strict_types=1);

/**
 * Captain identity, scoped to exactly one team.
 *
 * Fully implemented in M3 (server-side PIN verification). It exists now so the
 * captain role is a real, refusable gate rather than a missing symbol, and so
 * no module can accidentally be written against a half-defined identity.
 */
final class Captain
{
    private const SESSION_KEY = 'wdpl_captain_team';

    public static function isSignedIn(): bool
    {
        return self::teamId() !== null;
    }

    /** The signed-in captain's team id, or null. */
    public static function teamId(): ?string
    {
        Auth::startSession();
        $id = $_SESSION[self::SESSION_KEY] ?? null;
        return is_string($id) && $id !== '' ? $id : null;
    }

    /**
     * The team id, or a 401. Every captain-scoped query must filter on this
     * value and never on a team id taken from the request body.
     */
    public static function requireTeamId(): string
    {
        $id = self::teamId();
        if ($id === null) {
            throw new ApiError(401, 'captain_required', 'Captain sign-in required.');
        }
        return $id;
    }

    public static function signIn(string $teamId): void
    {
        Auth::startSession();
        session_regenerate_id(true);
        $_SESSION[self::SESSION_KEY] = $teamId;
    }

    public static function signOut(): void
    {
        Auth::startSession();
        unset($_SESSION[self::SESSION_KEY]);
        session_regenerate_id(true);
    }
}
