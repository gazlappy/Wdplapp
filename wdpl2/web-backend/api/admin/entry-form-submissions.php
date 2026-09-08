<?php
require_once __DIR__ . '/../_entry_forms.php';
ef_https();
ef_method('GET');
ef_admin();
ef_config();
function ef_cursor($name, $fallback) {
	if (!isset($_GET[$name])) return $fallback;
	$value = filter_var($_GET[$name], FILTER_VALIDATE_INT, array('options' => array('min_range' => 0)));
	ef_require($value !== false, 'Invalid collection cursor.');
	return $value;
}
$after = ef_cursor('after', 0);
$through = ef_cursor('through', (int)db()->query('SELECT COALESCE(MAX(sequence_id), 0) FROM entry_form_submissions')->fetchColumn());
ef_require($through >= $after, 'Invalid collection range.');
$query = db()->prepare('SELECT sequence_id, payload_json, received_utc FROM entry_form_submissions WHERE sequence_id > ? AND sequence_id <= ? ORDER BY sequence_id LIMIT 101');
$query->execute(array($after, $through));
$rows = $query->fetchAll();
$more = count($rows) > 100;
if ($more) array_pop($rows);
$submissions = array();
foreach ($rows as $row) {
	$item = json_decode($row['payload_json']);
	$item->clientSubmittedAt = $item->submittedAt;
	$item->submittedAt = str_replace(' ', 'T', $row['received_utc']) . 'Z';
	$submissions[] = $item;
}
$next = $more ? (int)$rows[count($rows) - 1]['sequence_id'] : null;
ef_reply(array('protocol' => 1, 'submissions' => $submissions, 'nextAfter' => $next, 'through' => $through));
