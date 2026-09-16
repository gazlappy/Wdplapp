<?php
declare(strict_types=1);

/**
 * The group table, worked out from the matches played so far.
 *
 * Kept separate from the database and HTTP for the same reason the scorecard
 * rules are: this is the part that decides who goes through, so it should be
 * readable and testable on its own.
 *
 * Two points for a win, one for a draw, and frames settle ties - the league's
 * usual reckoning. Only completed matches count; a match half-entered on the
 * night should not move anybody up the table.
 */
final class CompStandings
{
    const WIN  = 2;
    const DRAW = 1;

    /**
     * @param array $players rows of participant_id, name
     * @param array $matches rows of p1_id, p2_id, p1_score, p2_score, is_complete
     * @return array one row per player, best first
     */
    public static function table(array $players, array $matches): array
    {
        $rows = [];

        foreach ($players as $player) {
            $id = (string)$player['participant_id'];
            $rows[$id] = [
                'participant_id' => $id,
                'name'           => isset($player['name']) ? $player['name'] : '',
                'played'         => 0,
                'won'            => 0,
                'drawn'          => 0,
                'lost'           => 0,
                'frames_for'     => 0,
                'frames_against' => 0,
                'frame_diff'     => 0,
                'points'         => 0,
            ];
        }

        foreach ($matches as $match) {
            if (empty($match['is_complete'])) {
                continue;
            }

            $p1 = isset($match['p1_id']) ? (string)$match['p1_id'] : '';
            $p2 = isset($match['p2_id']) ? (string)$match['p2_id'] : '';

            // A result against somebody who is not in the group would quietly
            // corrupt the table, so it is ignored rather than guessed at.
            if (!isset($rows[$p1]) || !isset($rows[$p2])) {
                continue;
            }

            $s1 = (int)$match['p1_score'];
            $s2 = (int)$match['p2_score'];

            self::record($rows[$p1], $s1, $s2);
            self::record($rows[$p2], $s2, $s1);
        }

        $table = array_values($rows);

        usort($table, static function (array $a, array $b): int {
            if ($a['points'] !== $b['points'])         return $b['points'] - $a['points'];
            if ($a['frame_diff'] !== $b['frame_diff']) return $b['frame_diff'] - $a['frame_diff'];
            if ($a['frames_for'] !== $b['frames_for']) return $b['frames_for'] - $a['frames_for'];
            return strcasecmp((string)$a['name'], (string)$b['name']);
        });

        $position = 0;
        foreach ($table as $index => $row) {
            $table[$index]['position'] = ++$position;
        }

        return $table;
    }

    private static function record(array &$row, int $for, int $against): void
    {
        $row['played']++;
        $row['frames_for'] += $for;
        $row['frames_against'] += $against;
        $row['frame_diff'] = $row['frames_for'] - $row['frames_against'];

        if ($for > $against) {
            $row['won']++;
            $row['points'] += self::WIN;
        } elseif ($for < $against) {
            $row['lost']++;
        } else {
            $row['drawn']++;
            $row['points'] += self::DRAW;
        }
    }
}
