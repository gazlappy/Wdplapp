<?php
require __DIR__ . '/../../../wdpl2/web-backend/api/_admin_entry_review_rules.php';
function rejects($status, $notes) {
	try { admin_entry_review_values($status, $notes); }
	catch (InvalidArgumentException $e) { return; }
	throw new RuntimeException('Invalid entry review accepted');
}
foreach (array('pending', 'confirmed', 'rejected') as $status) {
	$review = admin_entry_review_values($status, 'Original notes');
	$row = array('sequence_id' => '7', 'form_id' => 'form-1', 'client_id' => 'client-1', 'payload_json' => '{"answer":"Original"}');
	$before = $row;
	$payload = admin_entry_review_payload($row, $review);
	if ($payload !== array('submissionSequence' => 7, 'formId' => 'form-1', 'clientId' => 'client-1', 'status' => $status, 'notes' => 'Original notes'))
		throw new RuntimeException('Review identity changed');
	if ($row !== $before || isset($payload['payload_json'])) throw new RuntimeException('Submission answers modified or leaked into metadata');
}
rejects('approved', '');
rejects(null, '');
rejects('pending', null);
rejects('pending', array());
rejects('pending', "\xC3\x28");
rejects('pending', str_repeat("\xC3\xA9", 8001));
admin_entry_review_values('pending', str_repeat("\xC3\xA9", 8000));
echo "Entry review rules passed\n";
