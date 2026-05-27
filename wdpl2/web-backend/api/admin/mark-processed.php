<?php
// mark-processed.php — admin: mark one or more submissions as processed.
// Body: { "ids": [1,2,3], "by": "admin name", "notes": "optional" }
// PHP 5.6 compatible.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
require_admin();
require_post();

$body = read_json_body();
$ids  = isset($body['ids']) ? $body['ids'] : [];
$by   = substr((string)(isset($body['by'])    ? $body['by']    : 'admin'), 0, 120);
$note = substr((string)(isset($body['notes']) ? $body['notes'] : ''),     0, 500);

if (!is_array($ids) || count($ids) === 0) {
    json_response(['error' => 'ids[] required'], 400);
}
$ids = array_values(array_filter(array_map('intval', $ids), function ($i) { return $i > 0; }));
if (count($ids) === 0) {
    json_response(['error' => 'ids[] must contain positive integers'], 400);
}

$placeholders = implode(',', array_fill(0, count($ids), '?'));
$sql = "UPDATE submissions
           SET processed     = 1,
               processed_utc = UTC_TIMESTAMP(),
               processed_by  = ?,
               notes         = ?
         WHERE id IN ($placeholders)";

$params = array_merge([$by, $note], $ids);
$stmt   = db()->prepare($sql);
$stmt->execute($params);

json_response(['ok' => true, 'updated' => $stmt->rowCount()]);
