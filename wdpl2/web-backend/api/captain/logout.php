<?php
// captain/logout.php — POST clears cookie.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_captain.php';
require_post();
captain_logout();
json_response(array('ok' => true));
