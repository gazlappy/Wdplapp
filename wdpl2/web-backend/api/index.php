<?php

/**
 * The only entry point for the WDPL backend.
 *
 * Routing, authentication, CORS, body limits and error shaping all happen here
 * exactly once. Module handlers contain feature logic and nothing else - the
 * previous backend repeated this preamble across seventy files, which is how it
 * drifted out of consistency.
 *
 *   GET|POST /api/index.php?m=<module>&a=<action>
 *
 * Everything above the try block is deliberately dependency-free and uses the
 * oldest syntax possible, so that a failure ANYWHERE else - including a parse
 * error in a core file on an unexpected PHP version - is still reported as JSON
 * rather than as a blank 500 that cannot be diagnosed without host log access.
 *
 * There is no declare(strict_types=1) here for the same reason: this file has
 * to survive contact with a broken deployment.
 */

ini_set('display_errors', '0');
error_reporting(E_ALL);

/** Emits a JSON error without relying on any core class having loaded. */
function wdpl_emit_error($status, $code, $message)
{
    if (!headers_sent()) {
        http_response_code($status);
        header('Content-Type: application/json; charset=utf-8');
        header('Cache-Control: no-store');
    }
    echo json_encode(array(
        'ok' => false,
        'error' => array('code' => $code, 'message' => $message),
    ));
}

/**
 * Base name and line only - enough to fix, without publishing server paths.
 *
 * The running PHP version is included because a boot failure is very often a
 * version mismatch, and the version the host advertises elsewhere (phpMyAdmin,
 * a control panel) is not always the one serving this directory.
 */
function wdpl_describe($type, $message, $file, $line)
{
    return $type . ': ' . $message
        . ' [' . basename($file) . ':' . $line . ']'
        . ' (PHP ' . PHP_VERSION . ')';
}

/*
 * Last line of defence for failures that are not throwable at all: a compile
 * error in this very file, or exhausted memory. Without this the server returns
 * an empty 500 and the only clue is a host error log, which is not always
 * available or even enabled.
 */
register_shutdown_function(function () {
    $fatal = error_get_last();
    if ($fatal === null) {
        return;
    }
    $fatalTypes = array(E_ERROR, E_PARSE, E_CORE_ERROR, E_COMPILE_ERROR, E_USER_ERROR);
    if (!in_array($fatal['type'], $fatalTypes, true)) {
        return;
    }
    wdpl_emit_error(500, 'php_fatal', wdpl_describe(
        'PHP fatal', $fatal['message'], $fatal['file'], $fatal['line']
    ));
});

try {
    // Inside the try so a parse error in any of these is reported rather than
    // killing the request silently. PHP 7+ raises a catchable ParseError.
    require __DIR__ . '/core/Http.php';
    require __DIR__ . '/core/Config.php';
    require __DIR__ . '/core/Db.php';
    require __DIR__ . '/core/Passwords.php';
    require __DIR__ . '/core/Auth.php';
    require __DIR__ . '/core/Captain.php';
    require __DIR__ . '/core/Runner.php';
    require __DIR__ . '/core/Module.php';
    require __DIR__ . '/core/Registry.php';
    require __DIR__ . '/core/Schema.php';

    Http::bootstrap();

    $moduleId = Http::query('m');
    $action   = Http::query('a');

    if ($moduleId === '' || $action === '') {
        throw new ApiError(400, 'bad_request', 'Both m (module) and a (action) are required.');
    }

    $class   = Registry::find($moduleId);
    $actions = $class::actions();

    if (!isset($actions[$action])) {
        throw new ApiError(404, 'unknown_action', "Module {$moduleId} has no action {$action}.");
    }

    $spec = $actions[$action];
    Auth::gate($spec['role']);

    Http::ok(call_user_func($spec['fn']));
} catch (ApiConflict $e) {
    // A refusal that carries the current state, so the caller can see what
    // actually happened instead of guessing.
    Http::conflict($e->getMessage(), $e->current);
} catch (ApiError $e) {
    Http::fail($e->status, $e->errorCode, $e->getMessage());
} catch (Error $e) {
    /*
     * Engine-level failure: a parse error in a core file, a missing class, a bad
     * call. These mean the deployment itself is broken, so the detail IS
     * returned - a backend that cannot boot has no user data to leak, and
     * without this the only symptom is an unexplained 500.
     */
    error_log('[wdpl] ' . $e->getMessage() . ' @ ' . $e->getFile() . ':' . $e->getLine());
    wdpl_emit_error(500, 'backend_broken', wdpl_describe(
        get_class($e), $e->getMessage(), $e->getFile(), $e->getLine()
    ));
} catch (Throwable $e) {
    // Ordinary runtime exceptions can carry query or data detail, so only the
    // server log gets the specifics.
    error_log('[wdpl] ' . get_class($e) . ': ' . $e->getMessage() . ' @ ' . $e->getFile() . ':' . $e->getLine());
    wdpl_emit_error(500, 'server_error', 'The server failed to handle that request.');
}
