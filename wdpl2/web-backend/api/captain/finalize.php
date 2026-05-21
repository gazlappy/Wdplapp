<?php
// captain/finalize.php — captain submits THEIR view of the shared scorecard.
// Writes a normal 'match_result' row into `submissions` (so the existing
// admin inbox reconciliation kicks in — diverging cards or single-captain
// finalizations surface there as usual).
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_captain.php';
require_post();

$c    = require_captain();
$body = read_json_body();
$fid  = trim((string)(isset($body['fixture_id']) ? $body['fixture_id'] : ''));
$notes= trim((string)(isset($body['notes']) ? $body['notes'] : ''));
if ($fid === '') json_response(array('error' => 'fixture_id required'), 400);

$fx = db()->prepare(
    'SELECT fixture_id, season_id, home_team_id, away_team_id,
            home_team_name, away_team_name, fixture_date
       FROM league_fixtures WHERE fixture_id = :f LIMIT 1');
$fx->execute(array(':f' => $fid));
$fixture = $fx->fetch();
if (!$fixture) json_response(array('error' => 'fixture not found'), 404);
if ($fixture['home_team_id'] !== $c['team_id'] && $fixture['away_team_id'] !== $c['team_id'])
    json_response(array('error' => 'not your fixture'), 403);

$side = ($fixture['home_team_id'] === $c['team_id']) ? 'home' : 'away';

$row = db()->prepare('SELECT version, state_json FROM live_scorecards WHERE fixture_id = :f LIMIT 1');
$row->execute(array(':f' => $fid));
$live = $row->fetch();
if (!$live) json_response(array('error' => 'no scorecard to finalize'), 400);

$state = json_decode($live['state_json'], true);
if (!is_array($state) || empty($state['frames'])) json_response(array('error' => 'no frames'), 400);

// Sanity: every frame must have a winner.
foreach ($state['frames'] as $f) {
    if (empty($f['winner'])) json_response(array('error' => 'not all frames scored'), 400);
}

// Build a per-side payload matching submit-result.php's shape so the existing
// MatchResultImporter / inbox can reconcile both captains' submissions.
$frames = array();
foreach ($state['frames'] as $f) {
    $frames[] = array(
        'number'           => (int)$f['number'],
        'home_player_id'   => isset($f['home_player_id'])   ? $f['home_player_id']   : null,
        'home_player_name' => isset($f['home_player_name']) ? $f['home_player_name'] : null,
        'away_player_id'   => isset($f['away_player_id'])   ? $f['away_player_id']   : null,
        'away_player_name' => isset($f['away_player_name']) ? $f['away_player_name'] : null,
        'home_player2_id'  => isset($f['home_player2_id'])  ? $f['home_player2_id']  : null,
        'away_player2_id'  => isset($f['away_player2_id'])  ? $f['away_player2_id']  : null,
        'winner'           => $f['winner'],
        'eight_ball'       => !empty($f['eight_ball']),
        'is_doubles'       => !empty($f['is_doubles']),
    );
}

$payload = array(
    'fixture_id'   => $fid,
    'season_id'    => $fixture['season_id'],
    'submitted_by' => array(
        'team_id'   => $c['team_id'],
        'team_name' => $c['team_name'],
        'side'      => $side,
        'username'  => $c['username'],
        'shared'    => true,
        'live_version' => (int)$live['version'],
    ),
    'fixture_meta' => array(
        'home_team_id'   => $fixture['home_team_id'],
        'away_team_id'   => $fixture['away_team_id'],
        'home_team_name' => $fixture['home_team_name'],
        'away_team_name' => $fixture['away_team_name'],
        'fixture_date'   => $fixture['fixture_date'],
    ),
    'frames'       => $frames,
    'new_players'  => array(),
    'notes'        => $notes !== '' ? $notes : (isset($state['notes']) ? (string)$state['notes'] : ''),
);

$ins = db()->prepare(
    'INSERT INTO submissions
        (type, season_id, reference_id, payload_json, submitter, submitter_ip)
     VALUES ("match_result", :sid, :rid, :p, :s, :ip)');
$ins->execute(array(
    ':sid' => $fixture['season_id'],
    ':rid' => $fid,
    ':p'   => json_encode($payload, JSON_UNESCAPED_SLASHES),
    ':s'   => ($c['display_name'] !== null && $c['display_name'] !== ''
                 ? $c['display_name'] . ' (' . $c['team_name'] . ')'
                 : $c['team_name']),
    ':ip'  => isset($_SERVER['REMOTE_ADDR']) ? $_SERVER['REMOTE_ADDR'] : null,
));
$subId = (int)db()->lastInsertId();

// Stamp the live row.
$col = ($side === 'home') ? 'home_finalized_at' : 'away_finalized_at';
$colV= ($side === 'home') ? 'home_finalized_version' : 'away_finalized_version';
db()->prepare("UPDATE live_scorecards
                  SET $col = UTC_TIMESTAMP(), $colV = :v
                WHERE fixture_id = :f")
   ->execute(array(':v' => (int)$live['version'], ':f' => $fid));

// Re-read to report status.
$row2 = db()->prepare('SELECT home_finalized_at, away_finalized_at FROM live_scorecards WHERE fixture_id = :f');
$row2->execute(array(':f' => $fid));
$st = $row2->fetch();

json_response(array(
    'ok'             => true,
    'id'             => $subId,
    'your_side'      => $side,
    'home_finalized' => !empty($st['home_finalized_at']),
    'away_finalized' => !empty($st['away_finalized_at']),
), 201);
