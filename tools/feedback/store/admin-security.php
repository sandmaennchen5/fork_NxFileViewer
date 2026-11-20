<?php
declare(strict_types=1);
require_once __DIR__ . '/database.php';

final class AdminLoginRateLimited extends DomainException {
    public function __construct(public readonly int $retryAfter) {
        parent::__construct('Anmeldung fehlgeschlagen oder zu viele Versuche. Bitte später erneut versuchen.');
    }
}

function adminSecurityPolicy(array $config): array {
    $defaults = [
        'idle_timeout_seconds' => 1200, 'absolute_timeout_seconds' => 28800,
        'login_ip_limit' => 10, 'login_ip_window_seconds' => 3600,
        'login_global_limit' => 50, 'login_global_window_seconds' => 900,
    ];
    $input = $config['admin_security'] ?? [];
    if (!is_array($input)) throw new InvalidArgumentException('Invalid admin security configuration');
    $ranges = [
        'idle_timeout_seconds' => [60, 86400], 'absolute_timeout_seconds' => [300, 604800],
        'login_ip_limit' => [1, 1000], 'login_ip_window_seconds' => [60, 86400],
        'login_global_limit' => [1, 1000], 'login_global_window_seconds' => [60, 86400],
    ];
    foreach ($defaults as $key => $default) {
        $value = $input[$key] ?? $default;
        if (!is_int($value) || $value < $ranges[$key][0] || $value > $ranges[$key][1])
            throw new InvalidArgumentException('Invalid admin security limit');
        $defaults[$key] = $value;
    }
    if ($defaults['absolute_timeout_seconds'] < $defaults['idle_timeout_seconds'])
        throw new InvalidArgumentException('Absolute admin timeout must not be shorter than idle timeout');
    return $defaults;
}

function adminSessionExpired(array $session, array $policy, int $now): bool {
    if (empty($session['poll_admin'])) return false;
    $started = $session['admin_started_at'] ?? null;
    $last = $session['admin_last_seen'] ?? null;
    return !is_int($started) || !is_int($last) || $started > $last || $last > $now ||
        $now - $started >= $policy['absolute_timeout_seconds'] ||
        $now - $last >= $policy['idle_timeout_seconds'];
}

function adminResetSession(): void {
    $_SESSION = [];
    if (!session_regenerate_id(true)) throw new RuntimeException('Cannot renew admin session');
    $_SESSION['csrf'] = bin2hex(random_bytes(32));
}

function adminStartSession(array $policy): bool {
    ini_set('session.use_strict_mode', '1');
    ini_set('session.use_only_cookies', '1');
    ini_set('session.use_trans_sid', '0');
    session_name('NXFILEADMIN');
    session_set_cookie_params([
        'lifetime' => 0, 'path' => '/admin', 'secure' => true,
        'httponly' => true, 'samesite' => 'Lax',
    ]);
    if (!session_start()) throw new RuntimeException('Cannot start admin session');
    $now = time();
    $expired = adminSessionExpired($_SESSION, $policy, $now);
    if ($expired) adminResetSession();
    elseif (!empty($_SESSION['poll_admin'])) $_SESSION['admin_last_seen'] = $now;
    $_SESSION['csrf'] ??= bin2hex(random_bytes(32));
    return $expired;
}

// Existing table supports both IP and account-wide counters without a schema migration.
// Reserve attempts atomically before doing password verification; no DB lock is held
// during the expensive hash check. Blocked IPs do not consume the global allowance.
function adminReserveLogin(PDO $db, array $config, array $policy, string $ip, ?int $now = null): void {
    $now ??= time();
    $secret = $config['support_secret'] ?? $config['poll_secret'] ?? '';
    if (!is_string($secret) || strlen($secret) < 32) throw new RuntimeException('Configure admin secret');
    databaseSqliteSchema($db, 'CREATE TABLE IF NOT EXISTS admin_login_rate (bucket TEXT PRIMARY KEY, attempts INTEGER, expires INTEGER)');
    $retry = 0;
    $db->beginTransaction();
    try {
        $db->prepare('DELETE FROM admin_login_rate WHERE expires <= ?')->execute([$now]);
        foreach (['ip', 'global'] as $scope) {
            $window = $policy['login_' . $scope . '_window_seconds'];
            $limit = $policy['login_' . $scope . '_limit'];
            $slot = intdiv($now, $window);
            $expires = ($slot + 1) * $window;
            $identity = $scope === 'ip' ? $ip : 'shared-admin-account';
            $bucket = hash_hmac('sha256', 'admin-v2:' . $scope . ':' . $slot . ':' . $identity, $secret);
            $statement = $db->prepare(databaseSql($db,
                'INSERT INTO admin_login_rate (bucket, attempts, expires) VALUES (?, 1, ?)
                 ON CONFLICT(bucket) DO UPDATE SET attempts = attempts + 1',
                'INSERT INTO admin_login_rate (bucket, attempts, expires) VALUES (?, 1, ?)
                 ON DUPLICATE KEY UPDATE attempts = attempts + 1'));
            $statement->execute([$bucket, $expires]);
            $statement = $db->prepare('SELECT attempts FROM admin_login_rate WHERE bucket = ?');
            $statement->execute([$bucket]);
            $attempts = (int)$statement->fetchColumn();
            $statement->closeCursor();
            if ($attempts > $limit) {
                $retry = max(1, $expires - $now);
                break;
            }
        }
        $db->commit();
    } catch (Throwable $error) {
        if ($db->inTransaction()) $db->rollBack();
        throw $error;
    }
    if ($retry > 0) throw new AdminLoginRateLimited($retry);
}
