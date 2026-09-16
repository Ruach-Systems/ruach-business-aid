$ErrorActionPreference = 'Stop'
if (!$env:PWA_ORIGIN -or !$env:API_ORIGIN) { throw 'PWA_ORIGIN and API_ORIGIN are required.' }
& (Join-Path $PSScriptRoot 'verify-release.ps1') -Origin $env:PWA_ORIGIN
$handler = [Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [Net.Http.HttpClient]::new($handler)
try {
    $client.DefaultRequestHeaders.Add('Origin', $env:PWA_ORIGIN)
    foreach ($path in @('/api/auth/session','/api/bootstrap','/api/sync/pull?businessId=00000000-0000-0000-0000-000000000001&cursor=0','/api/reports/summary?businessId=00000000-0000-0000-0000-000000000001&from=2026-01-01&to=2026-01-31')) {
        $response = $client.GetAsync($env:API_ORIGIN + $path).GetAwaiter().GetResult()
        if ([int]$response.StatusCode -ne 401) { throw 'An unauthenticated protected endpoint did not return 401.' }
        if (($response.Headers.GetValues('Access-Control-Allow-Origin') | Select-Object -First 1) -ne $env:PWA_ORIGIN) { throw 'Credentialed CORS origin mismatch.' }
    }
    $client.DefaultRequestHeaders.Remove('Origin') | Out-Null
    $client.DefaultRequestHeaders.Add('Origin','https://untrusted.example')
    $response = $client.GetAsync($env:API_ORIGIN + '/api/auth/session').GetAwaiter().GetResult()
    if ($response.Headers.Contains('Access-Control-Allow-Origin')) { throw 'Unexpected CORS permission.' }
    $response = $client.GetAsync($env:API_ORIGIN + '/api/auth/google').GetAwaiter().GetResult()
    if ([int]$response.StatusCode -ne 302 -or $response.Headers.Location.Host -ne 'accounts.google.com') { throw 'Google redirect is not configured.' }
} finally { $client.Dispose(); $handler.Dispose() }
Write-Host 'PASS: live shell, Google redirect, CORS, and authentication boundaries. Complete authenticated smoke tests on the configured devices.'
