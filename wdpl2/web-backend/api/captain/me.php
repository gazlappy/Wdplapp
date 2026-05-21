<?php
// captain/me.php — GET current captain + this week's fixtures + roster.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_captain.php';

$c = require_captain();
$team_id = $c['team_id'];

// "This week" = Monday 00:00 UTC of the current ISO week .. following Monday 00:00.
// A fixture only appears on the week it's actually being played; once BOTH
// captains have finalized the shared scorecard the fixture drops off the list
// (it's then on the admin's web inbox awaiting reconciliation).
$now    = time();
$start  = strtotime('monday this week 00:00:00', $now);
$end    = $start + 7 * 86400;

$fixtures = array();
try {
    $fxStmt = db()->prepare(
        'SELECT f.fixture_id, f.season_id, f.division_id, f.home_team_id, f.away_team_id,
                f.home_team_name, f.away_team_name, f.venue_name, f.fixture_date
           FROM league_fixtures f
      LEFT JOIN live_scorecards s ON s.fixture_id = f.fixture_id
          WHERE (f.home_team_id = :th OR f.away_team_id = :ta)
            AND f.fixture_date >= :s
            AND f.fixture_date <  :e
            AND NOT (s.home_finalized_at IS NOT NULL AND s.away_finalized_at IS NOT NULL)
          ORDER BY f.fixture_date ASC');
    $fxStmt->execute(array(
        ':th' => $team_id,
        ':ta' => $team_id,
        ':s'  => gmdate('Y-m-d H:i:s', $start),
        ':e'  => gmdate('Y-m-d H:i:s', $end),
    ));
    $fixtures = $fxStmt->fetchAll();
} catch (Exception $e) { /* league_fixtures or live_scorecards may not exist yet */ }

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

// League settings (populated by the desktop app via admin/publish-league.php).
// Falls back to WDPL defaults if the table doesn't exist yet.
$settings = array(
    'default_frames_per_match' => 15,
    'max_frames_per_player'    => 3,
);
try {
    $s = db()->query('SELECT setting_key, setting_value FROM league_settings')->fetchAll();
    foreach ($s as $row) {
        $k = $row['setting_key']; $v = $row['setting_value'];
        if ($k === 'default_frames_per_match') $settings['default_frames_per_match'] = (int)$v;
        else if ($k === 'max_frames_per_player') $settings['max_frames_per_player'] = (int)$v;
        else $settings[$k] = $v;
    }
} catch (Exception $e) { /* table not provisioned yet — use defaults */ }

json_response(array(
    'captain'  => array(
        'team_id'       => $c['team_id'],
        'team_name'     => $c['team_name'],
        'display_name'  => $c['display_name'],
        'division_name' => $c['division_name'],
    ),
    'fixtures' => $fixtures,
    'players'  => $players,
    'settings' => $settings,
));
