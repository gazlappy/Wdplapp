<?php
declare(strict_types=1);

/**
 * The player running one part of a competition on the night.
 *
 * A competition night is not run by the league secretary. Groups play at
 * different venues at the same time, and one player in each is nominated to
 * organise it. This is that person's identity, scoped to the single session
 * they were given a PIN for - never to the competition as a whole.
 *
 * Separate from Captain on purpose. The same person may well be both, but the
 * two grant different things, and a captain PIN must never open a competition
 * any more than a runner PIN opens a team's scorecard.
 */
final class Runner
{
    private const SESSION_KEY = 'wdpl_runner_session';

    public static function isSignedIn(): bool
    {
        return self::sessionId() !== null;
    }

    /** The competition session this runner holds, or null. */
    public static function sessionId(): ?string
    {
        Auth::startSession();
        $id = $_SESSION[self::SESSION_KEY] ?? null;
        return is_string($id) && $id !== '' ? $id : null;
    }

    /**
     * The session id, or a 401.
     *
     * Every runner-scoped query must filter on this value and never on an id
     * taken from the request body: holding one group's PIN must not make the
     * next group's results editable.
     */
    public static function requireSessionId(): string
    {
        $id = self::sessionId();
        if ($id === null) {
            throw new ApiError(401, 'runner_required', 'Sign in with the PIN for this group.');
        }
        return $id;
    }

    public static function signIn(string $sessionId): void
    {
        Auth::startSession();
        session_regenerate_id(true);
        $_SESSION[self::SESSION_KEY] = $sessionId;
    }

    public static function signOut(): void
    {
        Auth::startSession();
        unset($_SESSION[self::SESSION_KEY]);
        session_regenerate_id(true);
    }
}
