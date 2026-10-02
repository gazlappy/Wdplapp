<?php
declare(strict_types=1);

/**
 * The captains' card notes, as the league reads them in Messages.
 *
 *   php wdpl2.Tests/Features/WebPlatform/card.messages.test.php
 */

$root = dirname(__DIR__, 3) . '/wdpl2/web-backend/api';

require $root . '/core/Http.php';
require $root . '/core/Config.php';
require $root . '/core/Db.php';
require $root . '/core/Passwords.php';
require $root . '/core/Auth.php';
require $root . '/core/Captain.php';
require $root . '/core/Module.php';
require $root . '/modules/scorecards/Module.php';

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
        echo "  FAIL  {$name}\n";
        echo "          {$e->getMessage()}\n";
    }
}

function same($expected, $actual, string $message = ''): void
{
    if ($expected !== $actual) {
        throw new RuntimeException(
            ($message !== '' ? $message . ': ' : '') .
            'expected ' . var_export($expected, true) . ', got ' . var_export($actual, true)
        );
    }
}

function row(array $over = []): array
{
    return array_merge([
        'fixture_id' => 'f1', 'card_kind' => 'league', 'state' => 'live', 'match_date' => '2026-10-01',
        'home_team_name' => 'LEGENDS', 'away_team_name' => 'CHANGING LANES',
        'notes_at' => '2026-10-01 20:15:00', 'notes_read_at' => null, 'opened_at' => '2026-10-01 19:00:00',
    ], $over);
}

test('a captain\'s note is the message', function () {
    same('Started 20 minutes late.', ScorecardsModule::messageText("  Started 20 minutes late.\n"));
});

test('the solo stamp is not a message on its own', function () {
    same('', ScorecardsModule::messageText('[Submitted from one device by the home captain.]'));
});

test('words written with a solo card keep, without the stamp', function () {
    same('Their phone died.',
        ScorecardsModule::messageText("Their phone died.\n[Submitted from one device by the away captain.]"));
});

test('a note never read is unread', function () {
    same(true, ScorecardsModule::message(row(), 'x')['unread']);
});

test('a note read since it was written is read', function () {
    same(false, ScorecardsModule::message(row(['notes_read_at' => '2026-10-01 21:00:00']), 'x')['unread']);
});

test('a note changed after it was read is unread again', function () {
    $m = ScorecardsModule::message(row(['notes_at' => '2026-10-01 22:00:00', 'notes_read_at' => '2026-10-01 21:00:00']), 'x');
    same(true, $m['unread']);
});

test('a note from before notes were timed shows when the card opened', function () {
    $m = ScorecardsModule::message(row(['notes_at' => null]), 'x');
    same('2026-10-01 19:00:00', $m['written_at']);
    same(true, $m['is_cup'] === false);
});

echo "\n";
echo $failed === 0 ? "PASS  {$passed} checks\n" : "FAIL  {$failed} failed, {$passed} passed\n";
exit($failed === 0 ? 0 : 1);
