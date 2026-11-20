<?php
declare(strict_types=1);
require_once __DIR__ . '/github.php';

function supportIssueSummary(array $issue): array {
    $labels = [];
    foreach ($issue['labels'] ?? [] as $label) {
        $name = is_array($label) ? ($label['name'] ?? '') : $label;
        if (is_string($name) && $name !== '') $labels[] = $name;
    }
    return ['state' => $issue['state'] ?? 'unknown', 'reason' => $issue['state_reason'] ?? '',
        'status' => is_string($issue['status'] ?? null) ? $issue['status'] : '',
        'title' => (string)($issue['title'] ?? ''), 'labels' => $labels];
}
function supportIssueMatches(?array $issue, string $state, string $reason, string $label): bool {
    if ($state !== '' && ($issue['state'] ?? 'unknown') !== $state) return false;
    if ($reason !== '' && ($issue['reason'] ?? '') !== $reason &&
        !($reason === 'duplicate' && in_array('duplicate', array_map('strtolower', $issue['labels'] ?? []), true))) return false;
    return $label === '' || in_array($label, $issue['labels'] ?? [], true);
}
function supportGithubResponse(string $url, string $token, int $timeout): array {
    $handle = curl_init($url);
    $headers = ['Accept: application/vnd.github+json', 'X-GitHub-Api-Version: 2026-03-10'];
    if ($token !== '') $headers[] = 'Authorization: Bearer ' . $token;
    curl_setopt_array($handle, [
        CURLOPT_HTTPHEADER => $headers,
        CURLOPT_USERAGENT => 'NxFileViewer-Support-Admin', CURLOPT_RETURNTRANSFER => true,
        CURLOPT_CONNECTTIMEOUT => min(4, $timeout), CURLOPT_TIMEOUT => $timeout,
    ]);
    $response = curl_exec($handle);
    $status = curl_getinfo($handle, CURLINFO_RESPONSE_CODE);
    if (!is_string($response) || !in_array($status, [200, 404, 410], true))
        throw new RuntimeException('GitHub status unavailable');
    $result = json_decode($response, true, flags: JSON_THROW_ON_ERROR);
    if (!is_array($result)) throw new RuntimeException('Invalid GitHub response');
    return [$status, $result];
}
function supportGithubGet(string $url, string $token, int $timeout): array {
    [$status, $result] = supportGithubResponse($url, $token, $timeout);
    if ($status !== 200 || !array_is_list($result)) throw new RuntimeException('Invalid GitHub issue list');
    return $result;
}
function supportGithubIssueResponse(int $status, array $data): array {
    if ($status === 200) {
        if (!isset($data['number'], $data['state']) || isset($data['pull_request']))
            throw new RuntimeException('Invalid GitHub issue');
        return supportIssueSummary($data);
    }
    if (!in_array($status, [404, 410], true)) throw new RuntimeException('GitHub issue check failed');
    return ['state' => $status === 410 && ($data['message'] ?? '') === 'This issue was deleted' ? 'deleted' : 'unavailable',
        'reason' => '', 'status' => '', 'title' => '', 'labels' => []];
}
function supportGithubIssue(int $number, string $token, int $timeout): array {
    [$status, $result] = supportGithubResponse(
        'https://api.github.com/repos/sandmaennchen5/fork_NxFileViewer/issues/' . $number, $token, $timeout);
    return supportGithubIssueResponse($status, $result);
}
function supportGithubCache(array $config, ?callable $fetch = null, ?string $directory = null, array $tracked = [], ?callable $fetchIssue = null): array {
    $directory ??= __DIR__;
    $path = $directory . '/github-issues-cache.json';
    $cache = json_decode((string)@file_get_contents($path), true);
    if (!is_array($cache) || !is_array($cache['issues'] ?? null)) $cache = ['updated' => 0, 'attempted' => 0, 'issues' => []];
    if (time() - (int)($cache['attempted'] ?? 0) < 300) return $cache;
    $lock = @fopen($directory . '/github-issues-cache.lock', 'c');
    if (!$lock || !flock($lock, LOCK_EX | LOCK_NB)) {
        if ($lock) fclose($lock);
        $cache['warning'] = 'GitHub-Abgleich läuft oder der private Cache ist nicht beschreibbar.';
        return $cache;
    }
    try {
        $latest = json_decode((string)@file_get_contents($path), true);
        if (is_array($latest) && is_array($latest['issues'] ?? null)) $cache = $latest;
        if (time() - (int)($cache['attempted'] ?? 0) < 300) return $cache;
        $started = time();
        $cache['attempted'] = $started;
        try {
            $token = $fetch === null ? installationToken($config) : '';
            // A complete snapshot is required: incremental queries cannot detect deletion.
            // Do not publish partial results when any page fails.
            $issues = [];
            for ($page = 1; ; $page++) {
                $remaining = 20 - (time() - $started);
                if ($remaining < 1) throw new RuntimeException('GitHub refresh timed out');
                $url = 'https://api.github.com/repos/sandmaennchen5/fork_NxFileViewer/issues?state=all&sort=created&direction=asc&per_page=100&page=' . $page;
                $batch = $fetch !== null ? $fetch($url) : supportGithubGet($url, $token, min(8, $remaining));
                foreach ($batch as $issue) {
                    if (!isset($issue['pull_request']) && (int)($issue['number'] ?? 0) > 0)
                        $issues[(int)$issue['number']] = supportIssueSummary($issue);
                }
                if (count($batch) < 100) break;
            }
            foreach (array_unique(array_merge(array_keys($cache['issues']), $tracked)) as $number) {
                $number = (int)$number;
                if ($number > 0 && !isset($issues[$number])) {
                    // Confirm deletion through the individual endpoint: GitHub explicitly
                    // returns 410 + "This issue was deleted". Absence alone is insufficient.
                    if (($cache['issues'][$number]['state'] ?? '') === 'deleted') {
                        $issues[$number] = $cache['issues'][$number];
                        continue;
                    }
                    $remaining = 20 - (time() - $started);
                    if ($remaining < 1) throw new RuntimeException('GitHub refresh timed out');
                    $checked = $fetchIssue !== null
                        ? $fetchIssue($number, min(8, $remaining))
                        : supportGithubIssue($number, $token, min(8, $remaining));
                    if ($checked['title'] === '') $checked['title'] = $cache['issues'][$number]['title'] ?? '';
                    $issues[$number] = $checked;
                }
            }
            $cache['issues'] = $issues;
            $cache['updated'] = $started;
            unset($cache['warning']);
        } catch (Throwable $e) {
            $cache['warning'] = 'GitHub derzeit nicht erreichbar. Anzeige und Filter verwenden den letzten gespeicherten Stand.';
        }
        $temp = tempnam($directory, '.github-cache-');
        if ($temp === false) throw new RuntimeException('Cannot write cache');
        try {
            if (file_put_contents($temp, json_encode($cache, JSON_THROW_ON_ERROR)) === false || !rename($temp, $path))
                throw new RuntimeException('Cannot write cache');
        } finally { if (is_file($temp)) unlink($temp); }
    } catch (Throwable $e) {
        $cache['warning'] = 'Der GitHub-Zwischenspeicher konnte nicht aktualisiert werden.';
    } finally { flock($lock, LOCK_UN); fclose($lock); }
    return $cache;
}
