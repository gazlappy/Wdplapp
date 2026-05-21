<?php
// captain/account.php — GET/POST captain account details.
// GET → returns current captain (username, display_name, email).
// POST {action:'password', old_password, new_password} → change password.
// POST {action:'update', display_name?, email?} → update profile.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_captain.php';

$c = require_captain();
$team_id = $c['team_id'];

if ($_SERVER['REQUEST_METHOD'] === 'GET') {
    // Return editable captain fields (NOT password_hash).
    $stmt = db()->prepare(
        'SELECT username, display_name, email
           FROM captains WHERE team_id = :t LIMIT 1');
    $stmt->execute(array(':t' => $team_id));
    $row = $stmt->fetch();
    if (!$row) {
        json_response(array('error' => 'captain not found'), 404);
    }
    json_response(array('account' => $row));
}

require_post();
$body = read_json_body();
$action = trim((string)(isset($body['action']) ? $body['action'] : ''));

if ($action === 'password') {
    $old = (string)(isset($body['old_password']) ? $body['old_password'] : '');
    $new = (string)(isset($body['new_password']) ? $body['new_password'] : '');
    if ($old === '' || $new === '') {
        json_response(array('error' => 'old_password and new_password required'), 400);
    }
    if (strlen($new) < 6) {
        json_response(array('error' => 'new password must be at least 6 characters'), 400);
    }
    // Verify old password.
    $stmt = db()->prepare('SELECT password_hash FROM captains WHERE team_id = :t LIMIT 1');
    $stmt->execute(array(':t' => $team_id));
    $row = $stmt->fetch();
    if (!$row || !password_verify($old, $row['password_hash'])) {
        json_response(array('error' => 'old password incorrect'), 401);
    }
    // Update to new password.
    $hash = password_hash($new, PASSWORD_DEFAULT);
    db()->prepare('UPDATE captains SET password_hash = :h WHERE team_id = :t')
        ->execute(array(':h' => $hash, ':t' => $team_id));
    json_response(array('ok' => true, 'message' => 'Password updated'));
}

if ($action === 'update') {
    $display = isset($body['display_name']) ? trim((string)$body['display_name']) : null;
    $email   = isset($body['email']) ? trim((string)$body['email']) : null;
    $updates = array();
    $params  = array(':t' => $team_id);
    if ($display !== null) {
        $updates[] = 'display_name = :dn';
        $params[':dn'] = $display;
    }
    if ($email !== null) {
        // Basic email validation.
        if ($email !== '' && !filter_var($email, FILTER_VALIDATE_EMAIL)) {
            json_response(array('error' => 'invalid email format'), 400);
        }
        $updates[] = 'email = :em';
        $params[':em'] = $email;
    }
    if (count($updates) === 0) {
        json_response(array('ok' => true, 'message' => 'no changes'));
    }
    $sql = 'UPDATE captains SET ' . implode(', ', $updates) . ' WHERE team_id = :t';
    db()->prepare($sql)->execute($params);
    json_response(array('ok' => true, 'message' => 'Profile updated'));
}

json_response(array('error' => 'unknown action: ' . $action), 400);
