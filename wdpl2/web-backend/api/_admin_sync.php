<?php
require_once __DIR__ . '/_db.php';
require_once __DIR__ . '/_admin_sync_rules.php';

// Storage helpers are authentication-neutral; callers must authenticate first.
function admin_sync_guid() {
	$b = random_bytes(16);
	$b[6] = chr((ord($b[6]) & 0x0f) | 0x40);
	$b[8] = chr((ord($b[8]) & 0x3f) | 0x80);
	$h = bin2hex($b);
	return substr($h,0,8).'-'.substr($h,8,4).'-'.substr($h,12,4).'-'.substr($h,16,4).'-'.substr($h,20,12);
}

// Schema creation must run before any data transaction: MySQL DDL implicitly commits.
function admin_sync_ensure_schema() {
	$pdo = db();
	if ($pdo->inTransaction()) throw new LogicException('Initialize sync schema before starting a transaction.');
	$pdo->exec('CREATE TABLE IF NOT EXISTS admin_sync_state (
		singleton_id INT PRIMARY KEY, backend_id CHAR(36) NOT NULL,
		sequence_id BIGINT NOT NULL DEFAULT 0
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
	$pdo->prepare('INSERT IGNORE INTO admin_sync_state (singleton_id, backend_id) VALUES (1, ?)')->execute(array(admin_sync_guid()));
	$pdo->exec('CREATE TABLE IF NOT EXISTS admin_sync_records (
		record_kind VARCHAR(24) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
		record_id VARCHAR(160) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
		revision BIGINT NOT NULL, payload_json MEDIUMTEXT NOT NULL,
		PRIMARY KEY (record_kind, record_id)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
	$pdo->exec('CREATE TABLE IF NOT EXISTS admin_sync_changes (
		sequence_id BIGINT NOT NULL PRIMARY KEY,
		record_kind VARCHAR(24) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
		record_id VARCHAR(160) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
		revision BIGINT NOT NULL, season_id VARCHAR(64) NULL,
		request_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL UNIQUE,
		request_hash CHAR(64) NOT NULL, actor_id CHAR(36) NOT NULL,
		source VARCHAR(16) NOT NULL, payload_json MEDIUMTEXT NOT NULL,
		created_utc DATETIME NOT NULL,
		INDEX ix_sync_record (record_kind, record_id, revision)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
	$pdo->exec('CREATE TABLE IF NOT EXISTS admin_sync_acknowledgements (
		request_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
		request_hash CHAR(64) NOT NULL, actor_id CHAR(36) NOT NULL,
		record_kind VARCHAR(24) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
		record_id VARCHAR(160) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
		sequence_id BIGINT NOT NULL, revision BIGINT NOT NULL, created_utc DATETIME NOT NULL
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
}

function admin_sync_lock() {
	if (!db()->inTransaction()) throw new LogicException('Synchronization requires a transaction.');
	return db()->query('SELECT backend_id, sequence_id FROM admin_sync_state WHERE singleton_id = 1 FOR UPDATE')->fetch();
}

function admin_sync_read_record($kind, $id) {
	admin_sync_identity($kind, $id);
	$query = db()->prepare('SELECT revision, payload_json FROM admin_sync_records WHERE record_kind = ? AND record_id = ?');
	$query->execute(array($kind, $id));
	$row = $query->fetch();
	return $row ? array('revision' => (int)$row['revision'], 'payload' => json_decode($row['payload_json'], true)) : null;
}

// Call under the shared lock BEFORE changing domain data. A retry returns its original receipt.
function admin_sync_prepare_change($actor, $kind, $id, $expected, $requestId, $request) {
	admin_sync_identity($kind, $id);
	$expected = admin_sync_integer($expected, 'expected_revision');
	$requestId = admin_sync_request_id($requestId);
	admin_sync_lock();
	$hash = hash('sha256', json_encode(array($kind, $id, $expected, $request), JSON_THROW_ON_ERROR));
	$query = db()->prepare('SELECT sequence_id, revision, request_hash, actor_id FROM admin_sync_changes WHERE request_id = ?
		UNION ALL SELECT sequence_id, revision, request_hash, actor_id FROM admin_sync_acknowledgements WHERE request_id = ?');
	$query->execute(array($requestId, $requestId));
	$receipts = $query->fetchAll();
	if (count($receipts) > 1) throw new LogicException('Ambiguous synchronization receipt.');
	$previous = $receipts ? $receipts[0] : null;
	if ($previous) {
		if ($previous['actor_id'] !== $actor['user_id'] || !hash_equals($previous['request_hash'], $hash)) {
			return array('conflict' => true, 'error' => 'Request identity already used for a different change.');
		}
		return array('replayed' => true, 'sequence' => (int)$previous['sequence_id'], 'revision' => (int)$previous['revision']);
	}
	$current = admin_sync_read_record($kind, $id);
	if (($current ? $current['revision'] : 0) !== $expected) {
		return array('conflict' => true, 'error' => 'Record changed. Review both versions before saving.', 'current' => $current);
	}
	return array('request_id' => $requestId, 'request_hash' => $hash, 'revision' => $expected + 1);
}

// Persist a no-op acceptance in the caller's transaction, without extending the feed.
// Call only after the endpoint has checked the reviewed values against live data.
function admin_sync_acknowledge($actor, $kind, $id, $prepared) {
	if (!isset($prepared['request_hash'], $prepared['request_id'], $prepared['revision'])) throw new LogicException('Acknowledgement was not prepared.');
	$backend = admin_sync_lock();
	$current = admin_sync_read_record($kind, $id);
	if (!$current || $current['revision'] + 1 !== $prepared['revision']) throw new LogicException('Acknowledgement revision changed.');
	$query = db()->prepare('SELECT sequence_id, source FROM admin_sync_changes WHERE record_kind = ? AND record_id = ? AND revision = ?');
	$query->execute(array($kind, $id, $current['revision']));
	$last = $query->fetch();
	if (!$last || $last['source'] !== 'desktop') throw new LogicException('Only an unchanged desktop revision can be acknowledged without a change.');
	db()->prepare('INSERT INTO admin_sync_acknowledgements
		(request_id, request_hash, actor_id, record_kind, record_id, sequence_id, revision, created_utc)
		VALUES (?, ?, ?, ?, ?, ?, ?, UTC_TIMESTAMP())')
		->execute(array($prepared['request_id'], $prepared['request_hash'], $actor['user_id'], $kind, $id, $last['sequence_id'], $current['revision']));
	return array('protocol' => 1, 'backendId' => $backend['backend_id'], 'sequence' => (int)$last['sequence_id'], 'revision' => $current['revision']);
}

// Write the domain record and this journal receipt in the SAME transaction.
function admin_sync_commit_change($actor, $kind, $id, $season, $source, $payload, $prepared) {
	if (!isset($prepared['request_hash']) || !in_array($source, array('web', 'desktop'), true)) throw new LogicException('Change was not prepared.');
	$state = admin_sync_lock();
	$sequence = (int)$state['sequence_id'] + 1;
	$json = json_encode($payload, JSON_THROW_ON_ERROR);
	if (strlen($json) > 1048576) throw new InvalidArgumentException('Synchronization record exceeds 1 MiB.');
	db()->prepare('INSERT INTO admin_sync_records (record_kind, record_id, revision, payload_json) VALUES (?, ?, ?, ?)
		ON DUPLICATE KEY UPDATE revision = VALUES(revision), payload_json = VALUES(payload_json)')
		->execute(array($kind, $id, $prepared['revision'], $json));
	db()->prepare('INSERT INTO admin_sync_changes (sequence_id, record_kind, record_id, revision, season_id,
		request_id, request_hash, actor_id, source, payload_json, created_utc) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, UTC_TIMESTAMP())')
		->execute(array($sequence, $kind, $id, $prepared['revision'], $season, $prepared['request_id'], $prepared['request_hash'], $actor['user_id'], $source, $json));
	db()->prepare('UPDATE admin_sync_state SET sequence_id = ? WHERE singleton_id = 1')->execute(array($sequence));
	return array('protocol' => 1, 'backendId' => $state['backend_id'], 'sequence' => $sequence, 'revision' => $prepared['revision']);
}
