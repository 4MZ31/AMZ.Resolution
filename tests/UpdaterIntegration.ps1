param(
    [Parameter(Mandatory = $true)][string]$ApplicationExe,
    [string]$PayloadExe,
    [switch]$LegacyProtocol,
    [switch]$RequireRun
)
$ErrorActionPreference = 'Stop'
$applicationPath = (Resolve-Path -LiteralPath $ApplicationExe).Path
$payloadPath = if ($PayloadExe) { (Resolve-Path -LiteralPath $PayloadExe).Path } else { $applicationPath }
$targetName = if ($LegacyProtocol) { 'ResolutionTray.exe' } else { 'AMZ.Resolution.exe' }
$helperName = if ($LegacyProtocol) { 'ResolutionTray.Updater.exe' } else { 'AMZ.Resolution.Updater.exe' }
$payloadProduct = if ($LegacyProtocol) { 'ResolutionTray' } else { 'AMZ.Resolution' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$dataRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'AmdResolutionTray'
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$mutexName = 'Local\AmdResolutionTray-' + $sid
$mutex = [Threading.Mutex]::new($false, $mutexName)
$free = $false
try {
    try { $free = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $free = $true }
    if ($free) { $mutex.ReleaseMutex() }
}
finally { $mutex.Dispose() }
if (-not $free -or (Test-Path -LiteralPath (Join-Path $dataRoot 'recovery.json')) -or (Test-Path -LiteralPath (Join-Path $dataRoot 'last-update.json'))) {
    if ($RequireRun) { throw 'Updater integration tests require an isolated profile with no active application or recovery session.' }
    Write-Output 'SKIP: existing application, recovery session or update notification; user state preserved.'
    exit 0
}
$versionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($payloadPath)
$version = '{0}.{1}.{2}' -f $versionInfo.FileMajorPart, $versionInfo.FileMinorPart, $versionInfo.FileBuildPart
$failureDir = Join-Path $artifactsRoot 'update-failure-stub'
dotnet publish (Join-Path $PSScriptRoot 'UpdateFailureStub\UpdateFailureStub.csproj') -c Release -r win-x64 --self-contained true -o $failureDir -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false -p:Version=$version -p:Product=$payloadProduct
if ($LASTEXITCODE -ne 0) { throw 'Failure stub build failed.' }
$originalHash = (Get-FileHash -LiteralPath $applicationPath -Algorithm SHA256).Hash.ToLowerInvariant()
$expectedSettings = Join-Path $dataRoot 'settings.json'
$settingsHash = if (Test-Path -LiteralPath $expectedSettings) { (Get-FileHash -LiteralPath $expectedSettings -Algorithm SHA256).Hash } else { $null }
foreach ($case in @('success', 'rollback')) {
    $jobId = [Guid]::NewGuid()
    $stage = Join-Path (Join-Path $dataRoot 'Updates') $jobId.ToString('N')
    $targetDir = [IO.Path]::GetFullPath((Join-Path $artifactsRoot ('updater-integration-' + $jobId.ToString('N'))))
    if (-not $targetDir.StartsWith($artifactsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe integration target.' }
    New-Item -ItemType Directory -Path $stage, $targetDir -Force | Out-Null
    $target = Join-Path $targetDir $targetName
    $helperPath = Join-Path $stage $helperName
    $payload = Join-Path $stage 'payload.exe'
    Copy-Item -LiteralPath $applicationPath -Destination $target
    Copy-Item -LiteralPath $applicationPath -Destination $helperPath
    $sourcePayload = if ($case -eq 'success') { $payloadPath } else { Join-Path $failureDir 'AMZ.Resolution.exe' }
    Copy-Item -LiteralPath $sourcePayload -Destination $payload
    $payloadHash = (Get-FileHash -LiteralPath $payload -Algorithm SHA256).Hash.ToLowerInvariant()
    $ready = [Threading.EventWaitHandle]::new($false, [Threading.EventResetMode]::AutoReset, $mutexName + '-UpdateReady-' + $jobId)
    $parent = Start-Process -FilePath (Get-Command powershell.exe).Source -ArgumentList @('-NoProfile', '-NonInteractive', '-Command', 'Start-Sleep -Seconds 3') -WindowStyle Hidden -PassThru
    $job = [ordered]@{
        Id = $jobId.ToString(); ParentId = $parent.Id; ParentStartTicks = $parent.StartTime.ToUniversalTime().Ticks
        Target = $target; OldHash = $originalHash; PayloadHash = $payloadHash; Version = $version; ResumeMonitoring = $false
    }
    $jobPath = Join-Path $stage 'install.json'
    [IO.File]::WriteAllText($jobPath, ($job | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    $helper = Start-Process -FilePath $helperPath -ArgumentList @('--apply-update', ('"' + $jobPath + '"')) -WindowStyle Hidden -PassThru
    try {
        if (-not $ready.WaitOne(15000)) { throw "Updater readiness failed: $case" }
        if (-not $helper.WaitForExit(45000)) { throw "Updater did not complete: $case" }
        $expectedExit = if ($case -eq 'success') { 0 } else { 1 }
        if ($helper.ExitCode -ne $expectedExit) { throw "Updater exit $($helper.ExitCode), expected $expectedExit ($case)." }
        $actualHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
        $expectedHash = if ($case -eq 'success') { $payloadHash } else { $originalHash }
        if ($actualHash -ne $expectedHash) { throw "Target was not restored correctly: $case" }
        Start-Sleep -Milliseconds 500
        $child = Get-CimInstance Win32_Process -Filter "Name = '$targetName'" | Where-Object { $_.ExecutablePath -ieq $target }
        if (@($child).Count -ne 1) { throw "Expected exactly one restarted application: $case" }
        Write-Output "PASS native updater $case (restart, health acknowledgement/rollback, original bytes verified)."
    }
    finally {
        $ready.Dispose()
        if (-not $helper.HasExited) { Stop-Process -Id $helper.Id -Force }
        Get-CimInstance Win32_Process -Filter "Name = '$targetName'" | Where-Object { $_.ExecutablePath -ieq $target } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
        $helper.Dispose(); $parent.Dispose()
        $outcomePath = Join-Path $dataRoot 'last-update.json'
        if (Test-Path -LiteralPath $outcomePath) {
            $outcome = Get-Content -LiteralPath $outcomePath -Raw | ConvertFrom-Json
            if ($outcome.JobId -eq $jobId.ToString()) { Remove-Item -LiteralPath $outcomePath }
        }
    }
}
$actualSettingsHash = if (Test-Path -LiteralPath $expectedSettings) { (Get-FileHash -LiteralPath $expectedSettings -Algorithm SHA256).Hash } else { $null }
if ($actualSettingsHash -ne $settingsHash) { throw 'User settings changed during updater integration tests.' }
Write-Output 'PASS user settings preserved.'
