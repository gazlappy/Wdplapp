<?php
require_once __DIR__ . '/../_entry_forms.php';
ef_https();
$config = ef_config();
ef_cors($config);
if (isset($_SERVER['REQUEST_METHOD']) && $_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
	http_response_code(204);
	exit;
}
ef_method('POST');
ef_rate_limit();
$submission = ef_submission(ef_body(65536));
$payload = json_encode($submission, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
$hash = hash('sha256', $payload);
$pdo = db();
$pdo->beginTransaction();
try {
	// Serialize acceptance with definition publishing and competing retries.
	$config = ef_config(true);
	ef_cors($config);
	$query = $pdo->prepare('SELECT payload_hash FROM entry_form_submissions WHERE form_id = ? AND client_id = ?');
	$query->execute(array($submission->formId, $submission->id));
	$existing = $query->fetch();
	if ($existing) {
		$pdo->commit();
		if (!hash_equals($existing['payload_hash'], $hash)) ef_reply(array('error' => 'This reference already belongs to a different entry.'), 409);
		// Stored retries are acknowledged even if the form has since closed or changed.
		ef_reply(array('accepted' => true, 'id' => $submission->id));
	}
	$query = $pdo->prepare('SELECT definition_json FROM entry_form_definitions WHERE form_id = ? AND active = 1');
	$query->execute(array($submission->formId));
	$definition = $query->fetchColumn();
	if ($definition === false) {
		$pdo->rollBack();
		ef_reply(array('error' => 'This form is not accepting entries.'), 422);
	}
	$form = json_decode($definition);
	$now = new DateTimeImmutable('now', new DateTimeZone($config['time_zone']));
	ef_validate_values($submission, $form, $now);
	$pdo->prepare('INSERT INTO entry_form_submissions (form_id, client_id, payload_hash, payload_json, received_utc) VALUES (?, ?, ?, ?, UTC_TIMESTAMP())')
		->execute(array($submission->formId, $submission->id, $hash, $payload));
	$pdo->commit();
} catch (Exception $ex) {
	if ($pdo->inTransaction()) $pdo->rollBack();
	throw $ex;
}
ef_reply(array('accepted' => true, 'id' => $submission->id), 201);
