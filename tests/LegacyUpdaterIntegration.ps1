$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = [xml](Get-Content -LiteralPath (Join-Path $repoRoot 'src/AMZ.Resolution/AMZ.Resolution.csproj') -Raw)
if ($project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText -ne '0.7.0') { Write-Output 'SKIP: one-time v0.7.0 bridge test.'; exit 0 }
$legacyDir = Join-Path $repoRoot 'artifacts/legacy-client'
New-Item -ItemType Directory -Path $legacyDir -Force | Out-Null
$archive = Join-Path $legacyDir 'client-0.6.2.zip'
if (-not (Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest -Uri 'https://github.com/4MZ31/AMZ.Resolution/releases/download/v0.6.2/amd-resolution-tray-0.6.2-win-x64.zip' -OutFile $archive
}
# Pin the exact previously published client, not just a mutable tag or a download URL.
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne 'd6585727abe15710c4f8ec97684fd246728d31b79284f86d9ec77959fc2c7c75') { throw 'Legacy client checksum mismatch.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    $entry = $zip.GetEntry('ResolutionTray.exe')
    if ($null -eq $entry) { throw 'Legacy client missing.' }
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $legacyDir 'ResolutionTray.exe'), $true)
}
finally { $zip.Dispose() }
& (Join-Path $PSScriptRoot 'UpdaterIntegration.ps1') -ApplicationExe (Join-Path $legacyDir 'ResolutionTray.exe') -PayloadExe (Join-Path $repoRoot 'artifacts/legacy-bridge/AMZ.Resolution.exe') -LegacyProtocol -RequireRun
