param([string]$PublishRoot = (Join-Path $PSScriptRoot '../artifacts/pwa/wwwroot'), [string]$Origin = '')
$ErrorActionPreference = 'Stop'
$worker = Get-Content -Raw (Join-Path $PublishRoot 'sw.js')
$assetsText = Get-Content -Raw (Join-Path $PublishRoot 'service-worker-assets.js')
$assets = ($assetsText -replace '^self.assetsManifest\s*=\s*','' -replace ';\s*$','') | ConvertFrom-Json
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
    $client = [Net.Http.HttpClient]::new()
    try {
        foreach ($file in @('index.html','sw.js','service-worker-assets.js','manifest.webmanifest')) {
            $remote = $client.GetByteArrayAsync("$Origin/$file").GetAwaiter().GetResult()
            $local = [IO.File]::ReadAllBytes((Join-Path $PublishRoot $file))
            if ([Convert]::ToBase64String($remote) -ne [Convert]::ToBase64String($local)) { throw "Live file differs: $file" }
        }
    } finally { $client.Dispose() }
}
Write-Host 'PASS: release assets, content hashes, and PWA manifest.'
