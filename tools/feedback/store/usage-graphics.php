<?php
declare(strict_types=1);

require_once __DIR__ . '/statistics-texts.php';

function usageBadge(string $label, int|string $value): never {
    $left = max(80, 18 + preg_match_all('/./us', $label) * 7);
    $value = (string)$value;
    $right = max(38, preg_match_all('/./us', $value) * 7 + 20);
    $width = $left + $right;
    header('Content-Type: image/svg+xml; charset=utf-8');
    echo '<svg xmlns="http://www.w3.org/2000/svg" width="' . $width . '" height="24" viewBox="0 0 ' . $width . ' 24" role="img">';
    echo '<title>' . publicUsageEscape($label) . ': ' . publicUsageEscape($value) . '</title>';
    echo '<clipPath id="badge"><rect width="' . $width . '" height="24" rx="4"/></clipPath><g clip-path="url(#badge)">';
    echo '<rect width="' . $left . '" height="24" fill="#374151"/><rect x="' . $left . '" width="' . $right . '" height="24" fill="#2563eb"/>';
    echo '<g fill="white" font-family="Verdana,sans-serif" font-size="11" text-anchor="middle">';
    echo '<text x="' . ($left / 2) . '" y="16">' . publicUsageEscape($label) . '</text>';
    echo '<text x="' . ($left + $right / 2) . '" y="16">' . publicUsageEscape($value) . '</text></g></g></svg>';
    exit;
}

function usageTrendData(PDO $db, string $key, string $expression, int $periodDays = 30): array {
    $now = time();
    $today = gmdate('Y-m-d', $now);
    $captureStart = $db->query('SELECT start_day FROM usage_history_state WHERE singleton = 1')->fetchColumn();
    $start = min($today, max((string)$captureStart, gmdate('Y-m-d', $now - ($periodDays - 1) * 86400)));
    $first = strtotime($start . ' UTC');
    $s = $db->prepare('SELECT internal_id, day, last_seen, ' . $expression . ' AS label
        FROM usage_daily WHERE last_seen >= ? AND day <= ? ORDER BY day, last_seen');
    $s->execute([$first - $periodDays * 86400, $today]);
    $observations = $s->fetchAll(PDO::FETCH_ASSOC);
    $days = [];
    $series = [];
    $latest = [];
    $position = 0;
    for ($time = $first; $time <= $now; $time += 86400) {
        $day = gmdate('Y-m-d', $time);
        $end = $day === $today ? $now : $time + 86399;
        while (isset($observations[$position]) && $observations[$position]['day'] <= $day) {
            $row = $observations[$position++];
            $latest[$row['internal_id']] = ['seen' => (int)$row['last_seen'], 'label' => $key === 'installationen' ? usageText('trends.all_installations') : publicUsageLabel($key, $row['label'])];
        }
        $counts = [];
        foreach ($latest as $row) {
            if ($row['seen'] < $end - $periodDays * 86400) continue;
            $counts[$row['label']] = ($counts[$row['label']] ?? 0) + 1;
        }
        $index = count($days);
        foreach ($counts as $label => $count) {
            if (!isset($series[$label])) $series[$label] = array_fill(0, $index, 0);
        }
        foreach ($series as $label => &$values) $values[] = $counts[$label] ?? 0;
        unset($values);
        $days[] = $day;
    }
    uasort($series, fn($a, $b) => max($b) <=> max($a));
    if (count($series) > 6) {
        $rest = array_fill(0, count($days), 0);
        foreach (array_slice($series, 6, null, true) as $values)
            foreach ($values as $index => $value) $rest[$index] += $value;
        $series = array_slice($series, 0, 6, true);
        $series[usageText('trends.other')] = $rest;
    }
    return ['days' => $days, 'series' => $series, 'start' => $start];
}

function usageTrend(PDO $db, string $key, string $title, string $expression, int $periodDays = 30): never {
    $data = usageTrendData($db, $key, $expression, $periodDays);
    $textValues = ['title' => $title, 'days' => $periodDays, 'start' => $data['start']];
    $days = $data['days'];
    $series = $data['series'];
    $height = 360 + max(1, count($series)) * 22;
    $maximum = 1;
    foreach ($series as $values) $maximum = max($maximum, ...$values);
    $ticks = min(4, $maximum);
    $maximum = (int)(ceil($maximum / $ticks) * $ticks);
    $colors = ['#60a5fa', '#2dd4bf', '#c084fc', '#fb923c', '#fb7185', '#a3e635', '#94a3b8'];
    header('Content-Type: image/svg+xml; charset=utf-8');
    echo '<svg xmlns="http://www.w3.org/2000/svg" width="900" height="' . $height . '" viewBox="0 0 900 ' . $height . '" role="img">';
    echo '<title>' . publicUsageEscape(usageText('trends.title', $textValues)) . '</title>';
    echo '<desc>' . publicUsageEscape(usageText('trends.description', $textValues)) . '</desc>';
    echo '<rect width="900" height="' . $height . '" rx="12" fill="#111827"/>';
    echo '<text x="24" y="34" fill="white" font-family="sans-serif" font-size="21">' . publicUsageEscape(usageText('trends.title', $textValues)) . '</text>';
    echo '<text x="24" y="61" fill="#b8c4d5" font-family="sans-serif" font-size="13">' . publicUsageEscape(usageText('trends.subtitle', $textValues)) . '</text>';
    for ($tick = 0; $tick <= $ticks; $tick++) {
        $y = 285 - 190 * $tick / $ticks;
        echo '<line x1="62" x2="865" y1="' . $y . '" y2="' . $y . '" stroke="#334155"/>';
        echo '<text x="52" y="' . ($y + 4) . '" fill="#b8c4d5" font-family="sans-serif" font-size="11" text-anchor="end">' . (int)($maximum * $tick / $ticks) . '</text>';
    }
    $indices = array_unique([0, (int)floor((count($days) - 1) / 2), count($days) - 1]);
    foreach ($indices as $index) {
        $x = count($days) === 1 ? 460 : 62 + 803 * $index / (count($days) - 1);
        echo '<text x="' . $x . '" y="308" fill="#b8c4d5" font-family="sans-serif" font-size="11" text-anchor="middle">' . substr($days[$index], 5) . '</text>';
    }
    $line = 0;
    foreach ($series as $label => $values) {
        $color = $colors[$line];
        $points = [];
        foreach ($values as $index => $value) {
            $x = count($days) === 1 ? 460 : 62 + 803 * $index / (count($days) - 1);
            $y = 285 - 190 * $value / $maximum;
            $points[] = round($x, 2) . ',' . round($y, 2);
        }
        echo '<polyline fill="none" stroke="' . $color . '" stroke-width="3" points="' . implode(' ', $points) . '"/>';
        if (count($days) === 1) echo '<circle cx="' . $x . '" cy="' . $y . '" r="4" fill="' . $color . '"/>';
        $legendY = 339 + $line++ * 22;
        echo '<line x1="24" x2="44" y1="' . $legendY . '" y2="' . $legendY . '" stroke="' . $color . '" stroke-width="3"/>';
        echo '<text x="54" y="' . ($legendY + 4) . '" fill="white" font-family="sans-serif" font-size="12">' . publicUsageEscape(usageDisplayLabel((string)$label)) . '</text>';
    }
    if (!$series) echo '<text x="80" y="170" fill="#b8c4d5" font-family="sans-serif" font-size="14">' . publicUsageEscape(usageText('trends.empty')) . '</text>';
    echo '</svg>';
    exit;
}
