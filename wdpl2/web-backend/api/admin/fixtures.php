<?php
// admin/fixtures.php — fixture list + edits.
// GET ?season_id=&division_id=&q=&from=&to=  -> list
// POST {action:"update", fixture_id, fixture_date?, venue_name?, home_team_id?, away_team_id?}
// POST {action:"swap",   fixture_id}                 -> swap home/away
// POST {action:"postpone", fixture_id}               -> set fixture_date NULL
// DELETE {fixture_id}                                -> delete row (and live_scorecards row)
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
$me = require_admin();
$pdo = db();

$method = $_SERVER['REQUEST_METHOD'];

if ($method === 'GET') {
    $where = array(); $args = array();
    if (!empty($_GET['season_id']))   { $where[] = 'season_id = :s';   $args[':s'] = $_GET['season_id']; }
    if (!empty($_GET['division_id'])) { $where[] = 'division_id = :d'; $args[':d'] = $_GET['division_id']; }
    if (!empty($_GET['q'])) {
        $where[] = '(home_team_name LIKE :q OR away_team_name LIKE :q OR venue_name LIKE :q)';
        $args[':q'] = '%' . $_GET['q'] . '%';
    }
    if (!empty($_GET['from'])) { $where[] = 'fixture_date >= :fd'; $args[':fd'] = $_GET['from']; }
    if (!empty($_GET['to']))   { $where[] = 'fixture_date <= :td'; $args[':td'] = $_GET['to']; }
    $sql = 'SELECT f.fixture_id, f.season_id, f.division_id, f.division_name,
                   f.home_team_id, f.away_team_id, f.home_team_name, f.away_team_name,
                   f.venue_name, f.fixture_date,
                   (s.fixture_id IS NOT NULL)        AS has_live,
                   (s.home_finalized_at IS NOT NULL) AS home_finalized,
                   (s.away_finalized_at IS NOT NULL) AS away_finalized
              FROM league_fixtures f
         LEFT JOIN live_scorecards s ON s.fixture_id = f.fixture_id'
         . ($where ? (' WHERE ' . implode(' AND ', $where)) : '')
         . ' ORDER BY f.fixture_date IS NULL, f.fixture_date, f.division_name LIMIT 2000';
    try {
        $st = $pdo->prepare($sql); $st->execute($args);
        json_response(array('items' => $st->fetchAll()));
    } catch (Exception $e) { json_response(array('items' => array(), 'error' => $e->getMessage())); }
}

if ($method === 'DELETE') {
    $body = read_json_body();
    $fid = trim((string)(isset($body['fixture_id']) ? $body['fixture_id'] : ''));
    if ($fid === '') json_response(array('error' => 'fixture_id required'), 400);
    require_admin('superadmin');
    $pdo->prepare('DELETE FROM live_scorecards  WHERE fixture_id = :f')->execute(array(':f' => $fid));
    $d = $pdo->prepare('DELETE FROM league_fixtures WHERE fixture_id = :f');
    $d->execute(array(':f' => $fid));
    audit_log($me, 'fixture.delete', $fid);
    json_response(array('ok' => true, 'rows' => $d->rowCount()));
}

require_post();
$body   = read_json_body();
$action = trim((string)(isset($body['action']) ? $body['action'] : 'update'));
$fid    = trim((string)(isset($body['fixture_id']) ? $body['fixture_id'] : ''));
if ($fid === '') json_response(array('error' => 'fixture_id required'), 400);

$cur = $pdo->prepare('SELECT * FROM league_fixtures WHERE fixture_id = :f LIMIT 1');
$cur->execute(array(':f' => $fid));
$row = $cur->fetch();
if (!$row) json_response(array('error' => 'fixture not found'), 404);

function _team_name($pdo, $tid) {
    if (!$tid) return null;
    $s = $pdo->prepare('SELECT name FROM league_teams WHERE team_id = :t LIMIT 1');
    $s->execute(array(':t' => $tid));
    $r = $s->fetch();
    return $r ? $r['name'] : null;
}

if ($action === 'update') {
    $date = isset($body['fixture_date']) ? ($body['fixture_date'] === '' ? null : (string)$body['fixture_date']) : $row['fixture_date'];
    $ven  = isset($body['venue_name'])   ? ($body['venue_name']   === '' ? null : (string)$body['venue_name'])   : $row['venue_name'];
    $hid  = isset($body['home_team_id']) ? (string)$body['home_team_id'] : $row['home_team_id'];
    $aid  = isset($body['away_team_id']) ? (string)$body['away_team_id'] : $row['away_team_id'];
    $hn   = isset($body['home_team_id']) ? (_team_name($pdo, $hid) ?: $row['home_team_name']) : $row['home_team_name'];
    $an   = isset($body['away_team_id']) ? (_team_name($pdo, $aid) ?: $row['away_team_name']) : $row['away_team_name'];

    $pdo->prepare(
        'UPDATE league_fixtures
            SET fixture_date = :d, venue_name = :v,
                home_team_id = :h,  away_team_id = :a,
                home_team_name = :hn, away_team_name = :an
          WHERE fixture_id = :f')
       ->execute(array(':d'=>$date, ':v'=>$ven, ':h'=>$hid, ':a'=>$aid, ':hn'=>$hn, ':an'=>$an, ':f'=>$fid));
    audit_log($me, 'fixture.update', $fid, array('date'=>$date,'venue'=>$ven,'home'=>$hn,'away'=>$an));
    json_response(array('ok' => true));
}

if ($action === 'swap') {
    $pdo->prepare(
        'UPDATE league_fixtures
            SET home_team_id = :a, away_team_id = :h,
                home_team_name = :an, away_team_name = :hn
          WHERE fixture_id = :f')
       ->execute(array(
           ':a' => $row['away_team_id'], ':h' => $row['home_team_id'],
           ':an'=> $row['away_team_name'], ':hn'=> $row['home_team_name'],
           ':f' => $fid));
    // Live card teams are now backwards — drop it so it regenerates fresh.
    $pdo->prepare('DELETE FROM live_scorecards WHERE fixture_id = :f')->execute(array(':f' => $fid));
    audit_log($me, 'fixture.swap', $fid);
    json_response(array('ok' => true));
}

if ($action === 'postpone') {
    $pdo->prepare('UPDATE league_fixtures SET fixture_date = NULL WHERE fixture_id = :f')->execute(array(':f' => $fid));
    audit_log($me, 'fixture.postpone', $fid);
    json_response(array('ok' => true));
}

json_response(array('error' => 'unknown action'), 400);
