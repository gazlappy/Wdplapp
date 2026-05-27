<?php
// public/results.php — published frame results.
require __DIR__ . '/_pub.php';
$sid = isset($_GET['season_id']) && $_GET['season_id'] !== '' ? (string)$_GET['season_id'] : pub_current_season_id();

// Pull only finalized fixtures and read frame outcomes from live_scorecards
// (the captain-edited live JSON is the authoritative source).
$where = ''; $args = array();
if ($sid) { $where = 'AND f.season_id = :s'; $args[':s'] = $sid; }
try {
    $st = db()->prepare(
        "SELECT f.fixture_id, f.division_name, f.fixture_date,
                f.home_team_name, f.away_team_name,
                s.state_json
           FROM league_fixtures f
           JOIN live_scorecards s ON s.fixture_id = f.fixture_id
          WHERE s.home_finalized_at IS NOT NULL AND s.away_finalized_at IS NOT NULL
                $where
          ORDER BY f.fixture_date DESC LIMIT 2000");
    $st->execute($args);
    $items = array();
    foreach ($st->fetchAll() as $r) {
        $state = json_decode($r['state_json'], true);
        $frames = is_array($state) && isset($state['frames']) ? $state['frames'] : array();
        $hf = 0; $af = 0;
        foreach ($frames as $fr) {
            if (($fr['winner'] ?? null) === 'home') $hf++;
            else if (($fr['winner'] ?? null) === 'away') $af++;
        }
        $items[] = array(
            'fixture_id'     => $r['fixture_id'],
            'division_name'  => $r['division_name'],
            'fixture_date'   => $r['fixture_date'],
            'home_team_name' => $r['home_team_name'],
            'away_team_name' => $r['away_team_name'],
            'home_frames'    => $hf,
            'away_frames'    => $af,
            'frames'         => $frames,
        );
    }
    json_response(array('season_id' => $sid, 'items' => $items));
} catch (Exception $e) {
    json_response(array('error' => $e->getMessage(), 'items' => array()));
}
