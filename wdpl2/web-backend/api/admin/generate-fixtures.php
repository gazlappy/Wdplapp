<?php
// admin/generate-fixtures.php — round-robin fixture generator.
// POST {season_id, division_id?, team_ids:[...], start_date?, weekly?:true, double_round?:false, venue_lookup?:true}
//   -> creates league_fixtures rows. Returns the inserted count.
// Uses the circle method: for n teams (pad with a "BYE" if odd), n-1 rounds.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
$me = require_admin();
$pdo = db();
require_post();

$body = read_json_body();
$sid  = trim((string)(isset($body['season_id']) ? $body['season_id'] : ''));
$did  = isset($body['division_id']) ? (string)$body['division_id'] : null;
$tids = isset($body['team_ids']) && is_array($body['team_ids']) ? array_values(array_unique($body['team_ids'])) : array();
$startStr = isset($body['start_date']) ? trim((string)$body['start_date']) : '';
$weekly   = !isset($body['weekly']) ? true : !empty($body['weekly']);
$dbl      = !empty($body['double_round']);
$useVenue = !isset($body['venue_lookup']) ? true : !empty($body['venue_lookup']);

if ($sid === '' || count($tids) < 2) json_response(array('error' => 'season_id and at least 2 team_ids required'), 400);

// Load team names + venues for label lookup.
$qmarks = implode(',', array_fill(0, count($tids), '?'));
$st = $pdo->prepare("SELECT team_id, name, division_name, venue_name FROM league_teams WHERE team_id IN ($qmarks)");
$st->execute($tids);
$teams = array(); foreach ($st->fetchAll() as $r) $teams[$r['team_id']] = $r;
foreach ($tids as $t) if (!isset($teams[$t])) json_response(array('error' => 'team not found: ' . $t), 404);

// Round-robin (circle method).
$list = $tids;
if (count($list) % 2 === 1) $list[] = '__BYE__';
$n = count($list);
$rounds = array();
$rot = $list;
for ($r = 0; $r < $n - 1; $r++) {
    $pairs = array();
    for ($i = 0; $i < $n / 2; $i++) {
        $a = $rot[$i]; $b = $rot[$n - 1 - $i];
        if ($a === '__BYE__' || $b === '__BYE__') continue;
        // Alternate home/away each round so totals balance.
        if ($r % 2 === 0) $pairs[] = array($a, $b); else $pairs[] = array($b, $a);
    }
    $rounds[] = $pairs;
    // Rotate (keep first fixed).
    $tail = array_slice($rot, 1);
    array_unshift($tail, array_pop($tail));
    $rot = array_merge(array($rot[0]), $tail);
}
if ($dbl) {
    foreach ($rounds as $rr) {
        $swapped = array();
        foreach ($rr as $p) $swapped[] = array($p[1], $p[0]);
        $rounds[] = $swapped;
    }
}

// Insert fixtures.
$ins = $pdo->prepare(
    'INSERT INTO league_fixtures
        (fixture_id, season_id, division_id, home_team_id, away_team_id,
         home_team_name, away_team_name, venue_name, fixture_date)
     VALUES (:id, :sid, :did, :h, :a, :hn, :an, :v, :d)');

$start = null;
if ($startStr !== '') {
    try { $start = new DateTime($startStr); } catch (Exception $e) { $start = null; }
}

$count = 0;
foreach ($rounds as $idx => $pairs) {
    $date = null;
    if ($start) {
        $dt = clone $start;
        if ($weekly) $dt->modify('+' . ($idx * 7) . ' days');
        $date = $dt->format('Y-m-d');
    }
    foreach ($pairs as $p) {
        list($hid, $aid) = $p;
        $hn = $teams[$hid]['name']; $an = $teams[$aid]['name'];
        $venue = $useVenue ? $teams[$hid]['venue_name'] : null;
        $fid = bin2hex(function_exists('random_bytes') ? random_bytes(8) : openssl_random_pseudo_bytes(8));
        $fid = substr($fid, 0, 8) . '-rr' . substr($fid, 8);
        $ins->execute(array(
            ':id'=>$fid, ':sid'=>$sid, ':did'=>$did,
            ':h'=>$hid, ':a'=>$aid, ':hn'=>$hn, ':an'=>$an,
            ':v'=>$venue, ':d'=>$date));
        $count++;
    }
}
audit_log($me, 'fixtures.generate', $sid, array('inserted' => $count, 'teams' => count($tids), 'double' => $dbl));
json_response(array('ok' => true, 'inserted' => $count, 'rounds' => count($rounds)));
