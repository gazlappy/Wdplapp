<?php
// Entry-form endpoints deliberately do not inherit wildcard CORS or diagnostic responses.
define('WDPL_ENTRY_FORMS', true);
require_once __DIR__ . '/_db.php';
require_once __DIR__ . '/_entry_form_rules.php';

set_exception_handler(function ($ex) {
	if ($ex instanceof InvalidArgumentException) ef_reply(array('error' => $ex->getMessage()), 422);
	error_log('WDPL entry forms: ' . get_class($ex));
	ef_reply(array('error' => 'Entry service unavailable. Contact the league secretary.'), 500);
});

function ef_reply($data, $status = 200) {
	http_response_code($status);
	header('Content-Type: application/json; charset=utf-8');
	header('Cache-Control: no-store');
	header('X-Content-Type-Options: nosniff');
	echo json_encode($data, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
	exit;
}

function ef_https() {
	if ((!isset($_SERVER['HTTPS']) || $_SERVER['HTTPS'] === '' || $_SERVER['HTTPS'] === 'off') &&
		(!isset($_SERVER['SERVER_PORT']) || (int)$_SERVER['SERVER_PORT'] !== 443)) {
		ef_reply(array('error' => 'HTTPS required.'), 400);
	}
}

function ef_method($method) {
	if (!isset($_SERVER['REQUEST_METHOD']) || $_SERVER['REQUEST_METHOD'] !== $method) {
		header('Allow: ' . $method);
		ef_reply(array('error' => 'Method not allowed.'), 405);
	}
}

function ef_body($maxBytes) {
	$type = isset($_SERVER['CONTENT_TYPE']) ? strtolower(trim(explode(';', $_SERVER['CONTENT_TYPE'])[0])) : '';
	if ($type !== 'application/json') ef_reply(array('error' => 'JSON content required.'), 415);
	if (isset($_SERVER['CONTENT_LENGTH']) && (int)$_SERVER['CONTENT_LENGTH'] > $maxBytes) ef_reply(array('error' => 'Request too large.'), 413);
	$raw = file_get_contents('php://input', false, null, 0, $maxBytes + 1);
	if ($raw === false || strlen($raw) > $maxBytes) ef_reply(array('error' => 'Request too large.'), 413);
	$data = json_decode($raw);
	if (json_last_error() !== JSON_ERROR_NONE || !($data instanceof stdClass)) ef_reply(array('error' => 'Invalid JSON object.'), 400);
	return $data;
}

function ef_admin() {
	require_once __DIR__ . '/_admin.php';
	$header = isset($_SERVER['HTTP_AUTHORIZATION']) ? $_SERVER['HTTP_AUTHORIZATION'] :
		(isset($_SERVER['REDIRECT_HTTP_AUTHORIZATION']) ? $_SERVER['REDIRECT_HTTP_AUTHORIZATION'] : '');
	if (empty($_SERVER['PHP_AUTH_USER']) && stripos($header, 'Basic ') !== 0) ef_reply(array('error' => 'App admin authentication required.'), 401);
	// These are app endpoints, not cookie-authenticated browser actions. No bootstrap login.
	unset($_COOKIE[ADMIN_COOKIE]);
	unset($_SERVER['HTTP_X_ADMIN_TOKEN']);
	return require_admin('admin');
}

function ef_ensure_schema() {
	$pdo = db();
	$pdo->exec('CREATE TABLE IF NOT EXISTS entry_form_config (
		singleton_id INT NOT NULL PRIMARY KEY, origin VARCHAR(255) NOT NULL, time_zone VARCHAR(64) NOT NULL
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
	$pdo->exec("INSERT IGNORE INTO entry_form_config (singleton_id, origin, time_zone) VALUES (1, '', 'Europe/London')");
	$pdo->exec('CREATE TABLE IF NOT EXISTS entry_form_definitions (
		form_id VARCHAR(37) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
		active TINYINT NOT NULL DEFAULT 0, definition_json MEDIUMTEXT NOT NULL
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
	$pdo->exec('CREATE TABLE IF NOT EXISTS entry_form_submissions (
		sequence_id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
		form_id VARCHAR(37) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
		client_id VARCHAR(128) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
		payload_hash CHAR(64) CHARACTER SET ascii NOT NULL,
		payload_json MEDIUMTEXT NOT NULL, received_utc DATETIME NOT NULL,
		UNIQUE KEY entry_form_identity (form_id, client_id)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
	$pdo->exec('CREATE TABLE IF NOT EXISTS entry_form_rate_limits (
		rate_key CHAR(64) CHARACTER SET ascii NOT NULL, bucket BIGINT NOT NULL, hits INT NOT NULL,
		PRIMARY KEY (rate_key, bucket), INDEX entry_form_rate_expiry (bucket)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
}

function ef_config($lock = false) {
	$config = db()->query('SELECT origin, time_zone FROM entry_form_config WHERE singleton_id = 1' . ($lock ? ' FOR UPDATE' : ''))->fetch();
	if (!$config || $config['origin'] === '') ef_reply(array('error' => 'Entry forms have not been published to this backend.'), 503);
	return $config;
}

function ef_cors($config) {
	$origin = isset($_SERVER['HTTP_ORIGIN']) ? $_SERVER['HTTP_ORIGIN'] : '';
	header('Vary: Origin');
	if ($origin !== $config['origin']) ef_reply(array('error' => 'Website origin not allowed.'), 403);
	header('Access-Control-Allow-Origin: ' . $config['origin']);
	header('Access-Control-Allow-Methods: POST, OPTIONS');
	header('Access-Control-Allow-Headers: Content-Type, Accept');
	header('Access-Control-Max-Age: 600');
}

function ef_rate_limit() {
	$now = time();
	$ip = isset($_SERVER['REMOTE_ADDR']) ? $_SERVER['REMOTE_ADDR'] : 'unknown';
	$limits = array(
		array('ip:' . $ip, 3600, defined('ENTRY_FORM_IP_HOURLY_LIMIT') ? max(1, (int)ENTRY_FORM_IP_HOURLY_LIMIT) : 30),
		array('global', 60, defined('ENTRY_FORM_GLOBAL_MINUTE_LIMIT') ? max(1, (int)ENTRY_FORM_GLOBAL_MINUTE_LIMIT) : 300)
	);
	db()->prepare('DELETE FROM entry_form_rate_limits WHERE bucket < ?')->execute(array($now - 7200));
	foreach ($limits as $limit) {
		$key = hash_hmac('sha256', $limit[0], DB_PASS);
		$bucket = (int)(floor($now / $limit[1]) * $limit[1]);
		db()->prepare('INSERT INTO entry_form_rate_limits (rate_key, bucket, hits) VALUES (?, ?, 1) ON DUPLICATE KEY UPDATE hits = hits + 1')->execute(array($key, $bucket));
		$query = db()->prepare('SELECT hits FROM entry_form_rate_limits WHERE rate_key = ? AND bucket = ?');
		$query->execute(array($key, $bucket));
		if ((int)$query->fetchColumn() > $limit[2]) {
			header('Retry-After: ' . ($bucket + $limit[1] - $now));
			ef_reply(array('error' => 'Too many attempts. Please try later.'), 429);
		}
	}
}
