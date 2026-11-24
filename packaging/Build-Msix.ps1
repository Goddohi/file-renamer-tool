[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidatePattern('^\d+\.\d+\.\d+\.0$')]
    [string]$Version = '1.0.0.0',
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
$OutputEncoding = [Console]::OutputEncoding
$repoRoot = Split-Path $PSScriptRoot -Parent
$parsedVersion = [version]$Version
if ($parsedVersion.Major -lt 1 -or $parsedVersion.Major -gt 65535 -or
    $parsedVersion.Minor -gt 65535 -or $parsedVersion.Build -gt 65535) {
    throw 'MSIX version must have a major version of 1-65535, components of 0-65535, and a final component of 0.'
}

function Invoke-Checked {
    param([string]$Command, [string[]]$Arguments)
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE." }
}

# Restore official SDK tools locally; no Visual Studio packaging workload is required.
$toolsProject = Join-Path $PSScriptRoot 'tools\WindowsSdk.csproj'
$packagesPath = Join-Path $repoRoot '.packages'
Invoke-Checked 'dotnet' @('restore', $toolsProject, '--packages', $packagesPath, '--nologo')
[xml]$toolsXml = Get-Content -LiteralPath $toolsProject -Raw
$sdkVersion = $toolsXml.Project.ItemGroup.PackageReference.Version
$sdkPath = Join-Path $packagesPath "microsoft.windows.sdk.buildtools\$sdkVersion"
$makeAppx = Get-ChildItem -LiteralPath $sdkPath -Filter 'makeappx.exe' -Recurse |
    Where-Object { $_.Directory.Name -eq 'x64' } | Select-Object -First 1
if (-not $makeAppx) { throw "MakeAppx.exe was not found under $sdkPath." }

# Each run gets a new staging directory, so stale publish files cannot enter a package.
$runId = [guid]::NewGuid().ToString('N')
$workPath = Join-Path $repoRoot "artifacts\msix\work\$runId"
$layoutPath = Join-Path $workPath 'layout'
[void](New-Item -ItemType Directory -Path $layoutPath -Force)
$projectPath = Join-Path $repoRoot 'src\FileRenamer.App\FileRenamer.App.csproj'
Invoke-Checked 'dotnet' @(
    'publish', $projectPath, '-c', $Configuration, '-r', "win-$Architecture",
    '--self-contained', 'true', '-p:PublishSingleFile=false', '-p:PublishTrimmed=false',
    '-p:DebugType=None', '-p:DebugSymbols=false', "-p:Version=$Version",
    '-o', $layoutPath, '--nologo'
)

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Assets') -Destination $layoutPath -Recurse
[xml]$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Package.appxmanifest') -Raw -Encoding UTF8
$manifest.Package.Identity.Version = $Version
$manifest.Package.Identity.ProcessorArchitecture = $Architecture
$manifest.Save((Join-Path $layoutPath 'AppxManifest.xml'))

$packageName = "$($manifest.Package.Identity.Name)_${Version}_${Architecture}.msix"
$temporaryPackage = Join-Path $workPath $packageName
$packLog = Join-Path $workPath 'makeappx.log'
& $makeAppx.FullName pack /d $layoutPath /p $temporaryPackage /o /h SHA256 > $packLog 2>&1
if ($LASTEXITCODE -ne 0) {
    Get-Content -LiteralPath $packLog | Write-Host
    throw "MakeAppx failed with exit code $LASTEXITCODE. See $packLog."
}
Write-Host "MakeAppx validation passed. Log: $packLog"

# Publish the result only after MakeAppx's schema and semantic validation succeeds.
$outputPath = Join-Path $repoRoot "artifacts\msix\$Configuration"
[void](New-Item -ItemType Directory -Path $outputPath -Force)
$packagePath = Join-Path $outputPath $packageName
Copy-Item -LiteralPath $temporaryPackage -Destination $packagePath -Force
$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$packagePath.sha256", "$hash  $packageName`r`n", [Text.Encoding]::ASCII)
Write-Host "Unsigned MSIX: $packagePath"
Write-Host "SHA256: $hash"
Write-Host 'Ready for Partner Center package upload. Local installation requires signing and certificate trust.'
