<?php
// admin/login.php — POST {username, password} -> issues admin session.
// Special bootstrap: if the admin_users table is empty, the first POST
// auto-creates a user with the supplied credentials and logs them in.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
require_post();

$body = read_json_body();
$user = trim((string)(isset($body['username']) ? $body['username'] : ''));
$pass = (string)(isset($body['password']) ? $body['password'] : '');

if ($user === '' || $pass === '') {
    json_response(array('error' => 'username and password required'), 400);
}

// Bootstrap path: when nobody has signed up yet, create the first admin
// account from the very first successful POST so the SPA can get off the
// ground without manually editing the DB.
if (admin_users_count() === 0) {
    if (strlen($pass) < 6) {
        json_response(array('error' => 'first admin password must be at least 6 characters'), 400);
    }
    admin_create_user($user, $pass, $user, null, 'superadmin', 1);
}

$row = admin_login_check($user, $pass);
if (!$row) {
    json_response(array('error' => 'invalid credentials'), 401);
}

admin_issue_session($row['user_id']);

// Return the token so the SPA can fall back to bearer auth when cookies are stripped.
$token = isset($_COOKIE[ADMIN_COOKIE]) ? $_COOKIE[ADMIN_COOKIE] : null;
if (!$token) {
    $q = db()->prepare('SELECT token FROM admin_sessions WHERE user_id = :u ORDER BY created_utc DESC LIMIT 1');
    $q->execute(array(':u' => $row['user_id']));
    $r = $q->fetch();
    if ($r) $token = $r['token'];
}

json_response(array(
    'ok'           => true,
    'token'        => $token,
    'user_id'      => $row['user_id'],
    'username'     => $row['username'],
    'display_name' => $row['display_name'],
    'role'         => $row['role'],
));
