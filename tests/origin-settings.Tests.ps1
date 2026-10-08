$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\scripts\origin-settings.ps1')
$pairs = '[{"ApiOrigin":"https://api-businessaid.mashalsystems.com","PwaOrigin":"https://businessaid.mashalsystems.com"},{"ApiOrigin":"https://api.businessaid.ruachsystems.dev","PwaOrigin":"https://businessaid.ruachsystems.dev"}]'
$settings = Get-OriginSettings $pairs
if ($settings.Count -ne 4 -or $settings['App__OriginPairs__1__ApiOrigin'] -ne 'https://api.businessaid.ruachsystems.dev' -or
    $settings['App__OriginPairs__0__PwaOrigin'] -ne 'https://businessaid.mashalsystems.com') {
    throw 'Valid origin pair serialization failed.'
}
foreach ($invalid in @('null', '{}', '[]', '[{}]', '{"ApiOrigin":"https://api.example","PwaOrigin":"https://pwa.example"}',
    '[{"ApiOrigin":"https://api.example/","PwaOrigin":"https://pwa.example"}]',
    '[{"ApiOrigin":"http://localhost:5080","PwaOrigin":"https://pwa.example"}]',
    '[{"ApiOrigin":"https://user@api.example","PwaOrigin":"https://pwa.example"}]',
    '[{"ApiOrigin":"https://api.example","PwaOrigin":"https://pwa.example/path"}]',
    '[{"ApiOrigin":"https://api.example","PwaOrigin":"https://pwa.example"},{"ApiOrigin":"https://api.example","PwaOrigin":"https://other.example"}]')) {
    $rejected = $false
    try { Get-OriginSettings $invalid | Out-Null } catch { $rejected = $true }
    if (!$rejected) { throw "Invalid origin pair input was accepted: $invalid" }
}
Write-Host 'PASS: deployment origin settings serialize exact pairs and reject malformed or duplicate origins; no deployment executed.'
