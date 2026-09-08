<?php
// Shared transport checks; no database access so they can be tested independently.
function admin_transport_secure() {
	return (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') ||
		(isset($_SERVER['SERVER_PORT']) && (int)$_SERVER['SERVER_PORT'] === 443);
}

function admin_browser_request_allowed() {
	if (isset($_SERVER['HTTP_SEC_FETCH_SITE']) && $_SERVER['HTTP_SEC_FETCH_SITE'] === 'cross-site') return false;
	if (isset($_SERVER['HTTP_ORIGIN'])) {
		$expected = 'https://' . (isset($_SERVER['HTTP_HOST']) ? $_SERVER['HTTP_HOST'] : '');
		if ($_SERVER['HTTP_ORIGIN'] !== $expected) return false;
	}
	return isset($_SERVER['HTTP_X_WDPL_ADMIN']) && $_SERVER['HTTP_X_WDPL_ADMIN'] === '1';
}

function admin_guard_transport() {
	if (!admin_transport_secure()) json_response(array('error' => 'HTTPS is required for administration.'), 400);
	$method = isset($_SERVER['REQUEST_METHOD']) ? $_SERVER['REQUEST_METHOD'] : 'GET';
	if (!in_array($method, array('GET', 'HEAD', 'OPTIONS'), true)) {
		$browser = !empty($_COOKIE[ADMIN_COOKIE]) || isset($_SERVER['HTTP_ORIGIN']) || isset($_SERVER['HTTP_SEC_FETCH_SITE']);
		if ($browser && !admin_browser_request_allowed()) json_response(array('error' => 'Same-origin admin request required.'), 403);
		$type = isset($_SERVER['CONTENT_TYPE']) ? strtolower(trim(explode(';', $_SERVER['CONTENT_TYPE'])[0])) : '';
		if ($type !== 'application/json') json_response(array('error' => 'JSON content required.'), 415);
	}
}
