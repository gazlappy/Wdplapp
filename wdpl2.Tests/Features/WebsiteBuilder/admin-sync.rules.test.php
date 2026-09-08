<?php
require __DIR__ . '/../../../wdpl2/web-backend/api/_admin_sync_rules.php';
function check($value) { if (!$value) throw new RuntimeException('Assertion failed'); }
function rejects($action) { try { $action(); } catch (InvalidArgumentException $e) { return; } throw new RuntimeException('Invalid input accepted'); }
check(admin_sync_integer('0', 'cursor') === 0);
check(admin_sync_integer(42, 'revision') === 42);
foreach (array(-1, 1.5, true, '01', '1e2', '999999999999999999999999', array()) as $value) rejects(function () use ($value) { admin_sync_integer($value, 'revision'); });
admin_sync_identity('scorecard', 'fixture-123');
admin_sync_identity('entry_review', 'form-123:entry-456');
rejects(function () { admin_sync_identity('team', '123'); });
rejects(function () { admin_sync_identity('scorecard', "<script>"); });
check(admin_sync_request_id('AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA') === 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
rejects(function () { admin_sync_request_id('not-a-uuid'); });
echo "Admin sync rules passed\n";
