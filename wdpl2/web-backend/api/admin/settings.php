<?php
// admin/settings.php — read/write league_settings (key/value).
// GET                                -> { items: [{key,value}, ...] }
// POST {action:"set", key, value}    -> upsert one row.
// POST {action:"delete", key}        -> remove one row.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
$me = require_admin();

try {
    db()->exec(
        "CREATE TABLE IF NOT EXISTS league_settings (
            setting_key   VARCHAR(64)  NOT NULL PRIMARY KEY,
            setting_value VARCHAR(255) NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
} catch (Exception $e) {}

if ($_SERVER['REQUEST_METHOD'] === 'GET') {
    $rows = db()->query('SELECT setting_key AS `key`, setting_value AS `value` FROM league_settings ORDER BY setting_key')->fetchAll();
    json_response(array('items' => $rows));
}

require_post();
$body   = read_json_body();
$action = trim((string)(isset($body['action']) ? $body['action'] : 'set'));
$key    = trim((string)(isset($body['key']) ? $body['key'] : ''));
if ($key === '') json_response(array('error' => 'key required'), 400);
if (!preg_match('/^[a-z0-9_\.\-]{1,64}$/i', $key)) json_response(array('error' => 'invalid key'), 400);

if ($action === 'delete') {
    require_admin('superadmin');
    db()->prepare('DELETE FROM league_settings WHERE setting_key = :k')->execute(array(':k' => $key));
    audit_log($me, 'settings.delete', $key);
    json_response(array('ok' => true));
}

$val = isset($body['value']) ? (string)$body['value'] : '';
db()->prepare(
    'INSERT INTO league_settings (setting_key, setting_value) VALUES (:k, :v)
     ON DUPLICATE KEY UPDATE setting_value = VALUES(setting_value)')
   ->execute(array(':k' => $key, ':v' => $val));
audit_log($me, 'settings.set', $key, array('value' => $val));
json_response(array('ok' => true));
