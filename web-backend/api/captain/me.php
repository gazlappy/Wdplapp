<?php
// captain/me.php — GET current captain + this week's fixtures + roster.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_captain.php';

$c = require_captain();
$team_id = $c['team_id'];

// "This week" = Monday 00:00 UTC of the current ISO week .. +14 days
// (gives current week + next week so a captain who's late can still see last week).
$now    = time();
$start  = strtotime('monday this week 00:00:00', $now) - 7 * 86400; // last Monday
$end    = $start + 21 * 86400;                                       // 3-week window

$fixtures = array();
try {
    $fxStmt = db()->prepare(
        'SELECT fixture_id, season_id, division_id, home_team_id, away_team_id,
                home_team_name, away_team_name, venue_name, fixture_date
           FROM league_fixtures
          WHERE (home_team_id = :th OR away_team_id = :ta)
            AND fixture_date BETWEEN :s AND :e
          ORDER BY fixture_date ASC');
    $fxStmt->execute(array(
        ':th' => $team_id,
        ':ta' => $team_id,
        ':s'  => gmdate('Y-m-d H:i:s', $start),
        ':e'  => gmdate('Y-m-d H:i:s', $end),
    ));
    $fixtures = $fxStmt->fetchAll();
} catch (Exception $e) { /* league_fixtures may not exist yet */ }

// Players for this captain's team + the opposing team in each fixture (so they
// can record opposing pairings if needed).
$teamIds = array($team_id);
foreach ($fixtures as $f) {
    if ($f['home_team_id'] !== $team_id) $teamIds[] = $f['home_team_id'];
    if ($f['away_team_id'] !== $team_id) $teamIds[] = $f['away_team_id'];
}
$teamIds = array_values(array_unique($teamIds));

$players = array();
try {
    $placeholders = implode(',', array_fill(0, count($teamIds), '?'));
    $prStmt = db()->prepare(
        'SELECT player_id, team_id, full_name
           FROM league_players
          WHERE team_id IN (' . $placeholders . ')
            AND is_active = 1
          ORDER BY full_name');
    $prStmt->execute($teamIds);
    $players = $prStmt->fetchAll();
} catch (Exception $e) { /* league_players may not exist yet */ }

json_response(array(
    'captain'  => array(
        'team_id'       => $c['team_id'],
        'team_name'     => $c['team_name'],
        'display_name'  => $c['display_name'],
        'division_name' => $c['division_name'],
    ),
    'fixtures' => $fixtures,
    'players'  => $players,
));
