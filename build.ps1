param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'Build on Windows x64.' }
Push-Location $PSScriptRoot
try {
    dotnet run --project tests/AMZ.Resolution.Tests -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    dotnet run --project tests/AMZ.Resolution.UiTests -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'UI tests failed.' }
    $publishDir = Join-Path $PSScriptRoot 'artifacts\portable'
    dotnet publish src/AMZ.Resolution/AMZ.Resolution.csproj -c $Configuration -r win-x64 --self-contained true -o $publishDir -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false -p:CopyRetryCount=0
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath README.md, LICENSE -Destination $publishDir
    $docsDir = Join-Path $publishDir 'docs'
    New-Item -ItemType Directory -Path $docsDir -Force | Out-Null
    Copy-Item -LiteralPath docs/README.en.md, docs/THIRD-PARTY-NOTICES.txt -Destination $docsDir
    $project = [xml](Get-Content -LiteralPath src/AMZ.Resolution/AMZ.Resolution.csproj -Raw)
    $version = $project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
    $archive = Join-Path $PSScriptRoot ("artifacts\AMZ.Resolution-$version-win-x64.zip")
    Compress-Archive -LiteralPath (Join-Path $publishDir 'AMZ.Resolution.exe'), (Join-Path $publishDir 'README.md'), (Join-Path $publishDir 'LICENSE'), (Join-Path $publishDir 'docs') -DestinationPath $archive -Force
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText($archive + '.sha256', $hash + '  ' + [IO.Path]::GetFileName($archive) + [Environment]::NewLine)
    Write-Output "Portable archive: $archive"
    if ($version -eq '0.7.0') {
        # One-time compatibility build for clients that validate the original product identity.
        $bridgeDir = Join-Path $PSScriptRoot 'artifacts\legacy-bridge'
        dotnet publish src/AMZ.Resolution/AMZ.Resolution.csproj -c $Configuration -r win-x64 --self-contained true -o $bridgeDir -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false -p:LegacyUpdateBridge=true -p:CopyRetryCount=0
        if ($LASTEXITCODE -ne 0) { throw 'Legacy update bridge publish failed.' }
        Copy-Item -LiteralPath (Join-Path $bridgeDir 'AMZ.Resolution.exe') -Destination (Join-Path $bridgeDir 'ResolutionTray.exe')
        $bridgeArchive = Join-Path $PSScriptRoot ("artifacts\amd-resolution-tray-$version-win-x64.zip")
        Compress-Archive -LiteralPath (Join-Path $bridgeDir 'ResolutionTray.exe'), (Join-Path $publishDir 'LICENSE'), (Join-Path $publishDir 'docs') -DestinationPath $bridgeArchive -Force
        $bridgeHash = (Get-FileHash -LiteralPath $bridgeArchive -Algorithm SHA256).Hash.ToLowerInvariant()
        [IO.File]::WriteAllText($bridgeArchive + '.sha256', $bridgeHash + '  ' + [IO.Path]::GetFileName($bridgeArchive) + [Environment]::NewLine)
        Write-Output "Legacy update bridge: $bridgeArchive"
    }
}
finally { Pop-Location }
