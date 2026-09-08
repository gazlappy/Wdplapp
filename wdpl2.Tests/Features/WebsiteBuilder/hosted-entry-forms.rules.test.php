<?php
// Run with: php wdpl2.Tests/Features/WebsiteBuilder/hosted-entry-forms.rules.test.php
require_once __DIR__ . '/../../../wdpl2/web-backend/api/_entry_form_rules.php';

function check($condition, $message) {
	if (!$condition) throw new RuntimeException($message);
}
function rejects($action) {
	try { $action(); } catch (InvalidArgumentException $ex) { return; }
	throw new RuntimeException('Expected validation rejection.');
}
function definition() {
	return json_decode('{"protocol":1,"origin":"https://league.example.test","timeZone":"Europe/London","forms":[{"id":"form-0123456789abcdef0123456789abcdef","title":"Team entry","closed":false,"closingDate":"2026-06-30","fields":[{"label":"Team","type":"text","required":true,"options":[]},{"label":"Consent","type":"checkbox","required":true,"options":[]},{"label":"Division","type":"select","required":true,"options":["Red","Green"]}]}]}');
}
function entry() {
	return json_decode('{"id":"submission-1","formId":"form-0123456789abcdef0123456789abcdef","name":"Aces","values":{"Team":"Aces","Consent":"Yes","Division":"Red"},"submittedAt":"2026-06-30T12:00:00.000Z"}');
}
$tests = array();
$tests['valid definitions and values'] = function () {
	$definition = ef_definitions(definition());
	ef_validate_values(ef_submission(entry()), $definition->forms[0], new DateTimeImmutable('2026-06-30 23:59:59', new DateTimeZone('Europe/London')));
};
$tests['inclusive London closing date and immediate closure'] = function () {
	$form = definition()->forms[0];
	$at = new DateTimeImmutable('2026-07-01 00:00:00', new DateTimeZone('Europe/London'));
	rejects(function () use ($form, $at) { ef_validate_values(entry(), $form, $at); });
	$form->closed = true;
	rejects(function () use ($form) { ef_validate_values(entry(), $form, new DateTimeImmutable('2026-01-01')); });
};
$tests['required fields and consent'] = function () {
	foreach (array('', '   ') as $value) {
		$entry = entry(); $entry->values->Team = $value;
		rejects(function () use ($entry) { ef_validate_values(ef_submission($entry), definition()->forms[0], new DateTimeImmutable('2026-06-01')); });
	}
	$entry = entry(); $entry->values->Consent = 'No';
	rejects(function () use ($entry) { ef_validate_values(ef_submission($entry), definition()->forms[0], new DateTimeImmutable('2026-06-01')); });
};
$tests['unknown and missing fields'] = function () {
	$entry = entry(); $entry->values->Extra = 'unexpected';
	rejects(function () use ($entry) { ef_validate_values(ef_submission($entry), definition()->forms[0], new DateTimeImmutable('2026-06-01')); });
	unset($entry->values->Extra, $entry->values->Team);
	rejects(function () use ($entry) { ef_validate_values(ef_submission($entry), definition()->forms[0], new DateTimeImmutable('2026-06-01')); });
};
$tests['dropdown membership'] = function () {
	$entry = entry(); $entry->values->Division = 'Yellow';
	rejects(function () use ($entry) { ef_validate_values(ef_submission($entry), definition()->forms[0], new DateTimeImmutable('2026-06-01')); });
};
$tests['invalid types and oversized values'] = function () {
	foreach (array(array('not text'), true, null, str_repeat('a', 4001)) as $value) {
		$entry = entry(); $entry->values->Team = $value;
		rejects(function () use ($entry) { ef_submission($entry); });
	}
};
$tests['identities cannot contain paths or markup'] = function () {
	foreach (array('../entry', '<script>', '', str_repeat('x', 129)) as $value) {
		$entry = entry(); $entry->id = $value;
		rejects(function () use ($entry) { ef_submission($entry); });
	}
};
$tests['canonical retries ignore property order but preserve values'] = function () {
	$first = ef_submission(entry());
	$second = entry(); $second->values = (object)array('Division' => 'Red', 'Consent' => 'Yes', 'Team' => 'Aces');
	check(json_encode($first) === json_encode(ef_submission($second)), 'Key reordering changed identity.');
	$second->values->Team = 'Changed';
	check(json_encode($first) !== json_encode(ef_submission($second)), 'Changed values did not change identity.');
};
$tests['invalid origins and time zones'] = function () {
	foreach (array('http://league.example.test', 'https://user:pass@league.example.test', 'https://league.example.test/path', 'https://league.example.test?secret=x', 'null') as $origin) {
		rejects(function () use ($origin) { ef_origin($origin); });
	}
	$data = definition(); $data->timeZone = 'UTC';
	rejects(function () use ($data) { ef_definitions($data); });
};
$tests['duplicate or reserved labels and invalid options'] = function () {
	$data = definition(); $data->forms[0]->fields[1]->label = ' team ';
	rejects(function () use ($data) { ef_definitions($data); });
	$data = definition(); $data->forms[0]->fields[0]->label = '_formId';
	rejects(function () use ($data) { ef_definitions($data); });
	$data = definition(); $data->forms[0]->fields[2]->options = array('Red', 'Red');
	rejects(function () use ($data) { ef_definitions($data); });
};
$tests['typed values are validated'] = function () {
	foreach (array('email' => 'not-email', 'number' => 'Infinity', 'date' => '2026-02-30') as $type => $value) {
		$data = definition(); $data->forms[0]->fields[0]->type = $type;
		$entry = entry(); $entry->values->Team = $value;
		rejects(function () use ($data, $entry) { ef_validate_values(ef_submission($entry), $data->forms[0], new DateTimeImmutable('2026-06-01')); });
	}
};
$tests['invalid dates and duplicate form ids'] = function () {
	$data = definition(); $data->forms[0]->closingDate = '2026-02-30';
	rejects(function () use ($data) { ef_definitions($data); });
	$data = definition(); $data->forms[] = $data->forms[0];
	rejects(function () use ($data) { ef_definitions($data); });
};
foreach ($tests as $name => $test) {
	$test();
	echo 'PASS ' . $name . PHP_EOL;
}
echo count($tests) . ' rule tests passed.' . PHP_EOL;
