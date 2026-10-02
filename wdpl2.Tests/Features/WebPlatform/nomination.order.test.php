<?php
declare(strict_types=1);

/**
 * The order a card is filled in, for both kinds of card and for whoever is
 * holding the phone.
 *
 * The fault these guard: in solo - one captain entering both teams - the order
 * was skipped, so a cup card could be filled in any order, blind five and all.
 * NominationOrder::refusal is asked about the side a slot belongs to, so these
 * tests do not care who sent the pick. That is the point.
 *
 *   php wdpl2.Tests/Features/WebPlatform/nomination.order.test.php
 */

require dirname(__DIR__, 3) . '/wdpl2/web-backend/api/modules/scorecards/Order.php';

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
        echo "  FAIL  {$name}\n        " . $e->getMessage() . "\n";
    }
}

function ok(bool $cond, string $why): void { if (!$cond) throw new Exception($why); }

function card(int $n = 15): array
{
    $frames = [];
    for ($i = 0; $i < $n; $i++) {
        $frames[] = ['frame_no' => (string)($i + 1), 'is_doubles' => '0', 'winner' => 'none',
                     'home_player_id' => null, 'home_player_name' => null,
                     'away_player_id' => null, 'away_player_name' => null,
                     'home_player2_id' => null, 'home_player2_name' => null,
                     'away_player2_id' => null, 'away_player2_name' => null];
    }
    return $frames;
}

/** Fills a lead the way the server would once it has been allowed. */
function put(array &$frames, int $i, string $side, string $name): void
{
    $frames[$i][$side . '_player_name'] = $name;
}

/** Tries a lead; fills it only if the order allows. Returns the refusal, or null. */
function pick(array &$frames, int $i, string $side, bool $cup)
{
    $why = NominationOrder::refusal($frames, $i, $side, $cup);
    if ($why === null) put($frames, $i, $side, $side . ($i + 1));
    return $why;
}

echo "cup tie\n";

test('home names first; away cannot start', function () {
    $f = card();
    ok(pick($f, 0, 'away', true) !== null, 'away named frame 1 before home named anything');
    ok(pick($f, 0, 'home', true) === null, 'home could not name frame 1');
});

test('home names one, then away names two, then home two', function () {
    $f = card();
    ok(pick($f, 0, 'home', true) === null, 'home 1');
    ok(pick($f, 1, 'home', true) !== null, 'home named a second before away answered');
    ok(pick($f, 0, 'away', true) === null, 'away 1');
    ok(pick($f, 1, 'away', true) === null, 'away 2');
    ok(pick($f, 2, 'away', true) !== null, 'away named a third');
    ok(pick($f, 1, 'home', true) === null, 'home 2');
    ok(pick($f, 2, 'home', true) === null, 'home 3');
    ok(pick($f, 3, 'home', true) !== null, 'home named a fourth in a row');
});

test('frames are filled in order - no skipping ahead', function () {
    $f = card();
    ok(pick($f, 4, 'home', true) !== null, 'home jumped to frame 5');
    ok(pick($f, 0, 'home', true) === null, 'home frame 1');
    ok(pick($f, 3, 'away', true) !== null, 'away jumped to frame 4');
});

test('the blind five stay shut until both have named ten', function () {
    $f = card();
    ok(pick($f, 10, 'home', true) !== null, 'home opened frame 11 on an empty card');
    for ($i = 0; $i < 10; $i++) { put($f, $i, 'home', 'h'); }
    for ($i = 0; $i < 9; $i++)  { put($f, $i, 'away', 'a'); }
    ok(pick($f, 10, 'home', true) !== null, 'frame 11 opened with away still owing one');
    put($f, 9, 'away', 'a');
    ok(pick($f, 10, 'home', true) === null, 'frame 11 still shut after both named ten');
});

test('away names the blind five only after all five of home', function () {
    $f = card();
    for ($i = 0; $i < 10; $i++) { put($f, $i, 'home', 'h'); put($f, $i, 'away', 'a'); }
    for ($i = 10; $i < 14; $i++) { put($f, $i, 'home', 'h'); }
    ok(pick($f, 10, 'away', true) !== null, 'away saw the blind five with one of home still to name');
    put($f, 14, 'home', 'h');
    ok(pick($f, 10, 'away', true) === null, 'away still shut out after home named all five');
});

test('a whole cup card can be filled in the right order', function () {
    $f = card();
    // Home 1, then twos alternating, until both have ten.
    $order = [['home',0]];
    $h = 1; $a = 0;
    while ($h < 10 || $a < 10) {
        for ($k = 0; $k < 2 && $a < 10; $k++) $order[] = ['away', $a++];
        for ($k = 0; $k < 2 && $h < 10; $k++) $order[] = ['home', $h++];
    }
    for ($i = 10; $i < 15; $i++) $order[] = ['home', $i];
    for ($i = 10; $i < 15; $i++) $order[] = ['away', $i];

    foreach ($order as [$side, $i]) {
        $why = pick($f, $i, $side, true);
        ok($why === null, "{$side} frame " . ($i + 1) . " refused: {$why}");
    }
});

test('a doubles partner joins a lead already named, and not before', function () {
    $f = card();
    $f[0]['is_doubles'] = '1';
    ok(NominationOrder::refusal($f, 0, 'home2', true) !== null, 'partner before player 1');
    put($f, 0, 'home', 'h1');
    ok(NominationOrder::refusal($f, 0, 'home2', true) === null, 'partner refused after player 1');
});

test('a named player can be changed until the frame has a result', function () {
    $f = card();
    ok(pick($f, 0, 'home', true) === null, 'home 1');
    ok(pick($f, 0, 'away', true) === null, 'away 1');
    ok(pick($f, 1, 'away', true) === null, 'away 2');
    // Home has named frame 1 and it is not played: home may swap that player,
    // even though it is not home's turn to name anything new.
    ok(NominationOrder::refusal($f, 0, 'home', true) === null, 'could not change frame 1 before it was played');
    ok(NominationOrder::refusal($f, 0, 'home', true, true) === null, 'could not clear frame 1 before it was played');
    // A frame home has not named is still subject to the order.
    ok(NominationOrder::refusal($f, 3, 'home', true) !== null, 'home jumped ahead to frame 4');
});

test('once a frame has a result its players are held', function () {
    $f = card();
    put($f, 0, 'home', 'h1');
    put($f, 0, 'away', 'a1');
    $f[0]['winner'] = 'home';
    $why = NominationOrder::refusal($f, 0, 'home', true);
    ok($why !== null && strpos($why, 'Clear the result first') !== false, 'changed a played frame: ' . var_export($why, true));
    ok(NominationOrder::refusal($f, 0, 'away', true, true) !== null, 'cleared a player from a played frame');
    $f[0]['winner'] = 'none';
    ok(NominationOrder::refusal($f, 0, 'home', true) === null, 'still held after the result was cleared');
});

echo "league night\n";

test('home names any frame, any order', function () {
    $f = card();
    ok(pick($f, 6, 'home', false) === null, 'home frame 7 refused');
    ok(pick($f, 2, 'home', false) === null, 'home frame 3 refused');
});

test('away names a frame only once home has named it', function () {
    $f = card();
    ok(pick($f, 2, 'away', false) !== null, 'away named frame 3 before home');
    put($f, 2, 'home', 'h');
    ok(pick($f, 2, 'away', false) === null, 'away still shut out after home named frame 3');
});

test('away waits for all five of home in the last five', function () {
    $f = card();
    for ($i = 10; $i < 14; $i++) { put($f, $i, 'home', 'h'); }
    ok(pick($f, 12, 'away', false) !== null, 'away saw the last five early');
    put($f, 14, 'home', 'h');
    ok(pick($f, 12, 'away', false) === null, 'away shut out after home named all five');
});

echo "\n" . ($failed === 0 ? "PASS  {$passed} checks\n" : "FAIL  {$failed} of " . ($passed + $failed) . "\n");
exit($failed === 0 ? 0 : 1);
