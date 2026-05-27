<?php
// public/standings.php — same calc as admin/standings.php, no auth.
require __DIR__ . '/_pub.php';
$sid = isset($_GET['season_id']) && $_GET['season_id'] !== '' ? (string)$_GET['season_id'] : pub_current_season_id();
$inc = !empty($_GET['include_unfinalized']);

$where = array(); $args = array();
if ($sid) { $where[] = 'f.season_id = :s'; $args[':s'] = $sid; }
$finCond = $inc ? '' : ' AND s.home_finalized_at IS NOT NULL AND s.away_finalized_at IS NOT NULL';
$sql = 'SELECT f.fixture_id, f.division_name, f.home_team_id, f.away_team_id,
               f.home_team_name, f.away_team_name, s.state_json
          FROM league_fixtures f
     LEFT JOIN live_scorecards s ON s.fixture_id = f.fixture_id'
     . ($where ? (' WHERE ' . implode(' AND ', $where)) : '')
     . ($where ? ' AND' : ' WHERE') . ' s.fixture_id IS NOT NULL ' . $finCond;
try {
    $st = db()->prepare($sql); $st->execute($args); $rows = $st->fetchAll();
} catch (Exception $e) { json_response(array('error' => $e->getMessage(), 'divisions' => array())); }

$div = array();
foreach ($rows as $r) {
    $dn = $r['division_name'] ?: '(no division)';
    if (!isset($div[$dn])) $div[$dn] = array();
    foreach (array($r['home_team_id'] => $r['home_team_name'], $r['away_team_id'] => $r['away_team_name']) as $tid => $tn) {
        if (!$tid) continue;
        if (!isset($div[$dn][$tid])) $div[$dn][$tid] = array(
            'team_id'=>$tid,'team'=>$tn,'played'=>0,'won'=>0,'drew'=>0,'lost'=>0,
            'frames_for'=>0,'frames_against'=>0,'points'=>0);
    }
    $st2 = json_decode($r['state_json'], true);
    if (!is_array($st2) || empty($st2['frames'])) continue;
    $hf = 0; $af = 0;
    foreach ($st2['frames'] as $f) {
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
json_response(array('season_id' => $sid, 'generated_utc' => gmdate('c'), 'divisions' => $out));
