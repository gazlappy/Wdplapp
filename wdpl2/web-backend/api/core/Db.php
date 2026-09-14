<?php
declare(strict_types=1);

/**
 * PDO access plus the locking helpers the ownership model depends on.
 *
 * Shared hosting gives us no coordination primitive outside the database, so
 * every serialisation point in this backend is a real row lock - never a file,
 * never PHP state.
 */
final class Db
{
    /** @var PDO|null */
    private static $pdo = null;

    public static function pdo(): PDO
    {
        if (self::$pdo !== null) {
            return self::$pdo;
        }

        $host = (string)Config::get('db_host', 'localhost');
        $name = (string)Config::require('db_name');
        $user = (string)Config::require('db_user');
        $pass = (string)Config::get('db_password', '');

        $dsn = "mysql:host={$host};dbname={$name};charset=utf8mb4";
        try {
            self::$pdo = new PDO($dsn, $user, $pass, [
                PDO::ATTR_ERRMODE            => PDO::ERRMODE_EXCEPTION,
                PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
                PDO::ATTR_EMULATE_PREPARES   => false,
            ]);
        } catch (PDOException $ignored) {
            // Never surface the DSN or credentials.
            throw new ApiError(503, 'db_unavailable', 'Cannot connect to the database.');
        }
        return self::$pdo;
    }

    public static function query(string $sql, array $params = []): PDOStatement
    {
        $stmt = self::pdo()->prepare($sql);
        $stmt->execute($params);
        return $stmt;
    }

    public static function all(string $sql, array $params = []): array
    {
        return self::query($sql, $params)->fetchAll();
    }

    public static function one(string $sql, array $params = []): ?array
    {
        $row = self::query($sql, $params)->fetch();
        return $row === false ? null : $row;
    }

    public static function value(string $sql, array $params = [])
    {
        $value = self::query($sql, $params)->fetchColumn();
        return $value === false ? null : $value;
    }

    /**
     * Runs $fn inside a transaction, rolling back on any throw.
     *
     * Ownership transitions must call this and take their row lock inside it,
     * so the check and the write cannot be separated by another request.
     */
    public static function transaction(callable $fn)
    {
        $pdo = self::pdo();
        $pdo->beginTransaction();
        try {
            $result = $fn($pdo);
            $pdo->commit();
            return $result;
        } catch (Throwable $e) {
            if ($pdo->inTransaction()) {
                $pdo->rollBack();
            }
            throw $e;
        }
    }

    /** Locks a single row for the rest of the current transaction. */
    public static function lockRow(string $table, string $column, $value): ?array
    {
        if (!self::pdo()->inTransaction()) {
            throw new LogicException('lockRow must be called inside a transaction.');
        }
        $table  = self::identifier($table);
        $column = self::identifier($column);
        return self::one("SELECT * FROM {$table} WHERE {$column} = ? FOR UPDATE", [$value]);
    }

    /** Whitelist-style guard for the few places a name is interpolated. */
    public static function identifier(string $name): string
    {
        if (!preg_match('/^[A-Za-z_][A-Za-z0-9_]*$/', $name)) {
            throw new LogicException("Unsafe SQL identifier: {$name}");
        }
        return '`' . $name . '`';
    }

    public static function serverVersion(): string
    {
        return (string)self::pdo()->getAttribute(PDO::ATTR_SERVER_VERSION);
    }
}
