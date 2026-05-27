<?php
// admin/diag.php — diagnostic. Lists tables + row counts so we can see what's missing.
// Auth: admin login (session cookie / bearer) or HTTP Basic auth (MAUI app).
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
require_admin();

$want = array(
    'submissions',
    'captain_tokens',
    'captains',
    'captain_sessions',
    'league_teams',
    'league_players',
    'league_fixtures',
);

$out = array('database' => DB_NAME, 'tables' => array());
foreach ($want as $t) {
    $info = array('exists' => false, 'rows' => null, 'columns' => null, 'error' => null);
    try {
        $stmt = db()->query('SELECT COUNT(*) AS n FROM `' . $t . '`');
        $row = $stmt->fetch();
        $info['exists'] = true;
        $info['rows']   = (int)$row['n'];
        $cols = db()->query('SHOW COLUMNS FROM `' . $t . '`')->fetchAll();
        $info['columns'] = array_map(function ($c) { return $c['Field']; }, $cols);
    } catch (Exception $e) {
        $info['error'] = $e->getMessage();
    }
    $out['tables'][$t] = $info;
}

json_response($out);
