<?php
declare(strict_types=1);
require_once __DIR__ . '/database.php';
const PRIVATE_LOG_MAX_BYTES = 5242880;

function privateLogSchema(PDO $db): void {
    databaseSqliteSchema($db, 'CREATE TABLE IF NOT EXISTS private_issue_logs (
        ticket TEXT PRIMARY KEY, issue_number INTEGER UNIQUE,
        gzip_data BLOB NOT NULL, original_size INTEGER NOT NULL, created_at INTEGER NOT NULL
    )');
    // Queue-owned attachments stay available even before GitHub issue creation.
    require_once __DIR__.'/feedback-queue.php';
    feedbackSchema($db);
    $s = $db->prepare('DELETE FROM private_issue_logs WHERE issue_number IS NULL AND created_at < ? AND ticket NOT IN (SELECT ticket FROM feedback_queue)');
    $s->execute([time() - 86400]);
}
function privateLogDecode(mixed $value): ?array {
    if ($value === null) return null;
    if (!is_array($value) || !is_string($value['gzipBase64'] ?? null) || !is_int($value['size'] ?? null) ||
        $value['size'] < 0 || $value['size'] > PRIVATE_LOG_MAX_BYTES)
        throw new InvalidArgumentException('Invalid private log');
    $compressed = base64_decode($value['gzipBase64'], true);
    if ($compressed === false || strlen($compressed) > PRIVATE_LOG_MAX_BYTES + 65536)
        throw new InvalidArgumentException('Invalid private log');
    $original = @gzdecode($compressed, PRIVATE_LOG_MAX_BYTES + 1);
    if ($original === false || strlen($original) !== $value['size'])
        throw new InvalidArgumentException('Invalid private log');
    return ['gzip' => $compressed, 'size' => $value['size']];
}
function privateLogSave(PDO $db, string $ticket, ?array $log): void {
    if ($log === null) return;
    $s = $db->prepare('INSERT INTO private_issue_logs (ticket, gzip_data, original_size, created_at) VALUES (?, ?, ?, ?)');
    $s->bindValue(1, $ticket);
    $s->bindValue(2, $log['gzip'], PDO::PARAM_LOB);
    $s->bindValue(3, $log['size'], PDO::PARAM_INT);
    $s->bindValue(4, time(), PDO::PARAM_INT);
    $s->execute();
}
function privateLogLink(PDO $db, string $ticket, int $number): void {
    $s = $db->prepare('UPDATE private_issue_logs SET issue_number = ? WHERE ticket = ? AND issue_number IS NULL');
    $s->execute([$number, $ticket]);
}
function privateLogDelete(PDO $db, int $number): void {
    if ($number < 1) throw new InvalidArgumentException('Invalid issue number');
    $s = $db->prepare('DELETE FROM private_issue_logs WHERE issue_number = ?');
    $s->execute([$number]);
}
