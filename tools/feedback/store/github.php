<?php
declare(strict_types=1);
final class GitHubRequestError extends RuntimeException {
    public function __construct(public readonly int $httpStatus){parent::__construct('GitHub request failed');}
}
function githubBase64Url(string $bytes): string {
    return rtrim(strtr(base64_encode($bytes), '+/', '-_'), '=');
}
function github(string $url, array $payload, ?string $token = null): array
{
    $handle = curl_init($url);
    $oauth = $token === null;
    $headers = ['Accept: application/json', $oauth ? 'Content-Type: application/x-www-form-urlencoded' : 'Content-Type: application/json'];
    if ($token !== null) {
        $headers[] = 'Authorization: Bearer ' . $token;
    }

    curl_setopt_array($handle, [
        CURLOPT_POST => true,
        CURLOPT_POSTFIELDS => $oauth ? http_build_query($payload) : json_encode($payload, JSON_THROW_ON_ERROR),
        CURLOPT_HTTPHEADER => $headers,
        CURLOPT_USERAGENT => 'NxFileViewer-Feedback',
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_CONNECTTIMEOUT => 4,
        CURLOPT_TIMEOUT => 8,
    ]);
    $response = curl_exec($handle);
    $status = curl_getinfo($handle, CURLINFO_RESPONSE_CODE);
    if (!is_string($response) || $status < 200 || $status >= 300) {
        throw new GitHubRequestError((int)$status);
    }
    return json_decode($response, true, flags: JSON_THROW_ON_ERROR);
}

function installationToken(array $config): string
{
    $now = time();
    $claims = githubBase64Url('{"alg":"RS256","typ":"JWT"}') . '.' . githubBase64Url(json_encode([
        'iat' => $now - 60,
        'exp' => $now + 480,
        'iss' => $config['client_id'],
    ], JSON_THROW_ON_ERROR));
    $key = file_get_contents($config['private_key']);
    if ($key === false || !openssl_sign($claims, $signature, $key, OPENSSL_ALGO_SHA256)) {
        throw new RuntimeException('Could not sign GitHub App token');
    }
    $jwt = $claims . '.' . githubBase64Url($signature);
    $id = (int) $config['installation_id'];
    $result = github("https://api.github.com/app/installations/$id/access_tokens", [
        'repositories' => ['fork_NxFileViewer'],
        'permissions' => ['issues' => 'write'],
    ], $jwt);
    return $result['token'];
}

