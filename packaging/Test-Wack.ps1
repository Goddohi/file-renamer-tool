#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$WorkingRoot = (Join-Path $env:ProgramData 'FileRenamer\Wack')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$appCert = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\App Certification Kit\appcert.exe'
if (-not (Test-Path -LiteralPath $appCert)) {
    throw 'Install the Windows App Certification Kit component of the Windows SDK first. See packaging/README.md.'
}
$package = Get-AppxPackage -Name 'Dohi.FileRenamerDohi'
if (-not $package) { throw 'Install the signed test package before running WACK.' }
$runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$workingDirectory = Join-Path ([IO.Path]::GetFullPath($WorkingRoot)) $runId
if ($workingDirectory -match '[^\x00-\x7F]') {
    throw 'WACK/TAEF requires an ASCII working path. Supply -WorkingRoot with an ASCII-only absolute path.'
}
$reportRoot = Join-Path $repoRoot ('artifacts\msix\Wack\' + $runId)
[void](New-Item -ItemType Directory -Path $reportRoot -Force)
$dataPath = Join-Path $workingDirectory 'Data'
$tempPath = Join-Path $workingDirectory 'Temp'
[void](New-Item -ItemType Directory -Path $dataPath, $tempPath -Force)
$reportPath = Join-Path $reportRoot 'report.xml'
$workingReportPath = Join-Path $workingDirectory 'report.xml'
$logPath = Join-Path $reportRoot 'appcert.log'
Push-Location (Split-Path $appCert -Parent)
try {
    # The installed WACK CLI supports -dataFilePath and -temppath. Keep every
    # TAEF log path ASCII even when the Windows user profile contains Unicode.
    & $appCert reset -dataFilePath $dataPath 2>&1 | Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) { throw "WACK reset failed: $LASTEXITCODE" }
    & $appCert test -packagefullname $package.PackageFullName -reportoutputpath $workingReportPath -dataFilePath $dataPath -temppath $tempPath 2>&1 | Tee-Object -FilePath $logPath -Append
    $testExitCode = $LASTEXITCODE
    Get-ChildItem -LiteralPath $workingDirectory -File | Copy-Item -Destination $reportRoot
    Copy-Item -LiteralPath $dataPath -Destination (Join-Path $reportRoot 'diagnostics') -Recurse
    if (-not (Test-Path -LiteralPath $reportPath)) { throw "WACK did not produce a report. Exit code: $testExitCode; log: $logPath" }
    [xml]$report = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8
    $testResults = @($report.SelectNodes('/REPORT/REQUIREMENTS/REQUIREMENT/TEST') | ForEach-Object {
        [ordered]@{
            Name = $_.GetAttribute('NAME')
            Optional = $_.GetAttribute('OPTIONAL') -eq 'TRUE'
            Result = $_.SelectSingleNode('RESULT').InnerText
            Messages = @($_.SelectNodes('MESSAGES/MESSAGE') | ForEach-Object { $_.GetAttribute('TEXT') })
        }
    })
    $overallResult = $report.DocumentElement.GetAttribute('OVERALL_RESULT')
    $partialRun = $report.DocumentElement.GetAttribute('PARTIAL_RUN')
    [ordered]@{
        CheckedAt = (Get-Date).ToString('o')
        PackageFullName = $package.PackageFullName
        ToolVersion = $report.DocumentElement.GetAttribute('VERSION')
        ExitCode = $testExitCode
        OverallResult = $overallResult
        PartialRun = $partialRun
        WorkingDirectory = $workingDirectory
        Tests = $testResults
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $reportRoot 'summary.json') -Encoding UTF8
    Write-Host "WACK report: $reportPath"
    if ($testExitCode -ne 0) { throw "WACK returned $testExitCode. Review the report for failures or incomplete tests." }
    # Exit code zero only means the command ran; a FAIL report can still return zero.
    if ($overallResult -ne 'PASS' -or $partialRun -ne 'FALSE' -or $testResults.Count -eq 0) {
        throw "WACK did not pass: result=$overallResult, partial=$partialRun. Review $reportPath"
    }
    $optionalFindings = @($testResults | Where-Object { $_.Optional -and $_.Result -ne 'PASS' })
    Write-Host "WACK overall result: PASS; $($testResults.Count) test results recorded. Report and diagnostics: $reportRoot"
    if ($optionalFindings.Count -gt 0) {
        Write-Warning "$($optionalFindings.Count) optional test(s) have findings. Review summary.json; these findings are retained in the original report."
    }
} finally { Pop-Location }
