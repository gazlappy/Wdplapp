<?php
// admin/export.php — GET. Streams a JSON snapshot of WDPL tables.
// Query: ?download=1 for Content-Disposition attachment.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
$me = require_admin();

$tables = array(
    'league_teams', 'league_players', 'league_fixtures', 'league_settings',
    'league_frame_results', 'live_scorecards',
    'captains', 'captain_messages', 'captain_message_reads',
    'submissions', 'admin_users', 'admin_audit',
);

$out = array('exported_utc' => gmdate('c'), 'by' => $me['username'], 'tables' => array());
foreach ($tables as $t) {
    try {
        $rows = db()->query('SELECT * FROM `' . $t . '`')->fetchAll();
        // Strip password hashes from the dump.
        if ($t === 'admin_users') {
            foreach ($rows as &$r) { unset($r['password_hash']); }
        }
        $out['tables'][$t] = $rows;
    } catch (Exception $e) {
        $out['tables'][$t] = array('__error' => $e->getMessage());
    }
}
audit_log($me, 'export.download', null, array('tables' => count($tables)));

if (!empty($_GET['download'])) {
    if (!headers_sent()) {
        header('Content-Type: application/json; charset=utf-8');
        header('Content-Disposition: attachment; filename="wdpl-export-' . gmdate('Ymd-His') . '.json"');
    }
    echo json_encode($out);
    exit;
}
json_response($out);
