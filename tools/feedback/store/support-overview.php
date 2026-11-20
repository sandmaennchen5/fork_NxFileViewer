<?php
declare(strict_types=1);
require_once __DIR__ . '/support.php';
require_once __DIR__ . '/usage.php';

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

function supportIdOverviewRows(PDO $db, string $search, string $filter, int $days, int $page, ?int $now = null): array {
    if (strlen($search) > 200 || $page < 1 || $page > 1000000) throw new InvalidArgumentException('Invalid search');
    $cutoff = ($now ?? time()) - $days * 86400;
    $where = '(LOWER(p.support_id) LIKE LOWER(?) OR LOWER(p.internal_id) LIKE LOWER(?) OR LOWER(p.display_name) LIKE LOWER(?))';
    $args = array_fill(0, 3, '%' . $search . '%');
    switch ($filter) {
        case 'all': break;
        case 'reported': $where .= ' AND u.internal_id IS NOT NULL'; break;
        case 'never': $where .= ' AND u.internal_id IS NULL'; break;
        case 'active': $where .= ' AND u.last_seen >= ?'; $args[] = $cutoff; break;
        case 'inactive': $where .= ' AND u.last_seen < ?'; $args[] = $cutoff; break;
        default: throw new InvalidArgumentException('Invalid statistics filter');
    }
    $join = ' FROM support_profiles p LEFT JOIN usage_statistics u ON u.internal_id = p.internal_id ';
    $total = (int)supportQuery($db, 'SELECT COUNT(*)' . $join . 'WHERE ' . $where, $args)->fetchColumn();
    $statement = $db->prepare('SELECT p.internal_id, p.support_id, p.display_name, p.created_at, p.updated_at,
        u.version, u.platform, u.process_architecture, u.ui_framework, u.last_seen,
        (SELECT COUNT(*) FROM support_issues i WHERE i.internal_id = p.internal_id) AS issue_count'
        . $join . 'WHERE ' . $where . ' ORDER BY p.updated_at DESC, p.internal_id ASC LIMIT ? OFFSET ?');
    foreach ($args as $index => $value) $statement->bindValue($index + 1, $value);
    $statement->bindValue(count($args) + 1, 100, PDO::PARAM_INT);
    $statement->bindValue(count($args) + 2, ($page - 1) * 100, PDO::PARAM_INT);
    $statement->execute();
    return ['total' => $total, 'rows' => $statement->fetchAll(PDO::FETCH_ASSOC)];
}
