<?php
declare(strict_types=1);

/**
 * Cup nomination order.
 *
 * The league's rule, in its own words: the home team fills in one player, the
 * away team fills in two, the home team two, and so on to frame ten; then the
 * home captain fills in the last five before the away captain fills in theirs.
 *
 *   php wdpl2.Tests/Features/WebPlatform/cup.rules.test.php
 */

require dirname(__DIR__, 3) . '/wdpl2/web-backend/api/modules/scorecards/CupRules.php';

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

/** A blank card of 15 frames. */
function card(): array
{
    $out = [];
    for ($i = 1; $i <= 15; $i++) {
        $out[] = [
            'frame_no' => $i,
            'home_player_id' => null, 'home_player_name' => null,
            'away_player_id' => null, 'away_player_name' => null,
        ];
    }
    return $out;
}

/** Names a side's lead in one frame. */
function name(array &$frames, int $index, string $side): void
{
    $prefix = $side === 'home' ? 'home_player' : 'away_player';
    $frames[$index][$prefix . '_name'] = strtoupper($side) . ' ' . ($index + 1);
}

/**
 * Fills the card the way the rules allow, and records the order it happened in.
 *
 * Nothing here knows the intended pattern - it just keeps asking both sides
 * whether they may pick, which is what a captain does. The pattern the league
 * described has to fall out of the rules, or this produces something else.
 */
function playOut(array &$frames, int $stopAfter = 999): array
{
    $order = [];

    for ($step = 0; $step < 200 && count($order) < $stopAfter; $step++) {
        $moved = false;

        foreach (['home', 'away'] as $side) {
            $next = CupRules::nextOpenFrame($frames, $side);
            if ($next === null) continue;

            if (!CupRules::slotLocked($frames, $next, $side)) {
                name($frames, $next, $side);
                $order[] = $side;
                $moved = true;
                break;
            }
        }

        if (!$moved) break;
    }

    return $order;
}

/** Turns a list of sides into the block pattern, e.g. "H1 A2 H2". */
function blocks(array $order): string
{
    $out = [];
    $run = null;
    $n = 0;

    foreach ($order as $side) {
        if ($side === $run) { $n++; continue; }
        if ($run !== null) $out[] = strtoupper($run[0]) . $n;
        $run = $side;
        $n = 1;
    }

    if ($run !== null) $out[] = strtoupper($run[0]) . $n;

    return implode(' ', $out);
}

echo "\nCup nomination order\n";

test('home opens the card with one name', function () {
    $f = card();

    check(!CupRules::slotLocked($f, 0, 'home'), 'home should be able to start');
    check(CupRules::slotLocked($f, 0, 'away'), 'away cannot start the card');
});

test('away answers with two once home has named one', function () {
    $f = card();
    name($f, 0, 'home');

    check(!CupRules::slotLocked($f, 0, 'away'), 'away should answer frame 1');
    name($f, 0, 'away');

    check(!CupRules::slotLocked($f, 1, 'away'), 'and take a second');
    name($f, 1, 'away');

    // Two is their lot until home replies.
    check(CupRules::slotLocked($f, 2, 'away'), 'away should not get a third');
});

test('home then takes two', function () {
    $f = card();
    name($f, 0, 'home');
    name($f, 0, 'away');
    name($f, 1, 'away');

    check(!CupRules::slotLocked($f, 1, 'home'), 'home should get a second');
    name($f, 1, 'home');

    check(!CupRules::slotLocked($f, 2, 'home'), 'and a third');
    name($f, 2, 'home');

    check(CupRules::slotLocked($f, 3, 'home'), 'but not a fourth');
});

test('the whole card comes out as one, two, two to frame ten', function () {
    $f = card();
    $order = playOut($f, 20);

    // The league's rule, read back off the card rather than assumed.
    same('H1 A2 H2 A2 H2 A2 H2 A2 H2 A2 H1', blocks($order));

    same(10, CupRules::named($f, 'home'));
    same(10, CupRules::named($f, 'away'));
    check(CupRules::openFramesDone($f), 'the first ten should be filled');
});

test('the closing five stay shut until the first ten are filled', function () {
    $f = card();
    name($f, 0, 'home');

    check(CupRules::slotLocked($f, 10, 'home'), 'frame 11 should be shut');
    check(CupRules::slotLocked($f, 14, 'away'), 'and frame 15 too');
});

test('home names all five of the closing frames before away sees any', function () {
    $f = card();
    playOut($f, 20);

    // Home may name any of them; away none.
    for ($i = 10; $i < 15; $i++) {
        check(!CupRules::slotLocked($f, $i, 'home'), "home should open frame " . ($i + 1));
        check(CupRules::slotLocked($f, $i, 'away'), "away should not see frame " . ($i + 1));
    }

    // Four of the five is not enough to unlock the away side.
    for ($i = 10; $i < 14; $i++) name($f, $i, 'home');
    check(CupRules::slotLocked($f, 14, 'away'), 'four out of five should still be shut');

    name($f, 14, 'home');
    check(CupRules::blindFramesNamed($f), 'all five named');

    for ($i = 10; $i < 15; $i++) {
        check(!CupRules::slotLocked($f, $i, 'away'), 'away should now see them all');
    }
});

test('the open frames are filled in order', function () {
    $f = card();

    // Home may open frame 1, and only frame 1.
    check(!CupRules::slotLocked($f, 0, 'home'), 'frame 1 should be open to home');
    check(CupRules::slotLocked($f, 3, 'home'), 'no skipping ahead');
});

test('the card says whose turn it is', function () {
    $f = card();
    same('home', CupRules::turn($f)['side']);

    name($f, 0, 'home');
    same('away', CupRules::turn($f)['side'], 'away answers');

    playOut($f, 20);
    same('home', CupRules::turn($f)['side'], 'home names the closing five');

    for ($i = 10; $i < 15; $i++) name($f, $i, 'home');
    same('away', CupRules::turn($f)['side'], 'then away');
});

test('the allowance follows the blocks the other side has finished', function () {
    // Home opens before anybody has replied.
    same(1, CupRules::allowance('home', 0, 0));
    same(0, CupRules::allowance('away', 0, 0), 'away cannot start');

    same(2, CupRules::allowance('away', 1, 0));
    same(3, CupRules::allowance('home', 1, 2));
    same(4, CupRules::allowance('away', 3, 2));

    // Neither side runs past ten.
    same(10, CupRules::allowance('home', 9, 10));
    same(10, CupRules::allowance('away', 10, 9));
});

test('a card that is not fifteen frames still behaves', function () {
    $f = [];
    for ($i = 1; $i <= 10; $i++) {
        $f[] = ['frame_no' => $i, 'home_player_id' => null, 'home_player_name' => null,
                'away_player_id' => null, 'away_player_name' => null];
    }

    $order = playOut($f, 20);
    same('H1 A2 H2 A2 H2 A2 H2 A2 H2 A2 H1', blocks($order), 'ten frames, no blind five');
    check(CupRules::openFramesDone($f), 'both sides should have named all ten');
});

echo "\n";
echo $failed === 0 ? "PASS  {$passed} checks\n" : "FAIL  {$failed} failed, {$passed} passed\n";
exit($failed === 0 ? 0 : 1);
