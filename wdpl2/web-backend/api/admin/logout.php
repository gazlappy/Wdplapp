<?php
// admin/logout.php — POST clears the admin session cookie + DB row.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
require_post();
admin_logout();
json_response(array('ok' => true));
