<?php
declare(strict_types=1);

/**
 * WDPL scorecard rules.
 *
 * These are the league's actual playing rules, recovered from the previous
 * online scorecard. They are kept in their own file, free of database and HTTP
 * concerns, so they can be read, reasoned about and tested on their own - the
 * rules are the part that must be right, and burying them inside request
 * handling is how they got hard to verify last time.
 *
 * Every function here is pure: it takes a card state and answers a question
 * about it.
 */
final class ScorecardRules
{
    /** A frame conceded because no player was available. */
    const VOID_PLAYER_ID = 'ffffffff-ffff-ffff-ffff-ffffffffffff';

    /** Frames at the end of the match that the home captain nominates blind. */
    const BLIND_NOMINATION_FRAMES = 5;

    // ------------------------------------------------------- nomination order

    /**
     * True while the away captain is blocked from picking in the blind window.
     *
     * The WDPL rule: for the last five frames the home captain nominates all
     * five of their lead players before the away captain sees any of them, so
     * neither side can tailor the closing frames to the other's line-up.
     *
     * @param array $frames the card's frames, in order
     */
    public static function blindWindowLocked(array $frames): bool
    {
        $count = count($frames);
        if ($count < self::BLIND_NOMINATION_FRAMES) {
            return false;
        }

        for ($i = $count - self::BLIND_NOMINATION_FRAMES; $i < $count; $i++) {
            if (!self::hasHomeLead($frames[$i])) {
                return true;
            }
        }
        return false;
    }

    /**
     * True when the away captain may not yet pick for this frame.
     *
     * Two regimes:
     *  - the closing frames use the blind rule above: all five home leads first;
     *  - every earlier frame unlocks on its own, as soon as the home captain
     *    has nominated that frame's lead.
     */
    public static function awaySlotLocked(array $frames, int $index): bool
    {
        $count = count($frames);
        if ($index < 0 || $index >= $count) {
            return false;
        }

        if ($count >= self::BLIND_NOMINATION_FRAMES && $index >= $count - self::BLIND_NOMINATION_FRAMES) {
            return self::blindWindowLocked($frames);
        }

        return !self::hasHomeLead($frames[$index]);
    }

    /** Why an away pick was refused, phrased for the captain holding the phone. */
    public static function awayLockReason(array $frames, int $index): string
    {
        $count = count($frames);
        $inBlindWindow = $count >= self::BLIND_NOMINATION_FRAMES
            && $index >= $count - self::BLIND_NOMINATION_FRAMES;

        if ($inBlindWindow) {
            return 'Away picks for the last ' . self::BLIND_NOMINATION_FRAMES
                 . ' frames are locked until the home captain has nominated all of theirs.';
        }

        $frameNo = isset($frames[$index]['frame_no']) ? $frames[$index]['frame_no'] : ($index + 1);
        return 'This away slot is locked until the home captain nominates their player for frame ' . $frameNo . '.';
    }

    // ---------------------------------------------------------- player picks

    /**
     * Checks a proposed pick against the league's selection rules.
     *
     * @param array       $frames       all frames, in order
     * @param int         $index        frame being edited
     * @param string      $slot         home|home2|away|away2
     * @param string|null $playerId
     * @param string|null $playerName
     * @param int         $maxPerPlayer frames one player may play, per side
     * @return string|null the reason it is not allowed, or null if it is fine
     */
    public static function rejectPick(
        array $frames,
        int $index,
        string $slot,
        $playerId,
        $playerName,
        int $maxPerPlayer
    ) {
        $candidate = self::identity($playerId, $playerName);
        if ($candidate === null) {
            return null;   // clearing a slot is always allowed
        }

        // VOID is a concession, not a player. None of the selection limits
        // apply to it - a side may concede as many frames as it has to.
        if (self::isVoid($playerId, $playerName)) {
            return null;
        }

        $frame = $frames[$index];
        $isHomeSide = ($slot === 'home' || $slot === 'home2');
        $isPartner  = ($slot === 'home2' || $slot === 'away2');

        // 1. In a doubles frame the two players must be different people.
        $counterpart = $isPartner
            ? ($isHomeSide ? 'home' : 'away')
            : $slot . '2';
        $counterpartKey = self::slotIdentity($frame, $counterpart);
        if ($counterpartKey !== null && $counterpartKey === $candidate) {
            return 'Player 2 must be different from Player 1.';
        }

        // 2. Nobody plays both sides of the same frame.
        $opposite = $isHomeSide ? ['away', 'away2'] : ['home', 'home2'];
        foreach ($opposite as $otherSlot) {
            $otherKey = self::slotIdentity($frame, $otherSlot);
            if ($otherKey !== null && $otherKey === $candidate) {
                return 'Same player cannot play both sides of a frame.';
            }
        }

        // 3. A player may only play so many frames for their side.
        if ($maxPerPlayer > 0) {
            $played = 0;
            $sideSlots = $isHomeSide ? ['home', 'home2'] : ['away', 'away2'];
            foreach ($frames as $i => $other) {
                foreach ($sideSlots as $sideSlot) {
                    if ($i === $index && $sideSlot === $slot) {
                        continue;   // the slot being replaced does not count
                    }
                    if (self::slotIdentity($other, $sideSlot) === $candidate) {
                        $played++;
                        break;      // one frame counts once, even in doubles
                    }
                }
            }
            if ($played >= $maxPerPlayer) {
                $who = ($playerName !== null && $playerName !== '') ? $playerName : 'This player';
                return $who . ' has already played ' . $maxPerPlayer
                     . ' frame(s). Max ' . $maxPerPlayer . ' per match.';
            }
        }

        // 4. The same two players cannot meet twice in a match.
        $homeLead = ($slot === 'home') ? $candidate : self::slotIdentity($frame, 'home');
        $awayLead = ($slot === 'away') ? $candidate : self::slotIdentity($frame, 'away');
        if ($homeLead !== null && $awayLead !== null) {
            foreach ($frames as $i => $other) {
                if ($i === $index) {
                    continue;
                }
                if (self::slotIdentity($other, 'home') === $homeLead
                    && self::slotIdentity($other, 'away') === $awayLead) {
                    $frameNo = isset($other['frame_no']) ? $other['frame_no'] : ($i + 1);
                    return 'Pairing already used in frame ' . $frameNo . '. No repeat pairings.';
                }
            }
        }

        return null;
    }

    // -------------------------------------------------------------- scoring

    /** Frames won by each side, and how many have been played. */
    public static function score(array $frames): array
    {
        $home = 0;
        $away = 0;
        $played = 0;

        foreach ($frames as $frame) {
            $winner = isset($frame['winner']) ? $frame['winner'] : 'none';
            if ($winner === 'home') { $home++; $played++; }
            elseif ($winner === 'away') { $away++; $played++; }
        }

        return ['home' => $home, 'away' => $away, 'played' => $played];
    }

    /** True when every frame has a result, which finalising requires. */
    public static function allFramesScored(array $frames): bool
    {
        foreach ($frames as $frame) {
            $winner = isset($frame['winner']) ? $frame['winner'] : 'none';
            if ($winner !== 'home' && $winner !== 'away') {
                return false;
            }
        }
        return count($frames) > 0;
    }

    /** Frames still missing a result, for a message the captain can act on. */
    public static function unscoredFrames(array $frames): array
    {
        $missing = [];
        foreach ($frames as $i => $frame) {
            $winner = isset($frame['winner']) ? $frame['winner'] : 'none';
            if ($winner !== 'home' && $winner !== 'away') {
                $missing[] = isset($frame['frame_no']) ? (int)$frame['frame_no'] : ($i + 1);
            }
        }
        return $missing;
    }

    // -------------------------------------------------------------- helpers

    public static function isVoid($playerId, $playerName): bool
    {
        if (is_string($playerId) && strtolower(trim($playerId)) === self::VOID_PLAYER_ID) {
            return true;
        }
        return is_string($playerName) && strcasecmp(trim($playerName), 'VOID') === 0;
    }

    /**
     * How a player is identified for rule checks.
     *
     * Registered players compare by id. A player typed in on the night has no
     * id yet, so they compare by name - otherwise two ad-hoc picks of the same
     * person would look like different people and slip past the limits.
     */
    public static function identity($playerId, $playerName)
    {
        if (is_string($playerId) && trim($playerId) !== '') {
            return 'id:' . strtolower(trim($playerId));
        }
        if (is_string($playerName) && trim($playerName) !== '') {
            return 'nm:' . strtolower(trim($playerName));
        }
        return null;
    }

    /** @return string|null */
    public static function slotIdentity(array $frame, string $slot)
    {
        $prefix = self::slotPrefix($slot);
        if ($prefix === null) {
            return null;
        }
        $id   = isset($frame[$prefix . '_id']) ? $frame[$prefix . '_id'] : null;
        $name = isset($frame[$prefix . '_name']) ? $frame[$prefix . '_name'] : null;
        return self::identity($id, $name);
    }

    /** Maps a slot name to its column prefix. */
    public static function slotPrefix(string $slot)
    {
        switch ($slot) {
            case 'home':  return 'home_player';
            case 'home2': return 'home_player2';
            case 'away':  return 'away_player';
            case 'away2': return 'away_player2';
            default:      return null;
        }
    }

    public static function isValidSlot(string $slot): bool
    {
        return self::slotPrefix($slot) !== null;
    }

    /** Slots a captain on this side is allowed to set. */
    public static function slotsFor(string $side): array
    {
        return $side === 'home' ? ['home', 'home2'] : ['away', 'away2'];
    }

    private static function hasHomeLead(array $frame): bool
    {
        return self::slotIdentity($frame, 'home') !== null;
    }
}
