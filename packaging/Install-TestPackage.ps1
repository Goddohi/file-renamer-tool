#Requires -RunAsAdministrator
[CmdletBinding()]
param([switch]$Uninstall, [string]$TestRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $TestRoot) { $TestRoot = Join-Path $repoRoot 'artifacts\msix\Test' }
$testRoot = [IO.Path]::GetFullPath($TestRoot)
$receiptPath = Join-Path $testRoot 'installed.json'
$logPath = Join-Path $testRoot 'install.log'
Start-Transcript -Path $logPath -Append | Out-Null
try {
    if ($Uninstall) {
        if (-not (Test-Path -LiteralPath $receiptPath)) { throw 'No test installation receipt exists.' }
        $receipt = Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $installed = Get-AppxPackage -Name $receipt.PackageName
        if ($installed -and $installed.PackageFullName -ne $receipt.PackageFullName) {
            throw 'A different package version is installed; refusing to remove it.'
        }
        if ($installed) { Remove-AppxPackage -Package $receipt.PackageFullName -ErrorAction Stop }
        if ($receipt.AddedCertificate) {
            $certStorePath = "Cert:\LocalMachine\TrustedPeople\$($receipt.CertificateThumbprint)"
            if (Test-Path -LiteralPath $certStorePath) { Remove-Item -LiteralPath $certStorePath -Force }
        }
        Remove-Item -LiteralPath $receiptPath -Force
        Write-Host 'Removed the test app and its temporary certificate trust.'
        return
    }

    $metadata = Get-Content -LiteralPath (Join-Path $testRoot 'test-package.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if (Get-AppxPackage -Name $metadata.PackageName) { throw 'The app is already installed. Existing installations are not replaced by this test script.' }
    $packagePath = Join-Path $testRoot $metadata.PackageFile
    $certPath = Join-Path $testRoot $metadata.CertificateFile
    if ((Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash -ne $metadata.PackageSha256) { throw 'Test package hash mismatch.' }
    $certificate = New-Object Security.Cryptography.X509Certificates.X509Certificate2($certPath)
    $signature = Get-AuthenticodeSignature -LiteralPath $packagePath
    if ($certificate.Thumbprint -ne $metadata.CertificateThumbprint -or
        -not $signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
        throw 'Certificate and package signer do not match.'
    }
    $trustPath = "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)"
    $addedCertificate = -not (Test-Path -LiteralPath $trustPath)
    try {
        if ($addedCertificate) { [void](Import-Certificate -FilePath $certPath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople') }
        if ((Get-AuthenticodeSignature -LiteralPath $packagePath).Status -ne 'Valid') { throw 'Package signature validation failed.' }
        Add-AppxPackage -Path $packagePath -ErrorAction Stop
        $installed = Get-AppxPackage -Name $metadata.PackageName
        if (-not $installed -or $installed.PackageFullName -ne $metadata.PackageFullName -or $installed.Status -ne 'Ok') {
            throw 'Installed package identity or status differs from the expected result.'
        }
        [ordered]@{
            PackageName = $installed.Name
            PackageFullName = $installed.PackageFullName
            PackageFamilyName = $installed.PackageFamilyName
            InstallLocation = $installed.InstallLocation
            CertificateThumbprint = $certificate.Thumbprint
            AddedCertificate = $addedCertificate
            InstalledAt = (Get-Date).ToString('o')
        } | ConvertTo-Json | Set-Content -LiteralPath $receiptPath -Encoding UTF8
        $installed | Select-Object Name,PackageFullName,PackageFamilyName,InstallLocation,SignatureKind,Status | Format-List
    } catch {
        $partialInstall = Get-AppxPackage -Name $metadata.PackageName
        if ($partialInstall -and $partialInstall.PackageFullName -eq $metadata.PackageFullName) {
            Remove-AppxPackage -Package $partialInstall.PackageFullName -ErrorAction Stop
        }
        if ($addedCertificate -and (Test-Path -LiteralPath $trustPath)) { Remove-Item -LiteralPath $trustPath -Force }
        throw
    }
} finally { Stop-Transcript | Out-Null }
