<?php
declare(strict_types=1);
require __DIR__.'/server/policy.php';
$count=0;
function check(bool $ok,string $name):void { global $count; $count++; if(!$ok)throw new RuntimeException($name); }
$start=1800000000; $end=$start+7*86400;
check(trialDeadline($start,$start)===$end,'trial starts without any payment data');
check(trialDeadline($start,$end-1)===$end,'last free second');
check(trialDeadline($start,$end)===0,'exactly seven days locks');
check(trialDeadline($start,$end+100)===0,'expired trial never extends');
check(trialDeadline(0,$start)===0,'missing server activation rejected');
check(trialDeadline($start+1,$start)===0,'future activation rejected');
$sub=['plan_id'=>'P-PAID','custom_id'=>'account','status'=>'ACTIVE','start_time'=>gmdate('c',$end)];
function grant(array $s,array $tx,int $now):int { return entitlement($s,$tx,'P-PAID','account',299,$now); }
check(grant($sub,[],$end)===0,'active unpaid subscription gives no second trial');
$sub['billing_info']['next_billing_time']=gmdate('c',$end+30*86400);
check(grant($sub,[],$end)===0,'future billing date is not payment');
$tx=['status'=>'COMPLETED','time'=>gmdate('c',$end),'amount_with_breakdown'=>['gross_amount'=>['currency_code'=>'EUR','value'=>'2.99']]];
check(grant($sub,[$tx],$end)===$end+30*86400,'first payment immediately grants thirty days');
check(grant($sub,[$tx],$end+30*86400)===0,'unpaid renewal locks');
foreach(['PENDING','FAILED','DECLINED','REFUNDED','PARTIALLY_REFUNDED'] as $status) {
    $bad=$tx; $bad['status']=$status; check(grant($sub,[$bad],$end)===0,$status.' is not paid');
}
$bad=$tx;$bad['amount_with_breakdown']['gross_amount']['value']='1.99';
check(grant($sub,[$bad],$end)===0,'wrong price rejected');
$bad=$tx;$bad['amount_with_breakdown']['gross_amount']['currency_code']='USD';
check(grant($sub,[$bad],$end)===0,'wrong currency rejected');
$bad=$sub;$bad['custom_id']='stolen';
check(grant($bad,[$tx],$end)===0,'stolen subscription rejected');
$bad=$sub;$bad['plan_id']='P-OLD-PAYPAL-TRIAL';
check(grant($bad,[$tx],$end)===0,'obsolete PayPal trial plan rejected');
$bad=$sub;$bad['plan_overridden']=true;
check(grant($bad,[$tx],$end)===0,'overridden plan rejected');
$bad=$sub;$bad['status']='CANCELLED';
check(grant($bad,[$tx],$end+1)===$end+30*86400,'cancelled renewal preserves paid remainder');
$bad['status']='SUSPENDED';
check(grant($bad,[$tx],$end+1)===0,'suspended subscription locks');
$bad=$tx;$bad['time']=gmdate('c',$end+1);
check(grant($sub,[$bad],$end)===0,'future payment rejected');
$bad=$tx;$bad['time']=gmdate('c',$end-1);
check(grant($sub,[$bad],$end)===0,'payment before subscription rejected');
echo "$count payment and free-trial checks passed\n";
