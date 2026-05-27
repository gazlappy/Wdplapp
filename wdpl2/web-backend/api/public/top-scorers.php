<?php
// public/top-scorers.php — counts frame WINS per player across finalized live cards.
// GET ?season_id=&limit=50&team_id=&min_played=0
require __DIR__ . '/_pub.php';
$sid = isset($_GET['season_id']) && $_GET['season_id'] !== '' ? (string)$_GET['season_id'] : pub_current_season_id();
$limit = isset($_GET['limit']) ? max(1, min(500, (int)$_GET['limit'])) : 50;
$teamFilter = isset($_GET['team_id']) ? (string)$_GET['team_id'] : '';
$minPlayed  = isset($_GET['min_played']) ? max(0, (int)$_GET['min_played']) : 0;

$where = array("s.home_finalized_at IS NOT NULL", "s.away_finalized_at IS NOT NULL"); $args = array();
if ($sid) { $where[] = 'f.season_id = :s'; $args[':s'] = $sid; }
$sql = 'SELECT f.fixture_id, f.home_team_id, f.away_team_id,
               f.home_team_name, f.away_team_name, s.state_json
          FROM league_fixtures f
          JOIN live_scorecards s ON s.fixture_id = f.fixture_id
         WHERE ' . implode(' AND ', $where);
try {
    $st = db()->prepare($sql); $st->execute($args); $cards = $st->fetchAll();
} catch (Exception $e) { json_response(array('error' => $e->getMessage(), 'items' => array())); }

$agg = array(); // key -> stats
foreach ($cards as $c) {
    $state = json_decode($c['state_json'], true);
    if (!is_array($state) || empty($state['frames'])) continue;
    foreach ($state['frames'] as $f) {
        $w = $f['winner'] ?? null;
        // Each side has up to 2 players (lead + partner in doubles).
        $sides = array(
            'home' => array(
                array($f['home_player_id']  ?? null, $f['home_player_name']  ?? null, $c['home_team_id'], $c['home_team_name']),
                array($f['home_player2_id'] ?? null, $f['home_player2_name'] ?? null, $c['home_team_id'], $c['home_team_name']),
            ),
            'away' => array(
                array($f['away_player_id']  ?? null, $f['away_player_name']  ?? null, $c['away_team_id'], $c['away_team_name']),
                array($f['away_player2_id'] ?? null, $f['away_player2_name'] ?? null, $c['away_team_id'], $c['away_team_name']),
            ),
        );
        foreach ($sides as $side => $players) {
            foreach ($players as $p) {
                list($pid, $pname, $tid, $tname) = $p;
                if (!$pname) continue;
                $key = $pid ? ('id:' . strtolower($pid)) : ('nm:' . strtolower(trim($pname)));
                if (!isset($agg[$key])) $agg[$key] = array(
                    'player_id'=>$pid, 'name'=>$pname, 'team_id'=>$tid, 'team'=>$tname,
                    'played'=>0, 'won'=>0, 'lost'=>0, 'eight_balls'=>0);
                $agg[$key]['played']++;
                if ($w === $side) {
                    $agg[$key]['won']++;
                    if (!empty($f['eight_ball'])) $agg[$key]['eight_balls']++;
                } else if ($w === ($side === 'home' ? 'away' : 'home')) {
                    $agg[$key]['lost']++;
                }
            }
        }
    }
}
$items = array_values($agg);
if ($teamFilter !== '') {
    $items = array_values(array_filter($items, function($r) use ($teamFilter){ return $r['team_id'] === $teamFilter; }));
}
if ($minPlayed > 0) {
    $items = array_values(array_filter($items, function($r) use ($minPlayed){ return $r['played'] >= $minPlayed; }));
}
foreach ($items as &$r) {
    $r['win_pct'] = $r['played'] ? round(100 * $r['won'] / $r['played'], 1) : 0;
}
unset($r);
usort($items, function($a, $b){
    if ($a['won'] !== $b['won']) return $b['won'] - $a['won'];
    if ($a['win_pct'] !== $b['win_pct']) return $b['win_pct'] <=> $a['win_pct'];
    return strcmp($a['name'], $b['name']);
});
$items = array_slice($items, 0, $limit);
json_response(array('season_id' => $sid, 'generated_utc' => gmdate('c'), 'items' => $items));
