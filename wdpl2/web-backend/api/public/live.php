<?php
// public/live.php — public read-only live scores feed.
//
// GET                       -> { season_id, generated_utc, items:[ match summary ] }
// GET ?fixture_id=<id>      -> { season_id, generated_utc, match:{ ...summary, frames:[...] } }
//
// Data comes from the shared captain scorecards (live_scorecards) that both
// captains edit during a match, joined to the pushed fixture snapshot.
//
// PRIVACY / FAIRNESS: the WDPL nomination rules mean upcoming player picks must
// NOT be public while a match is in progress. This endpoint therefore only ever
// reveals the players of frames that already have a winner. Nothing about
// captains, sessions or contact details is exposed.
//
// PHP 5.6 compatible (no return types, no ??).
require __DIR__ . '/_pub.php';

// Polling endpoint: keep the cache window short so scores feel live.
if (!headers_sent()) {
	header('Cache-Control: public, max-age=5');
}

// Only matches touched within this window are considered "live".
define('LIVE_WINDOW_HOURS', 12);

/**
 * Derive the running score and frame list from a stored scorecard state.
 * Returns array(home_score, away_score, frames_played, frames_total, frames).
 */
function live_summarise_state($state)
{
	$home = 0;
	$away = 0;
	$played = 0;
	$frames = array();

	$raw = (is_array($state) && isset($state['frames']) && is_array($state['frames']))
		? $state['frames'] : array();

	foreach ($raw as $i => $f) {
		if (!is_array($f)) continue;

		$winner = isset($f['winner']) ? $f['winner'] : null;
		$winner = ($winner === 'home' || $winner === 'away') ? $winner : null;

		if ($winner === 'home') { $home++; $played++; }
		else if ($winner === 'away') { $away++; $played++; }

		$number = isset($f['number']) ? (int)$f['number'] : ($i + 1);
		$decided = ($winner !== null);

		$frames[] = array(
			'number'      => $number,
			'is_doubles'  => !empty($f['is_doubles']),
			'winner'      => $winner,
			'eight_ball'  => $decided && !empty($f['eight_ball']),
			// Players are only revealed once the frame has been decided.
			'home_player'  => $decided ? live_player_label($f, 'home') : null,
			'away_player'  => $decided ? live_player_label($f, 'away') : null,
		);
	}

	return array(
		'home_score'    => $home,
		'away_score'    => $away,
		'frames_played' => $played,
		'frames_total'  => count($frames),
		'frames'        => $frames,
	);
}

/**
 * Build a display label for one side of a frame ("A Smith" or "A Smith & B Jones").
 * Returns null when no player has been recorded.
 */
function live_player_label($frame, $side)
{
	$one = isset($frame[$side . '_player_name']) ? trim((string)$frame[$side . '_player_name']) : '';
	$two = isset($frame[$side . '_player2_name']) ? trim((string)$frame[$side . '_player2_name']) : '';

	if ($one !== '' && $two !== '') return $one . ' & ' . $two;
	if ($one !== '') return $one;
	if ($two !== '') return $two;
	return null;
}

/**
 * Match status: 'live' while either side is still editing,
 * 'final' once both captains have finalised.
 */
function live_status($row)
{
	$homeDone = !empty($row['home_finalized_at']);
	$awayDone = !empty($row['away_finalized_at']);
	if ($homeDone && $awayDone) return 'final';
	if ($homeDone || $awayDone) return 'confirming';
	return 'live';
}

$sid = isset($_GET['season_id']) && $_GET['season_id'] !== ''
	? (string)$_GET['season_id']
	: pub_current_season_id();

$fixtureId = isset($_GET['fixture_id']) && $_GET['fixture_id'] !== ''
	? (string)$_GET['fixture_id']
	: null;

$now = gmdate('Y-m-d H:i:s');

try {
	$args = array();
	$where = array();

	if ($fixtureId !== null) {
		$where[] = 'lc.fixture_id = :fid';
		$args[':fid'] = $fixtureId;
	} else {
		// Only recently-touched cards, so finished weeks drop off automatically.
		$where[] = 'lc.updated_utc >= :cutoff';
		$args[':cutoff'] = gmdate('Y-m-d H:i:s', time() - (LIVE_WINDOW_HOURS * 3600));
		if ($sid) {
			$where[] = 'fx.season_id = :sid';
			$args[':sid'] = $sid;
		}
	}

	$sql =
		'SELECT lc.fixture_id, lc.state_json, lc.updated_utc,
				lc.home_finalized_at, lc.away_finalized_at,
				fx.season_id, fx.division_id, fx.division_name,
				fx.home_team_id, fx.away_team_id,
				fx.home_team_name, fx.away_team_name,
				fx.venue_name, fx.fixture_date
		   FROM live_scorecards lc
		   JOIN league_fixtures fx ON fx.fixture_id = lc.fixture_id
		  WHERE ' . implode(' AND ', $where) . '
		  ORDER BY lc.updated_utc DESC
		  LIMIT 200';

	$st = db()->prepare($sql);
	$st->execute($args);
	$rows = $st->fetchAll();

	$items = array();
	foreach ($rows as $row) {
		$state = json_decode($row['state_json'], true);
		$sum = live_summarise_state($state);

		$item = array(
			'fixture_id'     => $row['fixture_id'],
			'division_id'    => $row['division_id'],
			'division_name'  => $row['division_name'],
			'home_team_id'   => $row['home_team_id'],
			'away_team_id'   => $row['away_team_id'],
			'home_team_name' => $row['home_team_name'],
			'away_team_name' => $row['away_team_name'],
			'venue_name'     => $row['venue_name'],
			'fixture_date'   => $row['fixture_date'],
			'home_score'     => $sum['home_score'],
			'away_score'     => $sum['away_score'],
			'frames_played'  => $sum['frames_played'],
			'frames_total'   => $sum['frames_total'],
			'status'         => live_status($row),
			'updated_utc'    => $row['updated_utc'],
		);

		if ($fixtureId !== null) {
			$item['frames'] = $sum['frames'];
			json_response(array(
				'season_id'     => $sid,
				'generated_utc' => $now,
				'match'         => $item,
			));
		}

		// A card that nobody has scored in yet isn't interesting to spectators.
		if ($item['frames_played'] > 0) $items[] = $item;
	}

	if ($fixtureId !== null) {
		json_response(array(
			'season_id'     => $sid,
			'generated_utc' => $now,
			'match'         => null,
		), 404);
	}

	json_response(array(
		'season_id'     => $sid,
		'generated_utc' => $now,
		'items'         => $items,
	));
} catch (Exception $e) {
	// Table may not exist yet (no scorecard has ever been opened) — degrade quietly.
	json_response(array(
		'season_id'     => $sid,
		'generated_utc' => $now,
		'items'         => array(),
		'error'         => $e->getMessage(),
	));
}
