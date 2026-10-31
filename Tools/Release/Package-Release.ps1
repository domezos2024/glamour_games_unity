param([string]$Version='3.0.0')
$ErrorActionPreference='Stop'
$w='E:\source\repos\glamour_games_unity'
$a='E:\AndroidStudioProjects\glamour_games_unity_android'
$release="$w\Builds\Release\$Version"
$player="$release\Windows"
$apk="$a\Builds\Release\$Version\GlamourGames-$Version-android-arm64.apk"
if(!(Test-Path "$player\GlamourGames.exe") -or !(Test-Path $apk)){throw 'Build both players before packaging.'}
foreach($f in @('LICENSE','THIRD_PARTY_NOTICES.md','CHANGELOG.md')){Copy-Item "$w\$f" "$player\$f" -Force}
New-Item -ItemType Directory -Force "$player\legal" | Out-Null
Copy-Item "$w\legal\MIT-legacy.txt" "$player\legal\MIT-legacy.txt" -Force
$iscc=Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
& $iscc "/DAppVer=$Version" "/DBuildDir=$player" "/DOutDir=$release" "$w\Tools\Installer\GlamourGames.iss" > "$w\Temp\release-installer.log"
if($LASTEXITCODE -ne 0){throw 'Installer failed'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stage=Join-Path $w ('Temp\release-portable-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $stage | Out-Null
Get-ChildItem -LiteralPath $player | Where-Object {$_.Name -notmatch 'BurstDebugInformation_DoNotShip|BackUpThisFolder_ButDontShipItWithYourGame'} | Copy-Item -Destination $stage -Recurse -Force
$zip="$release\GlamourGames-$Version-win-x64.zip"
if(Test-Path -LiteralPath $zip){Remove-Item -LiteralPath $zip}
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
$sdk='C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK'
$bt=Get-ChildItem "$sdk\build-tools" -Directory | Sort-Object Name -Descending | Select-Object -First 1
& "$($bt.FullName)\apksigner.bat" verify --print-certs $apk > "$a\Builds\Release\$Version\APK-signature.txt"
if($LASTEXITCODE -ne 0){throw 'APK signature invalid'}
$badging=& "$($bt.FullName)\aapt.exe" dump badging $apk
if(($badging -join [char]10) -notmatch "versionCode='30000'.*versionName='3.0.0'"){throw 'APK version mismatch'}
if(($badging -join [char]10) -notmatch 'android.permission.INTERNET'){throw 'APK INTERNET permission missing'}
$badging | Select-Object -First 4
foreach($r in @($release,"$a\Builds\Release\$Version")){
 $names=if($r -eq $release){@("GlamourGames-$Version-Setup.exe","GlamourGames-$Version-win-x64.zip")}else{@("GlamourGames-$Version-android-arm64.apk")}
 $sums=foreach($n in $names){$h=Get-FileHash -LiteralPath "$r\$n" -Algorithm SHA256; "$($h.Hash.ToLowerInvariant())  $n"}
 [IO.File]::WriteAllText("$r\SHA256SUMS.txt",($sums -join [char]10)+[char]10,[Text.UTF8Encoding]::new($false))
 Get-Item ($names | ForEach-Object {"$r\$_"}) | Select-Object Name,Length
}
'PACKAGING_OK'
