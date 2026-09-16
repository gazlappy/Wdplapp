<?php
declare(strict_types=1);

/**
 * Draw and knockout tree tests.
 *
 * The first case is the league's own sheet, read straight off it: eight slots
 * in the order 1, 5, 3, 7, 2, 6, 4, 8, with the byes falling where the paper
 * says they fall.
 *
 *   php wdpl2.Tests/Features/WebPlatform/comp.draw.test.php
 */

require dirname(__DIR__, 3) . '/wdpl2/web-backend/api/modules/comps/Draw.php';

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
    if (!$condition) throw new RuntimeException($message);
}

function same($expected, $actual, string $message = ''): void
{
    if ($expected !== $actual) {
        throw new RuntimeException(
            ($message !== '' ? $message . ': ' : '')
            . 'expected ' . json_encode($expected) . ', got ' . json_encode($actual)
        );
    }
}

/** Players named for the order they came out of the draw. */
function drawn(int $n): array
{
    $out = [];
    for ($i = 1; $i <= $n; $i++) $out[] = "p{$i}";
    return $out;
}

echo "\nThe draw sheet\n";

test('eight slots run 1, 5, 3, 7, 2, 6, 4, 8 - the league sheet', function () {
    same([1, 5, 3, 7, 2, 6, 4, 8], CompDraw::slotOrder(8));
});

test('the same rule gives four and sixteen', function () {
    same([1, 3, 2, 4], CompDraw::slotOrder(4));
    same([1, 9, 5, 13, 3, 11, 7, 15, 2, 10, 6, 14, 4, 12, 8, 16], CompDraw::slotOrder(16));
});

test('the first drawn goes top, the second into the bottom half', function () {
    $slots = CompDraw::place(drawn(8));

    same('p1', $slots[0], 'first drawn should be at the top');
    same('p2', $slots[4], 'second drawn should start the bottom half');

    // ...so they can only meet in the final.
    $tree = CompDraw::tree($slots);
    same(3, count($tree));
});

test('the bracket grows to the next power of two', function () {
    same(2, CompDraw::bracketSize(2));
    same(4, CompDraw::bracketSize(3));
    same(8, CompDraw::bracketSize(5));
    same(8, CompDraw::bracketSize(8));
    same(16, CompDraw::bracketSize(9));
});

test('six players leave byes exactly where the sheet shows them', function () {
    $slots = CompDraw::place(drawn(6));

    // Draw numbers 7 and 8 do not exist, so those slots are byes.
    same(['p1', 'p5', 'p3', null, 'p2', 'p6', 'p4', null], $slots);
});

test('a bye is settled without anybody pressing anything', function () {
    $tree = CompDraw::tree(CompDraw::place(drawn(6)));

    // Round one: p3 v bye, and p4 v bye, both already decided.
    $first = $tree[0];
    same(4, count($first));

    same('p3', $first[1]['winner'], 'p3 should be through on a bye');
    check($first[1]['complete'], 'the bye should be complete');

    same('p4', $first[3]['winner'], 'p4 should be through on a bye');

    // The real ties are not decided by anybody yet.
    same(null, $first[0]['winner']);
    check(!$first[0]['complete'], 'a real tie should still be to play');
});

test('byes carry into the next round', function () {
    $tree = CompDraw::tree(CompDraw::place(drawn(6)));

    // p3 went through on a bye, so it is already waiting in the semi-final.
    same('p3', $tree[1][0]['p2']);
    same('p4', $tree[1][1]['p2']);

    // ...opposite the winners of the ties still to be played.
    same(null, $tree[1][0]['p1']);
});

test('nobody is ever drawn against two byes', function () {
    // The bracket is the smallest power of two that holds the field, so more
    // than half the slots are always filled and two byes cannot meet. Worth
    // pinning: it is the reason a bye always sends a real player through.
    for ($players = 2; $players <= 40; $players++) {
        $first = CompDraw::tree(CompDraw::place(drawn($players)))[0];

        foreach ($first as $index => $match) {
            check(
                $match['p1'] !== null || $match['p2'] !== null,
                "{$players} players left match {$index} with nobody in it"
            );
        }
    }
});

test('every bye sends exactly one player through', function () {
    $first = CompDraw::tree(CompDraw::place(drawn(5)))[0];

    $byes = 0;
    foreach ($first as $match) {
        if ($match['p1'] === null || $match['p2'] === null) {
            $byes++;
            check($match['complete'], 'a bye should be settled');
            check($match['winner'] !== null, 'a bye should send somebody through');
        }
    }

    same(3, $byes, 'five in an eight bracket is three byes');
});

test('every round is built up front, empty where it is not known', function () {
    $tree = CompDraw::tree(CompDraw::place(drawn(8)));

    same(3, count($tree), 'eight players is three rounds');
    same(4, count($tree[0]));
    same(2, count($tree[1]));
    same(1, count($tree[2]));

    same(null, $tree[2][0]['p1'], 'the final should start empty');
});

test('rounds are named counting back from the final', function () {
    same('Final', CompDraw::roundName(3, 3));
    same('Semi-finals', CompDraw::roundName(2, 3));
    same('Quarter-finals', CompDraw::roundName(1, 3));
    same('Round 1', CompDraw::roundName(1, 4));
});

test('a winner knows which match and which side it goes to', function () {
    // Four matches feeding two: 0 and 1 meet, 2 and 3 meet.
    same([1, 0], CompDraw::nextSlot(0, 0, 3));
    same([1, 0], CompDraw::nextSlot(0, 1, 3));
    same([1, 1], CompDraw::nextSlot(0, 2, 3));

    same('p1', CompDraw::nextSide(0));
    same('p2', CompDraw::nextSide(1));

    same(null, CompDraw::nextSlot(2, 0, 3), 'the final leads nowhere');
});

test('two players is a final and nothing else', function () {
    $tree = CompDraw::tree(CompDraw::place(drawn(2)));

    same(1, count($tree));
    same('p1', $tree[0][0]['p1']);
    same('p2', $tree[0][0]['p2']);
});

test('one player or none is not a knockout', function () {
    same([], CompDraw::tree(CompDraw::place(drawn(1))));
    same([], CompDraw::tree(CompDraw::place([])));
});

echo "\n";
echo $failed === 0 ? "PASS  {$passed} checks\n" : "FAIL  {$failed} failed, {$passed} passed\n";
exit($failed === 0 ? 0 : 1);
