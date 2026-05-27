<?php
// _smtp.php — tiny SMTP client (no PHPMailer dependency). PHP 5.6 compatible.
// Reads config from league_settings keys:
//   smtp_host, smtp_port (default 587), smtp_user, smtp_pass,
//   smtp_secure (tls|ssl|none — default tls/STARTTLS on 587, ssl on 465),
//   smtp_from   (from address, falls back to no-reply@host)
//   smtp_from_name (display name, default "WDPL Admin")
// Returns array('ok'=>bool, 'error'=>string|null).

function smtp_config() {
    $cfg = array(
        'host' => '', 'port' => 587, 'user' => '', 'pass' => '',
        'secure' => 'tls', 'from' => '', 'from_name' => 'WDPL Admin'
    );
    try {
        $rows = db()->query('SELECT setting_key, setting_value FROM league_settings WHERE setting_key LIKE "smtp_%"')->fetchAll();
        foreach ($rows as $r) {
            $k = substr($r['setting_key'], 5);
            if ($k === 'port') $cfg['port'] = (int)$r['setting_value'];
            else if (isset($cfg[$k])) $cfg[$k] = $r['setting_value'];
        }
    } catch (Exception $e) {}
    return $cfg;
}

function smtp_is_configured() {
    $c = smtp_config();
    return $c['host'] !== '' && $c['from'] !== '';
}

function _smtp_cmd($conn, $cmd, &$reply, $expect = null) {
    if ($cmd !== null) fwrite($conn, $cmd . "\r\n");
    $reply = '';
    while (!feof($conn)) {
        $line = fgets($conn, 4096);
        if ($line === false) break;
        $reply .= $line;
        if (preg_match('/^\d{3} /', $line)) break;
    }
    if ($expect !== null) {
        if (substr($reply, 0, 1) === '') return false;
        $code = (int)substr($reply, 0, 3);
        if ($code !== $expect) return false;
    }
    return true;
}

function smtp_send($to, $subject, $body, $reply_to = null) {
    $c = smtp_config();
    if ($c['host'] === '' || $c['from'] === '') return array('ok' => false, 'error' => 'SMTP not configured');
    $port = $c['port'] ?: 587;
    $secure = strtolower($c['secure']);
    if ($secure === 'ssl' || $port === 465) {
        $remote = 'ssl://' . $c['host'] . ':' . $port;
    } else {
        $remote = $c['host'] . ':' . $port;
    }
    $errno = 0; $errstr = '';
    $conn = @stream_socket_client($remote, $errno, $errstr, 15);
    if (!$conn) return array('ok' => false, 'error' => "connect failed: $errstr ($errno)");
    stream_set_timeout($conn, 15);

    $reply = '';
    if (!_smtp_cmd($conn, null, $reply, 220)) { fclose($conn); return array('ok'=>false, 'error'=>'banner: '.$reply); }
    $host = isset($_SERVER['HTTP_HOST']) ? preg_replace('/[^a-z0-9\.\-]/i', '', $_SERVER['HTTP_HOST']) : 'localhost';
    if (!_smtp_cmd($conn, "EHLO $host", $reply, 250)) { fclose($conn); return array('ok'=>false, 'error'=>'EHLO: '.$reply); }
    if ($secure === 'tls' && (int)$port !== 465) {
        if (!_smtp_cmd($conn, 'STARTTLS', $reply, 220)) { fclose($conn); return array('ok'=>false, 'error'=>'STARTTLS: '.$reply); }
        if (!@stream_socket_enable_crypto($conn, true, STREAM_CRYPTO_METHOD_TLS_CLIENT)) {
            fclose($conn); return array('ok'=>false, 'error'=>'TLS handshake failed');
        }
        if (!_smtp_cmd($conn, "EHLO $host", $reply, 250)) { fclose($conn); return array('ok'=>false, 'error'=>'EHLO(2): '.$reply); }
    }
    if ($c['user'] !== '') {
        if (!_smtp_cmd($conn, 'AUTH LOGIN', $reply, 334)) { fclose($conn); return array('ok'=>false, 'error'=>'AUTH: '.$reply); }
        if (!_smtp_cmd($conn, base64_encode($c['user']), $reply, 334)) { fclose($conn); return array('ok'=>false, 'error'=>'AUTH user: '.$reply); }
        if (!_smtp_cmd($conn, base64_encode($c['pass']), $reply, 235)) { fclose($conn); return array('ok'=>false, 'error'=>'AUTH pass: '.$reply); }
    }
    if (!_smtp_cmd($conn, 'MAIL FROM:<' . $c['from'] . '>', $reply, 250)) { fclose($conn); return array('ok'=>false, 'error'=>'MAIL FROM: '.$reply); }
    if (!_smtp_cmd($conn, 'RCPT TO:<' . $to . '>', $reply, 250)) { fclose($conn); return array('ok'=>false, 'error'=>'RCPT: '.$reply); }
    if (!_smtp_cmd($conn, 'DATA', $reply, 354)) { fclose($conn); return array('ok'=>false, 'error'=>'DATA: '.$reply); }

    $name = $c['from_name'] ?: 'WDPL Admin';
    $data  = "From: $name <" . $c['from'] . ">\r\n";
    $data .= "To: <$to>\r\n";
    $data .= 'Subject: ' . str_replace(array("\r", "\n"), '', $subject) . "\r\n";
    if ($reply_to) $data .= "Reply-To: <$reply_to>\r\n";
    $data .= "MIME-Version: 1.0\r\nContent-Type: text/plain; charset=UTF-8\r\n";
    $data .= "Date: " . gmdate('r') . "\r\n\r\n";
    // Dot-stuff lines that start with a dot.
    $body = preg_replace('/^\./m', '..', str_replace("\r\n", "\n", $body));
    $body = str_replace("\n", "\r\n", $body);
    $data .= $body . "\r\n.\r\n";
    fwrite($conn, $data);
    if (!_smtp_cmd($conn, null, $reply, 250)) { fclose($conn); return array('ok'=>false, 'error'=>'send: '.$reply); }
    _smtp_cmd($conn, 'QUIT', $reply);
    fclose($conn);
    return array('ok' => true, 'error' => null);
}
