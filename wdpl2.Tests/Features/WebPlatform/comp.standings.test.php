<?php
declare(strict_types=1);

/**
 * Group table tests.
 *
 * The table decides who goes through, so it gets its own suite - no database,
 * no HTTP, just the reckoning.
 *
 *   php wdpl2.Tests/Features/WebPlatform/comp.standings.test.php
 */

require dirname(__DIR__, 3) . '/wdpl2/web-backend/api/modules/comps/Standings.php';

$passed = 0;
$failed = 0;

function test(string $name, callable $fn): void
{
    global $passed, $failed;
    try {
        $fn();
        $passed++;
        echo "  ok    {$name}\n";
    } catch (Throwable $e) {
        $failed++;
        echo "  FAIL  {$name}\n      {$e->getMessage()}\n";
    }
}

function check(bool $condition, string $message): void
{
    if (!$condition) {
        throw new RuntimeException($message);
    }
}

function same($expected, $actual, string $message = ''): void
{
    if ($expected !== $actual) {
        throw new RuntimeException(
            ($message !== '' ? $message . ': ' : '')
            . 'expected ' . var_export($expected, true) . ', got ' . var_export($actual, true)
        );
    }
}

function player(string $name): array
{
    return ['participant_id' => strtolower($name), 'name' => ucfirst($name)];
}

function played(string $a, int $sa, string $b, int $sb, bool $complete = true): array
{
    return [
        'p1_id' => strtolower($a), 'p2_id' => strtolower($b),
        'p1_score' => $sa, 'p2_score' => $sb,
        'is_complete' => $complete ? 1 : 0,
    ];
}

/** The row for one player, by name. */
function row(array $table, string $name): array
{
    foreach ($table as $entry) {
        if ($entry['participant_id'] === strtolower($name)) return $entry;
    }
    throw new RuntimeException("{$name} is not in the table");
}

echo "\nThe group table\n";

test('everyone starts on nothing', function () {
    $table = CompStandings::table([player('ann'), player('bob')], []);

    same(2, count($table));
    same(0, $table[0]['played']);
    same(0, $table[0]['points']);
    same(1, $table[0]['position']);
    same(2, $table[1]['position']);
});

test('a win is two points, a loss none', function () {
    $table = CompStandings::table(
        [player('ann'), player('bob')],
        [played('ann', 3, 'bob', 1)]
    );

    same(2, row($table, 'ann')['points']);
    same(1, row($table, 'ann')['won']);
    same(0, row($table, 'bob')['points']);
    same(1, row($table, 'bob')['lost']);
});

test('a draw is one point each', function () {
    $table = CompStandings::table(
        [player('ann'), player('bob')],
        [played('ann', 2, 'bob', 2)]
    );

    same(1, row($table, 'ann')['points']);
    same(1, row($table, 'bob')['points']);
    same(1, row($table, 'ann')['drawn']);
});

test('frames are counted both ways round', function () {
    $table = CompStandings::table(
        [player('ann'), player('bob')],
        [played('ann', 3, 'bob', 1)]
    );

    same(3, row($table, 'ann')['frames_for']);
    same(1, row($table, 'ann')['frames_against']);
    same(2, row($table, 'ann')['frame_diff']);

    same(1, row($table, 'bob')['frames_for']);
    same(3, row($table, 'bob')['frames_against']);
    same(-2, row($table, 'bob')['frame_diff']);
});

test('a match still being entered does not count', function () {
    $table = CompStandings::table(
        [player('ann'), player('bob')],
        [played('ann', 3, 'bob', 1, false)]
    );

    same(0, row($table, 'ann')['played']);
    same(0, row($table, 'ann')['points']);
});

test('frame difference separates players level on points', function () {
    // Ann and Cal both win once; Ann by more.
    $table = CompStandings::table(
        [player('ann'), player('bob'), player('cal')],
        [
            played('ann', 3, 'bob', 0),
            played('cal', 3, 'bob', 2),
        ]
    );

    same('ann', $table[0]['participant_id'], 'better frame difference should lead');
    same('cal', $table[1]['participant_id']);
    same('bob', $table[2]['participant_id']);
});

test('frames won separate players level on points and difference', function () {
    $table = CompStandings::table(
        [player('ann'), player('bob'), player('cal'), player('dee')],
        [
            played('ann', 3, 'bob', 2),   // ann +1
            played('cal', 4, 'dee', 3),   // cal +1, but more frames won
        ]
    );

    same('cal', $table[0]['participant_id'], 'more frames won should lead');
    same('ann', $table[1]['participant_id']);
});

test('a result against somebody outside the group is ignored', function () {
    $table = CompStandings::table(
        [player('ann'), player('bob')],
        [
            played('ann', 3, 'bob', 1),
            played('ann', 3, 'zoe', 0),   // zoe is not in this group
        ]
    );

    same(1, row($table, 'ann')['played'], 'the stray result should not count');
    same(2, row($table, 'ann')['points']);
});

test('positions run 1, 2, 3 in table order', function () {
    $table = CompStandings::table(
        [player('ann'), player('bob'), player('cal')],
        [played('ann', 3, 'bob', 0)]
    );

    same(1, $table[0]['position']);
    same(2, $table[1]['position']);
    same(3, $table[2]['position']);
});

test('an empty group is an empty table, not an error', function () {
    same([], CompStandings::table([], []));
});

echo "\n";
echo $failed === 0 ? "PASS  {$passed} checks\n" : "FAIL  {$failed} failed, {$passed} passed\n";
exit($failed === 0 ? 0 : 1);
