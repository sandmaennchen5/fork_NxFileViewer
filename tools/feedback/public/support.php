<?php
declare(strict_types=1);
require_once dirname(__DIR__).'/store/web-language.php';
if (defined('NX_ADMIN_SECTION')) adminRequirePermission('view_support');

if (!defined('NX_ADMIN_SECTION')) {
    $search = $_GET['q'] ?? '';
    header('Location: /admin/support' . (is_string($search) && $search !== '' ? '?q=' . rawurlencode($search) : ''), true, 303);
    exit;
}
header('Cache-Control: no-store');
header('Referrer-Policy: no-referrer');
header('X-Content-Type-Options: nosniff');
header("Content-Security-Policy: default-src 'self'; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'; form-action 'self'");
function supportEscape(string $s): string { return htmlspecialchars($s, ENT_QUOTES, 'UTF-8'); }
$rows = [];
$error = '';
$cache = ['updated' => 0, 'issues' => []];
$labels = [];
$search = $state = $reason = $label = '';
$confirmDelete = 0;
$states = ['' => webT('Alle'), 'open' => webT('Offen'), 'closed' => webT('Geschlossen'), 'deleted' => webT('Gelöscht'), 'unavailable' => webT('Nicht verfügbar'), 'unknown' => webT('Unbekannt')];
$reasons = ['' => webT('Alle'), 'completed' => webT('Erledigt'), 'not_planned' => webT('Nicht geplant'), 'duplicate' => webT('Duplikat (Grund oder Label)'), 'reopened' => webT('Wieder geöffnet')];
if (!empty($_SESSION['poll_admin'])) {
    try {
        $config = require dirname(__DIR__) . '/store/config.php';
        require_once dirname(__DIR__) . '/store/database.php';
        require_once dirname(__DIR__) . '/store/support.php';
        require_once dirname(__DIR__) . '/store/private-logs.php';
        require_once dirname(__DIR__) . '/store/github-status.php';
        $db = databaseOpen($config);
        supportSchema($db);
        privateLogSchema($db);
        foreach (['q', 'state', 'reason', 'label'] as $key) {
            $value = $_GET[$key] ?? '';
            if (!is_string($value) || strlen($value) > 200) throw new InvalidArgumentException();
            switch ($key) { case 'q': $search = $value; break; case 'state': $state = $value; break; case 'reason': $reason = $value; break; case 'label': $label = $value; }
        }
        if (!array_key_exists($state, $states) || !array_key_exists($reason, $reasons)) throw new InvalidArgumentException();
        $returnUrl = '/admin/support?' . http_build_query(['q' => $search, 'state' => $state, 'reason' => $reason, 'label' => $label]);
        if (($_SERVER['REQUEST_METHOD'] ?? '') === 'POST') {
            $csrf = $_POST['csrf'] ?? '';
            if (!is_string($csrf) || !hash_equals($_SESSION['csrf'], $csrf)) {
                http_response_code(403);
                exit(webT('Ungültige Anfrage. Bitte die Seite neu laden.'));
            }
            $number = filter_var($_POST['issue'] ?? null, FILTER_VALIDATE_INT, ['options' => ['min_range' => 1]]);
            adminRequirePermission('delete_logs');
            if (($_POST['action'] ?? '') !== 'delete-private-log' || !$number) throw new InvalidArgumentException();
            privateLogDelete($db, $number);
            $_SESSION['support_notice'] = webT('Das private Sitzungslog zu Issue #') . $number . webT(' wurde gelöscht. Die Support-Zuordnung und das GitHub-Issue bleiben erhalten.');
            header('Location: ' . $returnUrl, true, 303);
            exit;
        }
        $confirmDelete = !adminCan('delete_logs') ? 0 : (filter_var($_GET['delete_log'] ?? null, FILTER_VALIDATE_INT, ['options' => ['min_range' => 1]]) ?: 0);
        if ($confirmDelete && !supportQuery($db, 'SELECT 1 FROM private_issue_logs WHERE issue_number = ?', [$confirmDelete])->fetchColumn())
            $confirmDelete = 0;
        // Confirmation and deletion do not require a GitHub connection.
        if (!$confirmDelete) {
            $tracked = supportQuery($db, 'SELECT issue_number FROM support_issues')->fetchAll(PDO::FETCH_COLUMN);
            $cache = supportGithubCache($config, tracked: $tracked);
        }
        foreach ($cache['issues'] as $issue) foreach ($issue['labels'] as $name) $labels[$name] = $name;
        natcasesort($labels);
        $pattern = '%' . $search . '%';
        $query = supportQuery($db, 'SELECT p.support_id, p.display_name AS current_name,
            i.issue_number, i.display_name AS reported_name, i.created_at, l.original_size AS private_log_size
            FROM support_profiles p LEFT JOIN support_issues i ON i.internal_id = p.internal_id
            LEFT JOIN private_issue_logs l ON l.issue_number = i.issue_number
            WHERE LOWER(p.support_id) LIKE LOWER(?) OR LOWER(p.display_name) LIKE LOWER(?)
                OR LOWER(i.display_name) LIKE LOWER(?) OR CAST(i.issue_number AS CHAR) = ?
            ORDER BY p.updated_at DESC, i.created_at DESC',
            [$pattern, $pattern, $pattern, $search]);
        while ($row = $query->fetch(PDO::FETCH_ASSOC)) {
            $issue = $cache['issues'][$row['issue_number'] ?? 0] ?? null;
            if (!supportIssueMatches($issue, $state, $reason, $label)) continue;
            $row['github'] = $issue;
            $rows[] = $row;
            if (count($rows) >= 200) break;
        }
        $query->closeCursor();
    } catch (Throwable $e) {
        http_response_code(503);
        $error = webT('Die Support-Zuordnung konnte nicht geladen werden.');
    }
} else { http_response_code(401); }
?>
<!doctype html><html lang="<?=webLanguage()?>"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title><?=webT('NxFileViewer Support-Verwaltung')?></title>
<style>:root{color-scheme:light dark;font-family:system-ui,sans-serif}body{margin:30px;line-height:1.5}table{border-collapse:collapse;width:100%}td,th{padding:12px;text-align:left;border-bottom:1px solid #8886;vertical-align:top}code{overflow-wrap:anywhere}input,button,select{padding:10px;font:inherit}form.filters{display:flex;gap:12px;flex-wrap:wrap;align-items:end}label{display:block}.label{display:inline-block;border:1px solid #8888;border-radius:8px;padding:0 6px;margin:2px}.notice{padding:12px;border:1px solid #8888}.table-scroll{overflow-x:auto}</style>
<h1><?=webT('Support-Zuordnung')?></h1>
<?php adminNavigation('support'); ?>
<p><a href="/admin/support"><?=webT('Issues und private Logs')?></a> · <a href="/admin/support/ids"><?=webT('Support-IDs und Statistikmeldungen')?></a></p>
<?php if (empty($_SESSION['poll_admin'])): ?>
<p><?=webT('Bitte im ')?><a href="/admin/"><?=webT('Adminbereich anmelden')?></a><?=webT(' und anschließend diese Seite öffnen.')?></p>
<?php else: ?>
<?php if (isset($_SESSION['support_notice'])): ?><p class="notice" role="status"><?= supportEscape(webT($_SESSION['support_notice'])) ?></p><?php unset($_SESSION['support_notice']); endif ?>
<?php if ($confirmDelete): ?>
<section class="notice"><h2><?=webT('Privates Log zu Issue #')?><?= $confirmDelete ?><?=webT(' endgültig löschen?')?></h2>
<p><?=webT('Das unveränderte Sitzungslog wird aus der aktiven Datenbank entfernt. Vorhandene Sicherungen können es weiterhin enthalten.
Die Support-Zuordnung, das GitHub-Issue und dort veröffentlichte Log-Auszüge bleiben erhalten.')?></p>
<form method="post" action="<?= supportEscape($returnUrl) ?>">
<input type="hidden" name="csrf" value="<?= supportEscape($_SESSION['csrf']) ?>">
<input type="hidden" name="issue" value="<?= $confirmDelete ?>">
<button name="action" value="delete-private-log"><?=webT('Privates Log endgültig löschen')?></button>
<a href="<?= supportEscape($returnUrl) ?><?=webT('">Abbrechen')?></a></form></section>
<?php else: ?>
<p><?=webT('Bis zu 200 passende Zuordnungen. GitHub-Abgleich automatisch beim Seitenaufruf, höchstens alle 5 Minuten.
')?><?= $cache['updated'] ? webT('Letzter erfolgreicher Abgleich: ') . gmdate('Y-m-d H:i:s', (int)$cache['updated']) . ' UTC.' : webT('Noch kein erfolgreicher GitHub-Abgleich.') ?></p>
<?php if (isset($cache['warning'])): ?><p role="status"><?= supportEscape(webT($cache['warning'])) ?></p><?php endif ?>
<form method="get" class="filters">
<label><?=webT('Support-ID, Name oder Issue-Nummer')?><br><input name="q" maxlength="200" value="<?= supportEscape($search) ?>"></label>
<label><?=webT('GitHub-Status')?><br><select name="state"><?php foreach ($states as $value => $text): ?><option value="<?= supportEscape($value) ?>" <?= $state === $value ? 'selected' : '' ?>><?= supportEscape($text) ?></option><?php endforeach ?></select></label>
<label><?=webT('Schließungsgrund')?><br><select name="reason"><?php foreach ($reasons as $value => $text): ?><option value="<?= supportEscape($value) ?>" <?= $reason === $value ? 'selected' : '' ?>><?= supportEscape($text) ?></option><?php endforeach ?></select></label>
<label><?=webT('GitHub-Label')?><br><select name="label"><option value=""><?=webT('Alle')?></option><?php if ($label !== '' && !isset($labels[$label])) $labels[$label] = $label; foreach ($labels as $name): ?><option value="<?= supportEscape($name) ?>" <?= $label === $name ? 'selected' : '' ?>><?= supportEscape($name) ?></option><?php endforeach ?></select></label>
<button><?=webT('Filtern')?></button><a href="/admin/support"><?=webT('Zurücksetzen')?></a></form>
<p><?= supportEscape(webT($error)) ?></p>
<div class="table-scroll"><table><thead><tr><th>Support-ID</th><th><?=webT('Aktueller Anzeigename')?></th><th>Issue</th><th><?=webT('GitHub-Status / Labels')?></th><th><?=webT('Name bei Meldung')?></th><th><?=webT('Zeitpunkt (UTC)')?></th><th><?=webT('Privates Sitzungslog')?></th></tr></thead><tbody>
<?php foreach ($rows as $row): $issue = $row['github']; ?>
<tr><td><?= supportEscape($row['support_id']) ?></td>
<td><?= supportEscape($row['current_name']) ?></td>
<td><?php if ($row['issue_number'] !== null): ?><a href="https://github.com/sandmaennchen5/fork_NxFileViewer/issues/<?= (int)$row['issue_number'] ?>">#<?= (int)$row['issue_number'] ?></a><br><?= supportEscape($issue['title'] ?? '') ?><?php endif ?></td>
<td><?php if ($row['issue_number'] !== null): ?><?= supportEscape($states[$issue['state'] ?? 'unknown'] ?? webT('Unbekannt')) ?>
<?php if (($issue['state'] ?? '') === 'deleted'): ?><br><small><?=webT('GitHub hat die Löschung ausdrücklich bestätigt.')?></small><?php endif ?>
<?php if (($issue['state'] ?? '') === 'unavailable'): ?><br><small><?=webT('Auf GitHub nicht auffindbar oder nicht mehr zugänglich; keine bestätigte Löschung.')?></small><?php endif ?>
<?php if (!empty($issue['reason'])): ?><br><?= supportEscape($reasons[$issue['reason']] ?? $issue['reason']) ?><?php endif ?>
<?php if (!empty($issue['status'])): ?><br><?= supportEscape($issue['status']) ?><?php endif ?>
<br><?php foreach ($issue['labels'] ?? [] as $name): ?><span class="label"><?= supportEscape($name) ?></span><?php endforeach ?><?php endif ?></td>
<td><?= supportEscape($row['reported_name'] ?? '') ?></td>
<td><?= $row['created_at'] === null ? '' : gmdate('Y-m-d H:i:s', (int)$row['created_at']) ?></td>
<td><?php if ($row['private_log_size'] !== null): ?><?php if(adminCan('download_logs')): ?><a href="/admin/support/log?issue=<?= (int)$row['issue_number'] ?><?=webT('">Original herunterladen')?></a><?php endif ?>
(<?= (int)ceil($row['private_log_size'] / 1024) ?> KiB)<br>
<?php if(adminCan('delete_logs')): ?><a href="<?= supportEscape($returnUrl . '&delete_log=' . (int)$row['issue_number']) ?><?=webT('">Privates Log löschen…')?></a><?php endif ?><?php endif ?></td></tr>
<?php endforeach ?></tbody></table></div>
<?php if (!$rows): ?><p><?=webT('Keine passenden Zuordnungen.')?></p><?php endif ?>
<?php endif ?>
<?php endif ?></html>
