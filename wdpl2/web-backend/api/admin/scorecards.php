<?php
// admin/scorecards.php — admin view of live scorecards (in-progress + finalized).
// GET  -> list scorecards with status, version, last edit, finalization stamps.
//         Optional ?status=open|home_only|away_only|both
// POST {action:"clear", fixture_id}      -> clear ONE side's finalization (body: side=home|away)
// POST {action:"reset", fixture_id}      -> wipe live state entirely so a fresh card starts
// POST {action:"delete", fixture_id}     -> delete the live_scorecards row
//
// Auth: admin login (session cookie / bearer) or HTTP Basic auth (MAUI app).
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
$me = require_admin();

$method = isset($_SERVER['REQUEST_METHOD']) ? $_SERVER['REQUEST_METHOD'] : 'GET';

if ($method === 'GET') {
    $status = isset($_GET['status']) ? (string)$_GET['status'] : '';
    $where  = '';
    if     ($status === 'open')      $where = 'WHERE s.home_finalized_at IS NULL AND s.away_finalized_at IS NULL';
    elseif ($status === 'home_only') $where = 'WHERE s.home_finalized_at IS NOT NULL AND s.away_finalized_at IS NULL';
    elseif ($status === 'away_only') $where = 'WHERE s.home_finalized_at IS NULL AND s.away_finalized_at IS NOT NULL';
    elseif ($status === 'both')      $where = 'WHERE s.home_finalized_at IS NOT NULL AND s.away_finalized_at IS NOT NULL';

    $sql = "SELECT s.fixture_id, s.version, s.updated_utc,
                   s.home_finalized_at, s.away_finalized_at,
                   f.home_team_name, f.away_team_name, f.fixture_date,
                   f.division_id
              FROM live_scorecards s
         LEFT JOIN league_fixtures f ON f.fixture_id = s.fixture_id
            $where
          ORDER BY s.updated_utc DESC
             LIMIT 500";
    try {
        $rows = db()->query($sql)->fetchAll();
    } catch (Exception $e) {
        $rows = array();
    }
    json_response(array('items' => $rows));
}

require_post();
$body   = read_json_body();
$action = trim((string)(isset($body['action'])     ? $body['action']     : ''));
$fid    = trim((string)(isset($body['fixture_id']) ? $body['fixture_id'] : ''));
if ($fid === '') json_response(array('error' => 'fixture_id required'), 400);

if ($action === 'clear') {
    $side = strtolower(trim((string)(isset($body['side']) ? $body['side'] : '')));
    if ($side !== 'home' && $side !== 'away') json_response(array('error' => 'side must be home or away'), 400);
    $col  = ($side === 'home') ? 'home_finalized_at' : 'away_finalized_at';
    $colV = ($side === 'home') ? 'home_finalized_version' : 'away_finalized_version';
    $stmt = db()->prepare("UPDATE live_scorecards SET $col = NULL, $colV = NULL WHERE fixture_id = :f");
    $stmt->execute(array(':f' => $fid));
    audit_log($me, 'scorecard.clear', $fid, array('side' => $side));
    json_response(array('ok' => true, 'cleared' => $side, 'rows' => $stmt->rowCount()));
}

if ($action === 'reset') {
    require_admin('superadmin');
    // Drop the live state
    // they open the fixture. Also closes any pending match_result submissions.
    db()->beginTransaction();
    try {
        $d1 = db()->prepare('DELETE FROM live_scorecards WHERE fixture_id = :f');
        $d1->execute(array(':f' => $fid));
        $d2 = db()->prepare(
            "UPDATE submissions
                SET processed = 1, processed_utc = UTC_TIMESTAMP(),
                    processed_by = 'admin', notes = 'reset by admin'
              WHERE processed = 0 AND type = 'match_result' AND reference_id = :f");
        $d2->execute(array(':f' => $fid));
        db()->commit();
        audit_log($me, 'scorecard.reset', $fid);
        json_response(array('ok' => true, 'live_deleted' => $d1->rowCount(), 'submissions_closed' => $d2->rowCount()));
    } catch (Exception $e) {
        if (db()->inTransaction()) db()->rollBack();
        json_response(array('error' => $e->getMessage()), 500);
    }
}

if ($action === 'delete') {
    require_admin('superadmin');
    $stmt = db()->prepare('DELETE FROM live_scorecards WHERE fixture_id = :f');
    $stmt->execute(array(':f' => $fid));
    audit_log($me, 'scorecard.delete', $fid);
    json_response(array('ok' => true, 'rows' => $stmt->rowCount()));
}

json_response(array('error' => 'unknown action'), 400);
