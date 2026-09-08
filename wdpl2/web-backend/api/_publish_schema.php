<?php
// Run before the publication transaction; MySQL DDL commits implicitly.
function publish_ensure_result_schema($pdo) {
	$pdo->exec('CREATE TABLE IF NOT EXISTS league_settings (
		setting_key VARCHAR(64) NOT NULL PRIMARY KEY, setting_value VARCHAR(255) NULL
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
	$pdo->exec("CREATE TABLE IF NOT EXISTS league_frame_results (
		fixture_id VARCHAR(64) NOT NULL, frame_no INT NOT NULL,
		home_player_id VARCHAR(64) NULL, home_player_name VARCHAR(120) NULL,
		home_player2_id VARCHAR(64) NULL, home_player2_name VARCHAR(120) NULL,
		away_player_id VARCHAR(64) NULL, away_player_name VARCHAR(120) NULL,
		away_player2_id VARCHAR(64) NULL, away_player2_name VARCHAR(120) NULL,
		winner ENUM('home','away','none') NOT NULL DEFAULT 'none',
		eight_ball TINYINT(1) NOT NULL DEFAULT 0, is_doubles TINYINT(1) NOT NULL DEFAULT 0,
		source VARCHAR(16) NOT NULL DEFAULT 'app', updated_utc DATETIME NOT NULL,
		PRIMARY KEY (fixture_id, frame_no), KEY ix_frame_updated (updated_utc)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
}

function publish_pending_conflicts($seasonId, $body, $state) {
	// The caller holds admin_sync_lock until publication commits. Only an explicit
	// desktop resolution journal revision can supersede an outstanding web edit.
	$query = db()->prepare("SELECT c.record_id, c.revision, c.sequence_id, c.season_id, c.source
		FROM admin_sync_changes c JOIN admin_sync_records r
		ON r.record_kind = c.record_kind AND r.record_id = c.record_id AND r.revision = c.revision
		WHERE c.record_kind = 'scorecard'
		AND (c.season_id = ? OR c.season_id IS NULL) ORDER BY c.sequence_id");
	$query->execute(array($seasonId));
	$conflicts = array();
	$revisions = isset($body['sync_revisions']) && is_array($body['sync_revisions']) ? $body['sync_revisions'] : array();
	$sameBackend = isset($body['sync_backend_id']) && $body['sync_backend_id'] === $state['backend_id'];
	foreach ($query->fetchAll() as $row) {
		$revision = isset($revisions[$row['record_id']]) ? $revisions[$row['record_id']] : null;
		if ($row['source'] === 'web' || !$sameBackend || !is_int($revision) || $revision !== (int)$row['revision']) {
			$conflicts[] = $row;
			if (count($conflicts) === 100) break;
		}
	}
	return $conflicts;
}
