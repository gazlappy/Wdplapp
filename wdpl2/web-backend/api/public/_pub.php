<?php
// public/_pub.php — common headers for public read-only endpoints.
require_once __DIR__ . '/../_db.php';

if (!headers_sent()) {
    header('Access-Control-Allow-Origin: *');
    header('Cache-Control: public, max-age=60');
}

function pub_current_season_id() {
    try {
        $r = db()->query("SELECT setting_value FROM league_settings WHERE setting_key='season_id'")->fetch();
        return $r ? $r['setting_value'] : null;
    } catch (Exception $e) { return null; }
}
