<?php
// admin/cup.php — knockout cup bracket manager.
// Schema: cup_competitions(cup_id, name, season_id, created_utc),
//         cup_matches(match_id PK AUTO, cup_id, round_no, slot, home_team_id, away_team_id,
//                     home_team_name, away_team_name, winner_team_id, winner_team_name,
//                     scheduled_date, venue_name, notes).
// Round 1 slots are 1..N/2; the winner of slot K in round R feeds slot ceil(K/2) in round R+1.
//
// GET ?cup_id=...                         -> { cup, matches }
// GET                                     -> { items: [cups] }
// POST {action:"create", name, season_id?, team_ids:[...]}     -> creates cup + round 1 matches
// POST {action:"advance", match_id, winner_team_id}            -> sets winner and creates/updates next round slot
// POST {action:"schedule", match_id, scheduled_date?, venue_name?}
// DELETE {cup_id}                                              -> superadmin
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_admin.php';
$me = require_admin();
$pdo = db();

try {
    $pdo->exec(
        'CREATE TABLE IF NOT EXISTS cup_competitions (
            cup_id      CHAR(36) NOT NULL PRIMARY KEY,
            name        VARCHAR(120) NOT NULL,
            season_id   CHAR(36) NULL,
            created_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
         ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
    $pdo->exec(
        'CREATE TABLE IF NOT EXISTS cup_matches (
            match_id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
            cup_id             CHAR(36) NOT NULL,
            round_no           INT NOT NULL,
            slot               INT NOT NULL,
            home_team_id       VARCHAR(64) NULL,
            away_team_id       VARCHAR(64) NULL,
            home_team_name     VARCHAR(160) NULL,
            away_team_name     VARCHAR(160) NULL,
            winner_team_id     VARCHAR(64) NULL,
            winner_team_name   VARCHAR(160) NULL,
            scheduled_date     DATE NULL,
            venue_name         VARCHAR(160) NULL,
            notes              VARCHAR(255) NULL,
            UNIQUE KEY ux_cup_round_slot (cup_id, round_no, slot),
            KEY ix_cup (cup_id)
         ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
} catch (Exception $e) {}

function _cup_guid() {
    $b = function_exists('random_bytes') ? random_bytes(16) : openssl_random_pseudo_bytes(16);
    $b[6] = chr((ord($b[6]) & 0x0f) | 0x40);
    $b[8] = chr((ord($b[8]) & 0x3f) | 0x80);
    $h = bin2hex($b);
    return substr($h,0,8).'-'.substr($h,8,4).'-'.substr($h,12,4).'-'.substr($h,16,4).'-'.substr($h,20,12);
}

if ($_SERVER['REQUEST_METHOD'] === 'GET') {
    $cid = isset($_GET['cup_id']) ? (string)$_GET['cup_id'] : '';
    if ($cid === '') {
        $rows = $pdo->query('SELECT * FROM cup_competitions ORDER BY created_utc DESC')->fetchAll();
        json_response(array('items' => $rows));
    }
    $c = $pdo->prepare('SELECT * FROM cup_competitions WHERE cup_id = :c LIMIT 1');
    $c->execute(array(':c' => $cid)); $cup = $c->fetch();
    if (!$cup) json_response(array('error' => 'not found'), 404);
    $m = $pdo->prepare('SELECT * FROM cup_matches WHERE cup_id = :c ORDER BY round_no, slot');
    $m->execute(array(':c' => $cid));
    json_response(array('cup' => $cup, 'matches' => $m->fetchAll()));
}

if ($_SERVER['REQUEST_METHOD'] === 'DELETE') {
    require_admin('superadmin');
    $body = read_json_body();
    $cid = trim((string)(isset($body['cup_id']) ? $body['cup_id'] : ''));
    if ($cid === '') json_response(array('error' => 'cup_id required'), 400);
    $pdo->prepare('DELETE FROM cup_matches WHERE cup_id = :c')->execute(array(':c' => $cid));
    $pdo->prepare('DELETE FROM cup_competitions WHERE cup_id = :c')->execute(array(':c' => $cid));
    audit_log($me, 'cup.delete', $cid);
    json_response(array('ok' => true));
}

require_post();
$body = read_json_body();
$action = trim((string)(isset($body['action']) ? $body['action'] : ''));

if ($action === 'create') {
    $name = trim((string)(isset($body['name']) ? $body['name'] : ''));
    $sid  = isset($body['season_id']) ? (string)$body['season_id'] : null;
    $tids = isset($body['team_ids']) && is_array($body['team_ids']) ? array_values(array_unique($body['team_ids'])) : array();
    if ($name === '' || count($tids) < 2) json_response(array('error' => 'name and at least 2 team_ids required'), 400);
    // Pad to next power of two with BYEs.
    $n = 1; while ($n < count($tids)) $n *= 2;
    $padded = $tids;
    while (count($padded) < $n) $padded[] = null; // null => BYE
    // Look up team names.
    $names = array(); $real = array_values(array_filter($padded));
    if ($real) {
        $ph = implode(',', array_fill(0, count($real), '?'));
        $st = $pdo->prepare("SELECT team_id, name FROM league_teams WHERE team_id IN ($ph)");
        $st->execute($real);
        foreach ($st->fetchAll() as $r) $names[$r['team_id']] = $r['name'];
    }
    // Seed round 1.
    $cid = _cup_guid();
    $pdo->prepare('INSERT INTO cup_competitions (cup_id, name, season_id) VALUES (:i,:n,:s)')
        ->execute(array(':i'=>$cid, ':n'=>$name, ':s'=>$sid));
    // Optional shuffle to avoid alphabetical pairings.
    if (!empty($body['shuffle'])) shuffle($padded);
    $ins = $pdo->prepare(
        'INSERT INTO cup_matches (cup_id, round_no, slot, home_team_id, away_team_id, home_team_name, away_team_name, winner_team_id, winner_team_name)
         VALUES (:c, 1, :s, :ht, :at, :hn, :an, :wid, :wn)');
    $slot = 1;
    for ($i = 0; $i < $n; $i += 2, $slot++) {
        $h = $padded[$i]; $a = $padded[$i+1];
        $hn = $h ? ($names[$h] ?? 'Unknown') : null;
        $an = $a ? ($names[$a] ?? 'Unknown') : null;
        $wid = $wn = null;
        // Walkover: only one team present in the pairing -> they auto-advance.
        if ($h && !$a) { $wid = $h; $wn = $hn; }
        else if ($a && !$h) { $wid = $a; $wn = $an; }
        $ins->execute(array(':c'=>$cid, ':s'=>$slot, ':ht'=>$h, ':at'=>$a, ':hn'=>$hn, ':an'=>$an, ':wid'=>$wid, ':wn'=>$wn));
    }
    // Auto-create empty later rounds + propagate walkover winners.
    _cup_propagate_all($pdo, $cid);
    audit_log($me, 'cup.create', $cid, array('name' => $name, 'teams' => count($tids)));
    json_response(array('ok' => true, 'cup_id' => $cid));
}

if ($action === 'advance') {
    $mid = (int)(isset($body['match_id']) ? $body['match_id'] : 0);
    $wid = isset($body['winner_team_id']) ? (string)$body['winner_team_id'] : '';
    if ($mid <= 0 || $wid === '') json_response(array('error' => 'match_id and winner_team_id required'), 400);
    $m = $pdo->prepare('SELECT * FROM cup_matches WHERE match_id = :m'); $m->execute(array(':m'=>$mid));
    $row = $m->fetch(); if (!$row) json_response(array('error' => 'match not found'), 404);
    if ($wid !== $row['home_team_id'] && $wid !== $row['away_team_id']) json_response(array('error' => 'winner must be one of the match teams'), 400);
    $wn = ($wid === $row['home_team_id']) ? $row['home_team_name'] : $row['away_team_name'];
    $pdo->prepare('UPDATE cup_matches SET winner_team_id = :w, winner_team_name = :wn WHERE match_id = :m')
        ->execute(array(':w'=>$wid, ':wn'=>$wn, ':m'=>$mid));
    _cup_propagate_all($pdo, $row['cup_id']);
    audit_log($me, 'cup.advance', (string)$mid, array('winner' => $wid));
    json_response(array('ok' => true));
}

if ($action === 'schedule') {
    $mid = (int)(isset($body['match_id']) ? $body['match_id'] : 0);
    if ($mid <= 0) json_response(array('error' => 'match_id required'), 400);
    $pdo->prepare('UPDATE cup_matches SET scheduled_date = :d, venue_name = :v, notes = :n WHERE match_id = :m')
        ->execute(array(
            ':d' => isset($body['scheduled_date']) ? ($body['scheduled_date'] ?: null) : null,
            ':v' => isset($body['venue_name']) ? ($body['venue_name'] ?: null) : null,
            ':n' => isset($body['notes']) ? (string)$body['notes'] : null,
            ':m' => $mid));
    json_response(array('ok' => true));
}

function _cup_propagate_all($pdo, $cid) {
    // Find round count.
    $st = $pdo->prepare('SELECT MAX(round_no) AS r, COUNT(*) AS n FROM cup_matches WHERE cup_id = :c AND round_no = 1');
    $st->execute(array(':c' => $cid)); $row = $st->fetch();
    $r1count = (int)$row['n']; if ($r1count < 1) return;
    $totalRounds = 0; $x = $r1count; while ($x >= 1) { $totalRounds++; if ($x === 1) break; $x = (int)($x / 2); }
    for ($r = 2; $r <= $totalRounds; $r++) {
        $prev = $pdo->prepare('SELECT * FROM cup_matches WHERE cup_id = :c AND round_no = :r ORDER BY slot');
        $prev->execute(array(':c' => $cid, ':r' => $r - 1));
        $prevRows = $prev->fetchAll();
        $newSlots = count($prevRows) / 2;
        for ($s = 1; $s <= $newSlots; $s++) {
            $a = $prevRows[($s - 1) * 2];
            $b = $prevRows[($s - 1) * 2 + 1];
            // upsert.
            $sel = $pdo->prepare('SELECT * FROM cup_matches WHERE cup_id = :c AND round_no = :r AND slot = :s LIMIT 1');
            $sel->execute(array(':c' => $cid, ':r' => $r, ':s' => $s));
            $existing = $sel->fetch();
            $ht = $a['winner_team_id']; $hn = $a['winner_team_name'];
            $at = $b['winner_team_id']; $an = $b['winner_team_name'];
            if (!$existing) {
                $pdo->prepare(
                    'INSERT INTO cup_matches (cup_id, round_no, slot, home_team_id, away_team_id, home_team_name, away_team_name)
                     VALUES (:c, :r, :s, :ht, :at, :hn, :an)')
                   ->execute(array(':c'=>$cid, ':r'=>$r, ':s'=>$s, ':ht'=>$ht, ':at'=>$at, ':hn'=>$hn, ':an'=>$an));
            } else {
                // Only refresh if winner not yet set OR participants changed.
                if (!$existing['winner_team_id']) {
                    $pdo->prepare('UPDATE cup_matches SET home_team_id=:ht, away_team_id=:at, home_team_name=:hn, away_team_name=:an WHERE match_id=:m')
                        ->execute(array(':ht'=>$ht, ':at'=>$at, ':hn'=>$hn, ':an'=>$an, ':m'=>$existing['match_id']));
                }
            }
        }
    }
}

json_response(array('error' => 'unknown action'), 400);
