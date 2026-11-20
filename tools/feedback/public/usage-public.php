<?php
declare(strict_types=1);
require_once dirname(__DIR__).'/store/web-language.php';
header('Cache-Control: no-cache');
header('X-Content-Type-Options: nosniff');
header('Referrer-Policy: no-referrer');
header("Content-Security-Policy: default-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self'; frame-ancestors 'none'");
ini_set('display_errors', '0');
$charts = [
    'versionen' => [webT('Versionen'), 'version'],
    'betriebssysteme' => [webT('Betriebssysteme'), "CASE LOWER(platform) WHEN 'windows' THEN 'Windows' WHEN 'linux' THEN 'Linux' WHEN 'macos' THEN 'macOS' ELSE 'Weitere Systeme' END"],
    'architekturen' => [webT('Architekturen'), "CASE LOWER(process_architecture) WHEN 'x64' THEN 'x64' WHEN 'x86' THEN 'x86' WHEN 'arm64' THEN 'ARM64' WHEN 'arm' THEN 'ARM' ELSE 'Weitere Architekturen' END"],
    'oberflaechen' => [webT('Oberflächen'), "CASE ui_framework WHEN 'WPF' THEN 'WPF' WHEN 'Avalonia' THEN 'Avalonia' ELSE 'Weitere Oberflächen' END"],
    'dotnet' => [webT('.NET-Auslieferung'), "CASE WHEN distribution LIKE 'self-contained%' THEN 'Mit .NET-Laufzeit' WHEN distribution LIKE 'framework-dependent%' THEN 'Systemweite .NET-Laufzeit' ELSE 'Weitere Auslieferungen' END"],
];
function publicUsageEscape(string $value): string {
    return htmlspecialchars($value, ENT_QUOTES | ENT_XML1, 'UTF-8');
}
function publicUsageLabel(string $key, string $label): string {
    if ($key === 'versionen' && !preg_match('/\A[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?\z/', $label))
        return webT('Weitere Versionen');
    return $label;
}
function publicUsageRows(PDO $db, string $key, string $expression, int $cutoff, bool $limit = true): array {
    // Expressions come exclusively from the fixed chart definitions above.
    $statement = $db->prepare('SELECT ' . $expression . ' AS label, COUNT(*) AS devices
        FROM usage_statistics WHERE last_seen >= ? GROUP BY ' . $expression . ' ORDER BY devices DESC, label');
    $statement->execute([$cutoff]);
    $grouped = [];
    foreach ($statement->fetchAll(PDO::FETCH_ASSOC) as $row) {
        $label = publicUsageLabel($key, $row['label']);
        $grouped[$label] = ($grouped[$label] ?? 0) + (int)$row['devices'];
    }
    arsort($grouped);
    if ($limit && count($grouped) > 15) {
        $rest = array_sum(array_slice($grouped, 15, null, true));
        $grouped = array_slice($grouped, 0, 15, true);
        $grouped[webT('Weitere Einträge')] = $rest;
    }
    return $grouped;
}
$image = $_GET['chart'] ?? null;
if ($image !== null && (!is_string($image) || !isset($charts[$image]))) {
    http_response_code(404);
    exit(webT('Grafik nicht gefunden.'));
}
try {
    $config = require dirname(__DIR__) . '/store/config.php';
    require_once dirname(__DIR__) . '/store/database.php';
    require_once dirname(__DIR__) . '/store/usage.php';
    require_once dirname(__DIR__) . '/store/statistics-texts.php';
    foreach ($charts as $key => &$definition) $definition[0] = usageText('chart_titles.' . $key);
    unset($definition);
    $db = databaseOpen($config);
    usageSchema($db);
    $days = usagePeriodDays($config);
    $cutoff = time() - $days * 86400;
    $statement = $db->prepare('SELECT COUNT(*) FROM usage_statistics WHERE last_seen >= ?');
    $statement->execute([$cutoff]);
    $total = (int)$statement->fetchColumn();
    require_once dirname(__DIR__) . '/store/usage-graphics.php';
    $badge = $_GET['badge'] ?? null;
    $trend = $_GET['trend'] ?? null;
    if ($badge !== null) {
        if (!is_string($badge) || (!in_array($badge, ['installationen', 'installs'], true) && !isset($charts[$badge]))) {
            http_response_code(404);
            exit(webT('Badge nicht gefunden.'));
        }
        if ($badge === 'installationen') usageBadge(usageText('badges.installations', ['days' => $days]), $total);
        if ($badge === 'installs') usageBadge(usageText('badges.installs', ['days' => $days]), $total);
        [$title, $expression] = $charts[$badge];
        $rows = publicUsageRows($db, $badge, $expression, $cutoff, false);
        $combined = $_GET['kombiniert'] ?? '0';
        if (!is_string($combined) || !in_array($combined, ['0', '1'], true)) {
            http_response_code(400);
            exit(webT('Ungültiger Badge-Modus.'));
        }
        if ($combined === '1') usageBadge(usageText('badges.combined', ['title' => $title]), usageCombinedBadge($rows));
        $value = $_GET['wert'] ?? null;
        if ($value === null) usageBadge($title, count($rows));
        $allowed = match ($badge) {
            'versionen' => is_string($value) && (publicUsageLabel($badge, $value) === $value || $value === webT('Weitere Versionen')),
            'betriebssysteme' => in_array($value, ['Windows', 'Linux', 'macOS', webT('Weitere Systeme')], true),
            'architekturen' => in_array($value, ['x64', 'x86', 'ARM64', 'ARM', webT('Weitere Architekturen')], true),
            'oberflaechen' => in_array($value, ['WPF', 'Avalonia', webT('Weitere Oberflächen')], true),
            'dotnet' => in_array($value, [webT('Mit .NET-Laufzeit'), webT('Systemweite .NET-Laufzeit'), webT('Weitere Auslieferungen')], true),
        };
        if (!is_string($value) || !$allowed) {
            http_response_code(404);
            exit(webT('Badge nicht gefunden.'));
        }
        usageBadge(usageText('badges.single', ['title' => $title, 'label' => usageDisplayLabel($value)]), $rows[$value] ?? 0);
    }
    if ($trend !== null) {
        if (!is_string($trend) || ($trend !== 'installationen' && !isset($charts[$trend]))) {
            http_response_code(404);
            exit(webT('Verlauf nicht gefunden.'));
        }
        if ($trend === 'installationen') usageTrend($db, $trend, usageText('chart_titles.installationen'), webT("'Alle Installationen'"), $days);
        [$title, $expression] = $charts[$trend];
        usageTrend($db, $trend, $title, $expression, $days);
    }
    if ($image !== null) {
        [$title, $expression] = $charts[$image];
        $rows = publicUsageRows($db, $image, $expression, $cutoff);
        $height = max(180, 126 + count($rows) * 48);
        $textValues = ['title' => $title, 'days' => $days, 'total' => $total, 'updated' => gmdate('Y-m-d H:i')];
        header('Content-Type: image/svg+xml; charset=utf-8');
        echo '<svg xmlns="http://www.w3.org/2000/svg" width="900" height="' . $height . '" viewBox="0 0 900 ' . $height . '" role="img" aria-labelledby="title description">';
        echo '<title id="title">' . publicUsageEscape(usageText('bars.title', $textValues)) . '</title>';
        echo '<desc id="description">' . publicUsageEscape(usageText('bars.description', $textValues)) . '</desc>';
        echo '<rect width="900" height="' . $height . '" rx="12" fill="#111827"/>';
        echo '<defs><clipPath id="labels"><rect x="24" y="80" width="250" height="' . $height . '"/></clipPath></defs>';
        echo '<text x="24" y="36" fill="#f9fafb" font-family="sans-serif" font-size="22" font-weight="600">' . publicUsageEscape(usageText('bars.title', $textValues)) . '</text>';
        echo '<text x="24" y="64" fill="#b8c4d5" font-family="sans-serif" font-size="14">' . publicUsageEscape(usageText('bars.subtitle', $textValues)) . '</text>';
        $maximum = max([1, ...array_values($rows)]);
        $index = 0;
        foreach ($rows as $label => $count) {
            $y = 101 + $index++ * 48;
            $width = (int)round(460 * $count / $maximum);
            $percent = $total > 0 ? round(100 * $count / $total, 1) : 0;
            echo '<text x="24" y="' . ($y + 13) . '" clip-path="url(#labels)" fill="#f9fafb" font-family="sans-serif" font-size="14"><title>' . publicUsageEscape(usageDisplayLabel((string)$label)) . '</title>' . publicUsageEscape(usageDisplayLabel((string)$label)) . '</text>';
            echo '<rect x="288" y="' . $y . '" width="460" height="18" rx="5" fill="#283548"/>';
            echo '<rect x="288" y="' . $y . '" width="' . $width . '" height="18" rx="5" fill="#60a5fa"/>';
            echo '<text x="766" y="' . ($y + 14) . '" fill="#f9fafb" font-family="sans-serif" font-size="13">' . $count . ' · ' . $percent . '%</text>';
        }
        if (!$rows) echo '<text x="24" y="111" fill="#b8c4d5" font-family="sans-serif" font-size="14">' . publicUsageEscape(usageText('bars.empty')) . '</text>';
        echo '<text x="24" y="' . ($height - 15) . '" fill="#b8c4d5" font-family="sans-serif" font-size="11">' . publicUsageEscape(usageText('bars.footer', $textValues)) . '</text></svg>';
        exit;
    }
} catch (Throwable $e) {
    http_response_code(503);
    exit(webT('Die Statistik ist derzeit nicht verfügbar.'));
}
?>
<!doctype html><html lang="<?=webLanguage()?>"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<meta http-equiv="refresh" content="300"><title><?=webT('NxFileViewer Nutzungsstatistik')?></title>
<style>:root{color-scheme:light dark;font-family:system-ui,sans-serif}body{max-width:960px;margin:40px auto;padding:0 20px;line-height:1.6}img{display:block;width:100%;height:auto;border-radius:12px}section{margin:32px 0}h1{font-size:1.8rem}h2{font-size:1.2rem}code{overflow-wrap:anywhere}</style>
<h1><?=webT('NxFileViewer Nutzungsstatistik')?></h1><?php webLanguageSelector(); ?>
<?php foreach(['installationen','installs'] as $badge):$url='/statistik/badges/'.$badge.'.svg?lang='.webLanguage(); ?>
<p><a href="<?=publicUsageEscape($url)?>"><img style="width:auto;max-width:100%" src="<?=publicUsageEscape($url)?>" alt="<?=publicUsageEscape(usageText('badges.'.($badge==='installationen'?'installations':'installs'),['days'=>$days]))?>"></a></p>
<?php endforeach ?>
<section><h2><?=webT('Aktive Installationen im Verlauf')?></h2><a href="/statistik/verlauf/installationen.svg?lang=<?=webLanguage()?>"><img src="/statistik/verlauf/installationen.svg?lang=<?=webLanguage()?>" alt="<?=webT('Verlauf der aktiven Installationen')?>"></a></section>
<p><?=$total?> <?=webT('Kennungen haben sich in den letzten')?> <?=$days?> <?=webT('Tagen gemeldet. Jede Kennung zählt mit ihrer zuletzt gemeldeten Version und Umgebung.')?></p>
<?php foreach ($charts as $key => [$title, $expression]):
$bars='/statistik/'.$key.'.svg?lang='.webLanguage();$trend='/statistik/verlauf/'.$key.'.svg?lang='.webLanguage();$badge='/statistik/badges/'.$key.'.svg?lang='.webLanguage(); ?>
<section><h2><?=publicUsageEscape($title)?></h2>
<a href="<?=publicUsageEscape($bars)?>"><img src="<?=publicUsageEscape($bars)?>" alt="<?=webT('Aktive Kennungen nach')?> <?=publicUsageEscape($title)?>"></a>
<p><a href="<?=publicUsageEscape($bars)?>"><?=webT('Balkengrafik für README oder Webseite')?></a></p>
<a href="<?=publicUsageEscape($trend)?>"><img src="<?=publicUsageEscape($trend)?>" alt="<?=webT('Verlauf nach')?> <?=publicUsageEscape($title)?>"></a>
<p><a href="<?=publicUsageEscape($trend)?>"><?=webT('Verlauf für README oder Webseite')?></a></p>
<p><?=webT('Kombi-Badge:')?> <a href="<?=publicUsageEscape($badge.'&kombiniert=1')?>"><img style="width:auto;max-width:100%" src="<?=publicUsageEscape($badge.'&kombiniert=1')?>" alt="<?=publicUsageEscape($title)?> – <?=webT('alle Werte')?>"></a></p>
<p><?=webT('Einzel-Badges:')?>
<?php foreach (publicUsageRows($db, $key, $expression, $cutoff) as $label => $count): ?>
<a href="<?=publicUsageEscape($badge.'&wert='.rawurlencode((string)$label))?>"><?=publicUsageEscape(usageDisplayLabel((string)$label))?> (<?=$count?>)</a>
<?php endforeach ?></p></section>
<?php endforeach ?>
<p><?=webT('Die Seite aktualisiert sich alle fünf Minuten. Die Grafiken werden bei jedem Abruf aus den aktuellen Meldungen erzeugt. Eingebettete Bilder können durch externe Zwischenspeicher verzögert aktualisiert werden.')?></p>
</html>
