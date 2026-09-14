<?php
declare(strict_types=1);

/**
 * Creates each module's tables and records the version that was applied.
 *
 * Installation is explicit and admin-only: nothing creates tables as a side
 * effect of serving a request.
 */
final class Schema
{
    private const CORE = [
        "CREATE TABLE IF NOT EXISTS wdpl_schema (
            module        VARCHAR(64)  NOT NULL,
            applied_version INT        NOT NULL,
            applied_at    DATETIME     NOT NULL,
            PRIMARY KEY (module)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS wdpl_auth_attempts (
            id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
            bucket       VARCHAR(32)  NOT NULL,
            client       CHAR(64)     NOT NULL,
            attempted_at DATETIME     NOT NULL,
            PRIMARY KEY (id),
            KEY idx_bucket_client (bucket, client, attempted_at)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
    ];

    public static function installCore(): void
    {
        foreach (self::CORE as $ddl) {
            Db::pdo()->exec($ddl);
        }
    }

    /** Installs every discovered module. Safe to run repeatedly. */
    public static function installAll(): array
    {
        self::installCore();

        $applied = [];
        foreach (Registry::all() as $id => $class) {
            $applied[] = self::installModule($class);
        }
        return $applied;
    }

    /** @param class-string<Module> $class */
    public static function installModule(string $class): array
    {
        $id      = $class::id();
        $version = $class::schemaVersion();
        $current = self::appliedVersion($id);

        if ($current === $version) {
            return ['id' => $id, 'version' => $version, 'changed' => false];
        }

        foreach ($class::tables() as $ddl) {
            Db::pdo()->exec($ddl);
        }

        Db::query(
            'INSERT INTO wdpl_schema (module, applied_version, applied_at)
             VALUES (?, ?, UTC_TIMESTAMP())
             ON DUPLICATE KEY UPDATE applied_version = VALUES(applied_version), applied_at = VALUES(applied_at)',
            [$id, $version]
        );

        return ['id' => $id, 'version' => $version, 'changed' => true, 'previous' => $current];
    }

    public static function appliedVersion(string $moduleId): ?int
    {
        try {
            $value = Db::value('SELECT applied_version FROM wdpl_schema WHERE module = ?', [$moduleId]);
        } catch (PDOException) {
            // wdpl_schema itself does not exist yet.
            return null;
        }
        return $value === null ? null : (int)$value;
    }

    /** Per-module applied vs expected, for the app's health view. */
    public static function status(): array
    {
        $out = [];
        foreach (Registry::all() as $id => $class) {
            $applied  = self::appliedVersion($id);
            $expected = $class::schemaVersion();
            $out[] = [
                'id'       => $id,
                'title'    => $class::title(),
                'expected' => $expected,
                'applied'  => $applied,
                'current'  => $applied === $expected,
            ];
        }
        return $out;
    }
}
