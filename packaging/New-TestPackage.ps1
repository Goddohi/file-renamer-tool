[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+\.0$')]
    [string]$Version = '1.0.0.0',
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64',
    [string]$TestRoot
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
[xml]$manifest = Get-Content (Join-Path $PSScriptRoot 'Package.appxmanifest') -Raw -Encoding UTF8
$name = $manifest.Package.Identity.Name
$source = Join-Path $repoRoot "artifacts\msix\Release\${name}_${Version}_${Architecture}.msix"
if (-not (Test-Path -LiteralPath $source)) { throw "Build the Release package first: $source" }
if (-not $TestRoot) { $TestRoot = Join-Path $repoRoot 'artifacts\msix\Test' }
$testRoot = [IO.Path]::GetFullPath($TestRoot)
if (Test-Path (Join-Path $testRoot 'installed.json')) { throw 'Uninstall the previous test package before preparing another.' }
[void](New-Item -ItemType Directory -Path $testRoot -Force)
[xml]$toolsProject = Get-Content (Join-Path $PSScriptRoot 'tools\WindowsSdk.csproj') -Raw
$sdkVersion = $toolsProject.Project.ItemGroup.PackageReference.Version
$sdkRoot = Join-Path $repoRoot ".packages\microsoft.windows.sdk.buildtools\$sdkVersion"
$signTool = Get-ChildItem -LiteralPath $sdkRoot -Filter signtool.exe -Recurse |
    Where-Object { $_.Directory.Name -eq 'x64' } | Select-Object -First 1
if (-not $signTool) { throw 'Build the Release package first to restore SignTool.' }
$signedPath = Join-Path $testRoot "${name}_${Version}_${Architecture}.msix"
$certPath = Join-Path $testRoot 'FileRenamer-Test.cer'
$originalHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$cert = $null
try {
    $cert = New-SelfSignedCertificate -Type Custom -Subject $manifest.Package.Identity.Publisher `
        -FriendlyName 'File Renamer temporary MSIX test' -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable `
        -KeyUsage DigitalSignature -NotAfter (Get-Date).AddDays(7) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    Copy-Item -LiteralPath $source -Destination $signedPath -Force
    & $signTool.FullName sign /fd SHA256 /s My /sha1 $cert.Thumbprint $signedPath
    if ($LASTEXITCODE -ne 0) { throw "SignTool failed: $LASTEXITCODE" }
    [void](Export-Certificate -Cert $cert -FilePath $certPath -Force)
    $signature = Get-AuthenticodeSignature -LiteralPath $signedPath
    if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $cert.Thumbprint) {
        throw 'The signed package does not have the expected signer.'
    }
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $originalHash) { throw 'Release package changed.' }
    [ordered]@{
        PackageName = $name
        PackageFullName = "${name}_${Version}_${Architecture}__0xzf0d7sark16"
        PackageFile = [IO.Path]::GetFileName($signedPath)
        PackageSha256 = (Get-FileHash -LiteralPath $signedPath -Algorithm SHA256).Hash
        CertificateFile = [IO.Path]::GetFileName($certPath)
        CertificateThumbprint = $cert.Thumbprint
        Expires = $cert.NotAfter.ToString('o')
        ReleaseSha256 = $originalHash
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $testRoot 'test-package.json') -Encoding UTF8
    Write-Host "Signed test package: $signedPath"
    Write-Host "Public certificate: $certPath"
    Write-Host 'Private key removed after signing. Installation requires administrator rights to trust the test certificate.'
} finally {
    if ($cert) { Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($cert.Thumbprint)" -DeleteKey -Force }
}
