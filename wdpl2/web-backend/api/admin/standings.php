<?php
// admin/standings.php — compute and (optionally) publish league standings.
// GET ?season_id=&division=    -> { divisions: [{name, rows:[{team, played, won, lost, frames_for, frames_against, points}]}] }
// POST {action:"publish"}      -> writes the standings JSON to ../league/standings.json
//
// Rules: 1 point per frame won (8-ball ignored — extend later). Only fixtures
// where BOTH captains have finalized are counted unless 'include_unfinalized'.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
$me = require_admin();
$pdo = db();

function _stand_compute($pdo, $season_id, $include_unfinalized) {
    $where = array(); $args = array();
    if ($season_id) { $where[] = 'f.season_id = :s'; $args[':s'] = $season_id; }
    $finCond = $include_unfinalized ? '' :
        ' AND s.home_finalized_at IS NOT NULL AND s.away_finalized_at IS NOT NULL';
    $sql = 'SELECT f.fixture_id, f.division_name, f.home_team_id, f.away_team_id,
                   f.home_team_name, f.away_team_name, s.state_json
              FROM league_fixtures f
         LEFT JOIN live_scorecards s ON s.fixture_id = f.fixture_id'
         . ($where ? (' WHERE ' . implode(' AND ', $where)) : '')
         . ($where ? ' AND' : ' WHERE') . ' s.fixture_id IS NOT NULL ' . $finCond;
    $st = $pdo->prepare($sql); $st->execute($args); $rows = $st->fetchAll();

    $div = array();
    foreach ($rows as $r) {
        $dn = $r['division_name'] ?: '(no division)';
        if (!isset($div[$dn])) $div[$dn] = array();
        foreach (array($r['home_team_id'] => $r['home_team_name'], $r['away_team_id'] => $r['away_team_name']) as $tid => $tn) {
            if (!$tid) continue;
            if (!isset($div[$dn][$tid])) $div[$dn][$tid] = array(
                'team_id'=>$tid,'team'=>$tn,'played'=>0,'won'=>0,'lost'=>0,'drew'=>0,
                'frames_for'=>0,'frames_against'=>0,'points'=>0);
        }
        $state = json_decode($r['state_json'], true);
        if (!is_array($state) || empty($state['frames'])) continue;
        $hf = 0; $af = 0;
        foreach ($state['frames'] as $f) {
            if (($f['winner'] ?? null) === 'home') $hf++;
            else if (($f['winner'] ?? null) === 'away') $af++;
        }
        $hid = $r['home_team_id']; $aid = $r['away_team_id'];
        if ($hid) { $div[$dn][$hid]['played']++; $div[$dn][$hid]['frames_for']+=$hf; $div[$dn][$hid]['frames_against']+=$af; $div[$dn][$hid]['points']+=$hf; }
        if ($aid) { $div[$dn][$aid]['played']++; $div[$dn][$aid]['frames_for']+=$af; $div[$dn][$aid]['frames_against']+=$hf; $div[$dn][$aid]['points']+=$af; }
        if ($hf > $af)      { if ($hid) $div[$dn][$hid]['won']++;  if ($aid) $div[$dn][$aid]['lost']++; }
        else if ($af > $hf) { if ($aid) $div[$dn][$aid]['won']++;  if ($hid) $div[$dn][$hid]['lost']++; }
        else                { if ($hid) $div[$dn][$hid]['drew']++; if ($aid) $div[$dn][$aid]['drew']++; }
    }

    $out = array();
    foreach ($div as $name => $teams) {
        $arr = array_values($teams);
        usort($arr, function($a, $b){
            if ($a['points'] !== $b['points']) return $b['points'] - $a['points'];
            $da = $a['frames_for'] - $a['frames_against'];
            $db = $b['frames_for'] - $b['frames_against'];
            if ($da !== $db) return $db - $da;
            return strcmp($a['team'], $b['team']);
        });
        $out[] = array('name' => $name, 'rows' => $arr);
    }
    return $out;
}

if ($_SERVER['REQUEST_METHOD'] === 'GET') {
    $sid = isset($_GET['season_id']) ? (string)$_GET['season_id'] : '';
    $inc = !empty($_GET['include_unfinalized']);
    json_response(array('divisions' => _stand_compute($pdo, $sid, $inc), 'generated_utc' => gmdate('c')));
}

require_post();
$body = read_json_body();
$action = trim((string)(isset($body['action']) ? $body['action'] : ''));

if ($action === 'publish') {
    $sid = isset($body['season_id']) ? (string)$body['season_id'] : '';
    $data = array(
        'generated_utc' => gmdate('c'),
        'season_id'     => $sid,
        'divisions'     => _stand_compute($pdo, $sid, false),
    );
    $dir = __DIR__ . '/../../league';
    if (!is_dir($dir)) @mkdir($dir, 0755, true);
    $file = $dir . '/standings.json';
    $bytes = @file_put_contents($file, json_encode($data, JSON_PRETTY_PRINT));
    audit_log($me, 'standings.publish', $sid, array('bytes' => $bytes));
    json_response(array('ok' => $bytes !== false, 'bytes' => $bytes, 'path' => 'league/standings.json'));
}

json_response(array('error' => 'unknown action'), 400);
