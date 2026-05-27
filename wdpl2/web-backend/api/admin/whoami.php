<?php
// admin/whoami.php — returns the currently-authenticated admin (session or Basic auth).
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';

admin_ensure_schema();
$me = admin_current();
if (!$me) {
    json_response(array(
        'ok'              => false,
        'needs_bootstrap' => admin_users_count() === 0,
    ), 401);
}

json_response(array(
    'ok'              => true,
    'user_id'         => $me['user_id'],
    'username'        => $me['username'],
    'display_name'    => $me['display_name'],
    'role'            => $me['role'],
    'needs_bootstrap' => false,
));
