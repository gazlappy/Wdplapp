<?php
declare(strict_types=1);

require_once __DIR__ . '/Rules.php';
require_once __DIR__ . '/CupRules.php';

/**
 * Whether a slot may be filled yet, on either kind of card.
 *
 * The order a card is filled in is a playing rule, not a courtesy between two
 * phones. It used to be skipped whenever one captain was entering both teams
 * (solo), on the grounds that one person had set it aside by agreement - which
 * let a cup card be filled in any order at all, the blind five included. The
 * order now holds whoever is holding the phone: solo only changes who may tap
 * a side, never when that side may be filled.
 *
 * Asked about the side the SLOT belongs to, not the side of whoever sent it.
 * Without solo the two are always the same; with it they are what matters.
 *
 * Pure: no database, no HTTP.
 */
final class NominationOrder
{
    /**
     * Why this slot may not be filled yet, or null if it may.
     *
     * @param string $slot home|home2|away|away2
     * @param bool   $cup  true for a cup tie, false for a league night
     */
    public static function refusal(array $frames, int $index, string $slot, bool $cup, bool $clearing = false)
    {
        if ($index < 0 || $index >= count($frames)) {
            return null;
        }

        $side    = ($slot === 'home' || $slot === 'home2') ? 'home' : 'away';
        $partner = ($slot === 'home2' || $slot === 'away2');
        $frameNo = isset($frames[$index]['frame_no']) ? $frames[$index]['frame_no'] : ($index + 1);

        if ($cup) {
            // Players stay changeable until the frame is played. Once it has a
            // result, who played it is part of that result - so it is held,
            // and the way to change it is to clear the result first.
            if (self::isScored($frames[$index])) {
                return 'Frame ' . $frameNo . ' has a result. Clear the result first to change who played it.';
            }

            // A side that has already named this frame may change its mind:
            // the turn was taken when the name went down, and swapping it
            // does not take another.
            if (!$partner && self::hasLead($frames[$index], $side)) {
                return null;
            }
        }

        // Clearing a slot is otherwise always allowed.
        if ($clearing) {
            return null;
        }

        // A doubles partner joins a lead already named for that frame. The
        // order is decided by the leads; a partner is the second half of a
        // nomination that has already been allowed.
        if ($partner) {
            if (!self::hasLead($frames[$index], $side)) {
                return 'Name player 1 for frame ' . $frameNo . ' first.';
            }
            return null;
        }

        if ($cup) {
            return CupRules::slotLocked($frames, $index, $side)
                ? CupRules::lockReason($frames, $index, $side)
                : null;
        }

        // A league night: home names freely, away follows home frame by frame,
        // and the last five wait for all five of home's.
        if ($side === 'away' && ScorecardRules::awaySlotLocked($frames, $index)) {
            return ScorecardRules::awayLockReason($frames, $index);
        }

        return null;
    }

    private static function isScored(array $frame): bool
    {
        $winner = isset($frame['winner']) ? $frame['winner'] : 'none';
        return $winner === 'home' || $winner === 'away';
    }

    private static function hasLead(array $frame, string $side): bool
    {
        $prefix = $side === 'home' ? 'home_player' : 'away_player';
        $id   = isset($frame[$prefix . '_id']) ? $frame[$prefix . '_id'] : null;
        $name = isset($frame[$prefix . '_name']) ? $frame[$prefix . '_name'] : null;

        return (is_string($id) && trim($id) !== '') || (is_string($name) && trim($name) !== '');
    }
}
