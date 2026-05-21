<?php
// captain/fixtures.php — GET fixtures for the captain's team across multiple weeks.
// ?weeks=N (default 8) — returns fixtures from N/2 weeks ago to N/2 weeks ahead.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_captain.php';

$c = require_captain();
$team_id = $c['team_id'];

$weeks = isset($_GET['weeks']) ? (int)$_GET['weeks'] : 8;
if ($weeks < 1) $weeks = 1;
if ($weeks > 52) $weeks = 52;

$now = time();
$start = strtotime("monday this week 00:00:00", $now) - (int)($weeks / 2) * 7 * 86400;
$end   = $start + $weeks * 7 * 86400;

try {
    // Join with live_scorecards to determine finalized status.
    $stmt = db()->prepare(
        'SELECT f.fixture_id, f.season_id, f.division_id, f.home_team_id, f.away_team_id,
                f.home_team_name, f.away_team_name, f.venue_name, f.fixture_date,
                IF(s.home_finalized_at IS NOT NULL AND s.away_finalized_at IS NOT NULL, 1, 0) AS finalized
           FROM league_fixtures f
      LEFT JOIN live_scorecards s ON s.fixture_id = f.fixture_id
          WHERE (f.home_team_id = :th OR f.away_team_id = :ta)
            AND f.fixture_date >= :s
            AND f.fixture_date <  :e
          ORDER BY f.fixture_date ASC');
    $stmt->execute(array(
        ':th' => $team_id,
        ':ta' => $team_id,
        ':s'  => gmdate('Y-m-d H:i:s', $start),
        ':e'  => gmdate('Y-m-d H:i:s', $end),
    ));
    $fixtures = $stmt->fetchAll();
} catch (Exception $e) {
    $fixtures = array();
}

json_response(array(
    'weeks'    => $weeks,
    'start'    => gmdate('Y-m-d', $start),
    'end'      => gmdate('Y-m-d', $end),
    'fixtures' => $fixtures,
));
