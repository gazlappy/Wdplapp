<?php
// captain/availability.php — GET/POST player availability for a given week.
// GET ?week=YYYY-MM-DD (Monday) → returns availability for all captain's players.
// POST {player_id, week_start, available} → sets availability.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_captain.php';

$c = require_captain();
$team_id = $c['team_id'];

if ($_SERVER['REQUEST_METHOD'] === 'GET') {
    $week = isset($_GET['week']) ? trim($_GET['week']) : '';
    if ($week === '') {
        // Default to this ISO week Monday 00:00 UTC.
        $week = gmdate('Y-m-d', strtotime('monday this week 00:00:00'));
    }
    // Validate YYYY-MM-DD format.
    if (!preg_match('/^\d{4}-\d{2}-\d{2}$/', $week)) {
        json_response(array('error' => 'invalid week format'), 400);
    }
    // Get all active players for this team + their availability for this week.
    try {
        $stmt = db()->prepare(
            'SELECT p.player_id, p.full_name, COALESCE(a.available, 1) AS available
               FROM league_players p
          LEFT JOIN player_availability a ON a.player_id = p.player_id AND a.week_start_utc = :w
              WHERE p.team_id = :t AND p.is_active = 1
              ORDER BY p.full_name ASC');
        $stmt->execute(array(':t' => $team_id, ':w' => $week . ' 00:00:00'));
        $rows = $stmt->fetchAll();
        json_response(array('week' => $week, 'players' => $rows));
    } catch (Exception $e) {
        json_response(array('week' => $week, 'players' => array()));
    }
}

require_post();
$body = read_json_body();
$pid   = trim((string)(isset($body['player_id']) ? $body['player_id'] : ''));
$week  = trim((string)(isset($body['week_start']) ? $body['week_start'] : ''));
$avail = isset($body['available']) ? (bool)$body['available'] : true;

if ($pid === '' || $week === '') {
    json_response(array('error' => 'player_id and week_start required'), 400);
}
// Validate the player belongs to this captain's team.
$row = db()->prepare(
    'SELECT player_id FROM league_players WHERE player_id = :p AND team_id = :t LIMIT 1')
    ->execute(array(':p' => $pid, ':t' => $team_id));
$row = db()->query("SELECT player_id FROM league_players WHERE player_id='$pid' AND team_id='$team_id' LIMIT 1")->fetch();
if (!$row) {
    json_response(array('error' => 'player not found or not yours'), 404);
}

// Upsert availability.
db()->prepare(
    'INSERT INTO player_availability (player_id, week_start_utc, available, updated_utc)
     VALUES (:p, :w, :a, UTC_TIMESTAMP())
     ON DUPLICATE KEY UPDATE available = :a2, updated_utc = UTC_TIMESTAMP()')
    ->execute(array(':p' => $pid, ':w' => $week . ' 00:00:00', ':a' => $avail ? 1 : 0, ':a2' => $avail ? 1 : 0));

json_response(array('ok' => true));
