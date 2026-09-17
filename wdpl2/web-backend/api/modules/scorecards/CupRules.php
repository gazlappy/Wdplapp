<?php
declare(strict_types=1);

/**
 * Nomination order for a cup night.
 *
 * A cup card is played the same way as a league night - same frames, same
 * scoring, same sign-off - but it is filled in differently, and which team
 * counts as "home" is decided by a coin toss rather than by the fixture.
 *
 * The order, as the league plays it:
 *
 *   Frames 1-10   home names one, then the sides take turns naming two at a
 *                 time until both have named ten. Each side is therefore always
 *                 naming one player against an opponent they can see and one
 *                 they cannot.
 *   Frames 11-15  home names all five before the away captain sees any of them,
 *                 then away names all five.
 *
 * Expressed as counts rather than as a list of whose turn each frame is: a side
 * may name their next frame while they are still behind their allowance, and
 * the allowance follows the blocks the OTHER side has finished. Reading it off
 * their raw count instead lets each side answer the moment the other writes a
 * single name, and the card comes out one apiece rather than two at a time.
 *
 * Pure: no database, no HTTP. The same reasoning a captain does in their head.
 */
final class CupRules
{
    /** Frames filled by the alternating turns before the blind five. */
    const OPEN_FRAMES = 10;

    /** Frames at the end that each side nominates in one go. */
    const BLIND_FRAMES = 5;

    /**
     * Which nomination role a side is playing, given who won the toss.
     *
     * A cup tie has no home team until the coin lands, so the card's home and
     * away columns are just the order the draw listed the two teams in. The
     * toss decides which of them fills the card in first - and the scoreline
     * stays in the columns it started in, or collecting it would credit the
     * wrong team.
     */
    public static function role(string $side, ?string $tossWinner): string
    {
        return $side === ($tossWinner ?? 'home') ? 'home' : 'away';
    }

    /**
     * The card as the nomination rules see it: the toss winner's names in the
     * home columns, whichever columns they actually live in.
     */
    public static function roleView(array $frames, ?string $tossWinner): array
    {
        if (($tossWinner ?? 'home') !== 'away') {
            return $frames;
        }

        foreach ($frames as &$frame) {
            foreach (['_id', '_name', '2_id', '2_name'] as $suffix) {
                $h = 'home_player' . $suffix;
                $a = 'away_player' . $suffix;

                $was = isset($frame[$h]) ? $frame[$h] : null;
                $frame[$h] = isset($frame[$a]) ? $frame[$a] : null;
                $frame[$a] = $was;
            }
        }
        unset($frame);

        return $frames;
    }

    /**
     * How many leads a side is allowed to have named in the open frames.
     */
    public static function allowance(string $side, int $home, int $away): int
    {
        if ($side === 'home') {
            // Measured against the blocks away has FINISHED, not the names they
            // have put down. Counting raw names would let home answer the moment
            // away wrote their first, and the two sides would trade one apiece
            // instead of two at a time.
            return min(2 * intdiv($away, 2) + 1, self::OPEN_FRAMES);
        }

        // Away cannot start. After that they answer home's finished block, which
        // always leaves them one ahead - and that is what makes it two.
        if ($home === 0) {
            return 0;
        }

        return min($home % 2 === 1 ? $home + 1 : $home, self::OPEN_FRAMES);
    }

    /** Leads a side has named among the open frames. */
    public static function named(array $frames, string $side): int
    {
        $count = 0;
        $limit = min(count($frames), self::OPEN_FRAMES);

        for ($i = 0; $i < $limit; $i++) {
            if (self::hasLead($frames[$i], $side)) {
                $count++;
            }
        }

        return $count;
    }

    /** The next frame a side has to fill, or null when they have none left. */
    public static function nextOpenFrame(array $frames, string $side): ?int
    {
        $limit = min(count($frames), self::OPEN_FRAMES);

        for ($i = 0; $i < $limit; $i++) {
            if (!self::hasLead($frames[$i], $side)) {
                return $i;
            }
        }

        return null;
    }

    /** True once both sides have named all ten of the open frames. */
    public static function openFramesDone(array $frames): bool
    {
        return self::named($frames, 'home') >= self::OPEN_FRAMES
            && self::named($frames, 'away') >= self::OPEN_FRAMES;
    }

    /** True once home has named every one of the closing five. */
    public static function blindFramesNamed(array $frames): bool
    {
        $count = count($frames);
        if ($count < self::BLIND_FRAMES) {
            return false;
        }

        for ($i = $count - self::BLIND_FRAMES; $i < $count; $i++) {
            if (!self::hasLead($frames[$i], 'home')) {
                return false;
            }
        }

        return true;
    }

    /**
     * True when a side may not pick for this frame yet.
     *
     * @param string $side 'home' or 'away', as decided by the toss
     */
    public static function slotLocked(array $frames, int $index, string $side): bool
    {
        $count = count($frames);
        if ($index < 0 || $index >= $count) {
            return false;
        }

        $inBlind = $count > self::OPEN_FRAMES && $index >= $count - self::BLIND_FRAMES;

        if ($inBlind) {
            // The closing five are not opened until the card is filled to ten,
            // or the sides would be picking two things at once.
            if (!self::openFramesDone($frames)) {
                return true;
            }

            return $side === 'away' ? !self::blindFramesNamed($frames) : false;
        }

        // An open frame is filled in order, and only while it is your turn.
        $next = self::nextOpenFrame($frames, $side);
        if ($next === null || $next !== $index) {
            return true;
        }

        $home = self::named($frames, 'home');
        $away = self::named($frames, 'away');

        return self::named($frames, $side) >= self::allowance($side, $home, $away);
    }

    /** Why a pick was refused, phrased for the captain holding the phone. */
    public static function lockReason(array $frames, int $index, string $side): string
    {
        $count = count($frames);
        $inBlind = $count > self::OPEN_FRAMES && $index >= $count - self::BLIND_FRAMES;

        if ($inBlind) {
            if (!self::openFramesDone($frames)) {
                return 'The last ' . self::BLIND_FRAMES . ' frames open once the first '
                     . self::OPEN_FRAMES . ' are filled in.';
            }

            return 'Away picks for the last ' . self::BLIND_FRAMES
                 . ' frames are locked until the home captain has nominated all of theirs.';
        }

        $next = self::nextOpenFrame($frames, $side);
        $frameNo = isset($frames[$index]['frame_no']) ? $frames[$index]['frame_no'] : ($index + 1);

        if ($next !== null && $next !== $index) {
            $nextNo = isset($frames[$next]['frame_no']) ? $frames[$next]['frame_no'] : ($next + 1);
            return 'Fill the card in order - frame ' . $nextNo . ' is your next one.';
        }

        if ($next === null) {
            return 'You have named all ' . self::OPEN_FRAMES . ' of your open frames.';
        }

        return 'It is the other captain\'s turn. Frame ' . $frameNo . ' opens when they have named theirs.';
    }

    /**
     * What the card is waiting for, for the line at the top of the page.
     */
    public static function turn(array $frames): array
    {
        if (self::openFramesDone($frames)) {
            if (!self::blindFramesNamed($frames)) {
                return ['side' => 'home', 'what' => 'the last ' . self::BLIND_FRAMES . ' frames'];
            }

            return ['side' => 'away', 'what' => 'the last ' . self::BLIND_FRAMES . ' frames'];
        }

        $home = self::named($frames, 'home');
        $away = self::named($frames, 'away');

        $side = $home < self::allowance('home', $home, $away) ? 'home' : 'away';
        $left = self::allowance($side, $home, $away) - self::named($frames, $side);

        return ['side' => $side, 'what' => $left . ' more to name'];
    }

    private static function hasLead(array $frame, string $side): bool
    {
        $prefix = $side === 'home' ? 'home_player' : 'away_player';

        $id = isset($frame[$prefix . '_id']) ? $frame[$prefix . '_id'] : null;
        $name = isset($frame[$prefix . '_name']) ? $frame[$prefix . '_name'] : null;

        return (is_string($id) && trim($id) !== '')
            || (is_string($name) && trim($name) !== '');
    }
}
