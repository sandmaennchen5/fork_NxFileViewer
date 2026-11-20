<?php
declare(strict_types=1);
require_once __DIR__ . '/database.php';

function supportQuery(PDO $db, string $sql, array $args = []): PDOStatement {
    $statement = $db->prepare($sql);
    $statement->execute($args);
    return $statement;
}
function supportSchema(PDO $db): void {
    databaseSqliteSchema($db, 'CREATE TABLE IF NOT EXISTS support_profiles (
        internal_id TEXT PRIMARY KEY, support_id TEXT UNIQUE NOT NULL,
        display_name TEXT NOT NULL DEFAULT "", created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL
    );
    CREATE TABLE IF NOT EXISTS support_issues (
        issue_number INTEGER PRIMARY KEY, internal_id TEXT NOT NULL,
        display_name TEXT NOT NULL, created_at INTEGER NOT NULL
    )');

}
function supportInternalId(array $config, string $participant): string {
    if (!preg_match('/\A[a-f0-9]{64}\z/', $participant))
        throw new InvalidArgumentException('Invalid support participant');
    $secret = $config['support_secret'] ?? $config['poll_secret'] ?? '';
    if (strlen($secret) < 32) throw new RuntimeException('Configure support_secret');
    return hash_hmac('sha256', 'internal:' . $participant, $secret);
}
function supportProfile(PDO $db, array $config, string $participant, ?string $name = null): array {
    if (!preg_match('/\A[a-f0-9]{64}\z/', $participant))
        throw new InvalidArgumentException('Invalid support participant');
    $secret = $config['support_secret'] ?? $config['poll_secret'] ?? '';
    if (strlen($secret) < 32) throw new RuntimeException('Configure support_secret');
    $internal = supportInternalId($config, $participant);
    $public = 'NX-' . implode('-', str_split(strtoupper(substr(
        hash_hmac('sha256', 'public:' . $participant, $secret), 0, 20)), 4));
    $now = time();
    supportQuery($db, databaseSql($db, 'INSERT INTO support_profiles (internal_id, support_id, display_name, created_at, updated_at)
        VALUES (?, ?, ?, ?, ?) ON CONFLICT(internal_id) DO UPDATE SET updated_at = excluded.updated_at', 'INSERT INTO support_profiles (internal_id, support_id, display_name, created_at, updated_at)
        VALUES (?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE updated_at = VALUES(updated_at)'),
        [$internal, $public, $name ?? '', $now, $now]);
    if ($name !== null) supportQuery($db, 'UPDATE support_profiles SET display_name = ? WHERE internal_id = ?', [$name, $internal]);
    return ['internalId' => $internal, 'supportId' => $public];
}
function supportRecordIssue(PDO $db, array $profile, int $number, string $name): void {
    supportQuery($db, databaseSql($db, 'INSERT INTO support_issues (issue_number, internal_id, display_name, created_at)
        VALUES (?, ?, ?, ?) ON CONFLICT(issue_number) DO NOTHING', 'INSERT INTO support_issues (issue_number, internal_id, display_name, created_at)
        VALUES (?, ?, ?, ?) ON DUPLICATE KEY UPDATE issue_number = VALUES(issue_number)'),
        [$number, $profile['internalId'], $name, time()]);
    supportQuery($db, 'UPDATE support_profiles SET display_name = ?, updated_at = ? WHERE internal_id = ?',
        [$name, time(), $profile['internalId']]);
}
