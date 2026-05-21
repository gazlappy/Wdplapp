<?php
// captain/login.php — POST {username, password} -> sets cookie, returns captain profile.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_captain.php';
require_post();

$body = read_json_body();
$user = trim((string)(isset($body['username']) ? $body['username'] : ''));
$pass = (string)(isset($body['password']) ? $body['password'] : '');

if ($user === '' || $pass === '') {
    json_response(array('error' => 'username and password required'), 400);
}

$stmt = db()->prepare(
    'SELECT team_id, team_name, password_hash, display_name, division_id, division_name, enabled
       FROM captains WHERE username = :u LIMIT 1');
$stmt->execute(array(':u' => $user));
$row = $stmt->fetch();

if (!$row || !$row['enabled'] || !password_verify($pass, $row['password_hash'])) {
    json_response(array('error' => 'invalid credentials'), 401);
}

captain_login($row['team_id']);
db()->prepare('UPDATE captains SET last_login = UTC_TIMESTAMP() WHERE team_id = :t')
    ->execute(array(':t' => $row['team_id']));

// Also return the token in the JSON body so the client can fall back to an
// Authorization: Bearer header when the cookie doesn't survive the hop
// (cross-path, third-party cookie blockers, hosts that strip Set-Cookie, etc.).
$token = null;
if (!empty($_COOKIE[CAPTAIN_COOKIE])) $token = $_COOKIE[CAPTAIN_COOKIE];
// captain_login() set the cookie via setcookie() which only populates
// $_COOKIE on the *next* request; grab the token from the most recently
// inserted session row for this team instead.
if (!$token) {
    $q = db()->prepare(
        'SELECT token FROM captain_sessions
          WHERE team_id = :t ORDER BY created_utc DESC LIMIT 1');
    $q->execute(array(':t' => $row['team_id']));
    $r = $q->fetch();
    if ($r) $token = $r['token'];
}

json_response(array(
    'ok'            => true,
    'token'         => $token,
    'team_id'       => $row['team_id'],
    'team_name'     => $row['team_name'],
    'display_name'  => $row['display_name'],
    'division_name' => $row['division_name'],
));
