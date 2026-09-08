<?php
// Pure protocol validation shared by the synchronization endpoints and tests.
function admin_sync_integer($value, $name) {
	if (!(is_int($value) || (is_string($value) && preg_match('/^(0|[1-9][0-9]*)$/D', $value)))) {
		throw new InvalidArgumentException($name . ' must be a non-negative integer.');
	}
	$number = filter_var($value, FILTER_VALIDATE_INT, array('options' => array('min_range' => 0)));
	if ($number === false) throw new InvalidArgumentException($name . ' is out of range.');
	return $number;
}

function admin_sync_identity($kind, $id) {
	if (!in_array($kind, array('scorecard', 'entry_review'), true)) throw new InvalidArgumentException('Unsupported synchronization record.');
	if (!is_string($id) || !preg_match('/^[a-zA-Z0-9_.:-]{1,160}$/D', $id)) throw new InvalidArgumentException('Invalid synchronization identity.');
}

function admin_sync_request_id($id) {
	if (!is_string($id) || !preg_match('/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/iD', $id)) {
		throw new InvalidArgumentException('A UUID request_id is required for retry safety.');
	}
	return strtolower($id);
}
