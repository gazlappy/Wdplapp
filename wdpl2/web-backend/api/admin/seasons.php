<?php
// admin/seasons.php — list/create/set-current seasons.
// Schema: league_seasons(season_id CHAR(36), name, starts_on, ends_on, is_active).
// Current season is also mirrored to league_settings('season_id').
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
$me = require_admin();
$pdo = db();

try {
    $pdo->exec(
        'CREATE TABLE IF NOT EXISTS league_seasons (
            season_id  CHAR(36) NOT NULL PRIMARY KEY,
            name       VARCHAR(120) NOT NULL,
            starts_on  DATE NULL,
            ends_on    DATE NULL,
            is_active  TINYINT(1) NOT NULL DEFAULT 0,
            created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
         ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
    $pdo->exec(
        "CREATE TABLE IF NOT EXISTS league_settings (
            setting_key   VARCHAR(64)  NOT NULL PRIMARY KEY,
            setting_value VARCHAR(255) NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
} catch (Exception $e) {}

if ($_SERVER['REQUEST_METHOD'] === 'GET') {
    $rows = $pdo->query('SELECT * FROM league_seasons ORDER BY starts_on DESC, name')->fetchAll();
    $cur  = $pdo->query("SELECT setting_value FROM league_settings WHERE setting_key='season_id'")->fetch();
    $currentId = $cur ? $cur['setting_value'] : null;
    // Fallback: if no league_settings row, use is_active=1 (and self-heal by writing it).
    if (!$currentId) {
        foreach ($rows as $r) {
            if ((int)$r['is_active'] === 1) { $currentId = $r['season_id']; break; }
        }
        if (!$currentId && count($rows) > 0) {
            // last resort: most recent season by start date
            $currentId = $rows[0]['season_id'];
        }
        if ($currentId) {
            try {
                $pdo->prepare(
                    "INSERT INTO league_settings (setting_key, setting_value) VALUES ('season_id', :v)
                     ON DUPLICATE KEY UPDATE setting_value = VALUES(setting_value)")
                    ->execute(array(':v' => $currentId));
            } catch (Exception $e) {}
        }
    }
    json_response(array('items' => $rows, 'current_id' => $currentId));
}

require_post();
$body = read_json_body();
$action = trim((string)(isset($body['action']) ? $body['action'] : ''));

function _seasons_guid() {
    $b = function_exists('random_bytes') ? random_bytes(16) : openssl_random_pseudo_bytes(16);
    $b[6] = chr((ord($b[6]) & 0x0f) | 0x40);
    $b[8] = chr((ord($b[8]) & 0x3f) | 0x80);
    $h = bin2hex($b);
    return substr($h,0,8).'-'.substr($h,8,4).'-'.substr($h,12,4).'-'.substr($h,16,4).'-'.substr($h,20,12);
}

if ($action === 'create') {
    $name = trim((string)(isset($body['name']) ? $body['name'] : ''));
    if ($name === '') json_response(array('error' => 'name required'), 400);
    $sid  = isset($body['season_id']) && $body['season_id'] !== '' ? (string)$body['season_id'] : _seasons_guid();
    $s    = isset($body['starts_on']) ? ($body['starts_on'] ?: null) : null;
    $e    = isset($body['ends_on'])   ? ($body['ends_on']   ?: null) : null;
    $pdo->prepare('INSERT INTO league_seasons (season_id, name, starts_on, ends_on) VALUES (:i,:n,:s,:e)')
        ->execute(array(':i'=>$sid, ':n'=>$name, ':s'=>$s, ':e'=>$e));
    audit_log($me, 'season.create', $sid, array('name' => $name));
    json_response(array('ok' => true, 'season_id' => $sid));
}

if ($action === 'update') {
    $sid = trim((string)(isset($body['season_id']) ? $body['season_id'] : ''));
    if ($sid === '') json_response(array('error' => 'season_id required'), 400);
    $cur = $pdo->prepare('SELECT * FROM league_seasons WHERE season_id = :i'); $cur->execute(array(':i'=>$sid));
    $row = $cur->fetch(); if (!$row) json_response(array('error' => 'not found'), 404);
    $name = isset($body['name'])      ? (string)$body['name']      : $row['name'];
    $s    = isset($body['starts_on']) ? ($body['starts_on'] ?: null) : $row['starts_on'];
    $e    = isset($body['ends_on'])   ? ($body['ends_on']   ?: null) : $row['ends_on'];
    $pdo->prepare('UPDATE league_seasons SET name=:n, starts_on=:s, ends_on=:e WHERE season_id=:i')
        ->execute(array(':n'=>$name, ':s'=>$s, ':e'=>$e, ':i'=>$sid));
    audit_log($me, 'season.update', $sid);
    json_response(array('ok' => true));
}

if ($action === 'set_current') {
    $sid = trim((string)(isset($body['season_id']) ? $body['season_id'] : ''));
    if ($sid === '') json_response(array('error' => 'season_id required'), 400);
    $pdo->exec('UPDATE league_seasons SET is_active = 0');
    $pdo->prepare('UPDATE league_seasons SET is_active = 1 WHERE season_id = :i')->execute(array(':i' => $sid));
    $pdo->prepare(
        "INSERT INTO league_settings (setting_key, setting_value) VALUES ('season_id', :v)
         ON DUPLICATE KEY UPDATE setting_value = VALUES(setting_value)")
       ->execute(array(':v' => $sid));
    audit_log($me, 'season.set_current', $sid);
    json_response(array('ok' => true));
}

if ($action === 'delete') {
    require_admin('superadmin');
    $sid = trim((string)(isset($body['season_id']) ? $body['season_id'] : ''));
    if ($sid === '') json_response(array('error' => 'season_id required'), 400);
    $pdo->prepare('DELETE FROM league_seasons WHERE season_id = :i')->execute(array(':i' => $sid));
    audit_log($me, 'season.delete', $sid);
    json_response(array('ok' => true));
}

json_response(array('error' => 'unknown action'), 400);
