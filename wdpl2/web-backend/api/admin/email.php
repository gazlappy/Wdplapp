<?php
// admin/email.php — send mail to captain accounts via PHP mail().
// GET                       -> { recipients:[{team_name, email}, ...] }  (only enabled captains with an email)
// POST {action:"send", to:"all"|team_id, subject, body, reply_to?}
//
// Notes:
//  - cPanel hosts usually have mail() working out of the box; SPF must be set for
//    your domain or messages will be flagged. For higher deliverability switch
//    to SMTP later (PHPMailer / Symfony Mailer).
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
require __DIR__ . '/../_smtp.php';
$me = require_admin();
$pdo = db();

if ($_SERVER['REQUEST_METHOD'] === 'GET') {
    try {
        $rows = $pdo->query(
            "SELECT team_id, team_name, email FROM captains
              WHERE enabled = 1 AND email IS NOT NULL AND email <> ''
              ORDER BY team_name")->fetchAll();
    } catch (Exception $e) { $rows = array(); }
    json_response(array('recipients' => $rows));
}

require_post();
$body = read_json_body();
$action = trim((string)(isset($body['action']) ? $body['action'] : 'send'));
$to     = trim((string)(isset($body['to']) ? $body['to'] : 'all'));
$subj   = trim((string)(isset($body['subject']) ? $body['subject'] : ''));
$msg    = (string)(isset($body['body']) ? $body['body'] : '');
$reply  = trim((string)(isset($body['reply_to']) ? $body['reply_to'] : ''));
if ($subj === '' || $msg === '') json_response(array('error' => 'subject and body required'), 400);

$where = " WHERE enabled = 1 AND email IS NOT NULL AND email <> ''";
$args  = array();
if ($to !== 'all' && $to !== '') { $where .= ' AND team_id = :t'; $args[':t'] = $to; }
$st = $pdo->prepare("SELECT team_name, email FROM captains $where");
$st->execute($args); $rcpts = $st->fetchAll();
if (!count($rcpts)) json_response(array('error' => 'no recipients'), 400);

$fromHost = isset($_SERVER['HTTP_HOST']) ? preg_replace('/[^a-z0-9\.\-]/i', '', $_SERVER['HTTP_HOST']) : 'localhost';
$fromAddr = 'no-reply@' . $fromHost;
$headers  = "From: WDPL Admin <$fromAddr>\r\n";
$headers .= "MIME-Version: 1.0\r\n";
$headers .= "Content-Type: text/plain; charset=UTF-8\r\n";
if ($reply !== '') $headers .= "Reply-To: $reply\r\n";

$useSmtp = smtp_is_configured();
$sent = 0; $failed = array(); $errors = array();
foreach ($rcpts as $r) {
    if ($useSmtp) {
        $res = smtp_send($r['email'], $subj, $msg, $reply !== '' ? $reply : null);
        if ($res['ok']) { $sent++; }
        else { $failed[] = $r['email']; $errors[$r['email']] = $res['error']; }
    } else {
        $ok = @mail($r['email'], $subj, $msg, $headers, "-f$fromAddr");
        if ($ok) $sent++; else $failed[] = $r['email'];
    }
}
audit_log($me, 'email.send', $to, array('subject' => $subj, 'sent' => $sent, 'failed' => count($failed), 'transport' => $useSmtp ? 'smtp' : 'mail'));
json_response(array('ok' => true, 'sent' => $sent, 'failed' => $failed, 'recipients' => count($rcpts), 'transport' => $useSmtp ? 'smtp' : 'mail', 'errors' => $errors));
