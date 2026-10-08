param([string]$PublishRoot = (Join-Path $PSScriptRoot '../artifacts/pwa/wwwroot'), [string]$Origin = '')
$ErrorActionPreference = 'Stop'
$worker = Get-Content -Raw (Join-Path $PublishRoot 'sw.js')
$assetsText = Get-Content -Raw (Join-Path $PublishRoot 'service-worker-assets.js')
$assets = ($assetsText -replace '^self.assetsManifest\s*=\s*','' -replace ';\s*$','') | ConvertFrom-Json
$manifest = Get-Content -Raw (Join-Path $PublishRoot 'manifest.webmanifest') | ConvertFrom-Json
if ($manifest.name -ne 'Business Aid by RUACH' -or $manifest.short_name -ne 'Business Aid' -or $manifest.id -ne '/' -or $manifest.start_url -ne '/') {
    throw 'PWA identity or installation scope is incorrect.'
}
$references = @(
    (Get-Content -Raw (Join-Path $PublishRoot 'index.html')),
    (Get-Content -Raw (Join-Path $PublishRoot 'open.html')),
    (Get-Content -Raw (Join-Path $PublishRoot 'manifest.webmanifest')),
    (Get-Content -Raw (Join-Path $PublishRoot 'css/app.css'))
)
foreach ($reference in [regex]::Matches(($references -join "`n"), '/brand-assets/[^"''\s)]+')) {
    if (!(Test-Path -LiteralPath (Join-Path $PublishRoot $reference.Value.TrimStart('/')))) {
        throw "Branded shell reference is missing: $($reference.Value)"
    }
}
foreach ($asset in $assets.assets) {
    if ($asset.url -eq 'sw.js' -or $asset.url.Contains('service-worker')) { continue }
    $path = Join-Path $PublishRoot $asset.url
    if (!(Test-Path -LiteralPath $path)) { throw "Published asset is missing: $($asset.url)" }
    $bytes = [IO.File]::ReadAllBytes($path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = [Convert]::ToBase64String($sha.ComputeHash($bytes)) } finally { $sha.Dispose() }
    if ("sha256-$hash" -ne $asset.hash) { throw "Offline manifest hash mismatch: $($asset.url)" }
}
foreach ($file in Get-ChildItem (Join-Path $PublishRoot 'brand-assets') -File | Where-Object { $_.Extension -notin @('.br','.gz') }) {
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLower().Substring(0,16)
    if (!$file.Name.Contains(".$hash.")) { throw "Brand filename is not content-versioned: $($file.Name)" }
    if ($file.Name -notin ($assets.assets.url | ForEach-Object { Split-Path $_ -Leaf })) { throw "Brand asset missing from offline manifest." }
}
if (!$worker.Contains('mashal-blazor-') -or !$worker.Contains('skipWaiting')) { throw 'Published PWA update handler is missing.' }
if ($Origin) {
    function Normalize-LiveFile([string]$File, [byte[]]$Bytes) {
        if ($File -ne 'index.html') { return $Bytes }
        $html = [Text.Encoding]::UTF8.GetString($Bytes)
        $cloudflareBeacon = '(?s)\s*<script\b(?=[^>]*\bsrc=["'']https://static\.cloudflareinsights\.com/beacon\.min\.js/[^"'']+["''])[^>]*>\s*</script>'
        $html = [regex]::Replace($html, $cloudflareBeacon, '') -replace "\r\n?", "`n"
        return [Text.Encoding]::UTF8.GetBytes($html)
    }
    $client = [Net.Http.HttpClient]::new()
    try {
        foreach ($file in @('index.html','sw.js','service-worker-assets.js','manifest.webmanifest')) {
            $remote = Normalize-LiveFile $file ($client.GetByteArrayAsync("$Origin/$file").GetAwaiter().GetResult())
            $local = Normalize-LiveFile $file ([IO.File]::ReadAllBytes((Join-Path $PublishRoot $file)))
            if ([Convert]::ToBase64String($remote) -ne [Convert]::ToBase64String($local)) { throw "Live file differs: $file" }
        }
    } finally { $client.Dispose() }
}
Write-Host 'PASS: release assets, content hashes, and PWA manifest.'
