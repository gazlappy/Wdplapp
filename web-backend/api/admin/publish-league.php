<?php
// admin/publish-league.php — admin POST. Replaces the league snapshot used by the
// captain portal. Body shape (JSON):
// {
//   "season_id":"...guid...",
//   "teams":[{ "team_id","name","division_id","division_name","venue_name" }, ...],
//   "players":[{ "player_id","team_id","full_name","is_active" }, ...],
//   "fixtures":[{ "fixture_id","division_id","home_team_id","away_team_id",
//                 "home_team_name","away_team_name","venue_name","fixture_date" }, ...]
// }
// Auth: Basic auth handled by .htaccess (same as pending.php).
require __DIR__ . '/../_db.php';
require_post();

$body = read_json_body();
$season_id = isset($body['season_id']) ? (string)$body['season_id'] : null;
$teams     = isset($body['teams'])    && is_array($body['teams'])    ? $body['teams']    : array();
$players   = isset($body['players'])  && is_array($body['players'])  ? $body['players']  : array();
$fixtures  = isset($body['fixtures']) && is_array($body['fixtures']) ? $body['fixtures'] : array();

$pdo = db();
$pdo->beginTransaction();
try {
    // Wipe everything for this season (or all if null).
    if ($season_id !== null && $season_id !== '') {
        $pdo->prepare('DELETE FROM league_fixtures WHERE season_id = :s')->execute(array(':s' => $season_id));
        $pdo->prepare('DELETE FROM league_players  WHERE season_id = :s')->execute(array(':s' => $season_id));
        $pdo->prepare('DELETE FROM league_teams    WHERE season_id = :s')->execute(array(':s' => $season_id));
    } else {
        $pdo->exec('DELETE FROM league_fixtures');
        $pdo->exec('DELETE FROM league_players');
        $pdo->exec('DELETE FROM league_teams');
    }

    $ins = $pdo->prepare(
        'INSERT INTO league_teams (team_id, season_id, division_id, name, division_name, venue_name)
         VALUES (:id, :sid, :did, :n, :dn, :v)');
    foreach ($teams as $t) {
        $ins->execute(array(
            ':id'  => isset($t['team_id'])       ? $t['team_id']       : null,
            ':sid' => $season_id,
            ':did' => isset($t['division_id'])   ? $t['division_id']   : null,
            ':n'   => isset($t['name'])          ? $t['name']          : '',
            ':dn'  => isset($t['division_name']) ? $t['division_name'] : null,
            ':v'   => isset($t['venue_name'])    ? $t['venue_name']    : null,
        ));
    }

    $ins = $pdo->prepare(
        'INSERT INTO league_players (player_id, team_id, season_id, full_name, is_active)
         VALUES (:id, :tid, :sid, :n, :a)');
    foreach ($players as $p) {
        $ins->execute(array(
            ':id'  => isset($p['player_id']) ? $p['player_id'] : null,
            ':tid' => isset($p['team_id'])   ? $p['team_id']   : null,
            ':sid' => $season_id,
            ':n'   => isset($p['full_name']) ? $p['full_name'] : '',
            ':a'   => !empty($p['is_active']) ? 1 : 0,
        ));
    }

    $ins = $pdo->prepare(
        'INSERT INTO league_fixtures
            (fixture_id, season_id, division_id, home_team_id, away_team_id,
             home_team_name, away_team_name, venue_name, fixture_date)
         VALUES (:id, :sid, :did, :h, :a, :hn, :an, :v, :d)');
    foreach ($fixtures as $f) {
        $ins->execute(array(
            ':id'  => isset($f['fixture_id'])     ? $f['fixture_id']     : null,
            ':sid' => $season_id,
            ':did' => isset($f['division_id'])    ? $f['division_id']    : null,
            ':h'   => isset($f['home_team_id'])   ? $f['home_team_id']   : null,
            ':a'   => isset($f['away_team_id'])   ? $f['away_team_id']   : null,
            ':hn'  => isset($f['home_team_name']) ? $f['home_team_name'] : null,
            ':an'  => isset($f['away_team_name']) ? $f['away_team_name'] : null,
            ':v'   => isset($f['venue_name'])     ? $f['venue_name']     : null,
            ':d'   => isset($f['fixture_date'])   ? $f['fixture_date']   : null,
        ));
    }

    $pdo->commit();
} catch (Exception $e) {
    $pdo->rollBack();
    json_response(array('error' => $e->getMessage()), 500);
}

json_response(array(
    'ok'        => true,
    'teams'     => count($teams),
    'players'   => count($players),
    'fixtures'  => count($fixtures),
));
