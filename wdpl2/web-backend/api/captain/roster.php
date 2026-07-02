<?php
// captain/roster.php — GET/POST roster for the captain's team.
// GET: returns all players for the captain's team (active + retired).
// POST {action, player_id?, full_name?, is_active?}:
//   - "add" → inserts a new player (captain-added flag set).
//   - "update" → renames (captain-added only) or toggles active.
//   - "retire" / "reactivate" → toggles is_active.
// Every successful change also writes a 'roster_change' row into `submissions`
// so the admin Inbox in the MAUI app can apply the same change locally.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_captain.php';

$c = require_captain();
$team_id = $c['team_id'];

// Older installs may lack the captain-roster columns; make GET/POST resilient.
function ensure_roster_schema() {
	try { db()->exec("ALTER TABLE league_players ADD COLUMN added_by_captain TINYINT(1) NOT NULL DEFAULT 0"); } catch (Exception $e) { /* exists */ }
	try { db()->exec("ALTER TABLE league_players ADD COLUMN updated_utc DATETIME NULL"); } catch (Exception $e) { /* exists */ }
}

function team_season_id($team_id) {
	try {
		$s = db()->prepare('SELECT season_id FROM league_teams WHERE team_id = :t LIMIT 1');
		$s->execute(array(':t' => $team_id));
		$r = $s->fetch();
		return $r && !empty($r['season_id']) ? $r['season_id'] : null;
	} catch (Exception $e) { return null; }
}

// Record the change for the app's Inbox. Non-fatal if it fails.
function log_roster_change($c, $action, $player_id, $full_name, $extra) {
	try {
		$payload = array_merge(array(
			'action'    => $action,          // add | rename | retire | reactivate
			'player_id' => $player_id,
			'full_name' => $full_name,
			'team_id'   => $c['team_id'],
			'team_name' => $c['team_name'],
		), is_array($extra) ? $extra : array());
		$ins = db()->prepare(
			'INSERT INTO submissions
				(type, season_id, reference_id, payload_json, submitter, submitter_ip)
			 VALUES ("roster_change", :sid, :rid, :p, :s, :ip)');
		$ins->execute(array(
			':sid' => team_season_id($c['team_id']),
			':rid' => $player_id,
			':p'   => json_encode($payload, JSON_UNESCAPED_SLASHES),
			':s'   => ($c['display_name'] !== null && $c['display_name'] !== ''
						 ? $c['display_name'] . ' (' . $c['team_name'] . ')'
						 : $c['team_name']),
			':ip'  => isset($_SERVER['REMOTE_ADDR']) ? $_SERVER['REMOTE_ADDR'] : null,
		));
	} catch (Exception $e) { /* non-fatal */ }
}

if ($_SERVER['REQUEST_METHOD'] === 'GET') {
	// Return all players for this team, active + retired, ordered by name.
	try {
		ensure_roster_schema();
		$stmt = db()->prepare(
			'SELECT player_id, team_id, full_name, is_active, added_by_captain, updated_utc
			   FROM league_players
			  WHERE team_id = :t
			  ORDER BY full_name ASC');
		$stmt->execute(array(':t' => $team_id));
		$rows = $stmt->fetchAll();
		// Cast for JS strict comparisons — some hosts return all columns as strings.
		foreach ($rows as $i => $r) {
			$rows[$i]['is_active'] = (int)$r['is_active'];
			$rows[$i]['added_by_captain'] = (int)$r['added_by_captain'];
		}
		json_response(array('players' => $rows));
	} catch (Exception $e) {
		// league_players may not exist yet.
		json_response(array('players' => array()));
	}
}

require_post();
ensure_roster_schema();
$body = read_json_body();
$action = trim((string)(isset($body['action']) ? $body['action'] : ''));

if ($action === 'add') {
	$name = trim((string)(isset($body['full_name']) ? $body['full_name'] : ''));
	if ($name === '') {
		json_response(array('error' => 'full_name required'), 400);
	}
	// Same-name check: if this player already exists on the team, reuse them
	// (re-activating if retired) instead of inserting a duplicate.
	$dup = db()->prepare(
		'SELECT player_id, is_active FROM league_players
		  WHERE team_id = :t AND LOWER(TRIM(full_name)) = LOWER(:n) LIMIT 1');
	$dup->execute(array(':t' => $team_id, ':n' => strtolower($name)));
	$existing = $dup->fetch();
	if ($existing) {
		if (!$existing['is_active']) {
			db()->prepare(
				'UPDATE league_players SET is_active = 1, updated_utc = UTC_TIMESTAMP()
				  WHERE player_id = :p')
				->execute(array(':p' => $existing['player_id']));
			log_roster_change($c, 'reactivate', $existing['player_id'], $name, null);
			json_response(array('ok' => true, 'player_id' => $existing['player_id'], 'full_name' => $name, 'reactivated' => true));
		}
		json_response(array('error' => 'player already on your roster'), 409);
	}
	// Generate a new GUID for the player (captain-added players live in the same
	// table as admin-pushed players; we distinguish via added_by_captain flag).
	$pid = strtoupper(sprintf(
		'%04X%04X-%04X-%04X-%04X-%04X%04X%04X',
		mt_rand(0, 0xFFFF), mt_rand(0, 0xFFFF),
		mt_rand(0, 0xFFFF),
		mt_rand(0, 0x0FFF) | 0x4000,
		mt_rand(0, 0x3FFF) | 0x8000,
		mt_rand(0, 0xFFFF), mt_rand(0, 0xFFFF), mt_rand(0, 0xFFFF)
	));
	db()->prepare(
		'INSERT INTO league_players
		   (player_id, team_id, season_id, full_name, is_active, added_by_captain, updated_utc)
		 VALUES (:pid, :tid, :sid, :nm, 1, 1, UTC_TIMESTAMP())')
		->execute(array(':pid' => $pid, ':tid' => $team_id, ':sid' => team_season_id($team_id), ':nm' => $name));
	log_roster_change($c, 'add', $pid, $name, null);
	json_response(array('ok' => true, 'player_id' => $pid, 'full_name' => $name));
}

if ($action === 'update') {
	$pid  = trim((string)(isset($body['player_id']) ? $body['player_id'] : ''));
	$name = trim((string)(isset($body['full_name']) ? $body['full_name'] : ''));
	$active = isset($body['is_active']) ? (bool)$body['is_active'] : null;
	if ($pid === '') {
		json_response(array('error' => 'player_id required'), 400);
	}
	$sel = db()->prepare(
		'SELECT player_id, full_name, is_active, added_by_captain
		   FROM league_players
		  WHERE player_id = :p AND team_id = :t LIMIT 1');
	$sel->execute(array(':p' => $pid, ':t' => $team_id));
	$row = $sel->fetch();
	if (!$row) {
		json_response(array('error' => 'player not found or not yours'), 404);
	}
	// If admin-added, captain can only retire them, not rename.
	if (!$row['added_by_captain'] && $name !== '' && $name !== $row['full_name']) {
		json_response(array('error' => 'cannot rename admin-provisioned player'), 403);
	}
	$updates = array();
	$params = array(':p' => $pid, ':t' => $team_id);
	$renamed = false; $toggled = null;
	if ($name !== '' && $name !== $row['full_name']) {
		$updates[] = 'full_name = :nm';
		$params[':nm'] = $name;
		$renamed = true;
	}
	if ($active !== null && (bool)$row['is_active'] !== $active) {
		$updates[] = 'is_active = :act';
		$params[':act'] = $active ? 1 : 0;
		$toggled = $active;
	}
	if (count($updates) === 0) {
		json_response(array('ok' => true, 'message' => 'no changes'));
	}
	$sql = 'UPDATE league_players SET ' . implode(', ', $updates) .
		   ', updated_utc = UTC_TIMESTAMP() WHERE player_id = :p AND team_id = :t';
	db()->prepare($sql)->execute($params);
	if ($renamed)
		log_roster_change($c, 'rename', $pid, $name, array('old_name' => $row['full_name']));
	if ($toggled !== null)
		log_roster_change($c, $toggled ? 'reactivate' : 'retire', $pid, $renamed ? $name : $row['full_name'], null);
	json_response(array('ok' => true));
}

if ($action === 'retire' || $action === 'reactivate') {
	$pid = trim((string)(isset($body['player_id']) ? $body['player_id'] : ''));
	if ($pid === '') {
		json_response(array('error' => 'player_id required'), 400);
	}
	$sel = db()->prepare(
		'SELECT player_id, full_name FROM league_players
		  WHERE player_id = :p AND team_id = :t LIMIT 1');
	$sel->execute(array(':p' => $pid, ':t' => $team_id));
	$row = $sel->fetch();
	if (!$row) {
		json_response(array('error' => 'player not found or not yours'), 404);
	}
	$act = ($action === 'reactivate') ? 1 : 0;
	db()->prepare(
		'UPDATE league_players
			SET is_active = :a, updated_utc = UTC_TIMESTAMP()
		  WHERE player_id = :p AND team_id = :t')
		->execute(array(':a' => $act, ':p' => $pid, ':t' => $team_id));
	log_roster_change($c, $action, $pid, $row['full_name'], null);
	json_response(array('ok' => true));
}

json_response(array('error' => 'unknown action: ' . $action), 400);
