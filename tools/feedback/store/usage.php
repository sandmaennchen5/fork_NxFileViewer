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
    $db->exec(databaseSql($db,
        'CREATE TABLE IF NOT EXISTS usage_report_modes (internal_id TEXT PRIMARY KEY,report_type TEXT NOT NULL,system_reports_enabled INTEGER NULL,standard_last_seen INTEGER NULL,system_last_seen INTEGER NULL)',
        'CREATE TABLE IF NOT EXISTS usage_report_modes (internal_id CHAR(64) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,report_type VARCHAR(16) NOT NULL,system_reports_enabled TINYINT NULL,standard_last_seen BIGINT NULL,system_last_seen BIGINT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin'));
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

function usageRecordMode(PDO $db,string $internal,string $type,?bool $enabled,int $now): void {
    $s=$db->prepare(databaseSql($db,
        'INSERT INTO usage_report_modes (internal_id,report_type,system_reports_enabled,standard_last_seen,system_last_seen) VALUES (?,?,?,?,?)
        ON CONFLICT(internal_id) DO UPDATE SET report_type=excluded.report_type,system_reports_enabled=excluded.system_reports_enabled,
        standard_last_seen=COALESCE(excluded.standard_last_seen,usage_report_modes.standard_last_seen),system_last_seen=COALESCE(excluded.system_last_seen,usage_report_modes.system_last_seen)',
        'INSERT INTO usage_report_modes (internal_id,report_type,system_reports_enabled,standard_last_seen,system_last_seen) VALUES (?,?,?,?,?)
        ON DUPLICATE KEY UPDATE report_type=VALUES(report_type),system_reports_enabled=VALUES(system_reports_enabled),
        standard_last_seen=COALESCE(VALUES(standard_last_seen),standard_last_seen),system_last_seen=COALESCE(VALUES(system_last_seen),system_last_seen)'));
    $s->execute([$internal,$type,$enabled===null?null:(int)$enabled,$type==='standard'?$now:null,$type==='system'?$now:null]);
}
function usageModeLabel(?string $type,?int $enabled): string {
    $kind=match($type){'standard'=>webT('Standardreport'),'system'=>webT('Systembericht'),default=>webT('Älterer Bericht')};
    return $kind.' · '.($enabled===null?webT('Einstellung unbekannt'):($enabled?webT('Systemberichte aktiviert (7 Tage)'):webT('Nur Standardreport')));
}
