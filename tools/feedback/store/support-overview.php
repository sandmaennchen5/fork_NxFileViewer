<?php
declare(strict_types=1);
require_once __DIR__ . '/support.php';
require_once __DIR__ . '/usage.php';

function supportTestIdsSchema(PDO $db): void {
    $db->exec(databaseSql($db,
        'CREATE TABLE IF NOT EXISTS support_test_ids (
            admin_id TEXT NOT NULL, internal_id TEXT NOT NULL, PRIMARY KEY(admin_id, internal_id))',
        'CREATE TABLE IF NOT EXISTS support_test_ids (
            admin_id VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
            internal_id CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
            PRIMARY KEY(admin_id, internal_id)) ENGINE=InnoDB'));
}

function supportSetTestId(PDO $db, string $adminId, string $supportId, bool $marked): void {
    $internalId = supportQuery($db, 'SELECT internal_id FROM support_profiles WHERE support_id = ?', [$supportId])->fetchColumn();
    if ($internalId === false) throw new InvalidArgumentException('Unknown support ID');
    $sql = $marked
        ? databaseSql($db,
            'INSERT OR IGNORE INTO support_test_ids (admin_id, internal_id) VALUES (?, ?)',
            'INSERT INTO support_test_ids (admin_id, internal_id) VALUES (?, ?) ON DUPLICATE KEY UPDATE internal_id = VALUES(internal_id)')
        : 'DELETE FROM support_test_ids WHERE admin_id = ? AND internal_id = ?';
    supportQuery($db, $sql, [$adminId, $internalId]);
}

function supportOverview(PDO $db, int $days, ?int $now = null): array {
    $cutoff = ($now ?? time()) - $days * 86400;
    $row = supportQuery($db, 'SELECT COUNT(*) AS total,
        COALESCE(SUM(CASE WHEN u.internal_id IS NOT NULL THEN 1 ELSE 0 END), 0) AS with_usage,
        COALESCE(SUM(CASE WHEN u.last_seen >= ? THEN 1 ELSE 0 END), 0) AS active_usage
        FROM support_profiles p LEFT JOIN usage_statistics u ON u.internal_id = p.internal_id', [$cutoff])->fetch(PDO::FETCH_ASSOC);
    $row = array_map('intval', $row);
    $row['without_usage'] = $row['total'] - $row['with_usage'];
    $row['unmapped_usage'] = (int)$db->query('SELECT COUNT(*) FROM usage_statistics u
        WHERE NOT EXISTS (SELECT 1 FROM support_profiles p WHERE p.internal_id = u.internal_id)')->fetchColumn();
    return $row;
}

function supportIdOverviewRows(PDO $db, string $search, string $filter, int $days, int $page, ?int $now = null, ?string $adminId = null, bool $onlyTests = false): array {
    if (strlen($search) > 200 || $page < 1 || $page > 1000000) throw new InvalidArgumentException('Invalid search');
    $cutoff = ($now ?? time()) - $days * 86400;
    $where = '(LOWER(p.support_id) LIKE LOWER(?) OR LOWER(p.display_name) LIKE LOWER(?))';
    $args = array_fill(0, 2, '%' . $search . '%');
    switch ($filter) {
        case 'all': break;
        case 'reported': $where .= ' AND u.internal_id IS NOT NULL'; break;
        case 'never': $where .= ' AND u.internal_id IS NULL'; break;
        case 'active': $where .= ' AND u.last_seen >= ?'; $args[] = $cutoff; break;
        case 'inactive': $where .= ' AND u.last_seen < ?'; $args[] = $cutoff; break;
        default: throw new InvalidArgumentException('Invalid statistics filter');
    }
    $join = ' FROM support_profiles p LEFT JOIN usage_statistics u ON u.internal_id = p.internal_id LEFT JOIN usage_report_modes m ON m.internal_id=p.internal_id ';
    $testColumn = '0';
    if ($adminId !== null) {
        $join .= ' LEFT JOIN support_test_ids t ON t.internal_id = p.internal_id AND t.admin_id = ? ';
        array_unshift($args, $adminId);
        $testColumn = 'CASE WHEN t.internal_id IS NULL THEN 0 ELSE 1 END';
        if ($onlyTests) $where .= ' AND t.internal_id IS NOT NULL';
    }
    $total = (int)supportQuery($db, 'SELECT COUNT(*)' . $join . 'WHERE ' . $where, $args)->fetchColumn();
    $statement = $db->prepare('SELECT p.support_id, p.display_name, p.created_at, p.updated_at,
        u.version, u.platform, u.process_architecture, u.ui_framework, u.last_seen, u.os_description, u.distribution, u.runtime_version,
        m.report_type,m.system_reports_enabled,m.standard_last_seen,m.system_last_seen,' . $testColumn . ' AS is_test,
        (SELECT COUNT(*) FROM support_issues i WHERE i.internal_id = p.internal_id) AS issue_count'
        . $join . 'WHERE ' . $where . ' ORDER BY p.updated_at DESC, p.support_id ASC LIMIT ? OFFSET ?');
    foreach ($args as $index => $value) $statement->bindValue($index + 1, $value);
    $statement->bindValue(count($args) + 1, 100, PDO::PARAM_INT);
    $statement->bindValue(count($args) + 2, ($page - 1) * 100, PDO::PARAM_INT);
    $statement->execute();
    return ['total' => $total, 'rows' => $statement->fetchAll(PDO::FETCH_ASSOC)];
}
