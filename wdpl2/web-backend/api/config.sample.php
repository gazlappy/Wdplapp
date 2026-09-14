<?php
/**
 * Template for api/config.php.
 *
 * The real config.php is written by the app's Web Control -> Connection &
 * Deploy page and is deliberately NOT part of this repository: code deploys
 * must never overwrite working credentials, and credentials must never reach
 * source control.
 *
 * The admin hash is PBKDF2-SHA256, written as
 *     pbkdf2-sha256$<iterations>$<base64 salt>$<base64 hash>
 * so that .NET and PHP can both produce and verify it without extra libraries.
 * To generate one by hand:
 *
 *   php -r "$s=random_bytes(16);$h=hash_pbkdf2('sha256','your-password',$s,210000,32,true);
 *           echo 'pbkdf2-sha256$210000$'.base64_encode($s).'$'.base64_encode($h),PHP_EOL;"
 */

return [
    // Database (cPanel prefixes both with your account name).
    'db_host'     => 'localhost',
    'db_name'     => 'youracct_wdpl',
    'db_user'     => 'youracct_wdpl',
    'db_password' => '',

    // Administrator account. Store only the hash, never the password.
    'admin_user'          => 'wdpladmin',
    'admin_password_hash' => '',

    // Random per-install secret used to hash rate-limit identifiers.
    'pepper' => '',

    // Exact public origin of the website, for CORS. Scheme + host (+ port).
    // Leave blank to refuse all cross-origin browser calls.
    'allowed_origin' => 'https://wdpl.uk',

    // Set true ONLY when a reverse proxy terminates TLS and sets
    // X-Forwarded-Proto. On normal cPanel hosting leave this false.
    'behind_tls_proxy' => false,

    // Escape hatch for local testing only. Never true in production.
    'allow_insecure' => false,

    'max_body_bytes' => 262144,
];
