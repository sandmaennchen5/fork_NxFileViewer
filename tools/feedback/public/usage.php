<?php
declare(strict_types=1);
header('Cache-Control: no-store');
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
ini_set('display_errors', '0');
function usageReply(array $body, int $status): never {
    http_response_code($status);
    echo json_encode($body, JSON_THROW_ON_ERROR);
    exit;
}
try {
    if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'POST') {
        header('Allow: POST');
        usageReply(['error' => 'method'], 405);
    }
    $raw = file_get_contents('php://input', false, null, 0, 8193);
    if ($raw === false || strlen($raw) > 8192) usageReply(['error' => 'size'], 413);
    $input = json_decode($raw, true);
    if (!is_array($input)) usageReply(['error' => 'payload'], 400);
    $participant = $input['participant'] ?? '';
    if (!is_string($participant) || !preg_match('/\A[a-f0-9]{64}\z/', $participant))
        usageReply(['error' => 'participant'], 400);
    $reportType=$input['reportType']??'legacy';
    $enabled=$input['systemReportsEnabled']??null;
    if(!in_array($reportType,['standard','system','legacy'],true)||($enabled!==null&&!is_bool($enabled))||
        ($reportType!=='legacy'&&$enabled===null)||($reportType==='system'&&$enabled!==true))usageReply(['error'=>'report_type'],400);
    $environment = $input['environment'] ?? null;
    if (!is_array($environment)) usageReply(['error' => 'environment'], 400);
    $values = [];
    foreach (['version' => 128, 'buildVersion' => 256] as $key => $limit) {
        $value = $input[$key] ?? '';
        if (!is_string($value) || trim($value) === '' || strlen($value) > $limit || preg_match('/[\x00-\x1F]/', $value))
            usageReply(['error' => $key], 400);
        $values[] = $value;
    }
    foreach (['platform' => 64, 'osDescription' => 512, 'processArchitecture' => 32, 'osArchitecture' => 32,
        'uiFramework' => 64, 'distribution' => 128, 'runtimeVersion' => 128, 'runtimeIdentifier' => 128] as $key => $limit) {
        $value = $environment[$key] ?? '';
        if (!is_string($value) || trim($value) === '' || strlen($value) > $limit || preg_match('/[\x00-\x1F]/', $value))
            usageReply(['error' => $key], 400);
        $values[] = $value;
    }
    $config = require dirname(__DIR__) . '/store/config.php';
    require_once dirname(__DIR__) . '/store/database.php';
    require_once dirname(__DIR__) . '/store/support.php';
    require_once dirname(__DIR__) . '/store/usage.php';
    $db = databaseOpen($config);
    usageSchema($db);
    supportSchema($db);
    $now = time();
    $db->beginTransaction();
    $profile = supportProfile($db, $config, $participant);
    $internal = $profile['internalId'];
    $s = $db->prepare(databaseSql($db, 'INSERT INTO usage_statistics VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        ON CONFLICT(internal_id) DO UPDATE SET version=excluded.version, build_version=excluded.build_version,
        platform=excluded.platform, os_description=excluded.os_description,
        process_architecture=excluded.process_architecture, os_architecture=excluded.os_architecture,
        ui_framework=excluded.ui_framework, distribution=excluded.distribution,
        runtime_version=excluded.runtime_version, runtime_identifier=excluded.runtime_identifier, last_seen=excluded.last_seen', 'INSERT INTO usage_statistics VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        ON DUPLICATE KEY UPDATE version=VALUES(version), build_version=VALUES(build_version),
        platform=VALUES(platform), os_description=VALUES(os_description),
        process_architecture=VALUES(process_architecture), os_architecture=VALUES(os_architecture),
        ui_framework=VALUES(ui_framework), distribution=VALUES(distribution),
        runtime_version=VALUES(runtime_version), runtime_identifier=VALUES(runtime_identifier), last_seen=VALUES(last_seen)'));
    $s->execute(array_merge([$internal], $values, [$now]));
    usageRecordDay($db, $internal, $values, $now);
    usageRecordMode($db,$internal,$reportType,$enabled,$now);
    $db->commit();
    usageReply(['ok' => true], 200);
} catch (Throwable $e) {
    if (isset($db) && $db->inTransaction()) $db->rollBack();
    usageReply(['error' => 'unavailable'], 503);
}
