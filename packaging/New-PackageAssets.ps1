[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

$iconPath = Join-Path $PSScriptRoot '..\src\FileRenamer.App\Assets\app.ico'
$assetPath = Join-Path $PSScriptRoot 'Assets'
[void](New-Item -ItemType Directory -Path $assetPath -Force)
$stream = [IO.File]::OpenRead($iconPath)
try {
    $decoder = [Windows.Media.Imaging.BitmapDecoder]::Create(
        $stream, [Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
        [Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
    $source = $decoder.Frames | Sort-Object PixelWidth -Descending | Select-Object -First 1
} finally {
    $stream.Dispose()
}

foreach ($asset in @(
    @{ Name = 'StoreLogo'; Size = 50 },
    @{ Name = 'Square44x44Logo'; Size = 44 },
    @{ Name = 'Square150x150Logo'; Size = 150 }
)) {
    $visual = New-Object Windows.Media.DrawingVisual
    [Windows.Media.RenderOptions]::SetBitmapScalingMode($visual, [Windows.Media.BitmapScalingMode]::HighQuality)
    $context = $visual.RenderOpen()
    $context.DrawImage($source, (New-Object Windows.Rect(0, 0, $asset.Size, $asset.Size)))
    $context.Close()
    $bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap(
        $asset.Size, $asset.Size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $destination = Join-Path $assetPath ($asset.Name + '.png')
    $output = [IO.File]::Create($destination)
    try { $encoder.Save($output) } finally { $output.Dispose() }
    Write-Host "Created $destination"
}
