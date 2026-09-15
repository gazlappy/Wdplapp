<?php
declare(strict_types=1);

/**
 * An expected, client-visible failure. Anything else is a bug and becomes a
 * generic 500 so internal detail never reaches the caller.
 */
final class ApiError extends RuntimeException
{
    /** @var int HTTP status to return. */
    public $status;

    /**
     * Machine-readable code. Named errorCode, not code, because Exception
     * already declares its own int $code which cannot be redeclared.
     */
    public $errorCode;

    public function __construct(int $status, string $errorCode, string $message)
    {
        parent::__construct($message);
        $this->status = $status;
        $this->errorCode = $errorCode;
    }
}

/**
 * A refusal that carries the current state back with it.
 *
 * Used when two captains edit the same card at once: telling someone "that
 * failed" is not enough, because they need to see what the card actually looks
 * like now in order to decide what to do. The payload rides along with the
 * error rather than forcing a second request that could itself be stale.
 */
final class ApiConflict extends RuntimeException
{
    /** @var array */
    public $current;

    public function __construct(array $current, string $message = 'Someone else updated this first.')
    {
        parent::__construct($message);
        $this->current = $current;
    }
}

/**
 * Request/response plumbing. Every response on every path goes through here so
 * there is exactly one error shape and one set of headers.
 */
final class Http
{
    /** @var array|null */
    private static $body = null;

    /** @var int|null Overrides the configured body limit for one request. */
    private static $maxBytes = null;

    public static function bootstrap(): void
    {
        header('Content-Type: application/json; charset=utf-8');
        header('X-Content-Type-Options: nosniff');
        header('Referrer-Policy: no-referrer');
        header('Cache-Control: no-store');
        header_remove('X-Powered-By');

        $origin = (string)Config::get('allowed_origin', '');
        $sent = (string)($_SERVER['HTTP_ORIGIN'] ?? '');
        if ($origin !== '' && $sent !== '' && hash_equals($origin, $sent)) {
            header('Access-Control-Allow-Origin: ' . $origin);
            header('Access-Control-Allow-Credentials: true');
            header('Access-Control-Allow-Headers: Content-Type, Authorization');
            header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
            header('Vary: Origin');
        }

        if (self::method() === 'OPTIONS') {
            http_response_code(204);
            exit;
        }
    }

    public static function method(): string
    {
        return strtoupper((string)($_SERVER['REQUEST_METHOD'] ?? 'GET'));
    }

    /**
     * True only when the request genuinely arrived over TLS. A forwarded header
     * is trusted only when the sidecar explicitly says a proxy terminates TLS,
     * because anyone can send that header directly.
     */
    public static function isSecure(): bool
    {
        $https = strtolower((string)($_SERVER['HTTPS'] ?? ''));
        if ($https !== '' && $https !== 'off') {
            return true;
        }
        if ((int)($_SERVER['SERVER_PORT'] ?? 0) === 443) {
            return true;
        }
        if (Config::get('behind_tls_proxy', false) === true) {
            $proto = strtolower((string)($_SERVER['HTTP_X_FORWARDED_PROTO'] ?? ''));
            return $proto === 'https';
        }
        return false;
    }

    public static function requireSecure(): void
    {
        if (!self::isSecure() && Config::get('allow_insecure', false) !== true) {
            throw new ApiError(403, 'https_required', 'This API requires HTTPS.');
        }
    }

    /** Query-string parameter. */
    public static function query(string $name, string $default = ''): string
    {
        $value = $_GET[$name] ?? $default;
        return is_string($value) ? trim($value) : $default;
    }

    /** Decoded JSON request body, or an empty array for GET/empty bodies. */
    public static function body(): array
    {
        if (self::$body !== null) {
            return self::$body;
        }
        $raw = file_get_contents('php://input');
        if ($raw === false || $raw === '') {
            return self::$body = [];
        }
        $max = self::$maxBytes !== null
            ? self::$maxBytes
            : (int)Config::get('max_body_bytes', 262144);
        if (strlen($raw) > $max) {
            throw new ApiError(413, 'body_too_large', 'Request body is too large.');
        }
        $decoded = json_decode($raw, true);
        if (!is_array($decoded)) {
            throw new ApiError(400, 'bad_json', 'Request body must be a JSON object.');
        }
        return self::$body = $decoded;
    }

    /**
     * Raises the body limit for the current request only.
     *
     * The configured limit constrains anonymous callers. An authenticated
     * admin pushing a whole season legitimately sends far more, so that action
     * opts in explicitly rather than the limit being raised for everyone.
     * Must be called before the body is first read.
     */
    public static function setMaxBytes(int $bytes): void
    {
        self::$maxBytes = $bytes;
    }

    public static function field(string $name, $default = null)
    {
        return self::body()[$name] ?? $default;
    }

    public static function requireField(string $name)
    {
        $value = self::body()[$name] ?? null;
        if ($value === null || $value === '') {
            throw new ApiError(400, 'missing_field', "Missing required field: {$name}.");
        }
        return $value;
    }

    public static function ok($data = null): void
    {
        http_response_code(200);
        echo json_encode(['ok' => true, 'data' => $data], JSON_UNESCAPED_SLASHES);
        exit;
    }

    public static function fail(int $status, string $code, string $message): void
    {
        http_response_code($status);
        echo json_encode(
            ['ok' => false, 'error' => ['code' => $code, 'message' => $message]],
            JSON_UNESCAPED_SLASHES
        );
        exit;
    }

    /** A refusal that still returns the current state, so the caller can catch up. */
    public static function conflict(string $message, array $current): void
    {
        http_response_code(409);
        echo json_encode(
            [
                'ok'    => false,
                'error' => ['code' => 'conflict', 'message' => $message],
                'data'  => $current,
            ],
            JSON_UNESCAPED_SLASHES
        );
        exit;
    }
}
