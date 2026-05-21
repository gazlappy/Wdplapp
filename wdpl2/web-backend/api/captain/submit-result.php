<?php
// captain/submit-result.php — POST result card. Wraps the existing submissions table.
// Body shape (JSON):
// {
//   "fixture_id": "...guid...",
//   "frames": [
//     { "number":1, "home_player_id":"...", "home_player_name":"...",
//                   "away_player_id":"...", "away_player_name":"...",
//                   "winner":"home"|"away", "eight_ball":false, "is_doubles":false,
//                   "home_player2_id":null, "away_player2_id":null }, ...
//   ],
//   "new_players": [ { "name":"Joe Bloggs" } ],
//   "notes": "optional free text"
// }
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_captain.php';
require_post();

$c   = require_captain();
$body = read_json_body();

$fixture_id  = trim((string)(isset($body['fixture_id']) ? $body['fixture_id'] : ''));
$frames      = isset($body['frames'])      && is_array($body['frames'])      ? $body['frames']      : array();
$new_players = isset($body['new_players']) && is_array($body['new_players']) ? $body['new_players'] : array();
$notes       = trim((string)(isset($body['notes']) ? $body['notes'] : ''));

if ($fixture_id === '' || empty($frames)) {
    json_response(array('error' => 'fixture_id and frames required'), 400);
}

// Verify the fixture belongs to this captain.
$fx = db()->prepare(
    'SELECT fixture_id, season_id, home_team_id, away_team_id,
            home_team_name, away_team_name, fixture_date
       FROM league_fixtures WHERE fixture_id = :f LIMIT 1');
$fx->execute(array(':f' => $fixture_id));
$fixture = $fx->fetch();
if (!$fixture) json_response(array('error' => 'fixture not found'), 404);
if ($fixture['home_team_id'] !== $c['team_id'] && $fixture['away_team_id'] !== $c['team_id']) {
    json_response(array('error' => 'not your fixture'), 403);
}

$side = ($fixture['home_team_id'] === $c['team_id']) ? 'home' : 'away';

$payload = array(
    'fixture_id'   => $fixture_id,
    'season_id'    => $fixture['season_id'],
    'submitted_by' => array(
        'team_id'   => $c['team_id'],
        'team_name' => $c['team_name'],
        'side'      => $side,
        'username'  => $c['username'],
    ),
    'fixture_meta' => array(
        'home_team_id'   => $fixture['home_team_id'],
        'away_team_id'   => $fixture['away_team_id'],
        'home_team_name' => $fixture['home_team_name'],
        'away_team_name' => $fixture['away_team_name'],
        'fixture_date'   => $fixture['fixture_date'],
    ),
    'frames'       => $frames,
    'new_players'  => $new_players,
    'notes'        => $notes,
);

$ins = db()->prepare(
    'INSERT INTO submissions
        (type, season_id, reference_id, payload_json, submitter, submitter_ip)
     VALUES ("match_result", :sid, :rid, :payload, :submitter, :ip)');
$ins->execute(array(
    ':sid'       => $fixture['season_id'],
    ':rid'       => $fixture_id,
    ':payload'   => json_encode($payload, JSON_UNESCAPED_SLASHES),
    ':submitter' => $c['display_name'] !== null && $c['display_name'] !== ''
                       ? $c['display_name'] . ' (' . $c['team_name'] . ')'
                       : $c['team_name'],
    ':ip'        => isset($_SERVER['REMOTE_ADDR']) ? $_SERVER['REMOTE_ADDR'] : null,
));

json_response(array('ok' => true, 'id' => (int)db()->lastInsertId()), 201);
