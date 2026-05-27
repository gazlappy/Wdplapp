<?php
// admin/broadcast.php — POST {subject, body} -> announce to all captains.
// Stores ONE row in captain_messages with to_team_id = NULL (broadcast), from_team_id = NULL (admin).
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
$me = require_admin();
require_post();

$body = read_json_body();
$subject = trim((string)(isset($body['subject']) ? $body['subject'] : ''));
$msg     = trim((string)(isset($body['body'])    ? $body['body']    : ''));
if ($subject === '' || $msg === '') json_response(array('error' => 'subject and body required'), 400);

try {
    db()->exec(
        "CREATE TABLE IF NOT EXISTS captain_messages (
            message_id   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
            from_team_id VARCHAR(64)  NULL,
            to_team_id   VARCHAR(64)  NULL,
            subject      VARCHAR(200) NOT NULL,
            body         TEXT         NOT NULL,
            sent_utc     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
} catch (Exception $e) {}

db()->prepare(
    'INSERT INTO captain_messages (from_team_id, to_team_id, subject, body)
     VALUES (NULL, NULL, :s, :b)')
   ->execute(array(':s' => $subject, ':b' => $msg));
$id = db()->lastInsertId();
audit_log($me, 'broadcast.send', (string)$id, array('subject' => $subject));
json_response(array('ok' => true, 'message_id' => $id));
