<?php
// admin/captains.php — admin endpoint to create/update captain accounts.
// GET  -> list all captains.
// POST -> upsert. Body: { "team_id","team_name","division_id","division_name",
//                         "username","password","display_name","email","enabled":true }
// If "password" is omitted on an update, the existing hash is kept.
// Auth: Basic auth handled by .htaccess.
require __DIR__ . '/../_db.php';

$method = isset($_SERVER['REQUEST_METHOD']) ? $_SERVER['REQUEST_METHOD'] : '';
if ($method === 'GET') {
    $rows = db()->query(
        'SELECT team_id, team_name, division_name, username, display_name, email,
                enabled, created_utc, last_login
           FROM captains ORDER BY team_name')->fetchAll();
    $teams = array();
    try {
        $teams = db()->query(
            'SELECT team_id, name AS team_name, division_id
               FROM league_teams ORDER BY name')->fetchAll();
    } catch (Exception $e) { /* league_teams may not exist yet — ignore */ }
    json_response(array('items' => $rows, 'teams' => $teams));
}

if ($method === 'DELETE') {
    $body = read_json_body();
    $team_id = trim((string)(isset($body['team_id']) ? $body['team_id'] : ''));
    if ($team_id === '') {
        json_response(array('error' => 'team_id required'), 400);
    }
    $stmt = db()->prepare('DELETE FROM captains WHERE team_id = :t');
    $stmt->execute(array(':t' => $team_id));
    json_response(array('ok' => true, 'deleted' => $stmt->rowCount()));
}

require_post();
$body = read_json_body();
$team_id = trim((string)(isset($body['team_id']) ? $body['team_id'] : ''));
$team_name = trim((string)(isset($body['team_name']) ? $body['team_name'] : ''));
$username = trim((string)(isset($body['username']) ? $body['username'] : ''));
$password = (string)(isset($body['password']) ? $body['password'] : '');

if ($team_id === '' || $team_name === '' || $username === '') {
    json_response(array('error' => 'team_id, team_name and username required'), 400);
}

$existing = db()->prepare('SELECT password_hash FROM captains WHERE team_id = :t');
$existing->execute(array(':t' => $team_id));
$row = $existing->fetch();

$hash = $row ? $row['password_hash'] : null;
if ($password !== '') {
    $hash = password_hash($password, PASSWORD_DEFAULT);
} elseif (!$row) {
    json_response(array('error' => 'password required for new captain'), 400);
}

$enabled = !empty($body['enabled']) ? 1 : (isset($body['enabled']) ? 0 : 1);

$sql = 'INSERT INTO captains
            (team_id, team_name, division_id, division_name, username, password_hash, display_name, email, enabled)
        VALUES
            (:tid, :tn, :did, :dn, :u, :ph, :disp, :em, :en)
        ON DUPLICATE KEY UPDATE
            team_name     = VALUES(team_name),
            division_id   = VALUES(division_id),
            division_name = VALUES(division_name),
            username      = VALUES(username),
            password_hash = VALUES(password_hash),
            display_name  = VALUES(display_name),
            email         = VALUES(email),
            enabled       = VALUES(enabled)';
db()->prepare($sql)->execute(array(
    ':tid'  => $team_id,
    ':tn'   => $team_name,
    ':did'  => isset($body['division_id'])   ? $body['division_id']   : null,
    ':dn'   => isset($body['division_name']) ? $body['division_name'] : null,
    ':u'    => $username,
    ':ph'   => $hash,
    ':disp' => isset($body['display_name'])  ? $body['display_name']  : null,
    ':em'   => isset($body['email'])         ? $body['email']         : null,
    ':en'   => $enabled,
));

json_response(array('ok' => true, 'team_id' => $team_id));
