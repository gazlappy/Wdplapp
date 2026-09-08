<?php
require __DIR__ . '/../../../wdpl2/web-backend/api/_admin_security.php';
function check($value, $message) { if (!$value) throw new RuntimeException($message); }
$_SERVER = array('HTTPS' => 'on', 'HTTP_HOST' => 'league.example.test', 'HTTP_ORIGIN' => 'https://league.example.test', 'HTTP_X_WDPL_ADMIN' => '1');
check(admin_transport_secure(), 'HTTPS accepted');
check(admin_browser_request_allowed(), 'Same-origin protected request accepted');
$_SERVER['HTTP_ORIGIN'] = 'https://attacker.example.test';
check(!admin_browser_request_allowed(), 'Cross-origin rejected');
$_SERVER['HTTP_ORIGIN'] = 'https://league.example.test';
unset($_SERVER['HTTP_X_WDPL_ADMIN']);
check(!admin_browser_request_allowed(), 'Missing header rejected');
$_SERVER['HTTP_X_WDPL_ADMIN'] = '1';
$_SERVER['HTTP_SEC_FETCH_SITE'] = 'cross-site';
check(!admin_browser_request_allowed(), 'Cross-site metadata rejected');
$_SERVER = array('HTTPS' => 'off', 'SERVER_PORT' => 80);
check(!admin_transport_secure(), 'HTTP rejected');
echo "Admin security rules passed\n";
