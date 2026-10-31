<?php
declare(strict_types=1);
// One-time, fixed merchant-catalog migration. No customer subscription or charge is created.
function migratePaidPlans(array $config): array {
    $dir=__DIR__.'/_private';
    $lock=fopen($dir.'/catalog-migration.lock','c');
    if (!$lock || !flock($lock,LOCK_EX|LOCK_NB)) throw new RuntimeException('Migration busy');
    $latest=require $dir.'/config.php';
    if (!empty($latest['trial_without_payment'])) { flock($lock,LOCK_UN); fclose($lock); return $latest; }
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
        return $result === '' ? [] : json_decode($result, true, 64, JSON_THROW_ON_ERROR);
    }

    $stateFile=$dir.'/paid-plans-v2.json';
    $state=is_file($stateFile)?json_decode(file_get_contents($stateFile),true,32,JSON_THROW_ON_ERROR):[];
    $oldPlans=$config['plans'];
    foreach ($oldPlans as $platform=>$old) {
        if (empty($state[$platform])) {
            $detail=api($base,$config,'GET','/v1/billing/plans/'.rawurlencode($old['id']));
            $name=$platform==='windows'?'Windows':'Android';
            $created=api($base,$config,'POST','/v1/billing/plans',[
                'product_id'=>$detail['product_id'],'name'=>$name.' - '.number_format($old['cents']/100,2,',','').' EUR / 30 Tage',
                'description'=>'Kostenpflichtiges Abo nach der kostenlosen App-Testzeit. Keine weitere PayPal-Testphase.',
                'status'=>'ACTIVE','billing_cycles'=>[[
                    'frequency'=>['interval_unit'=>'DAY','interval_count'=>30],'tenure_type'=>'REGULAR',
                    'sequence'=>1,'total_cycles'=>0,'pricing_scheme'=>['fixed_price'=>[
                        'value'=>number_format($old['cents']/100,2,'.',''),'currency_code'=>'EUR']]
                ]],'payment_preferences'=>['auto_bill_outstanding'=>false,'payment_failure_threshold'=>1]
            ],'glamour-paid-v2-'.$platform);
            if (!preg_match('/^P-[A-Z0-9]+$/D',$created['id']??'')) throw new RuntimeException('Invalid new plan');
            $state[$platform]=['id'=>$created['id'],'cents'=>$old['cents']];
            file_put_contents($stateFile,json_encode($state,JSON_THROW_ON_ERROR),LOCK_EX);
        }
    }
    foreach ($oldPlans as $old) {
        $detail=api($base,$config,'GET','/v1/billing/plans/'.rawurlencode($old['id']));
        if (($detail['status']??'')==='ACTIVE') api($base,$config,'POST','/v1/billing/plans/'.rawurlencode($old['id']).'/deactivate',null);
    }
    $config['legacy_trial_plans']=$oldPlans;
    $config['plans']=$state;
    $config['trial_without_payment']=true;
    $config['billing_model']=2;
    if (!is_file($dir.'/config.pre-device-trial.php')) copy($dir.'/config.php',$dir.'/config.pre-device-trial.php');
    $php="<?php\nreturn json_decode(base64_decode('".base64_encode(json_encode($config,JSON_THROW_ON_ERROR))."'),true,512,JSON_THROW_ON_ERROR);\n";
    file_put_contents($dir.'/config.next.php',$php,LOCK_EX); chmod($dir.'/config.next.php',0600);
    rename($dir.'/config.next.php',$dir.'/config.php');
    flock($lock,LOCK_UN); fclose($lock);
    return $config;
}
