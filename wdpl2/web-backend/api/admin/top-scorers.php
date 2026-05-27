<?php
// admin/top-scorers.php — auth wrapper around public top-scorers logic with audit.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
require_admin();
// Just proxy to the public endpoint logic — keep them DRY.
require __DIR__ . '/../public/_pub.php';
require __DIR__ . '/../public/top-scorers.php';
