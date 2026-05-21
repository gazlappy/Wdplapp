<?php
// _db.php — shared database connection + helpers.
// PHP 5.6 compatible (no return types, no strict_types, no ??).
//
// Credentials live in _db.config.php (same folder) so deploys from the
// MAUI app don't trample your real DB password. If _db.config.php is
// missing, the hard-coded fallbacks below are used (edit at your peril —
// the next backend deploy will overwrite this file).

if (is_file(__DIR__ . '/_db.config.php')) {
    include_once __DIR__ . '/_db.config.php';
}
if (!defined('DB_HOST')) define('DB_HOST', 'localhost');
if (!defined('DB_NAME')) define('DB_NAME', 'youracct_inbox');   // <-- fallback only
if (!defined('DB_USER')) define('DB_USER', 'youracct_wdpl');    // <-- fallback only
if (!defined('DB_PASS')) define('DB_PASS', 'CHANGE-ME');        // <-- fallback only

// Turn unexpected errors into a JSON 500 so the client sees the cause
// instead of a blank Apache 500 page. Safe for a private admin tool.
set_exception_handler(function ($ex) {
    if (!headers_sent()) {
        http_response_code(500);
        header('Content-Type: application/json; charset=utf-8');
        header('Access-Control-Allow-Origin: *');
    }
    echo json_encode(array(
        'error'   => 'server_exception',
        'message' => $ex->getMessage(),
        'where'   => basename($ex->getFile()) . ':' . $ex->getLine(),
    ));
    exit;
});
set_error_handler(function ($severity, $message, $file, $line) {
    if (!(error_reporting() & $severity)) return false;
    throw new ErrorException($message, 0, $severity, $file, $line);
});

function db() {
    static $pdo = null;
    if ($pdo === null) {
        $pdo = new PDO(
            'mysql:host=' . DB_HOST . ';dbname=' . DB_NAME . ';charset=utf8mb4',
            DB_USER, DB_PASS,
            array(
                PDO::ATTR_ERRMODE            => PDO::ERRMODE_EXCEPTION,
                PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
                PDO::ATTR_EMULATE_PREPARES   => false,
            )
        );
    }
    return $pdo;
}

function send_cors_headers() {
    header('Access-Control-Allow-Origin: *'); // tighten to your domain if you prefer
    header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
    header('Access-Control-Allow-Headers: Content-Type, Authorization, X-Captain-Token');
    header('Access-Control-Max-Age: 86400');
}

// Always answer the browser's CORS preflight request immediately.
if (isset($_SERVER['REQUEST_METHOD']) && $_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    send_cors_headers();
    http_response_code(204);
    exit;
}

function json_response($data, $status = 200) {
    http_response_code($status);
    header('Content-Type: application/json; charset=utf-8');
    header('Cache-Control: no-store');
    send_cors_headers();
    echo json_encode($data, JSON_UNESCAPED_SLASHES);
    exit;
}

function require_post() {
    $method = isset($_SERVER['REQUEST_METHOD']) ? $_SERVER['REQUEST_METHOD'] : '';
    if ($method !== 'POST') {
        json_response(array('error' => 'POST required'), 405);
    }
}

function read_json_body() {
    $raw = file_get_contents('php://input');
    if ($raw === false) $raw = '';
    $data = json_decode($raw, true);
    if (is_array($data)) return $data;
    // Fall back to form-encoded posts so plain HTML forms also work.
    if (!empty($_POST)) return $_POST;
    return array();
}
