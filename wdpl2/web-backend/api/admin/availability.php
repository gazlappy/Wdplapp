<?php
// admin/availability.php — admin overview of player availability for a given week.
// GET ?week=YYYY-MM-DD&team_id=
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
require_admin();
$pdo = db();
try {
    $pdo->exec(
        'CREATE TABLE IF NOT EXISTS player_availability (
            player_id      VARCHAR(64) NOT NULL,
            week_start_utc DATETIME    NOT NULL,
            available      TINYINT(1)  NOT NULL DEFAULT 1,
            updated_utc    DATETIME    NOT NULL,
            PRIMARY KEY (player_id, week_start_utc)
         ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
} catch (Exception $e) {}

$week = isset($_GET['week']) ? trim($_GET['week']) : '';
if (!preg_match('/^\d{4}-\d{2}-\d{2}$/', $week)) $week = gmdate('Y-m-d', strtotime('monday this week 00:00:00'));
$tfilt = isset($_GET['team_id']) ? (string)$_GET['team_id'] : '';

$args = array(':w' => $week . ' 00:00:00');
$sql = 'SELECT t.team_id, t.name AS team_name, p.player_id, p.full_name, p.is_active,
               COALESCE(a.available, 1) AS available, a.updated_utc
          FROM league_players p
          JOIN league_teams t ON t.team_id = p.team_id
     LEFT JOIN player_availability a ON a.player_id = p.player_id AND a.week_start_utc = :w';
if ($tfilt !== '') { $sql .= ' WHERE t.team_id = :t'; $args[':t'] = $tfilt; }
$sql .= ' ORDER BY t.name, p.full_name';
try { $st = $pdo->prepare($sql); $st->execute($args); $rows = $st->fetchAll(); }
catch (Exception $e) { $rows = array(); }

// Group by team.
$teams = array();
foreach ($rows as $r) {
    $tid = $r['team_id'];
    if (!isset($teams[$tid])) $teams[$tid] = array('team_id'=>$tid, 'team_name'=>$r['team_name'], 'players'=>array(), 'count_yes'=>0, 'count_no'=>0);
    $teams[$tid]['players'][] = array(
        'player_id' => $r['player_id'], 'full_name' => $r['full_name'],
        'is_active' => (int)$r['is_active'], 'available' => (int)$r['available'],
        'updated_utc' => $r['updated_utc']);
    if ((int)$r['available'] === 1) $teams[$tid]['count_yes']++; else $teams[$tid]['count_no']++;
}
json_response(array('week' => $week, 'teams' => array_values($teams)));
