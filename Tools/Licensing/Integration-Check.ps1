$ErrorActionPreference='Stop'
$endpoint='https://domezos-ware.com/glamour_licensing/index.php'
function RequestTrial([string]$key) {
    $nonce=([guid]::NewGuid().ToString('N')+[guid]::NewGuid().ToString('N'))
    $body=@{action='verify';key=$key;platform='windows';device=('e'*64);nonce=$nonce}|ConvertTo-Json
    $reply=Invoke-RestMethod -Uri $endpoint -Method Post -ContentType 'application/json' -Body $body
    $raw=[Convert]::FromBase64String($reply.payload)
    $claims=[Text.Encoding]::UTF8.GetString($raw)|ConvertFrom-Json
    if($claims.kind -ne 'trial' -or $claims.nonce -ne $nonce){throw 'Trial response invalid'}
    $settings=Get-Content -LiteralPath 'E:\source\repos\glamour_games_unity\Assets\Resources\Glamour\LicenseSettings.json' -Raw|ConvertFrom-Json
    $rsa=[Security.Cryptography.RSA]::Create()
    $parameters=New-Object Security.Cryptography.RSAParameters
    $parameters.Modulus=[Convert]::FromBase64String($settings.modulus)
    $parameters.Exponent=[Convert]::FromBase64String($settings.exponent)
    $rsa.ImportParameters($parameters)
    if(!$rsa.VerifyData($raw,[Convert]::FromBase64String($reply.signature),[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)){throw 'Signature invalid'}
    $rsa.Dispose()
    if($claims.expires_at-$claims.issued_at -gt 120 -or $claims.expires_at -gt $claims.entitlement_until){throw 'Deadline invalid'}
    return $claims
}
$one=RequestTrial ('c'*64)
$two=RequestTrial ('d'*64)
if($one.entitlement_until -ne $two.entitlement_until){throw 'New key reset trial'}
Write-Output 'Live server: automatic trial without payment details, signature, nonce, 120-second lease and stable deadline across key changes verified.'
Write-Output ('Trial deadline (UTC): '+[DateTimeOffset]::FromUnixTimeSeconds($one.entitlement_until).ToString('o'))
