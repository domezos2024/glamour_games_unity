<?php
// Real credentials belong only in /dw.com/glamour_licensing/_private/config.php on server com.
return [
    'enabled'=>false, 'sandbox'=>true, 'trial_without_payment'=>true, 'billing_model'=>2,
    'client_id'=>'', 'client_secret'=>'',
    'private_key_file'=>__DIR__.'/signing-key.php', 'database_file'=>__DIR__.'/licenses.sqlite',
    'public_url'=>'https://domezos-ware.com/glamour_licensing/index.php',
    // Live paid plans WITHOUT a PayPal trial. Use separate plans for sandbox testing.
    'plans'=>[
        'windows'=>['id'=>'P-5FV55168KL4254910NLSSYNA','cents'=>299],
        'android'=>['id'=>'P-86J656498A592441KNLSSYNI','cents'=>199],
    ],
];
