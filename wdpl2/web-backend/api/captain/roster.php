<?php
// captain/roster.php — GET/POST roster for the captain's team.
// GET: returns all players for the captain's team (active + retired).
// POST {action, player_id?, full_name?, is_active?}:
//   - "add" → inserts a new player (captain-added flag set).
//   - "update" → renames or toggles active (only if captain originally added them OR admin hasn't locked).
//   - "retire" → sets is_active=0.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_captain.php';

$c = require_captain();
$team_id = $c['team_id'];

if ($_SERVER['REQUEST_METHOD'] === 'GET') {
    // Return all players for this team, active + retired, ordered by name.
    try {
        $stmt = db()->prepare(
            'SELECT player_id, team_id, full_name, is_active, added_by_captain, updated_utc
               FROM league_players
              WHERE team_id = :t
              ORDER BY full_name ASC');
        $stmt->execute(array(':t' => $team_id));
        $rows = $stmt->fetchAll();
        json_response(array('players' => $rows));
    } catch (Exception $e) {
        // league_players may not exist yet.
        json_response(array('players' => array()));
    }
}

require_post();
$body = read_json_body();
$action = trim((string)(isset($body['action']) ? $body['action'] : ''));

if ($action === 'add') {
    $name = trim((string)(isset($body['full_name']) ? $body['full_name'] : ''));
    if ($name === '') {
        json_response(array('error' => 'full_name required'), 400);
    }
    // Generate a new GUID for the player (captain-added players live in the same
    // table as admin-pushed players; we distinguish via added_by_captain flag).
    $pid = strtoupper(sprintf(
        '%04X%04X-%04X-%04X-%04X-%04X%04X%04X',
        mt_rand(0, 0xFFFF), mt_rand(0, 0xFFFF),
        mt_rand(0, 0xFFFF),
        mt_rand(0, 0x0FFF) | 0x4000,
        mt_rand(0, 0x3FFF) | 0x8000,
        mt_rand(0, 0xFFFF), mt_rand(0, 0xFFFF), mt_rand(0, 0xFFFF)
    ));
    db()->prepare(
        'INSERT INTO league_players
           (player_id, team_id, full_name, is_active, added_by_captain, updated_utc)
         VALUES (:pid, :tid, :nm, 1, 1, UTC_TIMESTAMP())')
        ->execute(array(':pid' => $pid, ':tid' => $team_id, ':nm' => $name));
    json_response(array('ok' => true, 'player_id' => $pid, 'full_name' => $name));
}

if ($action === 'update') {
    $pid  = trim((string)(isset($body['player_id']) ? $body['player_id'] : ''));
    $name = trim((string)(isset($body['full_name']) ? $body['full_name'] : ''));
    $active = isset($body['is_active']) ? (bool)$body['is_active'] : null;
    if ($pid === '') {
        json_response(array('error' => 'player_id required'), 400);
    }
    // Only allow edits to players that belong to this captain's team
    // AND were either added by the captain OR have no admin lock.
    // (For now we assume all admin-pushed players have added_by_captain=0.)
    $row = db()->prepare(
        'SELECT player_id, full_name, is_active, added_by_captain
           FROM league_players
          WHERE player_id = :p AND team_id = :t LIMIT 1')
        ->execute(array(':p' => $pid, ':t' => $team_id));
    $row = db()->query("SELECT * FROM league_players WHERE player_id='$pid' AND team_id='$team_id' LIMIT 1")->fetch();
    if (!$row) {
        json_response(array('error' => 'player not found or not yours'), 404);
    }
    // If admin-added, captain can only retire them, not rename.
    if (!$row['added_by_captain'] && $name !== '' && $name !== $row['full_name']) {
        json_response(array('error' => 'cannot rename admin-provisioned player'), 403);
    }
    $updates = array();
    $params = array(':p' => $pid, ':t' => $team_id);
    if ($name !== '' && $name !== $row['full_name']) {
        $updates[] = 'full_name = :nm';
        $params[':nm'] = $name;
    }
    if ($active !== null && (bool)$row['is_active'] !== $active) {
        $updates[] = 'is_active = :act';
        $params[':act'] = $active ? 1 : 0;
    }
    if (count($updates) === 0) {
        json_response(array('ok' => true, 'message' => 'no changes'));
    }
    $sql = 'UPDATE league_players SET ' . implode(', ', $updates) .
           ', updated_utc = UTC_TIMESTAMP() WHERE player_id = :p AND team_id = :t';
    db()->prepare($sql)->execute($params);
    json_response(array('ok' => true));
}

if ($action === 'retire') {
    $pid = trim((string)(isset($body['player_id']) ? $body['player_id'] : ''));
    if ($pid === '') {
        json_response(array('error' => 'player_id required'), 400);
    }
    // Captain can retire any player on their team (admin-added or captain-added).
    db()->prepare(
        'UPDATE league_players
            SET is_active = 0, updated_utc = UTC_TIMESTAMP()
          WHERE player_id = :p AND team_id = :t')
        ->execute(array(':p' => $pid, ':t' => $team_id));
    json_response(array('ok' => true));
}

json_response(array('error' => 'unknown action: ' . $action), 400);
