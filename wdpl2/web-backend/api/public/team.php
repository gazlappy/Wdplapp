<?php
// public/team.php — team detail (squad + fixtures + record).
// GET ?team_id=...
require __DIR__ . '/_pub.php';
$tid = isset($_GET['team_id']) ? (string)$_GET['team_id'] : '';
if ($tid === '') json_response(array('error' => 'team_id required'), 400);
try {
    $tt = db()->prepare('SELECT team_id, name AS team_name, division_name, venue_name FROM league_teams WHERE team_id = :t LIMIT 1');
    $tt->execute(array(':t' => $tid)); $team = $tt->fetch();
    if (!$team) json_response(array('error' => 'team not found'), 404);

    $ps = db()->prepare('SELECT player_id, full_name, is_active FROM league_players WHERE team_id = :t ORDER BY full_name');
    $ps->execute(array(':t' => $tid)); $players = $ps->fetchAll();

    $fx = db()->prepare(
        'SELECT fixture_id, fixture_date, venue_name, home_team_id, away_team_id,
                home_team_name, away_team_name
           FROM league_fixtures
          WHERE home_team_id = :t OR away_team_id = :t
          ORDER BY fixture_date IS NULL, fixture_date LIMIT 500');
    $fx->execute(array(':t' => $tid)); $fixtures = $fx->fetchAll();

    json_response(array('team' => $team, 'players' => $players, 'fixtures' => $fixtures));
} catch (Exception $e) {
    json_response(array('error' => $e->getMessage()), 500);
}
