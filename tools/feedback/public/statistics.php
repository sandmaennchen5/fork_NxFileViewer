<?php
declare(strict_types=1);
require_once dirname(__DIR__).'/store/web-language.php';
if (defined('NX_ADMIN_SECTION')) adminRequirePermission('view_statistics');

if (!defined('NX_ADMIN_SECTION') || NX_ADMIN_SECTION !== 'statistik') {
    header('Location: /admin/statistik', true, 303);
    exit;
}
$rows = [];
$error = '';
try {
    $config = require dirname(__DIR__) . '/store/config.php';
    require_once dirname(__DIR__) . '/store/database.php';
    require_once dirname(__DIR__) . '/store/usage.php';
    $db = databaseOpen($config);
    usageSchema($db);
    $days = usagePeriodDays($config);
    $s = $db->prepare("SELECT version, platform, os_description, process_architecture,
        ui_framework, distribution, COALESCE(d.program_delivery, 'unknown') AS program_delivery, m.report_type,m.system_reports_enabled, COUNT(*) AS devices, MAX(last_seen) AS last_seen
        FROM usage_statistics u LEFT JOIN usage_report_modes m ON m.internal_id=u.internal_id LEFT JOIN usage_program_delivery d ON d.internal_id=u.internal_id WHERE last_seen >= ?
        GROUP BY version, platform, os_description, process_architecture, ui_framework, distribution,COALESCE(d.program_delivery, 'unknown'),m.report_type,m.system_reports_enabled
        ORDER BY version DESC, devices DESC");
    $s->execute([time() - $days * 86400]);
    $rows = $s->fetchAll(PDO::FETCH_ASSOC);
} catch (Throwable $e) {
    http_response_code(503);
    $error = webT('Die Nutzungsstatistik konnte nicht geladen werden.');
}
$total = array_sum(array_column($rows, 'devices'));
?>
<!doctype html><html lang="<?=webLanguage()?>"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title><?=webT('NxFileViewer Nutzungsstatistik')?></title>
<style>:root{color-scheme:light dark;font-family:system-ui,sans-serif}body{margin:30px;line-height:1.5}table{border-collapse:collapse;width:100%}th,td{text-align:left;padding:12px;border-bottom:1px solid #8886}button{padding:8px;font:inherit}</style>
<h1><?=webT('Nutzungsstatistik')?></h1><p><a href="/statistik?lang=<?=webLanguage()?>"><?=webT('Öffentliche Grafiken und README-Links')?></a></p><?php adminNavigation('statistik'); ?>
<p><?= (int)$total ?><?=webT(' Kennungen haben sich in den letzten ')?><?= $days ?? 30 ?><?=webT(' Tagen gemeldet. Jede Kennung zählt mit ihrer zuletzt gemeldeten Version und Umgebung.')?></p>
<p role="alert"><?= adminEscape(webT($error)) ?></p>
<table><thead><tr><th>Version</th><th><?=webT('Plattform / Betriebssystem')?></th><th><?=webT('Architektur')?></th><th><?=webT('Oberfläche')?></th><th><?=webT('Programmauslieferung')?></th><th><?=webT('.NET-Auslieferung')?></th><th><?=webT('Berichtsart')?></th><th><?=webT('Kennungen')?></th><th><?=webT('Letzte Meldung (UTC)')?></th></tr></thead><tbody>
<?php foreach ($rows as $row): ?>
<tr><td><?= adminEscape($row['version']) ?></td><td><?= adminEscape($row['platform']) ?><br><?= adminEscape($row['os_description']) ?></td>
<td><?= adminEscape($row['process_architecture']) ?></td><td><?= adminEscape($row['ui_framework']) ?></td>
<td><?= adminEscape(usageProgramDeliveryLabel($row['program_delivery'])) ?></td><td><?= adminEscape($row['distribution']) ?></td><td><?=adminEscape(usageModeLabel($row['report_type'],$row['system_reports_enabled']===null?null:(int)$row['system_reports_enabled']))?></td><td><?= (int)$row['devices'] ?></td><td><?= gmdate('Y-m-d H:i:s', (int)$row['last_seen']) ?></td></tr>
<?php endforeach ?></tbody></table></html>
