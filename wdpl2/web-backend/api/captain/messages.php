<?php
// captain/messages.php — messaging inbox.
// GET            → list inbox (received + broadcasts) with from-name + read status.
// GET ?sent=1    → list sent messages.
// GET ?recipients=1 → list other captains the user can send to.
// POST {action:'send', to_team_id|null, subject, body} → send (null = broadcast NOT allowed for captains).
// POST {action:'mark_read', message_id}              → mark a single message read.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_captain.php';

$c = require_captain();
$team_id = $c['team_id'];

if ($_SERVER['REQUEST_METHOD'] === 'GET') {

    if (!empty($_GET['recipients'])) {
        // Other captains in the league (any team that has an enabled captain login).
        try {
            $stmt = db()->prepare(
                'SELECT team_id, team_name, division_name
                   FROM captains
                  WHERE enabled = 1 AND team_id <> :t
                  ORDER BY division_name, team_name');
            $stmt->execute(array(':t' => $team_id));
            json_response(array('recipients' => $stmt->fetchAll()));
        } catch (Exception $e) {
            json_response(array('recipients' => array()));
        }
    }

    if (!empty($_GET['sent'])) {
        try {
            $stmt = db()->prepare(
                'SELECT m.message_id, m.to_team_id, COALESCE(c.team_name, "(broadcast)") AS to_name,
                        m.subject, m.body, m.sent_utc
                   FROM captain_messages m
              LEFT JOIN captains c ON c.team_id = m.to_team_id
                  WHERE m.from_team_id = :t
                  ORDER BY m.sent_utc DESC
                  LIMIT 100');
            $stmt->execute(array(':t' => $team_id));
            json_response(array('sent' => $stmt->fetchAll()));
        } catch (Exception $e) {
            json_response(array('sent' => array()));
        }
    }

    // Inbox: messages directly to this team OR broadcasts (to_team_id IS NULL).
    try {
        $stmt = db()->prepare(
            'SELECT m.message_id, m.from_team_id,
                    COALESCE(c.team_name, "Admin") AS from_name,
                    m.to_team_id, m.subject, m.body, m.sent_utc,
                    (r.message_id IS NOT NULL) AS is_read
               FROM captain_messages m
          LEFT JOIN captains c ON c.team_id = m.from_team_id
          LEFT JOIN captain_message_reads r ON r.message_id = m.message_id AND r.team_id = :rt
              WHERE m.to_team_id = :t OR m.to_team_id IS NULL
              ORDER BY m.sent_utc DESC
              LIMIT 100');
        $stmt->execute(array(':t' => $team_id, ':rt' => $team_id));
        $rows = $stmt->fetchAll();
        // Cast is_read to bool for JSON clarity.
        foreach ($rows as &$r) { $r['is_read'] = (bool)$r['is_read']; }
        json_response(array('inbox' => $rows));
    } catch (Exception $e) {
        json_response(array('inbox' => array()));
    }
}

require_post();
$body = read_json_body();
$action = trim((string)(isset($body['action']) ? $body['action'] : ''));

if ($action === 'send') {
    $to      = isset($body['to_team_id']) ? trim((string)$body['to_team_id']) : '';
    $subject = trim((string)(isset($body['subject']) ? $body['subject'] : ''));
    $msg     = trim((string)(isset($body['body']) ? $body['body'] : ''));
    if ($to === '' || $subject === '' || $msg === '') {
        json_response(array('error' => 'to_team_id, subject and body required'), 400);
    }
    // Captains cannot broadcast - validate recipient is a real captain.
    $r = db()->prepare('SELECT team_id FROM captains WHERE team_id = :t AND enabled = 1 LIMIT 1');
    $r->execute(array(':t' => $to));
    if (!$r->fetch()) {
        json_response(array('error' => 'unknown recipient'), 404);
    }
    db()->prepare(
        'INSERT INTO captain_messages (from_team_id, to_team_id, subject, body)
         VALUES (:f, :t, :s, :b)')
        ->execute(array(':f' => $team_id, ':t' => $to, ':s' => $subject, ':b' => $msg));
    json_response(array('ok' => true, 'message_id' => db()->lastInsertId()));
}

if ($action === 'mark_read') {
    $mid = (int)(isset($body['message_id']) ? $body['message_id'] : 0);
    if ($mid <= 0) {
        json_response(array('error' => 'message_id required'), 400);
    }
    // Verify the message is actually visible to this captain (direct or broadcast).
    $m = db()->prepare(
        'SELECT to_team_id FROM captain_messages WHERE message_id = :m LIMIT 1');
    $m->execute(array(':m' => $mid));
    $row = $m->fetch();
    if (!$row || ($row['to_team_id'] !== null && $row['to_team_id'] !== $team_id)) {
        json_response(array('error' => 'message not found'), 404);
    }
    db()->prepare(
        'INSERT IGNORE INTO captain_message_reads (message_id, team_id) VALUES (:m, :t)')
        ->execute(array(':m' => $mid, ':t' => $team_id));
    json_response(array('ok' => true));
}

json_response(array('error' => 'unknown action: ' . $action), 400);
