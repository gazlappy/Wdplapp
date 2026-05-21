<?php
// captain/history.php — GET past scorecards for the signed-in captain's team.
// Returns matches with per-frame detail, drawn from league_frame_results
// (populated by both web submissions and the desktop app via publish-league).
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_captain.php';

$c = require_captain();
$team_id = $c['team_id'];

// Best-effort table create so a fresh deploy works even before the first push.
try {
    db()->exec(
        "CREATE TABLE IF NOT EXISTS league_frame_results (
            fixture_id       VARCHAR(64) NOT NULL,
            frame_no         INT NOT NULL,
            home_player_id   VARCHAR(64) NULL,
            home_player_name VARCHAR(120) NULL,
            home_player2_id  VARCHAR(64) NULL,
            home_player2_name VARCHAR(120) NULL,
            away_player_id   VARCHAR(64) NULL,
            away_player_name VARCHAR(120) NULL,
            away_player2_id  VARCHAR(64) NULL,
            away_player2_name VARCHAR(120) NULL,
            winner           ENUM('home','away','none') NOT NULL DEFAULT 'none',
            eight_ball       TINYINT(1) NOT NULL DEFAULT 0,
            is_doubles       TINYINT(1) NOT NULL DEFAULT 0,
            source           VARCHAR(16) NOT NULL DEFAULT 'app',
            updated_utc      DATETIME NOT NULL,
            PRIMARY KEY (fixture_id, frame_no),
            KEY ix_frame_updated (updated_utc)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
} catch (Exception $e) { /* ignore */ }

// Pull all fixtures this team has played (or had any frames recorded for).
$matches = array();
try {
    $stmt = db()->prepare(
        'SELECT f.fixture_id, f.season_id, f.division_id, f.home_team_id, f.away_team_id,
                f.home_team_name, f.away_team_name, f.venue_name, f.fixture_date,
                (SELECT COUNT(*) FROM league_frame_results r WHERE r.fixture_id = f.fixture_id) AS frame_count,
                (SELECT SUM(r.winner="home") FROM league_frame_results r WHERE r.fixture_id = f.fixture_id) AS home_score,
                (SELECT SUM(r.winner="away") FROM league_frame_results r WHERE r.fixture_id = f.fixture_id) AS away_score,
                (SELECT MAX(r.updated_utc) FROM league_frame_results r WHERE r.fixture_id = f.fixture_id) AS last_update,
                (SELECT MAX(r.source) FROM league_frame_results r WHERE r.fixture_id = f.fixture_id) AS source
           FROM league_fixtures f
          WHERE (f.home_team_id = :th OR f.away_team_id = :ta)
          ORDER BY f.fixture_date DESC');
    $stmt->execute(array(':th' => $team_id, ':ta' => $team_id));
    $rows = $stmt->fetchAll();

    // Keep only fixtures with at least one recorded frame.
    foreach ($rows as $r) {
        if ((int)$r['frame_count'] <= 0) continue;
        $r['home_score'] = (int)$r['home_score'];
        $r['away_score'] = (int)$r['away_score'];
        $r['frame_count'] = (int)$r['frame_count'];
        $matches[] = $r;
    }
} catch (Exception $e) {
    // league_fixtures may not exist yet — return empty.
    json_response(array('matches' => array()));
}

// Optionally include per-frame detail when ?fixture_id=... is supplied.
$fid = isset($_GET['fixture_id']) ? trim((string)$_GET['fixture_id']) : '';
if ($fid !== '') {
    // Verify the fixture belongs to this captain.
    $allowed = false;
    foreach ($matches as $m) { if ($m['fixture_id'] === $fid) { $allowed = true; break; } }
    if (!$allowed) json_response(array('error' => 'not your fixture'), 403);

    $fr = db()->prepare(
        'SELECT frame_no, home_player_id, home_player_name, home_player2_name,
                away_player_id, away_player_name, away_player2_name,
                winner, eight_ball, is_doubles, source, updated_utc
           FROM league_frame_results
          WHERE fixture_id = :f
          ORDER BY frame_no');
    $fr->execute(array(':f' => $fid));
    json_response(array(
        'fixture_id' => $fid,
        'frames'     => $fr->fetchAll(),
    ));
}

json_response(array('matches' => $matches));
