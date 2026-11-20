<?php
declare(strict_types=1);

function databaseIsMariaDb(PDO $db): bool {
    return $db->getAttribute(PDO::ATTR_DRIVER_NAME) === 'mysql';
}
function databaseSql(PDO $db, string $sqlite, string $mariadb): string {
    return databaseIsMariaDb($db) ? $mariadb : $sqlite;
}
function databaseSqliteSchema(PDO $db, string $sql): void {
    if (!databaseIsMariaDb($db)) $db->exec($sql);
}
function databaseTables(): array {
    $id = 'CHAR(64) CHARACTER SET ascii COLLATE ascii_bin';
    $bucket = 'VARCHAR(128) CHARACTER SET ascii COLLATE ascii_bin';
    return [
        'support_profiles' => "internal_id $id PRIMARY KEY, support_id VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL UNIQUE,
            display_name VARCHAR(160) NOT NULL DEFAULT '', created_at BIGINT NOT NULL, updated_at BIGINT NOT NULL",
        'support_issues' => "issue_number BIGINT PRIMARY KEY, internal_id $id NOT NULL, display_name VARCHAR(160) NOT NULL,
            created_at BIGINT NOT NULL, INDEX support_issue_owner(internal_id)",
        'feedback_queue' => "ticket CHAR(64) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
            request_key CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL UNIQUE,
            mode VARCHAR(16) NOT NULL, title TEXT NOT NULL, body MEDIUMTEXT NOT NULL,
            internal_id CHAR(64) NULL, support_id VARCHAR(32) NOT NULL, display_name VARCHAR(160) NOT NULL,
            status VARCHAR(24) NOT NULL, number BIGINT NULL, author VARCHAR(128) NOT NULL DEFAULT '',
            credential TEXT NULL, credential_expires BIGINT NOT NULL DEFAULT 0,
            attempts INT NOT NULL DEFAULT 0, next_attempt BIGINT NOT NULL, lease_until BIGINT NOT NULL DEFAULT 0,
            last_error VARCHAR(64) NOT NULL DEFAULT '', created_at BIGINT NOT NULL, updated_at BIGINT NOT NULL,
            INDEX feedback_due(status,next_attempt), INDEX feedback_created(created_at)",
        'reports' => "ticket $id PRIMARY KEY,state $id UNIQUE,verifier VARCHAR(128),status VARCHAR(16),expires BIGINT,INDEX report_expiry(expires)",
        'rate_limit' => "bucket $bucket PRIMARY KEY, count INT, expires BIGINT",
        'support_rate' => "bucket $bucket PRIMARY KEY, count INT, expires BIGINT",
        'admin_users' => "id VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
            username VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL UNIQUE,
            password_hash VARCHAR(255) NOT NULL, permissions TEXT NOT NULL,
            totp_required TINYINT NOT NULL DEFAULT 0, totp_secret TEXT NULL,
            totp_last_step BIGINT NOT NULL DEFAULT -1, recovery_hashes TEXT NOT NULL,
            auth_version BIGINT NOT NULL DEFAULT 1, created_at BIGINT NOT NULL,
            updated_at BIGINT NOT NULL, last_login_at BIGINT NULL",
        'admin_login_rate' => "bucket $bucket PRIMARY KEY, attempts INT, expires BIGINT",
        'private_issue_logs' => "ticket $id PRIMARY KEY, issue_number BIGINT UNIQUE, gzip_data MEDIUMBLOB NOT NULL,
            original_size INT NOT NULL, created_at BIGINT NOT NULL",
        'polls' => "id BIGINT PRIMARY KEY, question TEXT NOT NULL, closed TINYINT NOT NULL DEFAULT 0, kind VARCHAR(12) NOT NULL DEFAULT 'choice', show_in_app TINYINT NOT NULL DEFAULT 1, show_results_in_app TINYINT NOT NULL DEFAULT 1, notify_in_app TINYINT NOT NULL DEFAULT 1",
        'poll_options' => 'id BIGINT AUTO_INCREMENT PRIMARY KEY, poll_id BIGINT NOT NULL, label TEXT NOT NULL, active TINYINT NOT NULL DEFAULT 1, position INT NOT NULL DEFAULT 0, INDEX poll_option_owner(poll_id)',
        'poll_translations' => 'poll_id BIGINT NOT NULL, language VARCHAR(2) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, question TEXT NOT NULL, PRIMARY KEY(poll_id,language)',
        'poll_option_translations' => 'option_id BIGINT NOT NULL, language VARCHAR(2) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, label TEXT NOT NULL, PRIMARY KEY(option_id,language)',
        'poll_votes' => "poll_id BIGINT NOT NULL, participant $id NOT NULL, option_id BIGINT NOT NULL,
            PRIMARY KEY(poll_id, participant), INDEX poll_vote_option(poll_id, option_id)",
        'poll_responses' => "poll_id BIGINT NOT NULL, participant $id NOT NULL, kind VARCHAR(12) NOT NULL, answer TEXT NULL, created_at BIGINT NOT NULL, PRIMARY KEY(poll_id,participant)",
        'poll_rate' => "bucket $bucket PRIMARY KEY, count INT NOT NULL, expires BIGINT NOT NULL",
        'poll_sequence' => 'singleton TINYINT PRIMARY KEY, last_id BIGINT NOT NULL',
        'usage_statistics' => "internal_id $id PRIMARY KEY, version VARCHAR(128) NOT NULL, build_version VARCHAR(256) NOT NULL,
            platform VARCHAR(64) NOT NULL, os_description VARCHAR(512) NOT NULL,
            process_architecture VARCHAR(32) NOT NULL, os_architecture VARCHAR(32) NOT NULL,
            ui_framework VARCHAR(64) NOT NULL, distribution VARCHAR(128) NOT NULL,
            runtime_version VARCHAR(128) NOT NULL, runtime_identifier VARCHAR(128) NOT NULL,
            last_seen BIGINT NOT NULL, INDEX usage_last_seen(last_seen)",
        'usage_daily' => "internal_id $id NOT NULL, day DATE NOT NULL, version VARCHAR(128) NOT NULL,
            platform VARCHAR(64) NOT NULL, process_architecture VARCHAR(32) NOT NULL,
            ui_framework VARCHAR(64) NOT NULL, distribution VARCHAR(128) NOT NULL, last_seen BIGINT NOT NULL,
            PRIMARY KEY(internal_id, day), INDEX usage_daily_seen(last_seen)",
        'usage_history_state' => 'singleton TINYINT PRIMARY KEY, start_day DATE NOT NULL',
    ];
}
// Initial installation only. Existing test databases are not upgraded.
function databaseMariaDbSchema(PDO $db): void {
    $db->exec('CREATE TABLE IF NOT EXISTS feedback_initialized (singleton TINYINT PRIMARY KEY) ENGINE=InnoDB');
    if($db->query('SELECT singleton FROM feedback_initialized WHERE singleton=1')->fetchColumn()!==false)return;
    foreach(databaseTables() as $table=>$columns)
        $db->exec("CREATE TABLE IF NOT EXISTS `$table` ($columns) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin");
    $db->exec('INSERT IGNORE INTO feedback_initialized (singleton) VALUES (1)');
}
function databaseOpen(array $config): PDO {
    if (!empty($config['database_maintenance'])) throw new RuntimeException('Database maintenance');
    $driver = $config['database_driver'] ?? 'sqlite';
    $options = [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_STRINGIFY_FETCHES => false];
    if ($driver === 'sqlite') {
        $db = new PDO('sqlite:' . $config['database'], options: $options);
        $db->exec('PRAGMA busy_timeout = 5000');
        return $db;
    }
    if (!in_array($driver, ['mariadb', 'mysql'], true)) throw new InvalidArgumentException('Unknown database driver');
    $settings = $config['mariadb'] ?? [];
    foreach (['host', 'database', 'user', 'password'] as $key)
        if (!is_string($settings[$key] ?? null) || $settings[$key] === '' || str_contains($settings[$key], "\0"))
            throw new InvalidArgumentException('Incomplete MariaDB configuration');
    if (!preg_match('/\A[a-zA-Z0-9_.-]+\z/', $settings['host']) ||
        !preg_match('/\A[a-zA-Z0-9_-]+\z/', $settings['database']))
        throw new InvalidArgumentException('Invalid MariaDB host or database');
    $port = filter_var($settings['port'] ?? 3306, FILTER_VALIDATE_INT, ['options' => ['min_range' => 1, 'max_range' => 65535]]);
    if (!$port) throw new InvalidArgumentException('Invalid MariaDB port');
    $options[PDO::ATTR_EMULATE_PREPARES] = false;
    $foundRows = defined('Pdo\Mysql::ATTR_FOUND_ROWS') ? constant('Pdo\Mysql::ATTR_FOUND_ROWS') : constant('PDO::MYSQL_ATTR_FOUND_ROWS');
    $options[$foundRows] = false;
    $db = new PDO('mysql:host=' . $settings['host'] . ';port=' . $port . ';dbname=' . $settings['database'] . ';charset=utf8mb4',
        $settings['user'], $settings['password'], $options);
    $db->exec("SET time_zone = '+00:00'");
    $db->exec("SET SESSION sql_mode = CONCAT_WS(',', @@sql_mode, 'STRICT_ALL_TABLES', 'NO_AUTO_VALUE_ON_ZERO')");
    databaseMariaDbSchema($db);
    return $db;
}
