<?php
// admin/login.php — POST {username, password} -> issues admin session.
// Accounts are provisioned explicitly; public login never creates administrators.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
require_post();

$body = read_json_body();
$user = trim((string)(isset($body['username']) ? $body['username'] : ''));
$pass = (string)(isset($body['password']) ? $body['password'] : '');

if ($user === '' || $pass === '' || strlen($user) > 60 || strlen($pass) > 1024) {
    json_response(array('error' => 'username and password required'), 400);
}

db()->exec('CREATE TABLE IF NOT EXISTS admin_login_limits (rate_key CHAR(64) PRIMARY KEY, bucket BIGINT NOT NULL, hits INT NOT NULL) ENGINE=InnoDB');
$bucket = (int)(floor(time() / 900) * 900);
$key = hash_hmac('sha256', isset($_SERVER['REMOTE_ADDR']) ? $_SERVER['REMOTE_ADDR'] : 'unknown', DB_PASS);
db()->prepare('DELETE FROM admin_login_limits WHERE bucket < ?')->execute(array($bucket));
db()->prepare('INSERT INTO admin_login_limits (rate_key, bucket, hits) VALUES (?, ?, 1) ON DUPLICATE KEY UPDATE hits = hits + 1')->execute(array($key, $bucket));
$limit = db()->prepare('SELECT hits FROM admin_login_limits WHERE rate_key = ?');
$limit->execute(array($key));
if ((int)$limit->fetchColumn() > 20) {
    header('Retry-After: ' . ($bucket + 900 - time()));
    json_response(array('error' => 'Too many login attempts. Try again later.'), 429);
}

$row = admin_login_check($user, $pass);
if (!$row) {
    json_response(array('error' => 'invalid credentials'), 401);
}

admin_logout();
admin_issue_session($row['user_id']);

json_response(array(
    'ok'           => true,
    'user_id'      => $row['user_id'],
    'username'     => $row['username'],
    'display_name' => $row['display_name'],
    'role'         => $row['role'],
));
