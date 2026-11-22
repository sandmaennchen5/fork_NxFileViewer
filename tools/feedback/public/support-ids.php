<?php
declare(strict_types=1);
require_once dirname(__DIR__).'/store/web-language.php';
if (defined('NX_ADMIN_SECTION')) adminRequirePermission('view_support');

if (!defined('NX_ADMIN_SECTION') || NX_ADMIN_SECTION !== 'support-ids' || empty($_SESSION['poll_admin'])) {
    header('Location: /admin/support/ids', true, 303);
    exit;
}
if (!in_array($_SERVER['REQUEST_METHOD'] ?? 'GET', ['GET', 'HEAD'], true)) {
    header('Allow: GET, HEAD'); http_response_code(405); exit(webT('Methode nicht erlaubt.'));
}
function supportIdEscape(string $s): string { return htmlspecialchars($s, ENT_QUOTES, 'UTF-8'); }
$error = '';
$overview = $result = [];
$search = $filter = '';
$page = 1;
try {
    $config = require dirname(__DIR__) . '/store/config.php';
    require_once dirname(__DIR__) . '/store/database.php';
    require_once dirname(__DIR__) . '/store/support-overview.php';
    $db = databaseOpen($config);
    supportSchema($db);
    usageSchema($db);
    $days = usagePeriodDays($config);
    $search = $_GET['q'] ?? '';
    $filter = $_GET['usage'] ?? 'all';
    $page = filter_var($_GET['page'] ?? 1, FILTER_VALIDATE_INT, ['options'=>['min_range'=>1,'max_range'=>1000000]]);
    if (!is_string($search) || !is_string($filter) || $page === false) throw new InvalidArgumentException();
    $now = time();
    $overview = supportOverview($db, $days, $now);
    $result = supportIdOverviewRows($db, $search, $filter, $days, $page, $now);
    $cutoff = $now - $days * 86400;
} catch (Throwable $e) {
    http_response_code($e instanceof InvalidArgumentException ? 400 : 503);
    $error = $e instanceof InvalidArgumentException ? webT('Ungültige Suche oder Filterauswahl.') : webT('Die Support-ID-Übersicht ist derzeit nicht verfügbar.');
}
?>
<!doctype html><html lang="<?=webLanguage()?>"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title><?=webT('Support-IDs – NxFileViewer Verwaltung')?></title>
<style>:root{color-scheme:light dark;font-family:system-ui,sans-serif}body{margin:30px;line-height:1.5}.cards{display:flex;flex-wrap:wrap;gap:16px;margin:20px 0}.card{padding:16px;border:1px solid #8886;border-radius:10px}.card strong{display:block;font-size:1.6rem}form{display:flex;gap:12px;flex-wrap:wrap;align-items:end}input,select,button{padding:10px;font:inherit}table{border-collapse:collapse;width:100%}th,td{padding:12px;text-align:left;border-bottom:1px solid #8886;vertical-align:top}.table-scroll{overflow-x:auto}code{overflow-wrap:anywhere}</style>
<h1><?=webT('Vergebene Support-IDs')?></h1>
<?php adminNavigation('support'); ?>
<p><a href="/admin/support"><?=webT('Issues und private Logs')?></a> · <a href="/admin/support/ids" aria-current="page">Support-IDs</a></p>
<?php if ($error !== ''): ?><p role="alert"><?= supportIdEscape(webT($error)) ?></p>
<?php else: ?>
<div class="cards">
<div class="card"><?=webT('Registrierte Support-IDs')?><strong><?= $overview['total'] ?></strong></div>
<div class="card"><?=webT('Mindestens eine Statistikmeldung')?><strong><?= $overview['with_usage'] ?></strong></div>
<div class="card"><?=webT('Statistikmeldung in ')?><?= $days ?><?=webT(' Tagen')?><strong><?= $overview['active_usage'] ?></strong></div>
<div class="card"><?=webT('Noch keine Statistikmeldung')?><strong><?= $overview['without_usage'] ?></strong></div>
</div>
<p><?=webT('Jede Support-ID zählt einmal, unabhängig von der Anzahl ihrer Issues. Es sind nur serverseitig registrierte IDs erfasst, keine vollständige Zahl aller Programminstallationen.')?></p>
<p><?=webT('Eine fehlende Statistikmeldung bedeutet nicht automatisch „deaktiviert“: Das Programm kann länger nicht gestartet worden oder offline sein. Die tatsächliche Einstellung wird nicht gemeldet.')?></p>
<?php if ($overview['unmapped_usage'] > 0): ?><p><?= $overview['unmapped_usage'] ?><?=webT(' ältere Statistikkennungen haben noch keine registrierte öffentliche Support-ID. Sie zählen hier nicht als Support-ID. Bei ihrer nächsten Statistikmeldung wird die Zuordnung ergänzt.')?></p><?php endif ?>
<form method="get">
<label><?=webT('Support-ID oder Name')?><br><input name="q" maxlength="200" value="<?= supportIdEscape($search) ?>"></label>
<label><?=webT('Nutzungsstatistik')?><br><select name="usage"><?php foreach (['all'=>webT('Alle'), 'reported'=>webT('Mindestens eine Meldung'), 'active'=>strtr(webT('Meldung in {days} Tagen'),['{days}'=>(string)$days]), 'inactive'=>strtr(webT('Letzte Meldung älter als {days} Tage'),['{days}'=>(string)$days]), 'never'=>webT('Noch keine Meldung')] as $value=>$text): ?><option value="<?= $value ?>" <?= $filter===$value?'selected':'' ?>><?= supportIdEscape($text) ?></option><?php endforeach ?></select></label>
<button><?=webT('Filtern')?></button><a href="/admin/support/ids"><?=webT('Zurücksetzen')?></a></form>
<p><?= $result['total'] ?><?=webT(' passende Support-ID(s), Seite ')?><?= $page ?><?=webT('. Bis zu 100 IDs pro Seite.')?></p>
<div class="table-scroll"><table><thead><tr><th>Support-ID</th><th>Name</th><th><?=webT('Erstmals registriert (UTC)')?></th><th><?=webT('Letzter Kontakt (UTC)')?></th><th>Issues</th><th><?=webT('Nutzungsstatistik')?></th><th><?=webT('Letzte gemeldete Umgebung')?></th></tr></thead><tbody>
<?php foreach ($result['rows'] as $row): ?>
<tr><td><a href="/admin/support?q=<?= rawurlencode($row['support_id']) ?>"><?= supportIdEscape($row['support_id']) ?></a></td>
<td><?= supportIdEscape($row['display_name']) ?></td><td><?= gmdate('Y-m-d H:i:s',(int)$row['created_at']) ?></td><td><?= gmdate('Y-m-d H:i:s',(int)$row['updated_at']) ?></td>
<td><?= (int)$row['issue_count'] ?></td>
<td><?php if ($row['last_seen'] === null): ?><?=webT('Noch keine Meldung')?><?php else: ?><?= (int)$row['last_seen'] >= $cutoff ? strtr(webT('Meldung in {days} Tagen'),['{days}'=>(string)$days]) : webT('Keine aktuelle Meldung') ?><br><?= gmdate('Y-m-d H:i:s',(int)$row['last_seen']) ?> UTC<?php endif ?></td>
<td><?= supportIdEscape(implode(' · ',array_filter([$row['version'],$row['platform'],$row['process_architecture'],$row['ui_framework']],fn($s)=>$s!==null && $s!==''))) ?></td></tr>
<?php endforeach ?></tbody></table></div>
<?php if (!$result['rows']): ?><p><?=webT('Keine passenden Support-IDs.')?></p><?php endif ?>
<p><?php if ($page>1): ?><a href="?<?= supportIdEscape(http_build_query(['q'=>$search,'usage'=>$filter,'page'=>$page-1])) ?><?=webT('">Vorherige Seite')?></a><?php endif ?>
<?php if ($page*100<$result['total']): ?> · <a href="?<?= supportIdEscape(http_build_query(['q'=>$search,'usage'=>$filter,'page'=>$page+1])) ?><?=webT('">Nächste Seite')?></a><?php endif ?></p>
<?php endif ?></html>
