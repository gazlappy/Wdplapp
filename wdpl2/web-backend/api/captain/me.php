<?php
// captain/me.php — GET current captain + upcoming fixtures (configurable window) + roster.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_captain.php';

$c = require_captain();
$team_id = $c['team_id'];

// Fixture window = Monday 00:00 UTC of the current ISO week .. N weeks later,
// where N comes from the league setting `captain_fixture_weeks_ahead`
// (published by the desktop app, default 1 = this week only). A fixture only
// appears within that window; once BOTH captains have finalized the shared
// scorecard the fixture drops off the list (it's then on the admin's web
// inbox awaiting reconciliation).
$weeksAhead = 1;
try {
    $ws = db()->prepare("SELECT setting_value FROM league_settings WHERE setting_key = 'captain_fixture_weeks_ahead' LIMIT 1");
    $ws->execute();
    $wv = $ws->fetchColumn();
    if ($wv !== false && (int)$wv >= 1) $weeksAhead = min(52, (int)$wv);
} catch (Exception $e) { /* league_settings may not exist yet - default to 1 */ }
$now    = time();
$start  = strtotime('monday this week 00:00:00', $now);
$end    = $start + $weeksAhead * 7 * 86400;
// Look back too: an unfinished (or admin-reopened) scorecard from a previous
// week must stay visible until BOTH captains have finalized it, otherwise it
// is stuck in limbo with no way to open it.
$lookback = $start - 8 * 7 * 86400;

$fixtures = array();
try {
    $fxStmt = db()->prepare(
        'SELECT f.fixture_id, f.season_id, f.division_id, f.home_team_id, f.away_team_id,
                f.home_team_name, f.away_team_name, f.venue_name, f.fixture_date
           FROM league_fixtures f
      LEFT JOIN live_scorecards s ON s.fixture_id = f.fixture_id
          WHERE (f.home_team_id = :th OR f.away_team_id = :ta)
            AND f.fixture_date >= :lb
            AND f.fixture_date <  :e
            AND NOT (s.home_finalized_at IS NOT NULL AND s.away_finalized_at IS NOT NULL)
          ORDER BY f.fixture_date ASC');
    $fxStmt->execute(array(
        ':th' => $team_id,
        ':ta' => $team_id,
        ':lb' => gmdate('Y-m-d H:i:s', $lookback),
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
// Current ISO week Monday (matches the portal's state.thisWeek computation) so
// the scorecard picker can hide players marked unavailable on the roster card.
$dow = (int)gmdate('N'); // 1=Mon .. 7=Sun
$weekMonday = gmdate('Y-m-d', time() - ($dow - 1) * 86400) . ' 00:00:00';
try {
    $placeholders = implode(',', array_fill(0, count($teamIds), '?'));
    $prStmt = db()->prepare(
        'SELECT p.player_id, p.team_id, p.full_name, COALESCE(a.available, 1) AS available
           FROM league_players p
      LEFT JOIN player_availability a
             ON a.player_id = p.player_id AND a.week_start_utc = ?
          WHERE p.team_id IN (' . $placeholders . ')
            AND p.is_active = 1
          ORDER BY p.full_name');
    $prStmt->execute(array_merge(array($weekMonday), $teamIds));
    $players = $prStmt->fetchAll();
    foreach ($players as $i => $p) { $players[$i]['available'] = (int)$p['available']; }
} catch (Exception $e) {
    // player_availability may not exist yet — fall back to the plain roster.
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
    } catch (Exception $e2) { /* league_players may not exist yet */ }
}

// League settings (populated by the desktop app via admin/publish-league.php).
// Falls back to WDPL defaults if the table doesn't exist yet.
$settings = array(
    'default_frames_per_match' => 15,
    'max_frames_per_player'    => 3,
    'captain_fixture_weeks_ahead' => $weeksAhead,
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
