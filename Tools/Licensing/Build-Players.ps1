param([switch]$SkipWindows)
$ErrorActionPreference = 'Stop'
$winRoot = 'E:\source\repos\glamour_games_unity'
$androidRoot = 'E:\AndroidStudioProjects\glamour_games_unity_android'
$unityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
$statusPath = Join-Path $winRoot 'Temp\licensing-player-builds.json'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $statusPath) | Out-Null
function Status([string]$phase, [string]$detail = '') {
    [IO.File]::WriteAllText($statusPath, (@{phase=$phase;detail=$detail;at=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
}
function BuildPlayer([string]$root, [string]$platform, [string]$output) {
    New-Item -ItemType Directory -Force -Path (Join-Path $root 'Temp') | Out-Null
    $buildArgs = @('-batchmode','-quit','-projectPath',$root,'-executeMethod','GlamourGames.EditorTools.ProjectBootstrap.CiBuild',
        '-buildPath',$output,'-logFile',(Join-Path $root 'Temp\licensing-player-build.log'))
    if ($platform -eq 'Android') { $buildArgs += @('-buildTarget','Android') }
    $process = Start-Process -FilePath $unityPath -ArgumentList $buildArgs -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    $process.Refresh()
    if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $output)) { throw "$platform build failed (exit $($process.ExitCode)); see build log." }
}
$previous = @{}
foreach ($name in @('GLAMOUR_KEYSTORE','GLAMOUR_KEYSTORE_PASS','GLAMOUR_KEY_ALIAS','GLAMOUR_KEY_PASS')) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name,'Process')
}
try {
    if (!$SkipWindows) {
        Status 'windows'
        BuildPlayer $winRoot 'Windows' (Join-Path $winRoot 'Builds\LicenseTest\GlamourGames.exe')
    }
    Status 'android-signing'
    $signingPath = 'E:\Claude_Projekte\android_signing\keystore.properties'
    if (!(Test-Path -LiteralPath $signingPath)) { throw 'Android signing properties missing.' }
    $props = @{}
    $aliasMap = @{GLAMOUR_KEYSTORE='storeFile';GLAMOUR_KEYSTORE_PASS='storePassword';GLAMOUR_KEY_ALIAS='keyAlias';GLAMOUR_KEY_PASS='keyPassword'}
    foreach ($line in [IO.File]::ReadAllLines($signingPath)) {
        if ($line -match '^\s*(storeFile|storePassword|keyAlias|keyPassword|GLAMOUR_KEYSTORE|GLAMOUR_KEYSTORE_PASS|GLAMOUR_KEY_ALIAS|GLAMOUR_KEY_PASS)\s*=\s*(.*)$') {
            $propertyName = $matches[1]
            $propertyValue = $matches[2]
            if ($aliasMap.ContainsKey($propertyName)) { $propertyName = $aliasMap[$propertyName] }
            $props[$propertyName] = $propertyValue
        }
    }
    foreach ($required in @('storeFile','storePassword','keyAlias','keyPassword')) {
        if ([string]::IsNullOrEmpty($props[$required])) { throw 'Android signing property missing.' }
    }
    $storeFile = $props['storeFile'].Replace('\:',':').Replace('\\','\')
    if (![IO.Path]::IsPathRooted($storeFile)) { $storeFile = Join-Path (Split-Path -Parent $signingPath) $storeFile }
    if (!(Test-Path -LiteralPath $storeFile)) {
        $movedStore = $storeFile.Replace('E:\Claude Projekte\', 'E:\Claude_Projekte\')
        if (Test-Path -LiteralPath $movedStore) { $storeFile = $movedStore }
        else {
            $availableStores = @(Get-ChildItem -LiteralPath (Split-Path -Parent $signingPath) -File | Where-Object { $_.Extension -in @('.jks','.keystore') })
            if ($availableStores.Count -eq 1) { $storeFile = $availableStores[0].FullName }
            else { throw 'Android signing keystore missing or ambiguous.' }
        }
    }
    [Environment]::SetEnvironmentVariable('GLAMOUR_KEYSTORE',$storeFile,'Process')
    [Environment]::SetEnvironmentVariable('GLAMOUR_KEYSTORE_PASS',$props['storePassword'],'Process')
    [Environment]::SetEnvironmentVariable('GLAMOUR_KEY_ALIAS',$props['keyAlias'],'Process')
    [Environment]::SetEnvironmentVariable('GLAMOUR_KEY_PASS',$props['keyPassword'],'Process')
    Status 'android'
    BuildPlayer $androidRoot 'Android' (Join-Path $androidRoot 'Builds\Android\GlamourGamesUnityAndroid-licensing.apk')
    Status 'done'
} catch {
    Status 'failed' $_.Exception.Message
    exit 1
} finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name,$previous[$name],'Process') }
}
