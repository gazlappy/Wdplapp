<?php
require __DIR__ . '/../../../wdpl2/web-backend/api/_admin_scorecard_rules.php';
function rejects($value) { try { admin_scorecard_frames($value); } catch (InvalidArgumentException $e) { return; } throw new RuntimeException('Invalid frames accepted'); }
$frame = array('number' => 1, 'is_doubles' => false, 'eight_ball' => false, 'winner' => 'home',
	'home_player_id' => '00000000-0000-0000-0000-000000000001', 'away_player_id' => '00000000-0000-0000-0000-000000000002');
if (count(admin_scorecard_frames(array($frame))) !== 1) throw new RuntimeException('Valid frame rejected');
rejects(array($frame, $frame));
$bad = $frame; $bad['home_player_id'] = 'name-not-id'; rejects(array($bad));
$bad = $frame; $bad['winner'] = 'draw'; rejects(array($bad));
$bad = $frame; $bad['eight_ball'] = 1; rejects(array($bad));
$bad = $frame; $bad['winner'] = null; $bad['eight_ball'] = true; rejects(array($bad));
$bad = $frame; $bad['is_doubles'] = true; $bad['home_player2_id'] = $bad['home_player_id']; rejects(array($bad));
$bad = $frame; $bad['home_player_id'] = null; rejects(array($bad));
echo "Scorecard rules passed\n";
