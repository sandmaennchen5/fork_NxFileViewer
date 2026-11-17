<?php
declare(strict_types=1);

const REPOSITORY = 'sandmaennchen5/fork_NxFileViewer';

function reply(array $data, int $status = 200): never
{
    http_response_code($status);
    header('Content-Type: application/json; charset=utf-8');
    echo json_encode($data, JSON_THROW_ON_ERROR);
    exit;
}

function browserReply(string $message, int $status = 200): never
{
    http_response_code($status);
    header('Content-Type: text/plain; charset=utf-8');
    echo $message;
    exit;
}

function randomToken(): string
{
    return bin2hex(random_bytes(32));
}

function base64Url(string $bytes): string
{
    return rtrim(strtr(base64_encode($bytes), '+/', '-_'), '=');
}

function execute(PDO $db, string $sql, array $parameters = []): PDOStatement
{
    $statement = $db->prepare($sql);
    $statement->execute($parameters);
    return $statement;
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
        throw new RuntimeException('GitHub request failed');
    }
    return json_decode($response, true, flags: JSON_THROW_ON_ERROR);
}

function installationToken(array $config): string
{
    $now = time();
    $claims = base64Url('{"alg":"RS256","typ":"JWT"}') . '.' . base64Url(json_encode([
        'iat' => $now - 60,
        'exp' => $now + 480,
        'iss' => $config['client_id'],
    ], JSON_THROW_ON_ERROR));
    $key = file_get_contents($config['private_key']);
    if ($key === false || !openssl_sign($claims, $signature, $key, OPENSSL_ALGO_SHA256)) {
        throw new RuntimeException('Could not sign GitHub App token');
    }
    $jwt = $claims . '.' . base64Url($signature);
    $id = (int) $config['installation_id'];
    $result = github("https://api.github.com/app/installations/$id/access_tokens", [
        'repositories' => ['fork_NxFileViewer'],
        'permissions' => ['issues' => 'write'],
    ], $jwt);
    return $result['token'];
}

function createIssue(string $title, string $body, string $token): int
{
    $result = github('https://api.github.com/repos/' . REPOSITORY . '/issues',
        ['title' => $title, 'body' => $body], $token);
    return $result['number'];
}

function handleCallback(PDO $db, array $config): never
{
    $state = $_GET['state'] ?? '';
    if (!is_string($state) || !preg_match('/\A[a-f0-9]{64}\z/', $state)) {
        browserReply('Invalid authorization request.', 400);
    }
    $hash = hash('sha256', $state);
    // Claim the request atomically so reloading the callback cannot create two issues.
    $claim = execute($db, "UPDATE reports SET status = 'processing'
        WHERE state = ? AND status = 'pending' AND expires > ?", [$hash, time()]);
    if ($claim->rowCount() !== 1) {
        browserReply('This authorization request has expired or was already used.', 400);
    }
    $report = execute($db, 'SELECT title, body, verifier FROM reports WHERE state = ?', [$hash])->fetch(PDO::FETCH_ASSOC);
    try {
        $code = $_GET['code'] ?? '';
        if (!is_string($code) || $code === '' || strlen($code) > 256) {
            throw new RuntimeException('Authorization canceled');
        }
        $user = github('https://github.com/login/oauth/access_token', [
            'client_id' => $config['client_id'],
            'client_secret' => $config['client_secret'],
            'code' => $code,
            'code_verifier' => $report['verifier'],
            'redirect_uri' => $config['callback_url'],
            'repository_id' => $config['repository_id'],
        ]);
        if (!isset($user['access_token'])) {
            throw new RuntimeException('Authorization failed');
        }
        $number = createIssue($report['title'], $report['body'], $user['access_token']);
        execute($db, "UPDATE reports SET status = 'created', number = ?, title = '', body = '', verifier = '' WHERE state = ?",
            [$number, $hash]);
        browserReply("GitHub issue #$number created. You can return to NxFileViewer.");
    } catch (Throwable $error) {
        execute($db, "UPDATE reports SET status = 'failed', title = '', body = '', verifier = '' WHERE state = ?", [$hash]);
        browserReply('Issue creation could not be confirmed. Check GitHub Issues before trying again.', 502);
    }
}

header('Cache-Control: no-store');
header('Referrer-Policy: no-referrer');
header('X-Content-Type-Options: nosniff');
ini_set('display_errors', '0');

try {
    $config = require dirname(__DIR__) . '/nxfileviewer-feedback/config.php';
    $db = new PDO('sqlite:' . $config['database'], options: [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]);
    $db->exec('PRAGMA busy_timeout = 5000');
    $db->exec('CREATE TABLE IF NOT EXISTS reports (
        ticket TEXT PRIMARY KEY, state TEXT UNIQUE, title TEXT, body TEXT,
        verifier TEXT, status TEXT, number INTEGER, expires INTEGER
    )');
    $db->exec('CREATE INDEX IF NOT EXISTS report_expiry ON reports(expires)');
    $db->exec('CREATE TABLE IF NOT EXISTS rate_limit (
        bucket TEXT PRIMARY KEY, count INTEGER, expires INTEGER
    )');
    execute($db, 'DELETE FROM reports WHERE expires <= ?', [time()]);

    $method = $_SERVER['REQUEST_METHOD'] ?? '';
    if ($method === 'GET') {
        handleCallback($db, $config);
    }
    if ($method !== 'POST') {
        header('Allow: GET, POST');
        reply(['error' => 'method'], 405);
    }
    $raw = file_get_contents('php://input', false, null, 0, 32769);
    if ($raw === false || strlen($raw) > 32768) {
        reply(['error' => 'size'], 413);
    }
    $input = json_decode($raw, true);
    $mode = $input['mode'] ?? '';
    if ($mode === 'status') {
        $ticket = $input['ticket'] ?? '';
        if (!is_string($ticket) || !preg_match('/\A[a-f0-9]{64}\z/', $ticket)) {
            reply(['error' => 'ticket'], 400);
        }
        $report = execute($db, 'SELECT status, number FROM reports WHERE ticket = ?',
            [hash('sha256', $ticket)])->fetch(PDO::FETCH_ASSOC);
        if (!$report) {
            reply(['error' => 'expired'], 404);
        }
        if ($report['status'] === 'processing') {
            $report['status'] = 'pending';
        }
        reply($report);
    }

    $title = $input['title'] ?? null;
    $body = $input['body'] ?? null;
    if (!in_array($mode, ['github', 'anonymous'], true) ||
        !is_string($title) || !is_string($body) || trim($title) === '' ||
        trim($body) === '' || strlen($title) > 640 || strlen($body) > 24000 ||
        str_contains($title, "\n") || str_contains($title, "\r")) {
        reply(['error' => 'invalid_report'], 400);
    }

    // Limit submissions per source and across the endpoint, including authorization attempts.
    $hour = intdiv(time(), 3600);
    $ip = $_SERVER['REMOTE_ADDR'] ?? '';
    $source = hash_hmac('sha256', "$hour:$ip", $config['client_secret']);
    $db->beginTransaction();
    execute($db, 'DELETE FROM rate_limit WHERE expires <= ?', [time()]);
    foreach ([$source => 3, "global:$hour" => 30] as $bucket => $limit) {
        $counter = execute($db, 'INSERT INTO rate_limit (bucket, count, expires) VALUES (?, 1, ?)
            ON CONFLICT(bucket) DO UPDATE SET count = count + 1 WHERE count < ?',
            [$bucket, ($hour + 1) * 3600, $limit]);
        if ($counter->rowCount() === 0) {
            $db->rollBack();
            reply(['error' => 'rate_limit'], 429);
        }
    }
    $db->commit();

    if ($mode === 'anonymous') {
        $number = createIssue($title, $body . "\n\n_Gemeldet über NxFileViewer ohne GitHub-Konto._", installationToken($config));
        reply(['number' => $number], 201);
    }

    $ticket = randomToken();
    $state = randomToken();
    $verifier = randomToken();
    execute($db, "INSERT INTO reports (ticket, state, title, body, verifier, status, expires)
        VALUES (?, ?, ?, ?, ?, 'pending', ?)",
        [hash('sha256', $ticket), hash('sha256', $state), $title, $body, $verifier, time() + 650]);
    $authorization = 'https://github.com/login/oauth/authorize?' . http_build_query([
        'client_id' => $config['client_id'],
        'redirect_uri' => $config['callback_url'],
        'state' => $state,
        'code_challenge' => base64Url(hash('sha256', $verifier, true)),
        'code_challenge_method' => 'S256',
    ], encoding_type: PHP_QUERY_RFC3986);
    reply(['authorization' => $authorization, 'ticket' => $ticket]);
} catch (Throwable $error) {
    error_log('NxFileViewer feedback request failed (' . get_class($error) . ')');
    reply(['error' => 'service_unavailable'], 502);
}
