<?php
declare(strict_types=1);
require_once __DIR__ . '/database.php';
function usagePeriodDays(array $config): int {
    $days = filter_var($config['statistics_days'] ?? 30, FILTER_VALIDATE_INT,
        ['options' => ['min_range' => 1, 'max_range' => 3650]]);
    if ($days === false) throw new InvalidArgumentException('Invalid statistics_days');
    return $days;
}
function usageSchema(PDO $db): void {
    databaseSqliteSchema($db, 'CREATE TABLE IF NOT EXISTS usage_statistics (
        internal_id TEXT PRIMARY KEY, version TEXT NOT NULL, build_version TEXT NOT NULL,
        platform TEXT NOT NULL, os_description TEXT NOT NULL,
        process_architecture TEXT NOT NULL, os_architecture TEXT NOT NULL,
        ui_framework TEXT NOT NULL, distribution TEXT NOT NULL,
        runtime_version TEXT NOT NULL, runtime_identifier TEXT NOT NULL, last_seen INTEGER NOT NULL
    );
    CREATE INDEX IF NOT EXISTS usage_last_seen ON usage_statistics(last_seen);
    CREATE TABLE IF NOT EXISTS usage_daily (
        internal_id TEXT NOT NULL, day TEXT NOT NULL, version TEXT NOT NULL,
        platform TEXT NOT NULL, process_architecture TEXT NOT NULL, ui_framework TEXT NOT NULL,
        distribution TEXT NOT NULL, last_seen INTEGER NOT NULL, PRIMARY KEY(internal_id, day)
    );
    CREATE INDEX IF NOT EXISTS usage_daily_seen ON usage_daily(last_seen);
    CREATE TABLE IF NOT EXISTS usage_history_state (singleton INTEGER PRIMARY KEY CHECK(singleton = 1), start_day TEXT NOT NULL)');
    $s=$db->prepare(databaseSql($db,'INSERT OR IGNORE INTO usage_history_state VALUES (1,?)','INSERT IGNORE INTO usage_history_state VALUES (1,?)'));
    $s->execute([gmdate('Y-m-d')]);
}
function usageRecordDay(PDO $db, string $internal, array $values, int $now): void {
    $s = $db->prepare(databaseSql($db, 'INSERT INTO usage_daily VALUES (?, ?, ?, ?, ?, ?, ?, ?)
        ON CONFLICT(internal_id, day) DO UPDATE SET version=excluded.version, platform=excluded.platform,
        process_architecture=excluded.process_architecture, ui_framework=excluded.ui_framework,
        distribution=excluded.distribution, last_seen=excluded.last_seen', 'INSERT INTO usage_daily VALUES (?, ?, ?, ?, ?, ?, ?, ?)
        ON DUPLICATE KEY UPDATE version=VALUES(version), platform=VALUES(platform),
        process_architecture=VALUES(process_architecture), ui_framework=VALUES(ui_framework),
        distribution=VALUES(distribution), last_seen=VALUES(last_seen)'));
    $s->execute([$internal, gmdate('Y-m-d', $now), $values[0], $values[2], $values[4], $values[6], $values[7], $now]);
}
