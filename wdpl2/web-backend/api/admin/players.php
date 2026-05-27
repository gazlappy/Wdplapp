<?php
// admin/players.php — manage the player roster.
// GET ?team_id=&season_id=&q=  -> list players (+ usage counts in current live cards)
// POST {action:"create", team_id, season_id?, full_name, is_active?}
// POST {action:"update", player_id, team_id?, full_name?, is_active?}
// POST {action:"transfer", player_id, to_team_id}
// POST {action:"merge", keep_id, drop_ids:[...]}     -> rewrite live cards' name/id refs
// DELETE {player_id}
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
$me = require_admin();
$pdo = db();

if ($_SERVER['REQUEST_METHOD'] === 'GET') {
    $where = array(); $args = array();
    if (!empty($_GET['team_id']))   { $where[] = 'p.team_id = :t';   $args[':t'] = $_GET['team_id']; }
    if (!empty($_GET['season_id'])) { $where[] = 'p.season_id = :s'; $args[':s'] = $_GET['season_id']; }
    if (!empty($_GET['q']))         { $where[] = 'p.full_name LIKE :q'; $args[':q'] = '%' . $_GET['q'] . '%'; }
    $sql = 'SELECT p.player_id, p.team_id, t.name AS team_name, p.season_id,
                   p.full_name, p.is_active
              FROM league_players p
         LEFT JOIN league_teams t ON t.team_id = p.team_id'
         . ($where ? (' WHERE ' . implode(' AND ', $where)) : '')
         . ' ORDER BY t.name, p.full_name LIMIT 5000';
    try { $st = $pdo->prepare($sql); $st->execute($args); json_response(array('items' => $st->fetchAll())); }
    catch (Exception $e) { json_response(array('items' => array(), 'error' => $e->getMessage())); }
}

if ($_SERVER['REQUEST_METHOD'] === 'DELETE') {
    require_admin('superadmin');
    $body = read_json_body();
    $pid = trim((string)(isset($body['player_id']) ? $body['player_id'] : ''));
    if ($pid === '') json_response(array('error' => 'player_id required'), 400);
    $d = $pdo->prepare('DELETE FROM league_players WHERE player_id = :p');
    $d->execute(array(':p' => $pid));
    audit_log($me, 'player.delete', $pid);
    json_response(array('ok' => true, 'rows' => $d->rowCount()));
}

require_post();
$body = read_json_body();
$action = trim((string)(isset($body['action']) ? $body['action'] : ''));

function _new_guid() {
    $b = function_exists('random_bytes') ? random_bytes(16) : openssl_random_pseudo_bytes(16);
    $b[6] = chr((ord($b[6]) & 0x0f) | 0x40);
    $b[8] = chr((ord($b[8]) & 0x3f) | 0x80);
    $h = bin2hex($b);
    return substr($h,0,8).'-'.substr($h,8,4).'-'.substr($h,12,4).'-'.substr($h,16,4).'-'.substr($h,20,12);
}

if ($action === 'create') {
    $tid = trim((string)(isset($body['team_id']) ? $body['team_id'] : ''));
    $nm  = trim((string)(isset($body['full_name']) ? $body['full_name'] : ''));
    if ($tid === '' || $nm === '') json_response(array('error' => 'team_id and full_name required'), 400);
    $pid = isset($body['player_id']) && $body['player_id'] !== '' ? (string)$body['player_id'] : _new_guid();
    $sid = isset($body['season_id']) ? $body['season_id'] : null;
    $ac  = isset($body['is_active']) ? (!empty($body['is_active']) ? 1 : 0) : 1;
    $pdo->prepare(
        'INSERT INTO league_players (player_id, team_id, season_id, full_name, is_active)
         VALUES (:p, :t, :s, :n, :a)
         ON DUPLICATE KEY UPDATE team_id=VALUES(team_id), full_name=VALUES(full_name), is_active=VALUES(is_active)')
       ->execute(array(':p'=>$pid, ':t'=>$tid, ':s'=>$sid, ':n'=>$nm, ':a'=>$ac));
    audit_log($me, 'player.create', $pid, array('team' => $tid, 'name' => $nm));
    json_response(array('ok' => true, 'player_id' => $pid));
}

if ($action === 'update') {
    $pid = trim((string)(isset($body['player_id']) ? $body['player_id'] : ''));
    if ($pid === '') json_response(array('error' => 'player_id required'), 400);
    $cur = $pdo->prepare('SELECT * FROM league_players WHERE player_id = :p LIMIT 1');
    $cur->execute(array(':p' => $pid)); $row = $cur->fetch();
    if (!$row) json_response(array('error' => 'player not found'), 404);
    $tid = isset($body['team_id'])   ? (string)$body['team_id']   : $row['team_id'];
    $nm  = isset($body['full_name']) ? (string)$body['full_name'] : $row['full_name'];
    $ac  = isset($body['is_active']) ? (!empty($body['is_active']) ? 1 : 0) : (int)$row['is_active'];
    $pdo->prepare('UPDATE league_players SET team_id=:t, full_name=:n, is_active=:a WHERE player_id=:p')
        ->execute(array(':t'=>$tid, ':n'=>$nm, ':a'=>$ac, ':p'=>$pid));
    audit_log($me, 'player.update', $pid, array('team' => $tid, 'name' => $nm, 'active' => $ac));
    json_response(array('ok' => true));
}

if ($action === 'transfer') {
    $pid = trim((string)(isset($body['player_id']) ? $body['player_id'] : ''));
    $to  = trim((string)(isset($body['to_team_id']) ? $body['to_team_id'] : ''));
    if ($pid === '' || $to === '') json_response(array('error' => 'player_id and to_team_id required'), 400);
    $pdo->prepare('UPDATE league_players SET team_id = :t WHERE player_id = :p')
        ->execute(array(':t' => $to, ':p' => $pid));
    audit_log($me, 'player.transfer', $pid, array('to' => $to));
    json_response(array('ok' => true));
}

if ($action === 'merge') {
    require_admin('superadmin');
    $keep = trim((string)(isset($body['keep_id']) ? $body['keep_id'] : ''));
    $drop = isset($body['drop_ids']) && is_array($body['drop_ids']) ? $body['drop_ids'] : array();
    if ($keep === '' || !count($drop)) json_response(array('error' => 'keep_id and drop_ids required'), 400);
    $keepRow = $pdo->prepare('SELECT * FROM league_players WHERE player_id = :p LIMIT 1');
    $keepRow->execute(array(':p' => $keep)); $keepRow = $keepRow->fetch();
    if (!$keepRow) json_response(array('error' => 'keep player not found'), 404);

    // Rewrite live scorecards JSON (replace dropped ids with keep_id, name with keep name).
    $live = $pdo->query('SELECT fixture_id, state_json FROM live_scorecards')->fetchAll();
    $touched = 0;
    foreach ($live as $r) {
        $st = json_decode($r['state_json'], true);
        if (!is_array($st) || !isset($st['frames'])) continue;
        $changed = false;
        foreach ($st['frames'] as &$f) {
            foreach (array('home_player','home_player2','away_player','away_player2') as $px) {
                if (isset($f[$px.'_id']) && in_array($f[$px.'_id'], $drop, true)) {
                    $f[$px.'_id'] = $keep;
                    $f[$px.'_name'] = $keepRow['full_name'];
                    $changed = true;
                }
            }
        }
        unset($f);
        if ($changed) {
            $pdo->prepare('UPDATE live_scorecards SET state_json=:s, updated_utc=:u WHERE fixture_id=:f')
                ->execute(array(':s'=>json_encode($st), ':u'=>gmdate('Y-m-d H:i:s'), ':f'=>$r['fixture_id']));
            $touched++;
        }
    }
    // Delete duplicate player rows.
    $in = implode(',', array_fill(0, count($drop), '?'));
    $st = $pdo->prepare("DELETE FROM league_players WHERE player_id IN ($in)");
    $st->execute(array_values($drop));
    audit_log($me, 'player.merge', $keep, array('dropped' => $drop, 'live_touched' => $touched));
    json_response(array('ok' => true, 'live_cards_updated' => $touched, 'players_removed' => $st->rowCount()));
}

json_response(array('error' => 'unknown action'), 400);
