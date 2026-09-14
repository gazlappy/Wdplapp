<?php
declare(strict_types=1);

/**
 * The only entry point for the WDPL backend.
 *
 * Routing, authentication, CORS, body limits and error shaping all happen here
 * exactly once. Module handlers contain feature logic and nothing else - the
 * previous backend repeated this preamble across seventy files, which is how it
 * drifted out of consistency.
 *
 *   GET|POST /api/index.php?m=<module>&a=<action>
 */

require __DIR__ . '/core/Http.php';
require __DIR__ . '/core/Config.php';
require __DIR__ . '/core/Db.php';
require __DIR__ . '/core/Auth.php';
require __DIR__ . '/core/Captain.php';
require __DIR__ . '/core/Module.php';
require __DIR__ . '/core/Registry.php';
require __DIR__ . '/core/Schema.php';

// Never render PHP notices into a JSON response body.
ini_set('display_errors', '0');
error_reporting(E_ALL);

Http::bootstrap();

try {
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

    Http::ok(($spec['fn'])());
} catch (ApiError $e) {
    Http::fail($e->status, $e->errorCode, $e->getMessage());
} catch (Throwable $e) {
    // Log server-side, return nothing identifying to the caller.
    error_log('[wdpl] ' . get_class($e) . ': ' . $e->getMessage() . ' @ ' . $e->getFile() . ':' . $e->getLine());
    Http::fail(500, 'server_error', 'The server failed to handle that request.');
}
