<?php
// _captain.php — captain login session helpers. PHP 5.6 compatible.
require_once __DIR__ . '/_db.php';

define('CAPTAIN_COOKIE',  'wdpl_cap');
define('CAPTAIN_TTL_HRS', 24 * 14); // 2 weeks

function captain_random_token() {
    // 32 random bytes -> 64 hex chars.
    if (function_exists('random_bytes')) {
        return bin2hex(random_bytes(32));
    }
    return bin2hex(openssl_random_pseudo_bytes(32));
}

function captain_login($team_id) {
    $token = captain_random_token();
    $exp   = gmdate('Y-m-d H:i:s', time() + CAPTAIN_TTL_HRS * 3600);
    db()->prepare(
        'INSERT INTO captain_sessions (token, team_id, expires_utc)
         VALUES (:t, :tid, :exp)')
        ->execute(array(':t' => $token, ':tid' => $team_id, ':exp' => $exp));

    // Cookie: path /, http-only, secure when on https.
    $secure = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off');
    setcookie(CAPTAIN_COOKIE, $token, time() + CAPTAIN_TTL_HRS * 3600, '/', '', $secure, true);
    return $token;
}

function captain_logout() {
    $t = captain_current_token();
    if ($t) {
        db()->prepare('DELETE FROM captain_sessions WHERE token = :t')
            ->execute(array(':t' => $t));
    }
    setcookie(CAPTAIN_COOKIE, '', time() - 3600, '/');
}

function captain_current_token() {
    // Prefer the cookie, but fall back to an Authorization: Bearer <token>
    // header — useful when third-party / cross-path cookies are blocked or
    // when the host strips Set-Cookie on the login response.
    if (!empty($_COOKIE[CAPTAIN_COOKIE])) return $_COOKIE[CAPTAIN_COOKIE];
    $hdr = '';
    if (!empty($_SERVER['HTTP_AUTHORIZATION']))            $hdr = $_SERVER['HTTP_AUTHORIZATION'];
    else if (!empty($_SERVER['REDIRECT_HTTP_AUTHORIZATION'])) $hdr = $_SERVER['REDIRECT_HTTP_AUTHORIZATION'];
    else if (function_exists('getallheaders')) {
        $h = getallheaders();
        if (is_array($h)) {
            foreach ($h as $k => $v) {
                if (strcasecmp($k, 'Authorization') === 0) { $hdr = $v; break; }
            }
        }
    }
    if ($hdr && stripos($hdr, 'Bearer ') === 0) {
        return trim(substr($hdr, 7));
    }
    if (!empty($_SERVER['HTTP_X_CAPTAIN_TOKEN'])) return $_SERVER['HTTP_X_CAPTAIN_TOKEN'];
    return null;
}

function captain_current() {
    $t = captain_current_token();
    if (!$t) return null;
    $stmt = db()->prepare(
        'SELECT c.team_id, c.team_name, c.username, c.display_name, c.division_id, c.division_name,
                s.expires_utc
           FROM captain_sessions s
           JOIN captains c ON c.team_id = s.team_id
          WHERE s.token = :t AND c.enabled = 1 AND s.expires_utc > UTC_TIMESTAMP()
          LIMIT 1');
    $stmt->execute(array(':t' => $t));
    $row = $stmt->fetch();
    return $row ? $row : null;
}

function require_captain() {
    $c = captain_current();
    if (!$c) {
        json_response(array('error' => 'not logged in'), 401);
    }
    return $c;
}
