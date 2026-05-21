<?php
// pending.php — admin: list unprocessed submissions.
// PHP 5.6 compatible.
require __DIR__ . '/../_db.php';

$stmt = db()->prepare(
   'SELECT id, type, season_id, reference_id, payload_json,
           submitter, received_utc
      FROM submissions
     WHERE processed = 0
     ORDER BY received_utc ASC
     LIMIT 500');
$stmt->execute();

$rows = array_map(function ($r) {
    $r['payload'] = json_decode($r['payload_json'], true);
    unset($r['payload_json']);
    return $r;
}, $stmt->fetchAll());

json_response(['items' => $rows]);
