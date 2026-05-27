<?php
// admin/captain-account.php — quick captain-account ops.
// POST {action:"reset_password", team_id, password}      -> updates hash
// POST {action:"set_enabled",    team_id, enabled:0|1}
// POST {action:"kick_sessions",  team_id}                -> deletes captain_sessions rows
// POST {action:"transfer",       from_team_id, to_team_id} -> move captain row to a new team_id (and rewrite team_name)
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
$me = require_admin();
require_post();
$pdo = db();

$body = read_json_body();
$action = trim((string)(isset($body['action']) ? $body['action'] : ''));

if ($action === 'reset_password') {
    $tid = trim((string)(isset($body['team_id']) ? $body['team_id'] : ''));
    $pw  = (string)(isset($body['password']) ? $body['password'] : '');
    if ($tid === '' || strlen($pw) < 6) json_response(array('error' => 'team_id and password (min 6) required'), 400);
    $h = password_hash($pw, PASSWORD_DEFAULT);
    $pdo->prepare('UPDATE captains SET password_hash = :h WHERE team_id = :t')
        ->execute(array(':h' => $h, ':t' => $tid));
    $pdo->prepare('DELETE FROM captain_sessions WHERE team_id = :t')->execute(array(':t' => $tid));
    audit_log($me, 'captain.reset_password', $tid);
    json_response(array('ok' => true));
}

if ($action === 'set_enabled') {
    $tid = trim((string)(isset($body['team_id']) ? $body['team_id'] : ''));
    $en  = !empty($body['enabled']) ? 1 : 0;
    if ($tid === '') json_response(array('error' => 'team_id required'), 400);
    $pdo->prepare('UPDATE captains SET enabled = :e WHERE team_id = :t')->execute(array(':e' => $en, ':t' => $tid));
    if (!$en) $pdo->prepare('DELETE FROM captain_sessions WHERE team_id = :t')->execute(array(':t' => $tid));
    audit_log($me, 'captain.set_enabled', $tid, array('enabled' => $en));
    json_response(array('ok' => true));
}

if ($action === 'kick_sessions') {
    $tid = trim((string)(isset($body['team_id']) ? $body['team_id'] : ''));
    if ($tid === '') json_response(array('error' => 'team_id required'), 400);
    $d = $pdo->prepare('DELETE FROM captain_sessions WHERE team_id = :t');
    $d->execute(array(':t' => $tid));
    audit_log($me, 'captain.kick_sessions', $tid, array('rows' => $d->rowCount()));
    json_response(array('ok' => true, 'rows' => $d->rowCount()));
}

if ($action === 'transfer') {
    require_admin('superadmin');
    $from = trim((string)(isset($body['from_team_id']) ? $body['from_team_id'] : ''));
    $to   = trim((string)(isset($body['to_team_id'])   ? $body['to_team_id']   : ''));
    if ($from === '' || $to === '') json_response(array('error' => 'from_team_id and to_team_id required'), 400);
    $newTeam = $pdo->prepare('SELECT name, division_id, division_name FROM league_teams WHERE team_id = :t LIMIT 1');
    $newTeam->execute(array(':t' => $to)); $nt = $newTeam->fetch();
    if (!$nt) json_response(array('error' => 'destination team not found'), 404);
    $pdo->prepare(
        'UPDATE captains SET team_id = :to, team_name = :tn, division_id = :did, division_name = :dn WHERE team_id = :from')
       ->execute(array(':to' => $to, ':tn' => $nt['name'], ':did' => $nt['division_id'], ':dn' => $nt['division_name'], ':from' => $from));
    $pdo->prepare('DELETE FROM captain_sessions WHERE team_id = :t')->execute(array(':t' => $from));
    audit_log($me, 'captain.transfer', $from, array('to' => $to));
    json_response(array('ok' => true));
}

json_response(array('error' => 'unknown action'), 400);
