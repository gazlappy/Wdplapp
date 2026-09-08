<?php
// Validate frame edits before opening a transaction or changing a live scorecard.
function admin_scorecard_frames($frames) {
	if (!is_array($frames) || !array_is_list($frames) || count($frames) > 100) throw new InvalidArgumentException('Expected at most 100 frames.');
	$numbers = array();
	$result = array();
	foreach ($frames as $frame) {
		if (!is_array($frame) || !isset($frame['number']) || !is_int($frame['number']) ||
			$frame['number'] < 1 || $frame['number'] > 100 || isset($numbers[$frame['number']]))
			throw new InvalidArgumentException('Frame numbers must be unique integers between 1 and 100.');
		$numbers[$frame['number']] = true;
		if (!isset($frame['is_doubles'], $frame['eight_ball']) || !is_bool($frame['is_doubles']) || !is_bool($frame['eight_ball']))
			throw new InvalidArgumentException('Frame flags must be booleans.');
		$winner = isset($frame['winner']) ? $frame['winner'] : null;
		if (!in_array($winner, array(null, 'none', 'home', 'away'), true)) throw new InvalidArgumentException('Invalid frame winner.');
		$mapped = array('number' => $frame['number'], 'is_doubles' => $frame['is_doubles'],
			'eight_ball' => $frame['eight_ball'], 'winner' => $winner === 'none' ? null : $winner, 'pending_eight' => null);
		foreach (array('home_player_id', 'home_player2_id', 'away_player_id', 'away_player2_id') as $key) {
			$id = isset($frame[$key]) ? $frame[$key] : null;
			if ($id !== null && (!is_string($id) || !preg_match('/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/iD', $id)))
				throw new InvalidArgumentException('Explicit UUID player identities are required.');
			$mapped[$key] = $id === null ? null : strtolower($id);
		}
		if (!$mapped['is_doubles'] && ($mapped['home_player2_id'] !== null || $mapped['away_player2_id'] !== null))
			throw new InvalidArgumentException('Singles cannot include a second player.');
		foreach (array('home', 'away') as $side) {
			if ($mapped[$side . '_player_id'] !== null && $mapped[$side . '_player_id'] !== 'ffffffff-ffff-ffff-ffff-ffffffffffff' &&
				$mapped[$side . '_player_id'] === $mapped[$side . '_player2_id']) throw new InvalidArgumentException('A doubles pair must use distinct players.');
			if ($mapped['winner'] !== null && ($mapped[$side . '_player_id'] === null ||
				($mapped['is_doubles'] && $mapped[$side . '_player2_id'] === null))) throw new InvalidArgumentException('Winning frames require all participants.');
		}
		if ($mapped['eight_ball'] && $mapped['winner'] === null) throw new InvalidArgumentException('Eight-ball requires a winner.');
		$result[] = $mapped;
	}
	usort($result, function ($a, $b) { return $a['number'] <=> $b['number']; });
	return $result;
}

function admin_scorecard_resolve_players($pdo, $frames, $fixture) {
	$query = $pdo->prepare('SELECT full_name FROM league_players WHERE player_id = ? AND season_id = ? AND team_id = ?');
	foreach ($frames as &$frame) {
		foreach (array('home', 'away') as $side) {
			foreach (array('player', 'player2') as $slot) {
				$key = $side . '_' . $slot;
				$id = $frame[$key . '_id'];
				if ($id === null) $name = null;
				elseif ($id === 'ffffffff-ffff-ffff-ffff-ffffffffffff') $name = 'VOID';
				else {
					$query->execute(array($id, $fixture['season_id'], $fixture[$side . '_team_id']));
					$name = $query->fetchColumn();
					if ($name === false) throw new InvalidArgumentException('Player identity is not on the server season team. Review the roster first.');
				}
				$frame[$key . '_name'] = $name;
			}
		}
	}
	unset($frame);
	return $frames;
}
