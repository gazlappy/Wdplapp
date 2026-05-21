<?php
// captain/scorecard.php — shared live scorecard.
// GET  ?fixture_id=...           -> { version, state, your_side }
// POST { fixture_id, if_version, ops:[...] }
//      ops are small commands; server enforces side rules.
//      Returns 200 { version, state } or 409 { error:'conflict', version, state }.
require __DIR__ . '/../_db.php';
require __DIR__ . '/../_captain.php';

$c = require_captain();
$pdo = db();

// Auto-provision storage.
try {
    $pdo->exec(
        "CREATE TABLE IF NOT EXISTS live_scorecards (
            fixture_id        VARCHAR(64) NOT NULL PRIMARY KEY,
            version           INT NOT NULL DEFAULT 0,
            state_json        MEDIUMTEXT NOT NULL,
            updated_utc       DATETIME NOT NULL,
            home_finalized_at DATETIME NULL,
            away_finalized_at DATETIME NULL,
            home_finalized_version INT NULL,
            away_finalized_version INT NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
} catch (Exception $e) { /* ignore */ }

function load_fixture_for_captain($fixture_id, $cap) {
    $fx = db()->prepare(
        'SELECT fixture_id, season_id, home_team_id, away_team_id,
                home_team_name, away_team_name, venue_name, fixture_date
           FROM league_fixtures WHERE fixture_id = :f LIMIT 1');
    $fx->execute(array(':f' => $fixture_id));
    $row = $fx->fetch();
    if (!$row) json_response(array('error' => 'fixture not found'), 404);
    if ($row['home_team_id'] !== $cap['team_id'] && $row['away_team_id'] !== $cap['team_id'])
        json_response(array('error' => 'not your fixture'), 403);
    return $row;
}

function default_settings() {
    $s = array('default_frames_per_match' => 15, 'max_frames_per_player' => 3);
    try {
        $rows = db()->query('SELECT setting_key, setting_value FROM league_settings')->fetchAll();
        foreach ($rows as $r) {
            if ($r['setting_key'] === 'default_frames_per_match') $s['default_frames_per_match'] = (int)$r['setting_value'];
            else if ($r['setting_key'] === 'max_frames_per_player') $s['max_frames_per_player'] = (int)$r['setting_value'];
        }
    } catch (Exception $e) {}
    return $s;
}

function empty_state($fixture) {
    $s = default_settings();
    $n = $s['default_frames_per_match']; if ($n <= 0 || $n > 50) $n = 15;
    $frames = array();
    for ($i = 1; $i <= $n; $i++) {
        $frames[] = array(
            'number'         => $i,
            'is_doubles'     => false,
            'home_player_id' => null, 'home_player_name' => null,
            'home_player2_id'=> null, 'home_player2_name'=> null,
            'away_player_id' => null, 'away_player_name' => null,
            'away_player2_id'=> null, 'away_player2_name'=> null,
            'winner'         => null,
            'eight_ball'     => false,
            'pending_eight'  => null, // { by:'home'|'away', value:true/false }
        );
    }
    return array(
        'fixture_id'     => $fixture['fixture_id'],
        'home_team_name' => $fixture['home_team_name'],
        'away_team_name' => $fixture['away_team_name'],
        'fixture_date'   => $fixture['fixture_date'],
        'frames'         => $frames,
        'notes'          => '',
        'last_edit'      => null, // { by:'home'|'away', at: iso }
    );
}

function load_or_init_row($fixture) {
    $pdo = db();
    $sel = $pdo->prepare('SELECT version, state_json,
                                 home_finalized_version, away_finalized_version
                            FROM live_scorecards WHERE fixture_id = :f LIMIT 1');
    $sel->execute(array(':f' => $fixture['fixture_id']));
    $row = $sel->fetch();
    if ($row) {
        $state = json_decode($row['state_json'], true);
        if (!is_array($state)) $state = empty_state($fixture);
        return array(
            'version' => (int)$row['version'],
            'state'   => $state,
            'home_finalized_version' => $row['home_finalized_version'] !== null ? (int)$row['home_finalized_version'] : null,
            'away_finalized_version' => $row['away_finalized_version'] !== null ? (int)$row['away_finalized_version'] : null,
        );
    }
    $state = empty_state($fixture);
    $now = gmdate('Y-m-d H:i:s');
    $pdo->prepare('INSERT INTO live_scorecards (fixture_id, version, state_json, updated_utc)
                   VALUES (:f, 0, :s, :u)')
        ->execute(array(':f' => $fixture['fixture_id'], ':s' => json_encode($state), ':u' => $now));
    return array('version' => 0, 'state' => $state,
                 'home_finalized_version' => null, 'away_finalized_version' => null);
}

function save_state($fixture_id, $version, $state) {
    $now = gmdate('Y-m-d H:i:s');
    db()->prepare('UPDATE live_scorecards
                      SET version = :v, state_json = :s, updated_utc = :u
                    WHERE fixture_id = :f')
        ->execute(array(':v' => $version, ':s' => json_encode($state), ':u' => $now, ':f' => $fixture_id));
}

// WDPL roster/pairing rules. Returns null if OK, or an error string.
// $state is the current scorecard, $idx the frame being edited, $slot one of
// home|home2|away|away2, $playerId/$playerName the proposed pick.
function validate_player_pick($state, $idx, $slot, $playerId, $playerName, $maxPerPlayer) {
    // null clears, void = walkover -> bypass all rules.
    if ($playerId === null && ($playerName === null || $playerName === '')) return null;
    $isVoid = ($playerId !== null && strcasecmp((string)$playerId, 'FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF') === 0);
    if ($isVoid) return null;

    $isHomeSide  = ($slot === 'home' || $slot === 'home2');
    $isPartner   = ($slot === 'home2' || $slot === 'away2');
    $thisFrame   = $state['frames'][$idx];

    // Map a slot (home|home2|away|away2) to the actual state field prefix.
    // Lead slots use "<side>_player"; partner slots use "<side>_player2".
    $field = function($s) {
        if ($s === 'home')  return 'home_player';
        if ($s === 'away')  return 'away_player';
        if ($s === 'home2') return 'home_player2';
        if ($s === 'away2') return 'away_player2';
        return null;
    };
    $get = function($frame, $s) use ($field) {
        $f = $field($s); if ($f === null) return array(null, null);
        $id = isset($frame[$f . '_id'])   ? $frame[$f . '_id']   : null;
        $nm = isset($frame[$f . '_name']) ? $frame[$f . '_name'] : null;
        return array($id, $nm);
    };

    // Identity key for matching ad-hoc (id-less) new picks by name.
    $key = function($id, $name) {
        if ($id !== null && $id !== '') return 'id:' . strtolower((string)$id);
        if ($name !== null && $name !== '') return 'nm:' . strtolower(trim((string)$name));
        return null;
    };
    $slotKey = function($frame, $s) use ($get, $key) {
        list($id, $nm) = $get($frame, $s);
        return $key($id, $nm);
    };
    $candKey = $key($playerId, $playerName);
    if ($candKey === null) return null;

    // 1. Doubles partner cannot equal the partner already in this frame.
    if ($isPartner) {
        $mainSlot = $isHomeSide ? 'home' : 'away';
        $mk = $slotKey($thisFrame, $mainSlot);
        if ($mk !== null && $mk === $candKey) return 'Player 2 must be different from Player 1.';
    } else {
        // Setting the lead -> if the existing partner equals this candidate, reject.
        $partnerSlot = $slot . '2';
        $pk = $slotKey($thisFrame, $partnerSlot);
        if ($pk !== null && $pk === $candKey) return 'Player 2 must be different from Player 1.';
    }

    // 2. Same player cannot appear on both sides of the SAME frame.
    $opposite = $isHomeSide ? array('away','away2') : array('home','home2');
    foreach ($opposite as $os) {
        $ok = $slotKey($thisFrame, $os);
        if ($ok !== null && $ok === $candKey) return 'Same player cannot play both sides of a frame.';
    }

    // 3. Max frames per player on this side across the match.
    // Count distinct frames where this player appears on the same side (singles
    // or doubles slot), excluding the slot we're about to overwrite.
    $count = 0;
    foreach ($state['frames'] as $i => $fr) {
        $slots = $isHomeSide ? array('home','home2') : array('away','away2');
        foreach ($slots as $s) {
            if ($i === $idx && $s === $slot) continue; // exclude the slot being replaced
            $k = $slotKey($fr, $s);
            if ($k !== null && $k === $candKey) { $count++; break; } // count each frame once
        }
    }
    if ($maxPerPlayer > 0 && $count >= $maxPerPlayer) {
        $nm = $playerName ? $playerName : 'This player';
        return $nm . ' has already played ' . $maxPerPlayer . ' frame(s). Max ' . $maxPerPlayer . ' per match.';
    }

    // 4. No repeat pairings: same home-vs-away combination cannot recur.
    // Build the would-be pairing for this frame after the edit and compare to all OTHER frames.
    $homeMain = ($slot === 'home') ? $candKey : $slotKey($thisFrame, 'home');
    $awayMain = ($slot === 'away') ? $candKey : $slotKey($thisFrame, 'away');
    if ($homeMain !== null && $awayMain !== null) {
        foreach ($state['frames'] as $i => $fr) {
            if ($i === $idx) continue;
            $hk = $slotKey($fr, 'home');
            $ak = $slotKey($fr, 'away');
            if ($hk === $homeMain && $ak === $awayMain) {
                return 'Pairing already used in frame ' . $fr['number'] . '. No repeat pairings.';
            }
        }
    }

    return null;
}

$method = isset($_SERVER['REQUEST_METHOD']) ? $_SERVER['REQUEST_METHOD'] : 'GET';

if ($method === 'GET') {
    $fid = isset($_GET['fixture_id']) ? trim((string)$_GET['fixture_id']) : '';
    if ($fid === '') json_response(array('error' => 'fixture_id required'), 400);
    $fixture = load_fixture_for_captain($fid, $c);
    $row = load_or_init_row($fixture);
    $side = ($fixture['home_team_id'] === $c['team_id']) ? 'home' : 'away';
    json_response(array(
        'fixture_id' => $fid,
        'your_side'  => $side,
        'version'    => $row['version'],
        'state'      => $row['state'],
        'home_finalized' => $row['home_finalized_version'] !== null,
        'away_finalized' => $row['away_finalized_version'] !== null,
    ));
}

// ---- POST: apply ops ----
$body = read_json_body();
$fid  = isset($body['fixture_id']) ? trim((string)$body['fixture_id']) : '';
$if_v = isset($body['if_version']) ? (int)$body['if_version'] : -1;
$ops  = isset($body['ops']) && is_array($body['ops']) ? $body['ops'] : array();
if ($fid === '') json_response(array('error' => 'fixture_id required'), 400);

$fixture = load_fixture_for_captain($fid, $c);
$side = ($fixture['home_team_id'] === $c['team_id']) ? 'home' : 'away';

$pdo->beginTransaction();
try {
    // Lock row.
    $sel = $pdo->prepare('SELECT version, state_json FROM live_scorecards
                           WHERE fixture_id = :f FOR UPDATE');
    $sel->execute(array(':f' => $fid));
    $row = $sel->fetch();
    if (!$row) {
        // No row yet — seed it then re-lock.
        $pdo->commit();
        load_or_init_row($fixture);
        $pdo->beginTransaction();
        $sel->execute(array(':f' => $fid));
        $row = $sel->fetch();
    }
    $version = (int)$row['version'];
    $state   = json_decode($row['state_json'], true);
    if (!is_array($state)) $state = empty_state($fixture);

    if ($if_v !== -1 && $if_v !== $version) {
        $pdo->commit();
        json_response(array('error' => 'conflict', 'version' => $version, 'state' => $state), 409);
    }

    $other = ($side === 'home') ? 'away' : 'home';
    $changed = false;
    $rejections = array(); // [{ op_index, reason }]
    $settings = default_settings();
    $maxPerPlayer = isset($settings['max_frames_per_player']) ? (int)$settings['max_frames_per_player'] : 3;

    foreach ($ops as $opIdx => $op) {
        if (!is_array($op) || empty($op['kind'])) continue;
        $kind = $op['kind'];
        $idx  = isset($op['frame']) ? (int)$op['frame'] : -1;
        if ($idx < 0 || $idx >= count($state['frames'])) continue;
        $f = &$state['frames'][$idx];

        if ($kind === 'set_player') {
            // Only own side may set player slots.
            $slot = isset($op['slot']) ? (string)$op['slot'] : '';
            $allowed = ($side === 'home') ? array('home','home2') : array('away','away2');
            if (!in_array($slot, $allowed, true)) continue;

            $pid = isset($op['player_id'])   ? $op['player_id']   : null;
            $pnm = isset($op['player_name']) ? $op['player_name'] : null;

            // WDPL rules - reject pick (and skip op) if violated.
            $err = validate_player_pick($state, $idx, $slot, $pid, $pnm, $maxPerPlayer);
            if ($err !== null) {
                $rejections[] = array('op_index' => $opIdx, 'frame' => $f['number'], 'slot' => $slot, 'reason' => $err);
                unset($f);
                continue;
            }

            // Map slot -> actual state field (lead vs partner).
            $prefix = ($slot === 'home2') ? 'home_player2'
                    : (($slot === 'away2') ? 'away_player2'
                    : (($slot === 'home')  ? 'home_player'
                    : (($slot === 'away')  ? 'away_player' : null)));
            if ($prefix === null) { unset($f); continue; }
            $f[$prefix . '_id']   = $pid;
            $f[$prefix . '_name'] = $pnm;
            $changed = true;
        }
        else if ($kind === 'set_doubles') {
            // Either captain can toggle doubles (it affects both rosters).
            $f['is_doubles'] = !empty($op['value']);
            if (!$f['is_doubles']) {
                $f['home_player2_id'] = null; $f['home_player2_name'] = null;
                $f['away_player2_id'] = null; $f['away_player2_name'] = null;
            }
            $changed = true;
        }
        else if ($kind === 'set_winner') {
            // Either captain may record winner.
            $w = isset($op['value']) ? $op['value'] : null;
            if ($w !== 'home' && $w !== 'away' && $w !== null) continue;
            if ($f['winner'] === $w) { $f['winner'] = null; $f['eight_ball'] = false; $f['pending_eight'] = null; }
            else                     { $f['winner'] = $w; }
            $changed = true;
        }
        else if ($kind === 'propose_eight') {
            // Proposer toggles -> goes into pending_eight, other side must agree.
            if (!$f['winner']) continue;
            $val = !empty($op['value']);
            if ($val === (bool)$f['eight_ball']) continue; // already in that state
            $f['pending_eight'] = array('by' => $side, 'value' => $val);
            $changed = true;
        }
        else if ($kind === 'agree_eight') {
            $pe = isset($f['pending_eight']) ? $f['pending_eight'] : null;
            if (!$pe || $pe['by'] === $side) continue; // only the OTHER side can agree
            $f['eight_ball'] = !empty($pe['value']);
            $f['pending_eight'] = null;
            $changed = true;
        }
        else if ($kind === 'decline_eight') {
            $pe = isset($f['pending_eight']) ? $f['pending_eight'] : null;
            if (!$pe || $pe['by'] === $side) continue;
            $f['pending_eight'] = null;
            $changed = true;
        }
        else if ($kind === 'set_notes') {
            $state['notes'] = isset($op['value']) ? (string)$op['value'] : '';
            $changed = true;
        }
        unset($f);
    }

    if ($changed) {
        $version++;
        $state['last_edit'] = array('by' => $side, 'at' => gmdate('c'));
        save_state($fid, $version, $state);
    }
    $pdo->commit();

    json_response(array('version' => $version, 'state' => $state, 'your_side' => $side, 'rejections' => $rejections));
}
catch (Exception $e) {
    if ($pdo->inTransaction()) $pdo->rollBack();
    json_response(array('error' => $e->getMessage()), 500);
}
