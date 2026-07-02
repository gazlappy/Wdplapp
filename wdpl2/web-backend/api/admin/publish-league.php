<?php
// admin/publish-league.php — admin POST. Replaces the league snapshot used by the
// captain portal. Body shape (JSON):
// {
//   "season_id":"...guid...",
//   "teams":[{ "team_id","name","division_id","division_name","venue_name" }, ...],
//   "players":[{ "player_id","team_id","full_name","is_active" }, ...],
//   "fixtures":[{ "fixture_id","division_id","home_team_id","away_team_id",
//                 "home_team_name","away_team_name","venue_name","fixture_date" }, ...]
// }
// Auth: admin login (session cookie / bearer) or HTTP Basic auth (MAUI app).
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
require_admin();
require_post();

$body = read_json_body();
$season_id = isset($body['season_id']) ? (string)$body['season_id'] : null;
$teams     = isset($body['teams'])    && is_array($body['teams'])    ? $body['teams']    : array();
$players   = isset($body['players'])  && is_array($body['players'])  ? $body['players']  : array();
$fixtures  = isset($body['fixtures']) && is_array($body['fixtures']) ? $body['fixtures'] : array();
$settings  = isset($body['settings']) && is_array($body['settings']) ? $body['settings'] : array();
$results   = isset($body['results'])  && is_array($body['results'])  ? $body['results']  : array();

$pdo = db();

// Ensure schemas exist (first-ever publish on a fresh DB).
try {
    $pdo->exec(
        "CREATE TABLE IF NOT EXISTS league_teams (
            team_id       VARCHAR(64) NOT NULL,
            season_id     VARCHAR(64) NULL,
            division_id   VARCHAR(64) NULL,
            name          VARCHAR(160) NOT NULL,
            division_name VARCHAR(120) NULL,
            venue_name    VARCHAR(200) NULL,
            PRIMARY KEY (team_id),
            KEY ix_team_season (season_id)
         ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
    $pdo->exec(
        "CREATE TABLE IF NOT EXISTS league_players (
            player_id  VARCHAR(64) NOT NULL,
            team_id    VARCHAR(64) NULL,
            season_id  VARCHAR(64) NULL,
            full_name  VARCHAR(160) NOT NULL,
            is_active  TINYINT(1) NOT NULL DEFAULT 1,
            added_by_captain TINYINT(1) NOT NULL DEFAULT 0,
            updated_utc DATETIME NULL,
            PRIMARY KEY (player_id),
            KEY ix_player_team   (team_id),
            KEY ix_player_season (season_id)
         ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
    // Older installs created league_players without the captain-roster columns.
    try { $pdo->exec("ALTER TABLE league_players ADD COLUMN added_by_captain TINYINT(1) NOT NULL DEFAULT 0"); } catch (Exception $e) { /* already there */ }
    try { $pdo->exec("ALTER TABLE league_players ADD COLUMN updated_utc DATETIME NULL"); } catch (Exception $e) { /* already there */ }
    $pdo->exec(
        "CREATE TABLE IF NOT EXISTS league_fixtures (
            fixture_id     VARCHAR(64) NOT NULL,
            season_id      VARCHAR(64) NULL,
            division_id    VARCHAR(64) NULL,
            division_name  VARCHAR(120) NULL,
            home_team_id   VARCHAR(64) NULL,
            away_team_id   VARCHAR(64) NULL,
            home_team_name VARCHAR(160) NULL,
            away_team_name VARCHAR(160) NULL,
            venue_name     VARCHAR(200) NULL,
            fixture_date   DATETIME NULL,
            PRIMARY KEY (fixture_id),
            KEY ix_fx_season (season_id),
            KEY ix_fx_date   (fixture_date)
         ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
    $pdo->exec(
        "CREATE TABLE IF NOT EXISTS live_scorecards (
            fixture_id          VARCHAR(64) NOT NULL PRIMARY KEY,
            state_json          MEDIUMTEXT NULL,
            home_finalized_at   DATETIME NULL,
            away_finalized_at   DATETIME NULL,
            updated_utc         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
         ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
} catch (Exception $e) {
    json_response(array('error' => 'schema: ' . $e->getMessage()), 500);
}

$pdo->beginTransaction();
try {
    // Wipe everything for this season (or all if null) - but KEEP captain-added
    // players so a publish from the app never destroys the online rosters.
    if ($season_id !== null && $season_id !== '') {
        $pdo->prepare('DELETE FROM league_fixtures WHERE season_id = :s')->execute(array(':s' => $season_id));
        $pdo->prepare('DELETE FROM league_players  WHERE season_id = :s AND added_by_captain = 0')->execute(array(':s' => $season_id));
        $pdo->prepare('DELETE FROM league_teams    WHERE season_id = :s')->execute(array(':s' => $season_id));
    } else {
        $pdo->exec('DELETE FROM league_fixtures');
        $pdo->exec('DELETE FROM league_players WHERE added_by_captain = 0');
        $pdo->exec('DELETE FROM league_teams');
    }

    $ins = $pdo->prepare(
        'INSERT INTO league_teams (team_id, season_id, division_id, name, division_name, venue_name)
         VALUES (:id, :sid, :did, :n, :dn, :v)');
    foreach ($teams as $t) {
        $ins->execute(array(
            ':id'  => isset($t['team_id'])       ? $t['team_id']       : null,
            ':sid' => $season_id,
            ':did' => isset($t['division_id'])   ? $t['division_id']   : null,
            ':n'   => isset($t['name'])          ? $t['name']          : '',
            ':dn'  => isset($t['division_name']) ? $t['division_name'] : null,
            ':v'   => isset($t['venue_name'])    ? $t['venue_name']    : null,
        ));
    }

    $ins = $pdo->prepare(
        'INSERT INTO league_players (player_id, team_id, season_id, full_name, is_active, added_by_captain, updated_utc)
         VALUES (:id, :tid, :sid, :n, :a, 0, UTC_TIMESTAMP())
         ON DUPLICATE KEY UPDATE
            team_id = VALUES(team_id), season_id = VALUES(season_id),
            full_name = VALUES(full_name), is_active = VALUES(is_active),
            added_by_captain = 0, updated_utc = UTC_TIMESTAMP()');
    foreach ($players as $p) {
        $ins->execute(array(
            ':id'  => isset($p['player_id']) ? $p['player_id'] : null,
            ':tid' => isset($p['team_id'])   ? $p['team_id']   : null,
            ':sid' => $season_id,
            ':n'   => isset($p['full_name']) ? $p['full_name'] : '',
            ':a'   => !empty($p['is_active']) ? 1 : 0,
        ));
    }

    // A captain-added player becomes app-managed once the app imports the match
    // and republishes them (same name, same team, different id). Drop the stale
    // captain row so the roster doesn't show duplicates.
    try {
        $pdo->exec(
            "DELETE cp FROM league_players cp
              INNER JOIN league_players ap
                      ON ap.added_by_captain = 0
                     AND cp.added_by_captain = 1
                     AND ap.team_id = cp.team_id
                     AND ap.player_id <> cp.player_id
                     AND LOWER(TRIM(ap.full_name)) = LOWER(TRIM(cp.full_name))");
    } catch (Exception $e) { /* non-fatal */ }

    $ins = $pdo->prepare(
        'INSERT INTO league_fixtures
            (fixture_id, season_id, division_id, home_team_id, away_team_id,
             home_team_name, away_team_name, venue_name, fixture_date)
         VALUES (:id, :sid, :did, :h, :a, :hn, :an, :v, :d)');
    foreach ($fixtures as $f) {
        $ins->execute(array(
            ':id'  => isset($f['fixture_id'])     ? $f['fixture_id']     : null,
            ':sid' => $season_id,
            ':did' => isset($f['division_id'])    ? $f['division_id']    : null,
            ':h'   => isset($f['home_team_id'])   ? $f['home_team_id']   : null,
            ':a'   => isset($f['away_team_id'])   ? $f['away_team_id']   : null,
            ':hn'  => isset($f['home_team_name']) ? $f['home_team_name'] : null,
            ':an'  => isset($f['away_team_name']) ? $f['away_team_name'] : null,
            ':v'   => isset($f['venue_name'])     ? $f['venue_name']     : null,
            ':d'   => isset($f['fixture_date'])   ? $f['fixture_date']   : null,
        ));
    }

    $pdo->commit();
} catch (Exception $e) {
    $pdo->rollBack();
    json_response(array('error' => $e->getMessage()), 500);
}

// ---- league_settings (key/value) ---------------------------------------
$settings_written = 0;
if (!empty($settings)) {
    try {
        $pdo->exec(
            "CREATE TABLE IF NOT EXISTS league_settings (
                setting_key   VARCHAR(64)  NOT NULL PRIMARY KEY,
                setting_value VARCHAR(255) NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
        $up = $pdo->prepare(
            'INSERT INTO league_settings (setting_key, setting_value)
             VALUES (:k, :v)
             ON DUPLICATE KEY UPDATE setting_value = VALUES(setting_value)');
        foreach ($settings as $k => $v) {
            if (!is_string($k)) continue;
            $up->execute(array(':k' => $k, ':v' => is_scalar($v) ? (string)$v : json_encode($v)));
            $settings_written++;
        }
    } catch (Exception $e) { /* non-fatal */ }
}

// ---- league_frame_results (per-frame, source='app') --------------------
$frames_written = 0;
if (!empty($results)) {
    try {
        $pdo->exec(
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

        $now = gmdate('Y-m-d H:i:s');
        $up = $pdo->prepare(
            'INSERT INTO league_frame_results
                (fixture_id, frame_no,
                 home_player_id, home_player_name, home_player2_id, home_player2_name,
                 away_player_id, away_player_name, away_player2_id, away_player2_name,
                 winner, eight_ball, is_doubles, source, updated_utc)
             VALUES
                (:fid, :n, :hid, :hn, :h2id, :h2n, :aid, :an, :a2id, :a2n,
                 :w, :e, :d, :src, :u)
             ON DUPLICATE KEY UPDATE
                home_player_id    = VALUES(home_player_id),
                home_player_name  = VALUES(home_player_name),
                home_player2_id   = VALUES(home_player2_id),
                home_player2_name = VALUES(home_player2_name),
                away_player_id    = VALUES(away_player_id),
                away_player_name  = VALUES(away_player_name),
                away_player2_id   = VALUES(away_player2_id),
                away_player2_name = VALUES(away_player2_name),
                winner            = VALUES(winner),
                eight_ball        = VALUES(eight_ball),
                is_doubles        = VALUES(is_doubles),
                source            = VALUES(source),
                updated_utc       = VALUES(updated_utc)');

        foreach ($results as $match) {
            $fid = isset($match['fixture_id']) ? (string)$match['fixture_id'] : '';
            $fr  = (isset($match['frames']) && is_array($match['frames'])) ? $match['frames'] : array();
            if ($fid === '' || empty($fr)) continue;

            // Wipe existing app-sourced frames for this fixture so removed frames
            // (e.g. captain trimmed from 15 to 12) actually disappear.
            $pdo->prepare('DELETE FROM league_frame_results WHERE fixture_id = :f')
                ->execute(array(':f' => $fid));

            foreach ($fr as $f) {
                $up->execute(array(
                    ':fid'  => $fid,
                    ':n'    => isset($f['number']) ? (int)$f['number'] : 0,
                    ':hid'  => isset($f['home_player_id'])    ? $f['home_player_id']    : null,
                    ':hn'   => isset($f['home_player_name'])  ? $f['home_player_name']  : null,
                    ':h2id' => isset($f['home_player2_id'])   ? $f['home_player2_id']   : null,
                    ':h2n'  => isset($f['home_player2_name']) ? $f['home_player2_name'] : null,
                    ':aid'  => isset($f['away_player_id'])    ? $f['away_player_id']    : null,
                    ':an'   => isset($f['away_player_name'])  ? $f['away_player_name']  : null,
                    ':a2id' => isset($f['away_player2_id'])   ? $f['away_player2_id']   : null,
                    ':a2n'  => isset($f['away_player2_name']) ? $f['away_player2_name'] : null,
                    ':w'    => isset($f['winner']) && in_array($f['winner'], array('home','away'), true) ? $f['winner'] : 'none',
                    ':e'    => !empty($f['eight_ball']) ? 1 : 0,
                    ':d'    => !empty($f['is_doubles']) ? 1 : 0,
                    ':src'  => isset($f['source']) ? (string)$f['source'] : 'app',
                    ':u'    => $now,
                ));
                $frames_written++;
            }
        }
    } catch (Exception $e) { /* non-fatal — already committed league core data */ }
}

json_response(array(
    'ok'        => true,
    'teams'     => count($teams),
    'players'   => count($players),
    'fixtures'  => count($fixtures),
    'settings'  => $settings_written,
    'results'   => $frames_written,
));
