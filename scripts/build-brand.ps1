param([string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$brand = Join-Path $repo 'public\brand'
$web = Join-Path $repo 'src\Ruach.BusinessAid.Client\wwwroot'
$runtime = Join-Path $web 'brand-assets'
$master = Join-Path $repo 'branding\balanced-record-master.svg'
$fontRoot = Join-Path $repo 'branding\concepts\fonts'
. (Join-Path $PSScriptRoot 'brand-drawing.ps1') -FontRoot $fontRoot
$playwrightAssembly = Join-Path $repo "tests\Ruach.BusinessAid.Tests\bin\$Configuration\net10.0\Microsoft.Playwright.dll"
if (!(Test-Path $playwrightAssembly)) { throw 'Build the existing test project and install its Chromium runtime first. See branding/README.md.' }
Add-Type -Path $playwrightAssembly
$playwright = [Microsoft.Playwright.Playwright]::CreateAsync().GetAwaiter().GetResult()
$browser = $null

function Svg([string]$Body, [int]$Width = 512, [int]$Height = 512) {
    "<svg xmlns=`"http://www.w3.org/2000/svg`" width=`"$Width`" height=`"$Height`" viewBox=`"0 0 $Width $Height`"><title>Business Aid by RUACH</title>$Body</svg>"
}
function WriteSvg([string]$Name, [string]$Body) {
    [IO.File]::WriteAllText((Join-Path $brand "$Name.svg"), $Body)
}
function Lockup([string]$Ink, [string]$Type, [string]$Endorsement) {
    Svg ((SvgMark $master 0 0 96 $Ink) + (TextPath 'Business Aid' 'Manrope' 38 114 9 $Type) + (TextPath 'by RUACH' 'Inter' 20 116 61 $Endorsement)) 410 100
}
try {
    $browser = $playwright.Chromium.LaunchAsync([Microsoft.Playwright.BrowserTypeLaunchOptions]@{ Headless = $true }).GetAwaiter().GetResult()
    $page = $browser.NewPageAsync().GetAwaiter().GetResult()
    New-Item -ItemType Directory -Path $brand, $runtime -Force | Out-Null
    WriteSvg 'business-aid-wordmark' (Lockup '#B21F32' '#141821' '#696270')
    WriteSvg 'business-aid-wordmark-reversed' (Lockup '#FFFFFF' '#FFFFFF' '#FFFFFF')
    WriteSvg 'business-aid-wordmark-monochrome' (Lockup '#141821' '#141821' '#141821')
    WriteSvg 'business-aid-symbol' (Svg (SvgMark $master 0 0 96 '#B21F32') 96 96)
    WriteSvg 'business-aid-symbol-monochrome' (Svg (SvgMark $master 0 0 96 '#141821') 96 96)
    $tile = Svg ('<rect width="512" height="512" fill="#B21F32"/>' + (SvgMark $master 102 102 308 '#FFFFFF'))
    $favicon = Svg ('<rect width="512" height="512" rx="112" fill="#B21F32"/>' + (SvgMark $master 102 102 308 '#FFFFFF'))
    WriteSvg 'business-aid-app-icon' $tile
    WriteSvg 'favicon' $favicon

    function Raster([string]$Source, [string]$Name, [int]$Width, [int]$Height) {
        $page.SetViewportSizeAsync($Width, $Height).GetAwaiter().GetResult()
        $page.SetContentAsync("<html><body style='margin:0'><img style='display:block;width:100%;height:100%' src='data:image/svg+xml;base64,$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Source)))'></body></html>").GetAwaiter().GetResult()
        $page.Locator('img').EvaluateAsync[bool]('async img => { await img.decode(); return true; }').GetAwaiter().GetResult() | Out-Null
        $page.ScreenshotAsync([Microsoft.Playwright.PageScreenshotOptions]@{ Path = (Join-Path $brand "$Name.png"); OmitBackground = $true }).GetAwaiter().GetResult() | Out-Null
    }
    foreach ($size in @(16, 32, 48)) { Raster $favicon "favicon-$size" $size $size }
    foreach ($size in @(192, 512)) { Raster $tile "pwa-${size}x$size" $size $size }
    Raster $tile 'pwa-maskable-512x512' 512 512
    Raster $tile 'apple-touch-icon' 180 180
    Raster (Get-Content (Join-Path $brand 'business-aid-wordmark.svg') -Raw) 'business-aid-wordmark' 820 200

    $stream = [IO.File]::Create((Join-Path $brand 'favicon.ico'))
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $sizes = @(16, 32, 48)
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        foreach ($size in $sizes) {
            $bytes = [IO.File]::ReadAllBytes((Join-Path $brand "favicon-$size.png"))
            $writer.Write([byte]$size); $writer.Write([byte]$size); $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$bytes.Length); $writer.Write([uint32]$offset)
            $offset += $bytes.Length
        }
        foreach ($size in $sizes) { $writer.Write([IO.File]::ReadAllBytes((Join-Path $brand "favicon-$size.png"))) }
    } finally { $writer.Dispose(); $stream.Dispose() }

    $names = @('business-aid-wordmark.svg', 'business-aid-wordmark-reversed.svg', 'favicon.svg', 'favicon.ico',
        'apple-touch-icon.png', 'pwa-192x192.png', 'pwa-512x512.png', 'pwa-maskable-512x512.png')
    $preview = '<rect width="1200" height="740" fill="#F7F3ED"/>'
    $preview += TextPath 'Business Aid by RUACH' 'Manrope' 40 40 24 '#141821'
    $preview += TextPath 'Balanced Record / agent-selected / owner artwork review pending' 'Inter' 18 42 86 '#696270'
    $preview += '<g transform="translate(40 164)">' + (SvgMark $master 0 0 96 '#B21F32') + (TextPath 'Business Aid' 'Manrope' 38 114 9 '#141821') + (TextPath 'by RUACH' 'Inter' 20 116 61 '#696270') + '</g>'
    $preview += '<rect x="620" y="142" width="530" height="142" rx="16" fill="#141821"/>'
    $preview += '<g transform="translate(650 164)">' + (SvgMark $master 0 0 96 '#FFFFFF') + (TextPath 'Business Aid' 'Manrope' 38 114 9 '#FFFFFF') + (TextPath 'by RUACH' 'Inter' 20 116 61 '#FFFFFF') + '</g>'
    $preview += TextPath 'App icon / same geometry and safe padding' 'Inter' 20 40 330 '#141821'
    $preview += '<rect x="40" y="382" width="192" height="192" rx="40" fill="#B21F32"/>'
    $preview += SvgMark $master 78.25 420.25 115.5 '#FFFFFF'
    $preview += '<rect x="290" y="382" width="192" height="192" fill="#B21F32"/>'
    $preview += SvgMark $master 328.25 420.25 115.5 '#FFFFFF'
    $preview += TextPath 'One ink' 'Inter' 20 600 330 '#141821'
    $preview += SvgMark $master 600 390 160 '#141821'
    $preview += TextPath 'Actual favicon sizes / 16, 32, 48 px' 'Inter' 18 850 330 '#141821'
    foreach ($size in @(16, 32, 48)) {
        $x = 850 + (@(16, 32, 48).IndexOf($size) * 80)
        $data = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $brand "favicon-$size.png")))
        $preview += "<image x=`"$x`" y=`"410`" width=`"$size`" height=`"$size`" href=`"data:image/png;base64,$data`"/>"
    }
    $preview += TextPath 'Crimson #B21F32   Obsidian #141821   Ivory #F7F3ED   Manrope / Inter' 'Inter' 18 40 634 '#696270'
    $preview += TextPath 'Reversible branch integration only. Parent Living Breath unchanged. Not deployed.' 'Inter' 18 40 677 '#696270'
    WriteSvg 'brand-preview' (Svg $preview 1200 740)
    Raster (Svg $preview 1200 740) 'brand-preview' 1200 740
    $files = @($names | ForEach-Object { Join-Path $brand $_ }) + @(
        (Join-Path $fontRoot 'Inter-Variable.ttf'), (Join-Path $fontRoot 'Inter-OFL.txt'))
    $mapping = [ordered]@{}
    foreach ($file in $files) {
        $hash = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant().Substring(0, 16)
        $name = [IO.Path]::GetFileNameWithoutExtension($file)
        $extension = [IO.Path]::GetExtension($file)
        $hashed = "$name.$hash$extension"
        Copy-Item -LiteralPath $file -Destination (Join-Path $runtime $hashed)
        $mapping["$name$extension"] = $hashed
    }
    $targets = @((Join-Path $web 'index.html'), (Join-Path $web 'open.html'), (Join-Path $web 'manifest.webmanifest'),
        (Join-Path $web 'css\app.css'), (Join-Path $repo 'src\Ruach.BusinessAid.Client\Components\BrandLogo.razor'))
    foreach ($target in $targets) {
        $text = [IO.File]::ReadAllText($target)
        foreach ($entry in $mapping.GetEnumerator()) {
            $stem = [regex]::Escape([IO.Path]::GetFileNameWithoutExtension($entry.Key))
            $extension = [regex]::Escape([IO.Path]::GetExtension($entry.Key))
            $text = [regex]::Replace($text, "/brand-assets/$stem\.[a-z0-9]+$extension", "/brand-assets/$($entry.Value)")
        }
        [IO.File]::WriteAllText($target, $text)
    }
    # Only obsolete files in the dedicated generated runtime directory are removed.
    foreach ($file in Get-ChildItem $runtime -File) {
        if ($file.Name -notin $mapping.Values) { Remove-Item -LiteralPath $file.FullName }
    }
    $mapping | ConvertTo-Json | Set-Content (Join-Path $brand 'runtime-assets.json')
    $inventory = Get-ChildItem $brand -File | Where-Object Name -ne 'asset-inventory.json' | Sort-Object Name | ForEach-Object {
        [ordered]@{ file = $_.Name; bytes = $_.Length; sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    $inventory | ConvertTo-Json | Set-Content (Join-Path $brand 'asset-inventory.json')
} finally {
    if ($browser) { $browser.DisposeAsync().AsTask().GetAwaiter().GetResult() | Out-Null }
    $playwright.Dispose()
    $fonts.Dispose()
}
Write-Host 'Generated agent-selected Balanced Record assets and updated hashed runtime references. Owner review pending.'
