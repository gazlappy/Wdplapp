<?php
// admin/teams.php — admin team management.
// GET                                 -> list teams, plus a quick captain-username lookup.
// POST {action:"update", team_id, ...} -> update name/division_name/venue_name.
// POST {action:"create", team_id, name, division_id, division_name, venue_name, season_id}
//                                      -> insert a new team row (kept for one-offs).
// DELETE {team_id}                    -> remove the team row.
// Auth: admin login (session cookie / bearer) or HTTP Basic auth (MAUI app).
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
$me = require_admin();

$method = isset($_SERVER['REQUEST_METHOD']) ? $_SERVER['REQUEST_METHOD'] : 'GET';

if ($method === 'GET') {
    try {
        $teams = db()->query(
           'SELECT t.team_id, t.season_id, t.division_id, t.division_name,
                   t.name AS team_name, t.venue_name,
                   c.username AS captain_username, c.enabled AS captain_enabled
              FROM league_teams t
         LEFT JOIN captains c ON c.team_id = t.team_id
          ORDER BY t.division_name, t.name')->fetchAll();
    } catch (Exception $e) { $teams = array(); }
    json_response(array('items' => $teams));
}

if ($method === 'DELETE') {
    require_admin('superadmin');
    $body = read_json_body();
    $team_id = trim((string)(isset($body['team_id']) ? $body['team_id'] : ''));
    if ($team_id === '') json_response(array('error' => 'team_id required'), 400);
    $stmt = db()->prepare('DELETE FROM league_teams WHERE team_id = :t');
    $stmt->execute(array(':t' => $team_id));
    audit_log($me, 'team.delete', $team_id);
    json_response(array('ok' => true, 'rows' => $stmt->rowCount()));
}

require_post();
$body   = read_json_body();
$action = trim((string)(isset($body['action']) ? $body['action'] : 'update'));
$tid    = trim((string)(isset($body['team_id']) ? $body['team_id'] : ''));
if ($tid === '') json_response(array('error' => 'team_id required'), 400);

if ($action === 'update') {
    // Whitelisted columns only.
    $name = trim((string)(isset($body['name'])          ? $body['name']          : ''));
    $dn   = trim((string)(isset($body['division_name']) ? $body['division_name'] : ''));
    $vn   = trim((string)(isset($body['venue_name'])    ? $body['venue_name']    : ''));
    if ($name === '') json_response(array('error' => 'name required'), 400);

    $stmt = db()->prepare(
        'UPDATE league_teams
            SET name = :n,
                division_name = :dn,
                venue_name = :vn
          WHERE team_id = :t');
    $stmt->execute(array(':n' => $name, ':dn' => ($dn === '' ? null : $dn), ':vn' => ($vn === '' ? null : $vn), ':t' => $tid));

    // Keep the captain row's team_name in sync for the captain portal header.
    try {
        db()->prepare('UPDATE captains SET team_name = :n, division_name = :dn WHERE team_id = :t')
            ->execute(array(':n' => $name, ':dn' => ($dn === '' ? null : $dn), ':t' => $tid));
    } catch (Exception $e) { /* captains table may not exist yet */ }

    audit_log($me, 'team.update', $tid, array('name'=>$name,'division'=>$dn,'venue'=>$vn));
    json_response(array('ok' => true, 'rows' => $stmt->rowCount()));
}

if ($action === 'create') {
    $name = trim((string)(isset($body['name']) ? $body['name'] : ''));
    if ($name === '') json_response(array('error' => 'name required'), 400);
    $stmt = db()->prepare(
       'INSERT INTO league_teams (team_id, season_id, division_id, name, division_name, venue_name)
        VALUES (:tid, :sid, :did, :n, :dn, :vn)
        ON DUPLICATE KEY UPDATE name=VALUES(name), division_id=VALUES(division_id),
                                division_name=VALUES(division_name), venue_name=VALUES(venue_name)');
    $stmt->execute(array(
        ':tid' => $tid,
        ':sid' => isset($body['season_id'])     ? $body['season_id']     : null,
        ':did' => isset($body['division_id'])   ? $body['division_id']   : null,
        ':n'   => $name,
        ':dn'  => isset($body['division_name']) ? $body['division_name'] : null,
        ':vn'  => isset($body['venue_name'])    ? $body['venue_name']    : null,
    ));
    json_response(array('ok' => true, 'team_id' => $tid));
}

json_response(array('error' => 'unknown action'), 400);
