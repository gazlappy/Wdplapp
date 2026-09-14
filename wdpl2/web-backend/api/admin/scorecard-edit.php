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
require_once __DIR__ . '/../_admin.php';
require_once __DIR__ . '/../_admin_sync.php';
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

admin_sync_ensure_schema();
if ($_SERVER['REQUEST_METHOD'] === 'GET') {
    $fid = trim((string)(isset($_GET['fixture_id']) ? $_GET['fixture_id'] : ''));
    if ($fid === '') json_response(array('error' => 'fixture_id required'), 400);
    try {
        $pdo->beginTransaction();
        $backend = admin_sync_lock();
        $r = ase_load($fid);
        $record = admin_sync_read_record('scorecard', $fid);
        $pdo->commit();
        if (!$r) json_response(array('error' => 'no live card'), 404);
        json_response($r + array('protocol' => 1, 'backendId' => $backend['backend_id'], 'revision' => $record ? $record['revision'] : 0));
    } catch (Exception $e) {
        if ($pdo->inTransaction()) $pdo->rollBack();
        json_response(array('error' => 'Scorecard unavailable.'), 500);
    }
}

require_post();
$body   = read_json_body();
$action = trim((string)(isset($body['action']) ? $body['action'] : ''));
$fid    = trim((string)(isset($body['fixture_id']) ? $body['fixture_id'] : ''));
if ($fid === '') json_response(array('error' => 'fixture_id required'), 400);

try {
    $expected = admin_sync_integer($body['expected_revision'] ?? null, 'expected_revision');
    $expectedVersion = admin_sync_integer($body['expected_version'] ?? null, 'expected_version');
    $requestId = admin_sync_request_id($body['request_id'] ?? '');
    $pdo->beginTransaction();
    $backend = admin_sync_lock();
    if (($body['backend_id'] ?? '') !== $backend['backend_id']) {
        $pdo->rollBack(); json_response(array('error' => 'Backend changed. Reload and review the card.'), 409);
    }
    $prepared = admin_sync_prepare_change($me, 'scorecard', $fid, $expected, $requestId, $body);
    if (!empty($prepared['conflict'])) { $pdo->rollBack(); json_response($prepared, 409); }
    if (!empty($prepared['replayed'])) {
        $pdo->commit();
        json_response(array('protocol' => 1, 'backendId' => $backend['backend_id'], 'id' => $fid,
            'requestId' => $requestId, 'accepted' => true, 'revision' => $prepared['revision'], 'sequence' => $prepared['sequence']));
    }
    $sel = $pdo->prepare('SELECT version, state_json FROM live_scorecards WHERE fixture_id = :f FOR UPDATE');
    $sel->execute(array(':f' => $fid));
    $row = $sel->fetch();
    if (!$row) { $pdo->rollBack(); json_response(array('error' => 'no live card'), 404); }
    $ver   = (int)$row['version'];
    if ($ver !== $expectedVersion) {
        $pdo->rollBack(); json_response(array('error' => 'Live card changed. Reload and compare before editing.'), 409);
    }
    $query = $pdo->prepare('SELECT season_id FROM league_fixtures WHERE fixture_id = ?');
    $query->execute(array($fid));
    $season = $query->fetchColumn();
    if (!$season) throw new InvalidArgumentException('The fixture needs an explicit season before editing.');
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
        if (!count($state['frames'])) { $pdo->rollBack(); json_response(array('error' => 'no frames'), 422); }
        array_pop($state['frames']);
    }
    else if ($action === 'force_finalize') {
        $side = strtolower(trim((string)(isset($body['side']) ? $body['side'] : 'both')));
        $cols = '';
        $now = gmdate('Y-m-d H:i:s');
        $params = array(':f' => $fid);
        if ($side === 'home' || $side === 'both') {
            $cols .= 'home_finalized_at=:hu, home_finalized_version=:hv,';
            $params[':hu'] = $now; $params[':hv'] = $ver + 1;
        }
        if ($side === 'away' || $side === 'both') {
            $cols .= 'away_finalized_at=:au, away_finalized_version=:av,';
            $params[':au'] = $now; $params[':av'] = $ver + 1;
        }
        if ($cols === '') { $pdo->rollBack(); json_response(array('error' => 'bad side'), 400); }
        $cols = rtrim($cols, ',');
        $pdo->prepare("UPDATE live_scorecards SET $cols WHERE fixture_id=:f")->execute($params);
    }
    else {
        $pdo->rollBack();
        json_response(array('error' => 'unknown action'), 400);
    }

    $ver++;
    $state['last_edit'] = array('by' => 'admin:' . $me['username'], 'at' => gmdate('c'));
    if ($action !== 'force_finalize') {
        $pdo->prepare('UPDATE live_scorecards SET home_finalized_at = NULL, home_finalized_version = NULL,
            away_finalized_at = NULL, away_finalized_version = NULL WHERE fixture_id = ?')->execute(array($fid));
    }
    ase_save($fid, $ver, $state);
    $receipt = admin_sync_commit_change($me, 'scorecard', $fid, $season, 'web', ase_load($fid), $prepared);
    $pdo->commit();
    audit_log($me, 'scorecard.' . $action, $fid, isset($body['frame']) ? array('frame'=>$body['frame']) : null);
    json_response($receipt + array('id' => $fid, 'requestId' => $requestId, 'accepted' => true));
}
catch (InvalidArgumentException $e) {
    if ($pdo->inTransaction()) $pdo->rollBack();
    json_response(array('error' => $e->getMessage()), 422);
}
catch (Exception $e) {
    if ($pdo->inTransaction()) $pdo->rollBack();
    error_log('WDPL scorecard edit: ' . get_class($e));
    json_response(array('error' => 'Scorecard edit unavailable. Retry the same saved request.'), 500);
}
