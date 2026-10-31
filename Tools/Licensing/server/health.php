<?php
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
$checks = [
    'php_supported' => PHP_VERSION_ID >= 80100,
    'curl' => extension_loaded('curl'),
    'openssl' => extension_loaded('openssl'),
    'sqlite' => extension_loaded('pdo_sqlite'),
];
$configFile = __DIR__ . '/_private/config.php';
$checks['configured'] = is_file($configFile);
if (!in_array(false, $checks, true)) {
    try {
        $config = require $configFile;
        $checks['enabled'] = !empty($config['enabled']);
        $checks['storage_writable'] = is_writable(dirname($config['database_file']));
        $key = require $config['private_key_file'];
        $checks['signing'] = openssl_sign('health', $signature, $key, OPENSSL_ALGO_SHA256);
        $cacheFile = __DIR__ . '/_private/health-cache.json';
        $digest = hash('sha256', json_encode($config));
        $cache = is_file($cacheFile) ? json_decode(file_get_contents($cacheFile), true) : null;
        if (!$cache || ($cache['digest'] ?? '') !== $digest || ($cache['at'] ?? 0) < time() - 60) {
            $lock = fopen(__DIR__ . '/_private/health.lock', 'c');
            if (!$lock || !flock($lock, LOCK_EX | LOCK_NB)) throw new RuntimeException('Busy');
            $base = !empty($config['sandbox']) ? 'https://api-m.sandbox.paypal.com' : 'https://api-m.paypal.com';
            $ch = curl_init($base . '/v1/oauth2/token');
            curl_setopt_array($ch, [CURLOPT_RETURNTRANSFER => true, CURLOPT_POST => true,
                CURLOPT_USERPWD => $config['client_id'] . ':' . $config['client_secret'],
                CURLOPT_POSTFIELDS => 'grant_type=client_credentials', CURLOPT_TIMEOUT => 12,
                CURLOPT_CONNECTTIMEOUT => 5, CURLOPT_SSL_VERIFYPEER => true, CURLOPT_SSL_VERIFYHOST => 2]);
            $body = curl_exec($ch); $status = curl_getinfo($ch, CURLINFO_RESPONSE_CODE); curl_close($ch);
            if ($status !== 200 || $body === false) throw new RuntimeException('Authentication failed');
            $token = json_decode($body, true, 32, JSON_THROW_ON_ERROR)['access_token'];
            $providerChecks = ['paypal_auth' => true, 'trial_without_payment' => !empty($config['trial_without_payment'])];
            foreach ($config['plans'] as $platform => $plan) {
                $ch = curl_init($base . '/v1/billing/plans/' . rawurlencode($plan['id']));
                curl_setopt_array($ch, [CURLOPT_RETURNTRANSFER => true, CURLOPT_HTTPHEADER => ['Authorization: Bearer ' . $token],
                    CURLOPT_TIMEOUT => 12, CURLOPT_CONNECTTIMEOUT => 5, CURLOPT_SSL_VERIFYPEER => true, CURLOPT_SSL_VERIFYHOST => 2]);
                $body = curl_exec($ch); $status = curl_getinfo($ch, CURLINFO_RESPONSE_CODE); curl_close($ch);
                $detail = $body !== false ? json_decode($body, true, 32, JSON_THROW_ON_ERROR) : [];
                $cycles = $detail['billing_cycles'] ?? [];
                $regular = $cycles[0] ?? [];
                $providerChecks[$platform . '_plan'] = $status === 200 && ($detail['status'] ?? '') === 'ACTIVE' &&
                    count($cycles) === 1 && ($regular['tenure_type'] ?? '') === 'REGULAR' &&
                    ($regular['frequency']['interval_unit'] ?? '') === 'DAY' && (int)($regular['frequency']['interval_count'] ?? 0) === 30 &&
                    (int)($regular['total_cycles'] ?? -1) === 0 &&
                    ($regular['pricing_scheme']['fixed_price']['currency_code'] ?? '') === 'EUR' &&
                    (int)round((float)($regular['pricing_scheme']['fixed_price']['value'] ?? 0) * 100) === $plan['cents'];
            }
            $cache = ['at' => time(), 'digest' => $digest, 'checks' => $providerChecks];
            file_put_contents($cacheFile, json_encode($cache, JSON_THROW_ON_ERROR), LOCK_EX);
            flock($lock, LOCK_UN); fclose($lock);
        }
        $checks += $cache['checks'];
    } catch (Throwable $e) {
        $checks['provider_ready'] = false;
    }
}
http_response_code(in_array(false, $checks, true) ? 503 : 200);
echo json_encode(['ready' => !in_array(false, $checks, true), 'checks' => $checks]);
