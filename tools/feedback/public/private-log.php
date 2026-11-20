<?php
declare(strict_types=1);
if (defined('NX_ADMIN_SECTION')) adminRequirePermission('download_logs');

header('Cache-Control: no-store');
header('X-Content-Type-Options: nosniff');
header('Referrer-Policy: no-referrer');
header("Content-Security-Policy: default-src 'none'; sandbox");
if (!defined('NX_ADMIN_SECTION') || NX_ADMIN_SECTION !== 'support-log' || empty($_SESSION['poll_admin'])) {
    http_response_code(403);
    exit('Anmeldung erforderlich.');
}
$number = filter_var($_GET['issue'] ?? null, FILTER_VALIDATE_INT, ['options' => ['min_range' => 1]]);
$reportTicket=$_GET['report']??null;
if($reportTicket!==null&&(!is_string($reportTicket)||!preg_match('/\A[a-f0-9]{64}\z/',$reportTicket))){http_response_code(400);exit('Ungültige Kennung.');}
if (!$number && $reportTicket===null) {
    http_response_code(404);
    exit('Log nicht gefunden.');
}
session_write_close();
try {
    $config = require dirname(__DIR__) . '/store/config.php';
    require_once dirname(__DIR__) . '/store/database.php';
    require_once dirname(__DIR__) . '/store/private-logs.php';
    $db = databaseOpen($config);
    privateLogSchema($db);
    $s = $db->prepare('SELECT gzip_data, original_size FROM private_issue_logs WHERE '.($reportTicket!==null?'ticket':'issue_number').' = ?');
    $s->execute([$reportTicket??$number]);
    $row = $s->fetch(PDO::FETCH_ASSOC);
    $s->closeCursor();
    if (!$row) {
        http_response_code(404);
        exit('Log nicht gefunden.');
    }
    $original = @gzdecode($row['gzip_data'], PRIVATE_LOG_MAX_BYTES + 1);
    if ($original === false || strlen($original) !== (int)$row['original_size'])
        throw new RuntimeException('Invalid log data');
    header('Content-Type: text/plain; charset=utf-8');
    header('Content-Disposition: attachment; filename="NxFileViewer-Issue-' . $number . '-session.log"');
    header('Content-Length: ' . strlen($original));
    echo $original;
} catch (Throwable $e) {
    http_response_code(503);
    echo 'Das Sitzungslog ist derzeit nicht verfügbar.';
}
