<?php
// admin/reopen-fixture.php — admin: send a finalized scorecard back to the captains.
// Body: { "fixture_id": "...guid...", "by": "admin name", "notes": "optional" }
//
// Clears home/away finalization stamps on live_scorecards (so the fixture
// reappears in the captain portal under "Your fixtures this week") and marks
// any currently-pending match_result submissions for that fixture as processed
// with a "reopened" note so the inbox doesn't keep re-surfacing the old cards.
// PHP 5.6 compatible.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
require_admin();
require_post();

$body = read_json_body();
$fid  = trim((string)(isset($body['fixture_id']) ? $body['fixture_id'] : ''));
$by   = substr((string)(isset($body['by'])    ? $body['by']    : 'admin'), 0, 120);
$note = substr((string)(isset($body['notes']) ? $body['notes'] : 'reopened by admin'), 0, 500);

if ($fid === '') {
    json_response(array('error' => 'fixture_id required'), 400);
}

$pdo = db();
$pdo->beginTransaction();
try {
    // 1) Clear finalization on the live scorecard so the captain portal
    //    treats the fixture as in-progress again. Keep frames/state intact
    //    so the captains can edit + re-finalize on top of what's already there.
    $upd = $pdo->prepare(
        'UPDATE live_scorecards
            SET home_finalized_at = NULL, home_finalized_version = NULL,
                away_finalized_at = NULL, away_finalized_version = NULL
          WHERE fixture_id = :f');
    $upd->execute(array(':f' => $fid));
    $liveCleared = (int)$upd->rowCount();

    // 2) Mark any still-pending match_result submissions for this fixture
    //    as processed so the inbox no longer shows the old cards.
    $msg = trim('reopened: ' . $note);
    $ms = $pdo->prepare(
        "UPDATE submissions
            SET processed     = 1,
                processed_utc = UTC_TIMESTAMP(),
                processed_by  = :b,
                notes         = :n
          WHERE processed = 0
            AND type = 'match_result'
            AND reference_id = :f");
    $ms->execute(array(':b' => $by, ':n' => $msg, ':f' => $fid));
    $subsCleared = (int)$ms->rowCount();

    $pdo->commit();

    json_response(array(
        'ok'                 => true,
        'fixture_id'         => $fid,
        'live_cleared'       => $liveCleared,
        'submissions_closed' => $subsCleared,
    ));
} catch (Exception $e) {
    if ($pdo->inTransaction()) $pdo->rollBack();
    json_response(array('error' => $e->getMessage()), 500);
}
