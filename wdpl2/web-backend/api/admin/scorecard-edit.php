<?php
// admin/scorecard-edit.php — full admin control of a single live scorecard.
// GET ?fixture_id=...           -> { state, version, home_finalized, away_finalized }
// POST {action:"set_frame", fixture_id, frame, fields:{...}}  -> overwrite fields on a frame
// POST {action:"set_notes", fixture_id, value}
// POST {action:"add_frame", fixture_id}      -> append blank frame
// POST {action:"remove_frame", fixture_id}   -> remove last frame
// POST {action:"force_finalize", fixture_id, side}    side=home|away|both
// fields allowed: is_doubles, winner, eight_ball,
//                 home_player_id, home_player_name, home_player2_id, home_player2_name,
//                 away_player_id, away_player_name, away_player2_id, away_player2_name
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
$me = require_admin();
$pdo = db();

function ase_load($fid) {
    $pdo = db();
    $s = $pdo->prepare('SELECT version, state_json, home_finalized_version, away_finalized_version
                          FROM live_scorecards WHERE fixture_id = :f LIMIT 1');
    $s->execute(array(':f' => $fid));
    $row = $s->fetch();
    if (!$row) return null;
    $st = json_decode($row['state_json'], true);
    return array('version' => (int)$row['version'], 'state' => is_array($st) ? $st : array('frames' => array()),
                 'home_finalized' => $row['home_finalized_version'] !== null,
                 'away_finalized' => $row['away_finalized_version'] !== null);
}

function ase_save($fid, $ver, $state) {
    db()->prepare('UPDATE live_scorecards SET version=:v, state_json=:s, updated_utc=:u WHERE fixture_id=:f')
        ->execute(array(':v'=>$ver, ':s'=>json_encode($state), ':u'=>gmdate('Y-m-d H:i:s'), ':f'=>$fid));
}

if ($_SERVER['REQUEST_METHOD'] === 'GET') {
    $fid = trim((string)(isset($_GET['fixture_id']) ? $_GET['fixture_id'] : ''));
    if ($fid === '') json_response(array('error' => 'fixture_id required'), 400);
    $r = ase_load($fid);
    if (!$r) json_response(array('error' => 'no live card'), 404);
    json_response($r);
}

require_post();
$body   = read_json_body();
$action = trim((string)(isset($body['action']) ? $body['action'] : ''));
$fid    = trim((string)(isset($body['fixture_id']) ? $body['fixture_id'] : ''));
if ($fid === '') json_response(array('error' => 'fixture_id required'), 400);

$pdo->beginTransaction();
try {
    $sel = $pdo->prepare('SELECT version, state_json FROM live_scorecards WHERE fixture_id = :f FOR UPDATE');
    $sel->execute(array(':f' => $fid));
    $row = $sel->fetch();
    if (!$row) { $pdo->rollBack(); json_response(array('error' => 'no live card'), 404); }
    $ver   = (int)$row['version'];
    $state = json_decode($row['state_json'], true);
    if (!is_array($state)) $state = array('frames' => array());
    if (!isset($state['frames']) || !is_array($state['frames'])) $state['frames'] = array();

    if ($action === 'set_frame') {
        $idx = isset($body['frame']) ? (int)$body['frame'] : -1;
        $fields = isset($body['fields']) && is_array($body['fields']) ? $body['fields'] : array();
        if ($idx < 0 || $idx >= count($state['frames'])) { $pdo->rollBack(); json_response(array('error' => 'bad frame index'), 400); }
        $allowed = array('is_doubles','winner','eight_ball',
            'home_player_id','home_player_name','home_player2_id','home_player2_name',
            'away_player_id','away_player_name','away_player2_id','away_player2_name');
        foreach ($fields as $k => $v) {
            if (!in_array($k, $allowed, true)) continue;
            if ($k === 'is_doubles' || $k === 'eight_ball') $v = !empty($v);
            if ($k === 'winner' && !in_array($v, array('home','away',null), true)) continue;
            $state['frames'][$idx][$k] = $v;
        }
        if (!empty($state['frames'][$idx]['winner']) && ($state['frames'][$idx]['winner'] !== 'home' && $state['frames'][$idx]['winner'] !== 'away')) {
            $state['frames'][$idx]['winner'] = null;
        }
        $state['frames'][$idx]['pending_eight'] = null;
    }
    else if ($action === 'set_notes') {
        $state['notes'] = isset($body['value']) ? (string)$body['value'] : '';
    }
    else if ($action === 'add_frame') {
        $n = count($state['frames']) + 1;
        $state['frames'][] = array(
            'number' => $n, 'is_doubles' => false,
            'home_player_id' => null, 'home_player_name' => null,
            'home_player2_id'=> null, 'home_player2_name'=> null,
            'away_player_id' => null, 'away_player_name' => null,
            'away_player2_id'=> null, 'away_player2_name'=> null,
            'winner' => null, 'eight_ball' => false, 'pending_eight' => null);
    }
    else if ($action === 'remove_frame') {
        if (!count($state['frames'])) { $pdo->rollBack(); json_response(array('error' => 'no frames')); }
        array_pop($state['frames']);
    }
    else if ($action === 'force_finalize') {
        $side = strtolower(trim((string)(isset($body['side']) ? $body['side'] : 'both')));
        $cols = '';
        $now = gmdate('Y-m-d H:i:s');
        $params = array(':f' => $fid, ':u' => $now, ':v' => $ver);
        if ($side === 'home' || $side === 'both') $cols .= 'home_finalized_at=:u, home_finalized_version=:v,';
        if ($side === 'away' || $side === 'both') $cols .= 'away_finalized_at=:u, away_finalized_version=:v,';
        if ($cols === '') { $pdo->rollBack(); json_response(array('error' => 'bad side'), 400); }
        $cols = rtrim($cols, ',');
        $pdo->prepare("UPDATE live_scorecards SET $cols WHERE fixture_id=:f")->execute($params);
        $pdo->commit();
        audit_log($me, 'scorecard.force_finalize', $fid, array('side'=>$side));
        $r = ase_load($fid);
        json_response(array('ok' => true) + $r);
    }
    else {
        $pdo->rollBack();
        json_response(array('error' => 'unknown action'), 400);
    }

    $ver++;
    $state['last_edit'] = array('by' => 'admin:' . $me['username'], 'at' => gmdate('c'));
    ase_save($fid, $ver, $state);
    $pdo->commit();
    audit_log($me, 'scorecard.' . $action, $fid, isset($body['frame']) ? array('frame'=>$body['frame']) : null);
    $r = ase_load($fid);
    json_response(array('ok' => true) + $r);
}
catch (Exception $e) {
    if ($pdo->inTransaction()) $pdo->rollBack();
    json_response(array('error' => $e->getMessage()), 500);
}
