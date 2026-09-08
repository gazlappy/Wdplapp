<?php
function admin_entry_review_values($status, $notes) {
	if (!is_string($status) || !in_array($status, array('pending', 'confirmed', 'rejected'), true))
		throw new InvalidArgumentException('Choose pending, confirmed or rejected.');
	if (!is_string($notes) || strlen($notes) > 16000 || !preg_match('//u', $notes))
		throw new InvalidArgumentException('Review notes must be valid UTF-8 and at most 16000 bytes.');
	return array('status' => $status, 'notes' => $notes);
}

// Sequence IDs are scoped by the journal backend ID; form/client IDs remain in every payload.
function admin_entry_review_payload($row, $review) {
	return array('submissionSequence' => (int)$row['sequence_id'], 'formId' => $row['form_id'],
		'clientId' => $row['client_id'], 'status' => $review['status'], 'notes' => $review['notes']);
}
