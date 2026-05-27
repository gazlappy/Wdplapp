<?php
// admin/audit.php — GET admin audit log.
// Query: ?limit=200&action=...&actor=...&q=...
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
require_admin();
admin_ensure_audit_schema();

$limit = isset($_GET['limit']) ? max(1, min(1000, (int)$_GET['limit'])) : 200;
$where = array(); $args = array();
if (!empty($_GET['action'])) { $where[] = 'action LIKE :ac'; $args[':ac'] = '%' . $_GET['action'] . '%'; }
if (!empty($_GET['actor']))  { $where[] = 'actor_name LIKE :an'; $args[':an'] = '%' . $_GET['actor'] . '%'; }
if (!empty($_GET['q']))      { $where[] = '(target LIKE :q OR details LIKE :q)'; $args[':q'] = '%' . $_GET['q'] . '%'; }
$sql = 'SELECT audit_id, ts_utc, actor_name, action, target, details, ip
          FROM admin_audit'
     . ($where ? (' WHERE ' . implode(' AND ', $where)) : '')
     . ' ORDER BY audit_id DESC LIMIT ' . $limit;
$st = db()->prepare($sql);
$st->execute($args);
json_response(array('items' => $st->fetchAll()));
