<?php
// admin/users.php — manage admin user accounts.
// GET                                       -> list users + needs_bootstrap flag.
// POST {action:"create", username, password, display_name?, email?, role?, enabled?}
// POST {action:"update", user_id, display_name?, email?, role?, enabled?, password?}
// DELETE {user_id}                          -> remove user (cannot delete yourself).
// Auth: an admin session (or HTTP Basic auth recognised by _admin.php) is required
//       UNLESS the admin_users table is empty (bootstrap window, also handled by login.php).
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';

$method = isset($_SERVER['REQUEST_METHOD']) ? $_SERVER['REQUEST_METHOD'] : 'GET';

if ($method === 'GET') {
    admin_ensure_schema();
    // If nobody has signed up yet, let the SPA know so it can show the
    // first-time setup hint without needing auth.
    if (admin_users_count() === 0) {
        json_response(array('items' => array(), 'needs_bootstrap' => true));
    }
    $me = require_admin();
    $rows = db()->query(
        'SELECT user_id, username, display_name, email, role, enabled,
                created_utc, last_login
           FROM admin_users
          ORDER BY username')->fetchAll();
    json_response(array('items' => $rows, 'me' => $me['user_id'], 'needs_bootstrap' => false));
}

$me = require_admin();

if ($method === 'DELETE') {
    $body = read_json_body();
    $uid = trim((string)(isset($body['user_id']) ? $body['user_id'] : ''));
    if ($uid === '') json_response(array('error' => 'user_id required'), 400);
    if ($uid === $me['user_id']) json_response(array('error' => 'you cannot delete the account you are logged in as'), 400);

    db()->prepare('DELETE FROM admin_sessions WHERE user_id = :u')->execute(array(':u' => $uid));
    $d = db()->prepare('DELETE FROM admin_users WHERE user_id = :u');
    $d->execute(array(':u' => $uid));
    json_response(array('ok' => true, 'rows' => $d->rowCount()));
}

require_post();
$body   = read_json_body();
$action = trim((string)(isset($body['action']) ? $body['action'] : ''));

if ($action === 'create') {
    $u = trim((string)(isset($body['username']) ? $body['username'] : ''));
    $p = (string)(isset($body['password']) ? $body['password'] : '');
    if ($u === '' || $p === '') json_response(array('error' => 'username and password required'), 400);
    if (strlen($p) < 6)         json_response(array('error' => 'password must be at least 6 characters'), 400);
    if (admin_find_user_by_username($u)) json_response(array('error' => 'username already exists'), 409);

    $id = admin_create_user($u, $p,
        isset($body['display_name']) ? $body['display_name'] : null,
        isset($body['email'])        ? $body['email']        : null,
        isset($body['role'])         ? $body['role']         : 'admin',
        !empty($body['enabled']) ? 1 : (isset($body['enabled']) ? 0 : 1));
    json_response(array('ok' => true, 'user_id' => $id));
}

if ($action === 'update') {
    $uid = trim((string)(isset($body['user_id']) ? $body['user_id'] : ''));
    if ($uid === '') json_response(array('error' => 'user_id required'), 400);
    $row = admin_find_user_by_id($uid);
    if (!$row) json_response(array('error' => 'user not found'), 404);

    $disp = isset($body['display_name']) ? $body['display_name'] : $row['display_name'];
    $em   = isset($body['email'])        ? $body['email']        : $row['email'];
    $role = isset($body['role'])         ? $body['role']         : $row['role'];
    $en   = isset($body['enabled'])      ? (!empty($body['enabled']) ? 1 : 0) : (int)$row['enabled'];
    // Don't let the logged-in user disable themselves and lock the system.
    if ($uid === $me['user_id'] && $en === 0) {
        json_response(array('error' => 'you cannot disable the account you are logged in as'), 400);
    }

    $hash = $row['password_hash'];
    if (!empty($body['password'])) {
        $p = (string)$body['password'];
        if (strlen($p) < 6) json_response(array('error' => 'password must be at least 6 characters'), 400);
        $hash = password_hash($p, PASSWORD_DEFAULT);
    }

    db()->prepare(
       'UPDATE admin_users
           SET display_name = :d, email = :e, role = :r, enabled = :en, password_hash = :h
         WHERE user_id = :u')
      ->execute(array(':d' => $disp, ':e' => $em, ':r' => $role, ':en' => $en, ':h' => $hash, ':u' => $uid));

    if (!empty($body['password'])) {
        // Force re-login on other devices when the password changes.
        db()->prepare('DELETE FROM admin_sessions WHERE user_id = :u AND token <> :t')
            ->execute(array(':u' => $uid, ':t' => (string)admin_current_token()));
    }

    json_response(array('ok' => true));
}

json_response(array('error' => 'unknown action'), 400);
