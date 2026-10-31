<?php
declare(strict_types=1);
require_once __DIR__ . '/policy.php';
header('Cache-Control: no-store');
header('X-Content-Type-Options: nosniff');
header('X-Robots-Tag: noindex, nofollow');
header("Content-Security-Policy: default-src 'none'; frame-ancestors 'none'");
header('Referrer-Policy: no-referrer');
function reply(array $data, int $code = 200): never {
    http_response_code($code); header('Content-Type: application/json; charset=utf-8');
    echo json_encode($data, JSON_THROW_ON_ERROR); exit;
}
function issueLease(array $config, string $account, string $platform, string $nonce, int $until, string $kind): never {
    $now = time();
    if ($until <= $now + 1) reply(['error' => 'Testzeit oder bezahlter Zeitraum abgelaufen. Zahlung erforderlich.'], 403);
    $payload = json_encode(['account'=>$account,'platform'=>$platform,'nonce'=>$nonce,'kind'=>$kind,
        'issued_at'=>$now,'expires_at'=>min($until-1,$now+120),'entitlement_until'=>$until], JSON_THROW_ON_ERROR);
    $key = require $config['private_key_file'];
    if (!openssl_sign($payload,$signature,$key,OPENSSL_ALGO_SHA256)) throw new RuntimeException('Signing unavailable');
    reply(['payload'=>base64_encode($payload),'signature'=>base64_encode($signature)]);
}
if ($_SERVER['REQUEST_METHOD'] === 'GET' && isset($_GET['return'])) {
    header('Content-Type: text/plain; charset=utf-8');
    echo "Bitte zur App zurueckkehren und 'Zahlung pruefen' waehlen. Dieser Bildschirm bestaetigt keine Zahlung."; exit;
}
if ($_SERVER['REQUEST_METHOD'] !== 'POST') reply(['error'=>'POST required'],405);
if ((int)($_SERVER['CONTENT_LENGTH'] ?? 0) > 8192) reply(['error'=>'Request too large'],413);
try {
    $privateDir = __DIR__ . '/_private';
    if (!is_file($privateDir.'/config.php')) reply(['error'=>'Lizenzdienst noch nicht aktiviert.'],503);
    $config = require $privateDir.'/config.php';
    if (empty($config['enabled'])) reply(['error'=>'Lizenzdienst noch nicht aktiviert.'],503);
    $raw = file_get_contents('php://input',false,null,0,8193);
    if (strlen($raw)>8192) reply(['error'=>'Request too large'],413);
    $request=json_decode($raw,true,16,JSON_THROW_ON_ERROR);
    $key=$request['key']??''; $platform=$request['platform']??''; $nonce=$request['nonce']??'';
    $device=$request['device']??''; $action=$request['action']??'';
    foreach ([$key,$nonce,$device] as $value)
        if (!is_string($value)||!preg_match('/^[a-f0-9]{64}$/D',$value)) reply(['error'=>'Invalid request'],400);
    if (!is_string($platform)||!isset($config['plans'][$platform])||!in_array($action,['checkout','verify'],true))
        reply(['error'=>'Invalid request'],400);
    $account=hash('sha256',$key); $plan=$config['plans'][$platform]; $now=time();
    $db=new PDO('sqlite:'.$config['database_file'],null,null,[PDO::ATTR_ERRMODE=>PDO::ERRMODE_EXCEPTION]);
    $db->exec('PRAGMA busy_timeout=5000');
    $db->exec('CREATE TABLE IF NOT EXISTS licenses (account TEXT,platform TEXT,subscription TEXT UNIQUE,approval TEXT,request_id TEXT,created INTEGER,PRIMARY KEY(account,platform));
        CREATE TABLE IF NOT EXISTS device_trials (device TEXT,platform TEXT,started INTEGER NOT NULL,PRIMARY KEY(device,platform));
        CREATE TABLE IF NOT EXISTS limits (ip TEXT PRIMARY KEY,window INTEGER,hits INTEGER)');
    $ip=hash('sha256',$_SERVER['REMOTE_ADDR']??'unknown');
    $db->exec('BEGIN IMMEDIATE');
    $q=$db->prepare('SELECT window,hits FROM limits WHERE ip=?'); $q->execute([$ip]); $limit=$q->fetch(PDO::FETCH_ASSOC);
    $fresh=!$limit||(int)$limit['window']<=$now-60;
    $window=$fresh?$now:(int)$limit['window']; $hits=$fresh?1:(int)$limit['hits']+1;
    $db->prepare('INSERT OR REPLACE INTO limits VALUES(?,?,?)')->execute([$ip,$window,$hits]);
    $db->prepare('DELETE FROM limits WHERE window<?')->execute([$now-3600]);
    if ($hits<=30 && $action==='verify') {
        // Stable OS device identifier, hashed in the app. New local keys never reset this deadline.
        $db->prepare('INSERT OR IGNORE INTO device_trials VALUES(?,?,?)')->execute([$device,$platform,$now]);
    }
    $q=$db->prepare('SELECT started FROM device_trials WHERE device=? AND platform=?'); $q->execute([$device,$platform]);
    $started=(int)$q->fetchColumn(); $db->exec('COMMIT');
    if ($hits>30) reply(['error'=>'Bitte spaeter erneut versuchen.'],429);
    if ($action==='verify' && ($until=trialDeadline($started,$now))>0)
        issueLease($config,$account,$platform,$nonce,$until,'trial');
    $query=$db->prepare('SELECT * FROM licenses WHERE account=? AND platform=?');
    $query->execute([$account,$platform]); $license=$query->fetch(PDO::FETCH_ASSOC);
    $base=!empty($config['sandbox'])?'https://api-m.sandbox.paypal.com':'https://api-m.paypal.com';
    function api(string $base, array $config, string $method, string $path, ?array $body = null, ?string $requestId = null): array {
        static $token = null;
        if ($token === null) {
            $ch = curl_init($base . '/v1/oauth2/token');
            curl_setopt_array($ch, [CURLOPT_RETURNTRANSFER => true, CURLOPT_POST => true,
                CURLOPT_USERPWD => $config['client_id'] . ':' . $config['client_secret'],
                CURLOPT_POSTFIELDS => 'grant_type=client_credentials', CURLOPT_TIMEOUT => 12,
                CURLOPT_CONNECTTIMEOUT => 5, CURLOPT_SSL_VERIFYPEER => true, CURLOPT_SSL_VERIFYHOST => 2]);
            $result = curl_exec($ch); $status = curl_getinfo($ch, CURLINFO_RESPONSE_CODE); curl_close($ch);
            if ($result === false || $status !== 200) throw new RuntimeException('PayPal authentication unavailable');
            $token = json_decode($result, true, 32, JSON_THROW_ON_ERROR)['access_token'] ?? null;
            if (!is_string($token)) throw new RuntimeException('Missing token');
        }
        $headers = ['Authorization: Bearer ' . $token, 'Content-Type: application/json'];
        if ($requestId !== null) $headers[] = 'PayPal-Request-Id: ' . $requestId;
        $ch = curl_init($base . $path);
        curl_setopt_array($ch, [CURLOPT_RETURNTRANSFER => true, CURLOPT_CUSTOMREQUEST => $method,
            CURLOPT_HTTPHEADER => $headers, CURLOPT_TIMEOUT => 15, CURLOPT_CONNECTTIMEOUT => 5,
            CURLOPT_SSL_VERIFYPEER => true, CURLOPT_SSL_VERIFYHOST => 2]);
        if ($body !== null) curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($body, JSON_THROW_ON_ERROR));
        $result = curl_exec($ch); $status = curl_getinfo($ch, CURLINFO_RESPONSE_CODE); curl_close($ch);
        if ($result === false || $status < 200 || $status >= 300) throw new RuntimeException('PayPal request unavailable');
        return json_decode($result, true, 64, JSON_THROW_ON_ERROR);
    }

    if ($action==='checkout') {
        if (empty($config['trial_without_payment']) || empty($config['client_id']) || empty($config['client_secret']))
            reply(['error'=>'Zahlungsdienst wird gerade eingerichtet.'],503);
        if ($license && !empty($license['subscription'])) {
            $existing=api($base,$config,'GET','/v1/billing/subscriptions/'.rawurlencode($license['subscription']));
            if (($existing['status']??'')==='APPROVAL_PENDING' && !empty($license['approval']))
                reply(['approval_url'=>$license['approval']]);
            if (!in_array($existing['status']??'',['CANCELLED','EXPIRED'],true))
                reply(['error'=>'Ein Abo ist bereits vorhanden. Bitte Zahlung pruefen oder das Abo bei PayPal verwalten.'],409);
            // A new paid subscription after cancellation has no second free trial.
            $db->prepare('DELETE FROM licenses WHERE account=? AND platform=?')->execute([$account,$platform]);
        }
        $db->exec('BEGIN IMMEDIATE');
        $db->prepare('INSERT OR IGNORE INTO licenses(account,platform,request_id,created) VALUES(?,?,?,?)')
            ->execute([$account,$platform,bin2hex(random_bytes(16)),$now]);
        $query->execute([$account,$platform]); $license=$query->fetch(PDO::FETCH_ASSOC); $db->exec('COMMIT');
        if (!empty($license['approval'])) reply(['approval_url'=>$license['approval']]);
        $sub=api($base,$config,'POST','/v1/billing/subscriptions',[
            'plan_id'=>$plan['id'],'custom_id'=>$account,
            'application_context'=>['brand_name'=>'Glamour Games','shipping_preference'=>'NO_SHIPPING',
                'user_action'=>'SUBSCRIBE_NOW','return_url'=>$config['public_url'].'?return=success',
                'cancel_url'=>$config['public_url'].'?return=cancel']
        ],$license['request_id']);
        $approval='';
        foreach ($sub['links']??[] as $link) if (($link['rel']??'')==='approve') $approval=$link['href'];
        if (!preg_match('/^I-[A-Z0-9]+$/D',$sub['id']??'') || parse_url($approval,PHP_URL_SCHEME)!=='https' ||
            parse_url($approval,PHP_URL_HOST)!==(!empty($config['sandbox'])?'www.sandbox.paypal.com':'www.paypal.com'))
            throw new RuntimeException('Invalid approval response');
        $db->prepare('UPDATE licenses SET subscription=?,approval=? WHERE account=? AND platform=?')
            ->execute([$sub['id'],$approval,$account,$platform]);
        reply(['approval_url'=>$approval]);
    }
    if (!$license || empty($license['subscription']))
        reply(['error'=>'Deine 7 kostenlosen Tage sind abgelaufen. Du kannst jetzt freiwillig ein PayPal-Abo abschliessen.'],403);
    $sub=api($base,$config,'GET','/v1/billing/subscriptions/'.rawurlencode($license['subscription']));
    if (($sub['id']??'')!==$license['subscription']) reply(['error'=>'Lizenzzuordnung ungueltig.'],403);
    $from=gmdate('Y-m-d\TH:i:s\Z',$now-31*86400); $to=gmdate('Y-m-d\TH:i:s\Z',$now);
    $tx=api($base,$config,'GET','/v1/billing/subscriptions/'.rawurlencode($license['subscription']).
        '/transactions?start_time='.rawurlencode($from).'&end_time='.rawurlencode($to));
    $until=entitlement($sub,$tx['transactions']??[],$plan['id'],$account,$plan['cents'],$now);
    issueLease($config,$account,$platform,$nonce,$until,'paid');
} catch (JsonException $e) {
    reply(['error'=>'Invalid JSON'],400);
} catch (Throwable $e) {
    error_log('Glamour license request failed: '.get_class($e));
    reply(['error'=>'Lizenzpruefung derzeit nicht moeglich. Bitte online erneut versuchen.'],503);
}
