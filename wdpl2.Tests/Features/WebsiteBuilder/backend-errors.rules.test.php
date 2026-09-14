<?php
require __DIR__ . '/../../../wdpl2/web-backend/api/_db.php';
$handler = set_exception_handler(function ($error) {});
restore_exception_handler();
if (!is_callable($handler)) throw new RuntimeException('Global handler missing');
// Inspect a separate process so the handler exit code is tested as well.
$file = tempnam(sys_get_temp_dir(), 'wdpl-error-');
try {
	file_put_contents($file, '<?php require ' . var_export(realpath(__DIR__ . '/../../../wdpl2/web-backend/api/_db.php'), true) . '; throw new RuntimeException("PRIVATE_SENTINEL");');
	$pipes = array();
	$process = proc_open(array(PHP_BINARY, $file), array(0 => array('pipe','r'), 1 => array('pipe','w'), 2 => array('pipe','w')), $pipes);
	if (!is_resource($process)) throw new RuntimeException('Cannot launch PHP');
	fclose($pipes[0]);
	$output = stream_get_contents($pipes[1]); fclose($pipes[1]);
	$errors = stream_get_contents($pipes[2]); fclose($pipes[2]);
	$status = proc_close($process);
	if ($status === 0 || strpos($output . $errors, 'PRIVATE_SENTINEL') !== false) throw new RuntimeException('Unsafe exception response');
	$json = json_decode($output, true, 512, JSON_THROW_ON_ERROR);
	if (($json['error'] ?? '') !== 'server_exception' || isset($json['where'])) throw new RuntimeException('Invalid error response');
	echo "Backend exception rules passed\n";
} finally { unlink($file); }
