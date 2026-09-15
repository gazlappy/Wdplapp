<?php
declare(strict_types=1);

/**
 * WDPL scorecard rule tests.
 *
 * These are the league's playing rules, so they get their own suite. No
 * database, no HTTP - just the rules, which is the part that has to be right.
 *
 *   php wdpl2.Tests/Features/WebPlatform/scorecard.rules.test.php
 */

require dirname(__DIR__, 3) . '/wdpl2/web-backend/api/modules/scorecards/Rules.php';

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

/** Builds a card of $n empty frames. */
function frames(int $n): array
{
    $out = [];
    for ($i = 1; $i <= $n; $i++) {
        $out[] = [
            'frame_no' => $i, 'is_doubles' => 0, 'winner' => 'none',
            'home_player_id' => null, 'home_player_name' => null,
            'home_player2_id' => null, 'home_player2_name' => null,
            'away_player_id' => null, 'away_player_name' => null,
            'away_player2_id' => null, 'away_player2_name' => null,
        ];
    }
    return $out;
}

function put(array &$frames, int $index, string $slot, $id, $name = null): void
{
    $prefix = ScorecardRules::slotPrefix($slot);
    $frames[$index][$prefix . '_id'] = $id;
    $frames[$index][$prefix . '_name'] = $name;
}

function player(int $n): string
{
    return sprintf('%08d-0000-4000-8000-000000000000', $n);
}

// ------------------------------------------------------- nomination order

echo "\nNomination order (the away captain picks after the home captain)\n";

test('early frames unlock one at a time, as the home lead is named', function () {
    $f = frames(15);

    check(ScorecardRules::awaySlotLocked($f, 0), 'frame 1 should start locked');
    put($f, 0, 'home', player(1));
    check(!ScorecardRules::awaySlotLocked($f, 0), 'frame 1 should unlock once its home lead is set');

    // Naming frame 1 must not unlock frame 2.
    check(ScorecardRules::awaySlotLocked($f, 1), 'frame 2 unlocked too early');
});

test('the last five frames are blind: all five home leads first', function () {
    $f = frames(15);

    // Fill every early frame. The closing five must still be locked.
    for ($i = 0; $i < 10; $i++) {
        put($f, $i, 'home', player($i + 1));
    }
    for ($i = 10; $i < 15; $i++) {
        check(ScorecardRules::awaySlotLocked($f, $i), "frame " . ($i + 1) . " should be blind-locked");
    }

    // Four of the five is not enough - that is the whole point of the rule.
    for ($i = 10; $i < 14; $i++) {
        put($f, $i, 'home', player($i + 1));
    }
    check(ScorecardRules::blindWindowLocked($f), 'four of five home leads should still lock the window');
    check(ScorecardRules::awaySlotLocked($f, 10), 'frame 11 should still be locked');

    // The fifth releases all five at once.
    put($f, 14, 'home', player(15));
    check(!ScorecardRules::blindWindowLocked($f), 'all five home leads should open the window');
    for ($i = 10; $i < 15; $i++) {
        check(!ScorecardRules::awaySlotLocked($f, $i), "frame " . ($i + 1) . " should be unlocked");
    }
});

test('a short card has no blind window', function () {
    $f = frames(4);
    check(!ScorecardRules::blindWindowLocked($f), 'fewer frames than the window should not lock');
});

test('the refusal explains which rule applied', function () {
    $f = frames(15);
    check(strpos(ScorecardRules::awayLockReason($f, 0), 'frame 1') !== false,
        'early lock should name the frame');
    check(strpos(ScorecardRules::awayLockReason($f, 14), 'last 5') !== false,
        'blind lock should mention the last five');
});

// ------------------------------------------------------------ player picks

echo "\nSelection rules\n";

test('a player may not play both sides of one frame', function () {
    $f = frames(15);
    put($f, 0, 'home', player(1));
    same('Same player cannot play both sides of a frame.',
        ScorecardRules::rejectPick($f, 0, 'away', player(1), null, 3));
});

test('doubles partners must be two different people', function () {
    $f = frames(15);
    $f[0]['is_doubles'] = 1;
    put($f, 0, 'home', player(1));
    same('Player 2 must be different from Player 1.',
        ScorecardRules::rejectPick($f, 0, 'home2', player(1), null, 3));
});

test('a player may only play the permitted number of frames', function () {
    $f = frames(15);
    put($f, 0, 'home', player(1));
    put($f, 1, 'home', player(1));
    put($f, 2, 'home', player(1));

    $reason = ScorecardRules::rejectPick($f, 3, 'home', player(1), 'Alice', 3);
    check($reason !== null, 'a fourth frame should be refused');
    check(strpos($reason, 'Alice') !== false, 'the refusal should name the player');
    check(strpos($reason, '3') !== false, 'the refusal should state the limit');

    // The same player on the OTHER side is a different count.
    same(null, ScorecardRules::rejectPick($f, 3, 'away', player(1), 'Alice', 3),
        'the limit is per side');
});

test('replacing a player in a slot does not count against them', function () {
    $f = frames(15);
    put($f, 0, 'home', player(1));
    put($f, 1, 'home', player(1));
    put($f, 2, 'home', player(1));

    // Re-picking the same player into a slot they already hold is not a fourth frame.
    same(null, ScorecardRules::rejectPick($f, 2, 'home', player(1), 'Alice', 3));
});

test('doubles counts as one frame for a player, not two', function () {
    $f = frames(15);
    $f[0]['is_doubles'] = 1;
    put($f, 0, 'home', player(1));
    put($f, 0, 'home2', player(2));
    put($f, 1, 'home', player(1));

    // Player 1 has played frames 1 and 2. A third is still allowed.
    same(null, ScorecardRules::rejectPick($f, 2, 'home', player(1), 'Alice', 3));
});

test('the same two players cannot meet twice', function () {
    $f = frames(15);
    put($f, 0, 'home', player(1));
    put($f, 0, 'away', player(9));
    put($f, 1, 'home', player(1));

    $reason = ScorecardRules::rejectPick($f, 1, 'away', player(9), 'Bob', 3);
    check($reason !== null, 'a repeat pairing should be refused');
    check(strpos($reason, 'frame 1') !== false, 'the refusal should name the earlier frame');

    // A different opponent is fine.
    same(null, ScorecardRules::rejectPick($f, 1, 'away', player(8), 'Carol', 3));
});

test('clearing a slot is always allowed', function () {
    $f = frames(15);
    put($f, 0, 'home', player(1));
    put($f, 1, 'home', player(1));
    put($f, 2, 'home', player(1));
    same(null, ScorecardRules::rejectPick($f, 3, 'home', null, null, 3));
    same(null, ScorecardRules::rejectPick($f, 3, 'home', null, '', 3));
});

test('VOID is a concession and escapes every selection limit', function () {
    $f = frames(15);
    $void = ScorecardRules::VOID_PLAYER_ID;

    for ($i = 0; $i < 6; $i++) {
        put($f, $i, 'home', $void, 'VOID');
    }
    // Far more than max-per-player, and repeated against the same opponent.
    same(null, ScorecardRules::rejectPick($f, 6, 'home', $void, 'VOID', 3),
        'a side must be able to concede as many frames as it needs to');

    check(ScorecardRules::isVoid($void, null), 'void id not recognised');
    check(ScorecardRules::isVoid(null, 'void'), 'void name not recognised (case)');
});

test('players typed in on the night are matched by name', function () {
    $f = frames(15);
    // No ids - a walk-up player the captain typed. Two picks of the same name
    // must count as the same person, or the limits mean nothing.
    put($f, 0, 'home', null, 'Dave Smith');
    put($f, 1, 'home', null, 'dave smith');
    put($f, 2, 'home', null, 'DAVE SMITH');

    $reason = ScorecardRules::rejectPick($f, 3, 'home', null, 'Dave Smith', 3);
    check($reason !== null, 'ad-hoc players should still be limited');
});

// ------------------------------------------------------------------ scoring

echo "\nScoring\n";

test('the score counts frames won by each side', function () {
    $f = frames(5);
    $f[0]['winner'] = 'home';
    $f[1]['winner'] = 'away';
    $f[2]['winner'] = 'home';

    $score = ScorecardRules::score($f);
    same(2, $score['home']);
    same(1, $score['away']);
    same(3, $score['played'], 'unplayed frames must not count');
});

test('finalising requires every frame to have a result', function () {
    $f = frames(3);
    check(!ScorecardRules::allFramesScored($f), 'an empty card is not complete');

    $f[0]['winner'] = 'home';
    $f[1]['winner'] = 'away';
    same([3], ScorecardRules::unscoredFrames($f), 'should report exactly which frame is missing');

    $f[2]['winner'] = 'home';
    check(ScorecardRules::allFramesScored($f), 'a fully scored card should be complete');
    same([], ScorecardRules::unscoredFrames($f));
});

test('an empty card is never complete', function () {
    check(!ScorecardRules::allFramesScored([]), 'a card with no frames is not a finished match');
});

// ------------------------------------------------------------------- slots

echo "\nSlots\n";

test('only real slots are accepted', function () {
    foreach (['home', 'home2', 'away', 'away2'] as $slot) {
        check(ScorecardRules::isValidSlot($slot), "{$slot} should be valid");
    }
    foreach (['', 'home3', 'HOME', 'winner', 'home_player'] as $slot) {
        check(!ScorecardRules::isValidSlot($slot), "{$slot} should be rejected");
    }
});

test('a captain is only offered their own slots', function () {
    same(['home', 'home2'], ScorecardRules::slotsFor('home'));
    same(['away', 'away2'], ScorecardRules::slotsFor('away'));
});

echo "\n";
echo $failed === 0 ? "PASS  {$passed} checks\n" : "FAIL  {$failed} failed, {$passed} passed\n";
exit($failed === 0 ? 0 : 1);
