<?php
declare(strict_types=1);

/**
 * The draw, and the knockout tree it produces.
 *
 * A group is drawn out and played straight through as a knockout. The order
 * players come out of the draw decides where they stand in the tree, by the
 * league's usual pattern: the first name drawn goes at the top, the second into
 * the bottom half, so the first two drawn can only meet in the final.
 *
 * For eight that gives the slot order 1, 5, 3, 7, 2, 6, 4, 8 - which is the
 * paper sheet the league has always used. The rule behind it is recursive: each
 * pair is (n, n + half), and the pairs themselves run in the order for half the
 * size. That is why it keeps working at 4, 16 and 32 without a lookup table.
 *
 * Pure: no database, no HTTP, no randomness. The shuffle happens outside and is
 * handed in, so a draw can be replayed exactly in a test.
 */
final class CompDraw
{
    /** A bye is a slot in the tree with nobody in it. */
    const BYE = null;

    /**
     * The smallest bracket that holds this many players.
     */
    public static function bracketSize(int $players): int
    {
        if ($players < 2) return $players < 1 ? 0 : 1;

        $size = 1;
        while ($size < $players) {
            $size *= 2;
        }
        return $size;
    }

    /**
     * Draw numbers in slot order, top of the sheet to the bottom.
     *
     * @return int[] the draw number that belongs in each slot
     */
    public static function slotOrder(int $size): array
    {
        if ($size <= 1) return [1];

        $half = self::slotOrder(intdiv($size, 2));
        $out = [];

        foreach ($half as $number) {
            $out[] = $number;
            $out[] = $number + intdiv($size, 2);
        }

        return $out;
    }

    /**
     * Places drawn players into the sheet.
     *
     * @param  array $drawn participant ids, in the order they came out
     * @return array one entry per slot, top to bottom; null where there is a bye
     */
    public static function place(array $drawn): array
    {
        $drawn = array_values($drawn);
        $size = self::bracketSize(count($drawn));
        if ($size === 0) return [];

        $slots = [];
        foreach (self::slotOrder($size) as $number) {
            // Draw numbers past the end of the field are the byes, which is why
            // they fall to the bottom of each half rather than being scattered.
            $slots[] = $number <= count($drawn) ? $drawn[$number - 1] : self::BYE;
        }

        return $slots;
    }

    /**
     * Builds the whole tree from a placed first round.
     *
     * Every round is created up front, empty where the winner is not known yet,
     * so the sheet on the wall looks like the sheet on the phone from the start.
     * A first-round match against a bye is settled immediately - there is nobody
     * to play, and making someone press a button to say so helps no one.
     *
     * A group is only played as far as it has to be. If two go through and
     * four turned up, that is one round of two ties and both winners are
     * through - there is nothing to be gained by playing a final between two
     * players who have both already qualified.
     *
     * @param  array $slots  from {@see place()}
     * @param  int   $places how many go through to the next stage
     * @return array rounds, each a list of ['p1' => id|null, 'p2' => id|null,
     *               'winner' => id|null, 'complete' => bool]
     */
    public static function tree(array $slots, int $places = 1): array
    {
        $count = count($slots);
        if ($count < 2) return [];

        $places = max(1, $places);

        $rounds = [];
        $current = $slots;

        while (count($current) >= 2 && count($current) > $places) {
            $round = [];
            $next = [];

            for ($i = 0; $i < count($current); $i += 2) {
                $p1 = $current[$i];
                $p2 = $current[$i + 1];

                $winner = null;
                $complete = false;

                // A bye only decides a match when the other side is a real
                // player. Two byes together resolve to another bye, which is
                // how a very small field still produces a sensible tree.
                if ($p1 !== null && $p2 === null)      { $winner = $p1; $complete = true; }
                elseif ($p2 !== null && $p1 === null)  { $winner = $p2; $complete = true; }

                $round[] = [
                    'p1'       => $p1,
                    'p2'       => $p2,
                    'winner'   => $winner,
                    'complete' => $complete,
                ];

                $next[] = $winner;
            }

            $rounds[] = $round;
            $current = $next;
        }

        return $rounds;
    }

    /**
     * What a round is called.
     *
     * Only a group played down to a single winner has a final, a semi-final and
     * so on. When two go through, the last round decides who qualifies rather
     * than who wins, so borrowing those names would say something untrue about
     * what is at stake.
     */
    public static function roundName(int $roundNumber, int $totalRounds, int $places = 1): string
    {
        if ($places > 1) {
            return $roundNumber === $totalRounds
                ? 'Round ' . $roundNumber . ' - winners go through'
                : 'Round ' . $roundNumber;
        }

        switch ($totalRounds - $roundNumber) {
            case 0:  return 'Final';
            case 1:  return 'Semi-finals';
            case 2:  return 'Quarter-finals';
            default: return 'Round ' . $roundNumber;
        }
    }

    /**
     * How many rounds a field of this size plays to leave $places standing.
     */
    public static function roundsNeeded(int $bracketSize, int $places): int
    {
        $rounds = 0;
        $left = $bracketSize;
        $places = max(1, $places);

        while ($left >= 2 && $left > $places) {
            $left = intdiv($left, 2);
            $rounds++;
        }

        return $rounds;
    }

    /**
     * Where a match's winner goes next.
     *
     * @return array{0:int,1:int}|null [round index, match index], or null for the final
     */
    public static function nextSlot(int $roundIndex, int $matchIndex, int $totalRounds)
    {
        if ($roundIndex + 1 >= $totalRounds) return null;
        return [$roundIndex + 1, intdiv($matchIndex, 2)];
    }

    /** Which side of the next match a winner arrives on. */
    public static function nextSide(int $matchIndex): string
    {
        return $matchIndex % 2 === 0 ? 'p1' : 'p2';
    }
}
