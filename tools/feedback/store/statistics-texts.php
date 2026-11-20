<?php
declare(strict_types=1);

/** Presentation only: templates never become SQL or raw SVG/HTML. */
function usageTexts(): array {
    static $texts = null;
    if ($texts !== null) return $texts;
    $texts = json_decode((string)file_get_contents(__DIR__ . (function_exists('webLanguage') && webLanguage()==='en' ? '/statistics-texts.en.json' : '/statistics-texts.json')), true, flags: JSON_THROW_ON_ERROR);
    if (!is_array($texts)) throw new RuntimeException('Invalid statistics texts');
    return $texts;
}
function usageText(string $key, array $values = []): string {
    $text = usageTexts();
    foreach (explode('.', $key) as $part) $text = is_array($text) ? ($text[$part] ?? null) : null;
    if (!is_string($text)) throw new RuntimeException('Missing statistics text: ' . $key);
    $values += ['app' => usageTexts()['app_name']];
    $replace = [];
    foreach ($values as $name => $value) $replace['{' . $name . '}'] = (string)$value;
    return strtr($text, $replace);
}
function usageDisplayLabel(string $label): string {
    $replacement = usageTexts()['labels'][$label] ?? $label;
    return is_string($replacement) ? $replacement : $label;
}
function usageCombinedBadge(array $rows): string {
    $parts = [];
    foreach ($rows as $label => $count)
        $parts[] = usageText('badges.pair', ['label' => usageDisplayLabel((string)$label), 'count' => $count]);
    return $parts ? implode(usageText('badges.separator'), $parts) : usageText('badges.empty');
}
