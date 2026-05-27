<?php
require __DIR__ . '/_pub.php';
$sid = isset($_GET['season_id']) && $_GET['season_id'] !== '' ? (string)$_GET['season_id'] : pub_current_season_id();
$where = ''; $args = array();
if ($sid) { $where = 'WHERE season_id = :s'; $args[':s'] = $sid; }
try {
    $st = db()->prepare(
        "SELECT fixture_id, division_id, division_name,
                home_team_id, away_team_id,
                home_team_name, away_team_name,
                venue_name, fixture_date
           FROM league_fixtures
           $where
          ORDER BY fixture_date IS NULL, fixture_date, division_name LIMIT 5000");
    $st->execute($args);
    json_response(array('season_id' => $sid, 'items' => $st->fetchAll()));
} catch (Exception $e) {
    json_response(array('season_id' => $sid, 'items' => array(), 'error' => $e->getMessage()));
}
