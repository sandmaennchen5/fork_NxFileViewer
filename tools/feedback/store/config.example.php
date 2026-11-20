<?php
return [
    'client_id' => 'Iv1.example',
    'client_secret' => '',
    'installation_id' => 0,
    'repository_id' => 0,
    'callback_url' => 'https://nxfile.web.de/report.php',
    'private_key' => __DIR__ . '/github-app.pem',

    'database_driver' => 'mariadb',
    'database_maintenance' => false,
    'mariadb' => [
        'host' => 'localhost',
        'port' => 3306,
        'database' => 'DEINE_DATENBANK',
        'user' => 'DEIN_DB_BENUTZER',
        'password' => '', // Hier das echte Datenbank-Kennwort lokal eintragen.
    ],

    'database' => __DIR__ . '/feedback.sqlite',
    // Optional support_secret may be set once. If omitted, poll_secret is used. Keep it stable.
    // Optional: separate random secret (32+ characters) for HTTPS cron Basic Auth, username queue.
    // 'queue_cron_secret' => 'generate-a-separate-private-random-secret',
    'poll_secret' => '', // At least 32 random characters, keep stable.
    'poll_admin_password_hash' => '', // Bootstrap password for username admin; afterwards accounts are managed in /admin/users.
    // Optional: stable private encryption key (at least 32 characters). Without it, support_secret / poll_secret is used.
    // Do not change the effective key while TOTP accounts exist.
    // 'admin_totp_key' => 'generate-a-private-random-key-before-enabling-2fa',
    'admin_security' => [
        'idle_timeout_seconds' => 1200,
        'absolute_timeout_seconds' => 28800,
        'login_ip_limit' => 10,
        'login_ip_window_seconds' => 3600,
        'login_global_limit' => 50,
        'login_global_window_seconds' => 900,
    ],
    'statistics_days' => 30,
];
