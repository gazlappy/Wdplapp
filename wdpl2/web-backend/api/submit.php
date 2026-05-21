<?php
// submit.php — public endpoint. Accepts a submission from the website
// and stores it for an admin to review/apply in the MAUI app.
// PHP 5.6 compatible.
require __DIR__ . '/_db.php';
require_post();

$body = read_json_body();

$type        = trim((string)(isset($body['type'])         ? $body['type']         : ''));
$seasonId    = trim((string)(isset($body['seasonId'])     ? $body['seasonId']     : ''));
$referenceId = trim((string)(isset($body['referenceId'])  ? $body['referenceId']  : ''));
$payload     = isset($body['payload'])   ? $body['payload']   : null;
$token       = trim((string)(isset($body['token'])        ? $body['token']        : ''));
$submitter   = substr(trim((string)(isset($body['submitter']) ? $body['submitter'] : '')), 0, 120);

// If payload came in as a string (form-encoded), try to JSON-decode it.
if (is_string($payload)) {
    $decoded = json_decode($payload, true);
    if (is_array($decoded)) $payload = $decoded;
}

$allowed = ['match_result', 'availability', 'entry', 'generic'];
if (!in_array($type, $allowed, true) || $payload === null) {
    json_response(['error' => 'invalid type or payload'], 400);
}

// Require a captain token for match-result submissions.
if ($type === 'match_result') {
    if ($token === '') {
        json_response(['error' => 'token required'], 401);
    }
    $stmt = db()->prepare(
        'SELECT captain_name FROM captain_tokens
          WHERE token = :t AND enabled = 1 LIMIT 1');
    $stmt->execute([':t' => $token]);
    $row = $stmt->fetch();
    if (!$row) {
        json_response(['error' => 'invalid token'], 401);
    }
    db()->prepare('UPDATE captain_tokens SET last_used = UTC_TIMESTAMP() WHERE token = :t')
        ->execute([':t' => $token]);
    if ($submitter === '') $submitter = $row['captain_name'];
}

$ins = db()->prepare(
    'INSERT INTO submissions
        (type, season_id, reference_id, payload_json, submitter, submitter_ip)
     VALUES (:type, :sid, :rid, :payload, :submitter, :ip)');

$ins->execute([
    ':type'      => $type,
    ':sid'       => $seasonId    !== '' ? $seasonId    : null,
    ':rid'       => $referenceId !== '' ? $referenceId : null,
    ':payload'   => json_encode($payload, JSON_UNESCAPED_SLASHES),
    ':submitter' => $submitter   !== '' ? $submitter   : null,
    ':ip'        => isset($_SERVER['REMOTE_ADDR']) ? $_SERVER['REMOTE_ADDR'] : null,
]);

json_response(['ok' => true, 'id' => (int)db()->lastInsertId()], 201);
