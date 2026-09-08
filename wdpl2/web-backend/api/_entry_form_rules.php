<?php
// Pure validation shared by the hosted endpoints and CLI regression tests.
function ef_require($condition, $message) {
	if (!$condition) throw new InvalidArgumentException($message);
}

function ef_text($value, $max, $required = false) {
	ef_require(is_string($value), 'Expected a text value.');
	$length = preg_match_all('/./us', $value, $unused);
	ef_require($length !== false && $length <= $max, 'Text exceeds the allowed length or is not UTF-8.');
	ef_require(!$required || preg_match('/\S/u', $value), 'A required value is missing.');
	return $value;
}

function ef_date($value) {
	if (!is_string($value) || !preg_match('/^([0-9]{4})-([0-9]{2})-([0-9]{2})$/D', $value, $m)) return false;
	return checkdate((int)$m[2], (int)$m[3], (int)$m[1]);
}

function ef_origin($value) {
	ef_text($value, 255, true);
	ef_require(!preg_match('/[\x00-\x20\x7f]/', $value) && filter_var($value, FILTER_VALIDATE_URL) !== false, 'Invalid website origin.');
	$parts = parse_url($value);
	ef_require(is_array($parts) && isset($parts['scheme'], $parts['host']) && $parts['scheme'] === 'https' &&
		!isset($parts['user']) && !isset($parts['pass']) &&
		!isset($parts['query']) && !isset($parts['fragment']) && !isset($parts['path']), 'Use an HTTPS website origin without a path or credentials.');
	return $value;
}

function ef_definitions($data) {
	ef_require($data instanceof stdClass && isset($data->protocol, $data->origin, $data->timeZone, $data->forms), 'Invalid definition document.');
	ef_require($data->protocol === 1 && $data->timeZone === 'Europe/London', 'Unsupported entry-form protocol or time zone.');
	ef_origin($data->origin);
	ef_require(is_array($data->forms) && count($data->forms) <= 100, 'At most 100 published forms are supported.');
	$ids = array();
	foreach ($data->forms as $form) {
		ef_require($form instanceof stdClass && isset($form->id, $form->title, $form->closed, $form->fields) && property_exists($form, 'closingDate'), 'Invalid form definition.');
		ef_require(is_string($form->id) && preg_match('/^form-[a-f0-9]{32}$/D', $form->id) && !isset($ids[$form->id]), 'Invalid or duplicate form identity.');
		$ids[$form->id] = true;
		ef_text($form->title, 200, true);
		ef_require(is_bool($form->closed) && ($form->closingDate === null || ef_date($form->closingDate)), 'Invalid closing date or status.');
		ef_require(is_array($form->fields) && count($form->fields) > 0 && count($form->fields) <= 100, 'Forms require between 1 and 100 fields.');
		$labels = array();
		foreach ($form->fields as $field) {
			ef_require($field instanceof stdClass && isset($field->label, $field->type, $field->required, $field->options), 'Invalid field definition.');
			ef_text($field->label, 200, true);
			$key = strtolower(trim($field->label));
			ef_require($key !== '' && $key[0] !== '_' && !isset($labels[$key]), 'Duplicate or reserved field label.');
			$labels[$key] = true;
			ef_require(in_array($field->type, array('text', 'textarea', 'email', 'phone', 'number', 'date', 'select', 'checkbox'), true) && is_bool($field->required), 'Unsupported field type or required status.');
			ef_require(is_array($field->options) && count($field->options) <= 100, 'Invalid field options.');
			if ($field->type === 'select') ef_require(count($field->options) > 0, 'Dropdowns require options.');
			foreach ($field->options as $option) ef_text($option, 500, true);
			ef_require(count(array_unique($field->options, SORT_STRING)) === count($field->options), 'Duplicate dropdown option.');
		}
	}
	return $data;
}

function ef_submission($data) {
	ef_require($data instanceof stdClass && isset($data->id, $data->formId, $data->name, $data->values, $data->submittedAt), 'Invalid entry document.');
	ef_require(is_string($data->id) && preg_match('/^[a-zA-Z0-9-]{1,128}$/D', $data->id), 'Invalid submission identity.');
	ef_require(is_string($data->formId) && preg_match('/^form-[a-f0-9]{32}$/D', $data->formId), 'Invalid form identity.');
	ef_text($data->name, 4000);
	ef_require($data->values instanceof stdClass, 'Entry values must be an object.');
	$values = get_object_vars($data->values);
	ef_require(count($values) <= 100, 'Too many entry fields.');
	foreach ($values as $label => $value) {
		ef_text((string)$label, 200, true);
		ef_text($value, 4000);
	}
	ef_require(is_string($data->submittedAt) && preg_match('/^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?Z$/D', $data->submittedAt), 'Invalid client timestamp.');
	ef_require(ef_date(substr($data->submittedAt, 0, 10)) && (int)substr($data->submittedAt, 11, 2) < 24 &&
		(int)substr($data->submittedAt, 14, 2) < 60 && (int)substr($data->submittedAt, 17, 2) < 60, 'Invalid client timestamp.');
	// Canonical payload allows key-order-independent retries, without trusting the client clock.
	ksort($values, SORT_STRING);
	return (object)array('id' => $data->id, 'formId' => $data->formId, 'name' => $data->name,
		'values' => (object)$values, 'submittedAt' => $data->submittedAt);
}

function ef_validate_values($submission, $form, $now) {
	ef_require(!$form->closed && ($form->closingDate === null || $now->format('Y-m-d') <= $form->closingDate), 'This form is closed.');
	$values = get_object_vars($submission->values);
	$expected = array();
	foreach ($form->fields as $field) {
		$expected[] = $field->label;
		ef_require(array_key_exists($field->label, $values), 'An entry field is missing; reload the form.');
		$value = $values[$field->label];
		ef_text($value, $field->type === 'textarea' ? 4000 : 500, $field->required);
		if ($field->type === 'checkbox') {
			ef_require(in_array($value, array('Yes', 'No'), true) && (!$field->required || $value === 'Yes'), 'Required consent is missing or invalid.');
		} elseif ($value !== '') {
			if ($field->type === 'select') ef_require(in_array($value, $field->options, true), 'Invalid dropdown choice.');
			if ($field->type === 'email') ef_require(filter_var($value, FILTER_VALIDATE_EMAIL) !== false, 'Invalid email address.');
			if ($field->type === 'date') ef_require(ef_date($value), 'Invalid date value.');
			if ($field->type === 'number') ef_require(preg_match('/^-?(?:[0-9]+(?:\.[0-9]+)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$/D', $value) && is_finite((float)$value), 'Invalid numeric value.');
		}
	}
	ef_require(count($values) === count($expected), 'Unknown entry fields; reload the form.');
}
