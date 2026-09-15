<?php
declare(strict_types=1);

/**
 * Pure-PHP regression tests for the backend core.
 *
 * Covers everything that does not need MySQL: the password format shared with
 * the app, module discovery, the auth gate, HTTPS detection and the SQL
 * identifier guard. No composer, no PHPUnit - this runs anywhere PHP does.
 *
 *   php wdpl2.Tests/Features/WebPlatform/backend.rules.test.php
 */

$root = dirname(__DIR__, 3) . '/wdpl2/web-backend/api';

require $root . '/core/Http.php';
require $root . '/core/Config.php';
require $root . '/core/Db.php';
require $root . '/core/Passwords.php';
require $root . '/core/Auth.php';
require $root . '/core/Captain.php';
require $root . '/core/Module.php';
require $root . '/core/Registry.php';
require $root . '/core/Schema.php';

// ---------------------------------------------------------------- tiny runner

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
            ($message !== '' ? $message . ': ' : '') .
            'expected ' . var_export($expected, true) . ', got ' . var_export($actual, true)
        );
    }
}

/** Asserts an ApiError with a particular error code is thrown. */
function throwsApiError(string $expectedCode, callable $fn): void
{
    try {
        $fn();
    } catch (ApiError $e) {
        same($expectedCode, $e->errorCode, 'wrong error code');
        return;
    }
    throw new RuntimeException("expected ApiError '{$expectedCode}', nothing was thrown");
}

function verifyHash(string $password, string $stored): bool
{
    $method = new ReflectionMethod(Auth::class, 'verifyHash');
    $method->setAccessible(true);
    return (bool)$method->invoke(null, $password, $stored);
}

// ------------------------------------------------- admin password compatibility

echo "\nAdmin password format\n";

/*
 * The cross-language contract. This identical string is asserted from the .NET
 * side by PasswordHashTests.Verify_AcceptsAVectorGeneratedByPhp. If either
 * assertion fails, the app and a deployed backend disagree about the admin
 * password and the secretary is locked out.
 */
const SHARED_VECTOR = 'pbkdf2-sha256$210000$KioqKioqKioqKioqKioqKg==$idSuNmNd2D7SOvYi/JIjFbdSi8jexZuBGR/ccUlnu2Q=';

test('accepts the shared cross-language vector', function () {
    check(verifyHash('wdpl-shared-vector', SHARED_VECTOR), 'shared vector was rejected');
});

test('rejects a near-miss password', function () {
    check(!verifyHash('wdpl-shared-vecto', SHARED_VECTOR), 'truncated password was accepted');
    check(!verifyHash('WDPL-shared-vector', SHARED_VECTOR), 'wrong case was accepted');
    check(!verifyHash('', SHARED_VECTOR), 'empty password was accepted');
});

test('rejects malformed stored values', function () {
    foreach ([
        '',
        'not-a-hash',
        'bcrypt$210000$aaaa$bbbb',                 // wrong algorithm label
        'pbkdf2-sha256$100$KioqKioqKioqKioqKioqKg==$aaaa',  // iterations below the floor
        'pbkdf2-sha256$210000$KioqKioqKioqKioqKioqKg==',    // truncated
        'pbkdf2-sha256$210000$!!!$bbbb',           // salt is not base64
    ] as $stored) {
        check(!verifyHash('anything', $stored), "accepted malformed value: {$stored}");
    }
});

test('round-trips a hash generated here', function () {
    $salt = random_bytes(16);
    $hash = hash_pbkdf2('sha256', 'fresh-password', $salt, 210000, 32, true);
    $stored = 'pbkdf2-sha256$210000$' . base64_encode($salt) . '$' . base64_encode($hash);

    check(verifyHash('fresh-password', $stored), 'freshly generated hash was rejected');
});

// --------------------------------------------------- shared password verifier

echo "
Shared password verifier
";

test('Passwords::verify accepts the shared vector', function () {
    check(Passwords::verify('wdpl-shared-vector', SHARED_VECTOR), 'shared vector rejected');
});

test('Auth and captains use the SAME verifier', function () {
    // If these ever diverge, one realm can end up weaker than the other without
    // anything failing loudly. Auth::verifyHash must delegate to Passwords.
    $method = new ReflectionMethod(Auth::class, 'verifyHash');
    $method->setAccessible(true);
    same(
        Passwords::verify('wdpl-shared-vector', SHARED_VECTOR),
        (bool)$method->invoke(null, 'wdpl-shared-vector', SHARED_VECTOR),
        'Auth::verifyHash disagrees with Passwords::verify'
    );
});

test('isHash rejects a plaintext PIN', function () {
    // The captains push refuses anything that is not already hashed, so a
    // plaintext PIN cannot be stored as though it were a hash.
    check(!Passwords::isHash('1234'), 'plaintext accepted as a hash');
    check(!Passwords::isHash(''), 'empty accepted as a hash');
    check(!Passwords::isHash('sha256:abcdef'), 'foreign scheme accepted as a hash');
    check(Passwords::isHash(SHARED_VECTOR), 'a real hash was rejected');
});

test('a weak unsalted SHA-256 is not accepted', function () {
    // The scheme this replaced. hash('sha256', '1234') must never verify.
    check(!Passwords::verify('1234', hash('sha256', '1234')), 'bare sha256 verified');
    check(!Passwords::isHash(hash('sha256', '1234')), 'bare sha256 looked like a hash');
});

// ------------------------------------------------------------ module discovery

echo "\nModule registry\n";

test('discovers the system module', function () {
    $modules = Registry::all();
    check(isset($modules['system']), 'system module was not discovered');
    same('SystemModule', $modules['system'], 'unexpected class for system module');
});

test('every module id matches its folder and class', function () {
    foreach (Registry::all() as $id => $class) {
        same($id, $class::id(), "module {$class} reports a different id");
        check($class::schemaVersion() >= 1, "module {$id} has no schema version");
        check($class::title() !== '', "module {$id} has no title");
    }
});

test('unknown module is a 404, not a crash', function () {
    throwsApiError('unknown_module', function () { return Registry::find('nope'); });
});

test('every declared action has a valid role and callable', function () {
    $valid = [Role::Public, Role::Captain, Role::Admin];
    foreach (Registry::all() as $id => $class) {
        foreach ($class::actions() as $action => $spec) {
            check(isset($spec['role'], $spec['fn']), "{$id}/{$action} is missing role or fn");
            check(in_array($spec['role'], $valid, true), "{$id}/{$action} has an invalid role");
            check(is_callable($spec['fn']), "{$id}/{$action} is not callable");
        }
    }
});

test('system exposes the actions the app calls', function () {
    $actions = SystemModule::actions();
    foreach (['ping', 'login', 'logout', 'whoami', 'health', 'install'] as $expected) {
        check(isset($actions[$expected]), "system is missing the {$expected} action");
    }
    same(Role::Admin, $actions['health']['role'], 'health must require admin');
    same(Role::Admin, $actions['install']['role'], 'install must require admin');
    same(Role::Public, $actions['ping']['role'], 'ping must be public');
});

// ------------------------------------------------------------------ auth gates

echo "\nAuth gates\n";

test('public role passes without credentials', function () {
    Auth::gate(Role::Public);   // must not throw
});

test('unknown role is a programming error, not a silent pass', function () {
    $threw = false;
    try {
        Auth::gate('superuser');
    } catch (LogicException $ignored) {
        $threw = true;
    }
    check($threw, 'an unknown role was allowed through');
});

test('admin gate refuses a plaintext request', function () {
    $_SERVER['HTTPS'] = '';
    $_SERVER['SERVER_PORT'] = 80;
    throwsApiError('https_required', function () { return Auth::gate(Role::Admin); });
});

test('captain gate refuses a plaintext request', function () {
    $_SERVER['HTTPS'] = '';
    $_SERVER['SERVER_PORT'] = 80;
    throwsApiError('https_required', function () { return Auth::gate(Role::Captain); });
});

// ---------------------------------------------------------- captain scoping

echo "
Captain scoping
";

test('captains module exposes the expected roles', function () {
    $actions = CaptainsModule::actions();

    // Anything that reveals a team's data must be captain-gated; the sign-in
    // flow itself must not be, or nobody could ever sign in.
    foreach (['me', 'fixtures', 'roster', 'contacts'] as $private) {
        check(isset($actions[$private]), "missing action {$private}");
        same(Role::Captain, $actions[$private]['role'], "{$private} must require a captain");
    }
    foreach (['teams', 'login', 'logout'] as $open) {
        same(Role::Public, $actions[$open]['role'], "{$open} must be reachable without a session");
    }
    foreach (['push', 'status'] as $admin) {
        same(Role::Admin, $actions[$admin]['role'], "{$admin} must require admin");
    }
});

test('no captain session means no team id', function () {
    // Captain::requireTeamId is what every scoped query filters on, so it must
    // refuse rather than return null and let a query run unfiltered.
    throwsApiError('captain_required', function () { return Captain::requireTeamId(); });
});

// -------------------------------------------------- scorecard ownership

echo "
Scorecard ownership
";

test('only the app may hand cards out and take them back', function () {
    $actions = ScorecardsModule::actions();

    // Ownership transitions belong to the league, not to captains. If any of
    // these became captain-reachable, a captain could seize or release a card.
    foreach (['open', 'claim', 'reopen', 'state'] as $adminOnly) {
        check(isset($actions[$adminOnly]), "missing action {$adminOnly}");
        same(Role::Admin, $actions[$adminOnly]['role'], "{$adminOnly} must be admin-only");
    }

    // Scoring belongs to captains, and nothing here may be anonymous.
    // apply carries every edit as a batch of ops; roster feeds the picker.
    foreach (['mine', 'card', 'roster', 'apply', 'finalise'] as $captainOnly) {
        check(isset($actions[$captainOnly]), "missing action {$captainOnly}");
        same(Role::Captain, $actions[$captainOnly]['role'], "{$captainOnly} must require a captain");
    }
});

test('no scorecard action is reachable anonymously', function () {
    foreach (ScorecardsModule::actions() as $name => $spec) {
        check($spec['role'] !== Role::Public, "{$name} must not be public");
    }
});

test('the three ownership states are distinct', function () {
    $states = [
        ScorecardsModule::STATE_LIVE,
        ScorecardsModule::STATE_FINALISED,
        ScorecardsModule::STATE_CLAIMED,
    ];
    same(3, count(array_unique($states)), 'ownership states collide');
    foreach ($states as $state) {
        check(is_string($state) && $state !== '', 'an ownership state is empty');
    }
});

test('a conflict carries the current state back', function () {
    // Telling a captain "that failed" is not enough - they need to see what the
    // card says now, or they cannot tell whose entry won.
    $conflict = new ApiConflict(['version' => 7, 'home_score' => 3]);
    same(7, $conflict->current['version'], 'conflict lost its payload');
    check($conflict->getMessage() !== '', 'conflict has no message');
});

// ------------------------------------------------------------ HTTPS detection

echo "\nHTTPS detection\n";

test('detects TLS from the HTTPS server variable', function () {
    $_SERVER['HTTPS'] = 'on';
    $_SERVER['SERVER_PORT'] = 443;
    check(Http::isSecure(), 'HTTPS=on was not treated as secure');
});

test('treats HTTPS=off as plaintext', function () {
    $_SERVER['HTTPS'] = 'off';
    $_SERVER['SERVER_PORT'] = 80;
    check(!Http::isSecure(), 'HTTPS=off was treated as secure');
});

test('does not trust a forwarded proto header by default', function () {
    // Anyone can send this header directly, so it must be ignored unless the
    // sidecar explicitly says a proxy terminates TLS.
    $_SERVER['HTTPS'] = '';
    $_SERVER['SERVER_PORT'] = 80;
    $_SERVER['HTTP_X_FORWARDED_PROTO'] = 'https';

    check(!Http::isSecure(), 'a spoofable forwarded header was trusted');

    unset($_SERVER['HTTP_X_FORWARDED_PROTO']);
});

// --------------------------------------------------------- configuration guard

echo "\nConfiguration\n";

test('a missing sidecar is reported, not fatal', function () {
    // config.php is deliberately absent from the repository.
    throwsApiError('not_configured', function () { return Config::load(); });
});

test('get() falls back to a default when unconfigured', function () {
    same('fallback', Config::get('db_name', 'fallback'), 'get() did not fall back');
});

// ------------------------------------------------------------ SQL identifiers

echo "\nSQL identifier guard\n";

test('accepts ordinary table and column names', function () {
    same('`wdpl_teams`', Db::identifier('wdpl_teams'));
    same('`version`', Db::identifier('version'));
});

test('rejects anything that could break out of an identifier', function () {
    foreach (['teams; DROP TABLE x', 'teams`', '1teams', '', 'a b', 'teams--'] as $bad) {
        $threw = false;
        try {
            Db::identifier($bad);
        } catch (LogicException $ignored) {
            $threw = true;
        }
        check($threw, "unsafe identifier was accepted: {$bad}");
    }
});

// ----------------------------------------------------------------------- done

echo "\n";
echo $failed === 0
    ? "PASS  {$passed} checks\n"
    : "FAIL  {$failed} failed, {$passed} passed\n";

exit($failed === 0 ? 0 : 1);
