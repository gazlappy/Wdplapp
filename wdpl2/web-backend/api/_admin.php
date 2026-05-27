<?php
// _admin.php — admin session + login helpers. PHP 5.6 compatible.
// Two auth paths are supported so the existing MAUI desktop app keeps working:
//   1) Cookie / bearer session token (admin_sessions) - used by the web admin SPA.
//   2) HTTP Basic auth (Authorization: Basic ...) - used by the MAUI Web Inbox.
//      The username/password are looked up in the admin_users table.
// If the admin_users table doesn't exist yet, it is auto-created. If it has
// no rows, a one-off bootstrap login is allowed (see admin_login_check()).
require_once __DIR__ . '/_db.php';

define('ADMIN_COOKIE',  'wdpl_admin');
define('ADMIN_TTL_HRS', 12);

function admin_ensure_schema() {
    $pdo = db();
    $pdo->exec(
        'CREATE TABLE IF NOT EXISTS admin_users (
           user_id       CHAR(36)     NOT NULL PRIMARY KEY,
           username      VARCHAR(60)  NOT NULL UNIQUE,
           password_hash VARCHAR(255) NOT NULL,
           display_name  VARCHAR(120) NULL,
           email         VARCHAR(160) NULL,
           role          VARCHAR(20)  NOT NULL DEFAULT "admin",
           enabled       TINYINT(1)   NOT NULL DEFAULT 1,
           created_utc   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
           last_login    DATETIME     NULL
         ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
    $pdo->exec(
        'CREATE TABLE IF NOT EXISTS admin_sessions (
           token        CHAR(64)  NOT NULL PRIMARY KEY,
           user_id      CHAR(36)  NOT NULL,
           created_utc  DATETIME  NOT NULL DEFAULT CURRENT_TIMESTAMP,
           expires_utc  DATETIME  NOT NULL,
           INDEX ix_user (user_id),
           INDEX ix_exp  (expires_utc)
         ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
}

function admin_random_token() {
    if (function_exists('random_bytes')) return bin2hex(random_bytes(32));
    return bin2hex(openssl_random_pseudo_bytes(32));
}

function admin_guid() {
    $b = function_exists('random_bytes') ? random_bytes(16) : openssl_random_pseudo_bytes(16);
    $b[6] = chr((ord($b[6]) & 0x0f) | 0x40);
    $b[8] = chr((ord($b[8]) & 0x3f) | 0x80);
    $h = bin2hex($b);
    return substr($h,0,8).'-'.substr($h,8,4).'-'.substr($h,12,4).'-'.substr($h,16,4).'-'.substr($h,20,12);
}

function admin_users_count() {
    admin_ensure_schema();
    $r = db()->query('SELECT COUNT(*) AS n FROM admin_users')->fetch();
    return (int)($r ? $r['n'] : 0);
}

function admin_find_user_by_username($u) {
    admin_ensure_schema();
    $s = db()->prepare('SELECT * FROM admin_users WHERE username = :u LIMIT 1');
    $s->execute(array(':u' => $u));
    $r = $s->fetch();
    return $r ?: null;
}

function admin_find_user_by_id($id) {
    admin_ensure_schema();
    $s = db()->prepare('SELECT * FROM admin_users WHERE user_id = :i LIMIT 1');
    $s->execute(array(':i' => $id));
    $r = $s->fetch();
    return $r ?: null;
}

function admin_create_user($username, $password, $display_name = null, $email = null, $role = 'admin', $enabled = 1) {
    admin_ensure_schema();
    $hash = password_hash($password, PASSWORD_DEFAULT);
    $id = admin_guid();
    db()->prepare(
        'INSERT INTO admin_users
            (user_id, username, password_hash, display_name, email, role, enabled)
         VALUES (:i, :u, :h, :d, :e, :r, :en)')
       ->execute(array(
            ':i' => $id, ':u' => $username, ':h' => $hash,
            ':d' => $display_name, ':e' => $email, ':r' => $role, ':en' => $enabled ? 1 : 0));
    return $id;
}

function admin_issue_session($user_id) {
    admin_ensure_schema();
    $token = admin_random_token();
    $exp   = gmdate('Y-m-d H:i:s', time() + ADMIN_TTL_HRS * 3600);
    db()->prepare('INSERT INTO admin_sessions (token, user_id, expires_utc) VALUES (:t,:u,:e)')
        ->execute(array(':t' => $token, ':u' => $user_id, ':e' => $exp));
    $secure = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off');
    setcookie(ADMIN_COOKIE, $token, time() + ADMIN_TTL_HRS * 3600, '/', '', $secure, true);
    db()->prepare('UPDATE admin_users SET last_login = UTC_TIMESTAMP() WHERE user_id = :u')
        ->execute(array(':u' => $user_id));
    return $token;
}

function admin_current_token() {
    if (!empty($_COOKIE[ADMIN_COOKIE])) return $_COOKIE[ADMIN_COOKIE];
    $hdr = '';
    if (!empty($_SERVER['HTTP_AUTHORIZATION']))             $hdr = $_SERVER['HTTP_AUTHORIZATION'];
    else if (!empty($_SERVER['REDIRECT_HTTP_AUTHORIZATION'])) $hdr = $_SERVER['REDIRECT_HTTP_AUTHORIZATION'];
    else if (function_exists('getallheaders')) {
        $h = getallheaders();
        if (is_array($h)) foreach ($h as $k => $v) {
            if (strcasecmp($k, 'Authorization') === 0) { $hdr = $v; break; }
        }
    }
    if ($hdr && stripos($hdr, 'Bearer ') === 0) return trim(substr($hdr, 7));
    if (!empty($_SERVER['HTTP_X_ADMIN_TOKEN'])) return $_SERVER['HTTP_X_ADMIN_TOKEN'];
    return null;
}

function admin_logout() {
    $t = admin_current_token();
    if ($t) {
        db()->prepare('DELETE FROM admin_sessions WHERE token = :t')->execute(array(':t' => $t));
    }
    setcookie(ADMIN_COOKIE, '', time() - 3600, '/');
}

/**
 * Try every supported auth source and return the active admin user row
 * (or null if none match).
 */
function admin_current() {
    admin_ensure_schema();

    // 1) Session token (cookie / bearer).
    $tok = admin_current_token();
    if ($tok) {
        $s = db()->prepare(
            'SELECT u.*
               FROM admin_sessions s
               JOIN admin_users u ON u.user_id = s.user_id
              WHERE s.token = :t
                AND u.enabled = 1
                AND s.expires_utc > UTC_TIMESTAMP()
              LIMIT 1');
        $s->execute(array(':t' => $tok));
        $row = $s->fetch();
        if ($row) return $row;
    }

    // 2) HTTP Basic auth (used by the MAUI desktop app).
    $bu = null; $bp = null;
    if (!empty($_SERVER['PHP_AUTH_USER'])) {
        $bu = $_SERVER['PHP_AUTH_USER'];
        $bp = isset($_SERVER['PHP_AUTH_PW']) ? $_SERVER['PHP_AUTH_PW'] : '';
    } else {
        // PHP-CGI / FastCGI on cPanel doesn't populate PHP_AUTH_USER; the
        // Authorization header comes through verbatim instead.
        $hdr = '';
        if (!empty($_SERVER['HTTP_AUTHORIZATION']))               $hdr = $_SERVER['HTTP_AUTHORIZATION'];
        else if (!empty($_SERVER['REDIRECT_HTTP_AUTHORIZATION'])) $hdr = $_SERVER['REDIRECT_HTTP_AUTHORIZATION'];
        else if (function_exists('getallheaders')) {
            $h = getallheaders();
            if (is_array($h)) foreach ($h as $k => $v) {
                if (strcasecmp($k, 'Authorization') === 0) { $hdr = $v; break; }
            }
        }
        if ($hdr && stripos($hdr, 'Basic ') === 0) {
            $decoded = base64_decode(substr($hdr, 6), true);
            if ($decoded !== false && strpos($decoded, ':') !== false) {
                list($bu, $bp) = explode(':', $decoded, 2);
            }
        }
    }
    if ($bu !== null && $bu !== '') {
        $row = admin_find_user_by_username($bu);
        if ($row && (int)$row['enabled'] === 1 && password_verify((string)$bp, $row['password_hash'])) {
            return $row;
        }
    }

    return null;
}

function admin_role_rank($role) {
    $r = strtolower((string)$role);
    if ($r === 'superadmin') return 3;
    if ($r === 'admin')      return 2;
    if ($r === 'readonly')   return 1;
    return 2; // legacy rows with no role -> treat as admin
}

function require_admin($minRole = null) {
    $a = admin_current();
    if (!$a) {
        // Don't ask the browser for Basic-auth here - the SPA shows its own form.
        json_response(array('error' => 'unauthenticated'), 401);
    }
    if ($minRole !== null && admin_role_rank($a['role']) < admin_role_rank($minRole)) {
        json_response(array('error' => 'forbidden', 'required_role' => $minRole, 'your_role' => $a['role']), 403);
    }
    return $a;
}

function admin_ensure_audit_schema() {
    db()->exec(
        'CREATE TABLE IF NOT EXISTS admin_audit (
           audit_id    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
           ts_utc      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
           actor_id    CHAR(36)     NULL,
           actor_name  VARCHAR(120) NULL,
           action      VARCHAR(80)  NOT NULL,
           target      VARCHAR(200) NULL,
           details     TEXT         NULL,
           ip          VARCHAR(64)  NULL,
           INDEX ix_ts (ts_utc),
           INDEX ix_action (action)
         ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
}

function audit_log($actor, $action, $target = null, $details = null) {
    try {
        admin_ensure_audit_schema();
        $ip = isset($_SERVER['REMOTE_ADDR']) ? substr($_SERVER['REMOTE_ADDR'], 0, 64) : null;
        db()->prepare(
            'INSERT INTO admin_audit (actor_id, actor_name, action, target, details, ip)
             VALUES (:aid, :an, :ac, :tg, :de, :ip)')
           ->execute(array(
               ':aid' => is_array($actor) ? $actor['user_id'] : null,
               ':an'  => is_array($actor) ? ($actor['display_name'] ?: $actor['username']) : (string)$actor,
               ':ac'  => substr((string)$action, 0, 80),
               ':tg'  => $target !== null ? substr((string)$target, 0, 200) : null,
               ':de'  => $details !== null ? (is_string($details) ? $details : json_encode($details)) : null,
               ':ip'  => $ip,
           ));
    } catch (Exception $e) { /* never fail the request because of auditing */ }
}

/**
 * Validate a username+password pair. Returns the user row on success, null on
 * failure. Used by login.php.
 */
function admin_login_check($username, $password) {
    admin_ensure_schema();
    $row = admin_find_user_by_username($username);
    if (!$row || (int)$row['enabled'] !== 1) return null;
    if (!password_verify($password, $row['password_hash'])) return null;
    return $row;
}
