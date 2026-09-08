<?php
require_once __DIR__ . '/../_entry_forms.php';
ef_https();
ef_method('POST');
ef_admin();
$data = ef_definitions(ef_body(1048576));
ef_ensure_schema();
$pdo = db();
$pdo->beginTransaction();
try {
	$pdo->query('SELECT singleton_id FROM entry_form_config WHERE singleton_id = 1 FOR UPDATE')->fetch();
	$pdo->prepare('UPDATE entry_form_config SET origin = ?, time_zone = ? WHERE singleton_id = 1')->execute(array($data->origin, $data->timeZone));
	// Omitted/unpublished forms close; historical definitions and entries are never deleted.
	$pdo->exec('UPDATE entry_form_definitions SET active = 0');
	$save = $pdo->prepare('INSERT INTO entry_form_definitions (form_id, active, definition_json) VALUES (?, 1, ?) ON DUPLICATE KEY UPDATE active = 1, definition_json = VALUES(definition_json)');
	foreach ($data->forms as $form) {
		$save->execute(array($form->id, json_encode($form, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE)));
	}
	$pdo->commit();
} catch (Exception $ex) {
	if ($pdo->inTransaction()) $pdo->rollBack();
	throw $ex;
}
ef_reply(array('protocol' => 1, 'published' => true, 'count' => count($data->forms)));
