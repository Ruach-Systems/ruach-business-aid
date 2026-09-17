$ErrorActionPreference = 'Stop'
$publish = Join-Path $PSScriptRoot '../artifacts/api'
$webConfig = Join-Path $publish 'web.config'
$deploy = 'C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe'
if (!(Test-Path -LiteralPath $deploy)) { throw 'Web Deploy is required on the deployment runner.' }
if (!(Test-Path -LiteralPath $webConfig)) { throw 'The validated API publish artifact is missing web.config.' }
$required = @('ConnectionStrings__Mashal','Google__ClientId','Google__ClientSecret','DataProtection__CertificateBase64','App__Origin','MASHAL_ADMIN_EMAILS','WEBDEPLOY_ENDPOINT','WEBDEPLOY_SITE','WEBDEPLOY_USERNAME','WEBDEPLOY_PASSWORD')
foreach ($name in $required) { if (![Environment]::GetEnvironmentVariable($name)) { throw "Required environment value is missing: $name" } }
[xml]$config = Get-Content -LiteralPath $webConfig
$asp = $config.SelectSingleNode('//aspNetCore')
$asp.SetAttribute('hostingModel','outofprocess')
$variables = $config.CreateElement('environmentVariables')
$values = @{
  ASPNETCORE_ENVIRONMENT = 'Production'
  ConnectionStrings__Mashal = $env:ConnectionStrings__Mashal
  Google__ClientId = $env:Google__ClientId
  Google__ClientSecret = $env:Google__ClientSecret
  DataProtection__CertificateBase64 = $env:DataProtection__CertificateBase64
  DataProtection__CertificatePassword = $env:DataProtection__CertificatePassword
  App__Origin = $env:App__Origin
}
$adminEmails = @($env:MASHAL_ADMIN_EMAILS.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Select-Object -Unique)
if ($adminEmails.Count -eq 0) { throw 'At least one Mashal Admin email is required.' }
for ($index = 0; $index -lt $adminEmails.Count; $index++) {
  $address = [Net.Mail.MailAddress]::new($adminEmails[$index])
  if ($address.Address -ne $adminEmails[$index]) { throw 'Mashal Admin entries must be plain email addresses.' }
  $values["MashalAdmin__Emails__$index"] = $adminEmails[$index]
}
foreach ($entry in $values.GetEnumerator()) {
  $element = $config.CreateElement('environmentVariable')
  $element.SetAttribute('name',$entry.Key)
  $element.SetAttribute('value',[string]$entry.Value)
  [void]$variables.AppendChild($element)
}
[void]$asp.AppendChild($variables)
try {
  # Configuration exists only on the protected runner during this job. Never upload it.
  $config.Save($webConfig)
  $endpoint = $env:WEBDEPLOY_ENDPOINT
  if (!$endpoint.StartsWith('https://')) { throw 'Web Deploy endpoint must use HTTPS.' }
  # Web Deploy uses single quotes in its provider syntax.
  foreach ($value in @($endpoint,$env:WEBDEPLOY_SITE,$env:WEBDEPLOY_USERNAME,$env:WEBDEPLOY_PASSWORD)) {
    if ($value.Contains("'")) { throw 'Web Deploy provider values cannot contain a single quote.' }
  }
  $destination = "contentPath='$env:WEBDEPLOY_SITE',computerName='$endpoint',userName='$env:WEBDEPLOY_USERNAME',password='$env:WEBDEPLOY_PASSWORD',authType='Basic'"
  $arguments = @('-verb:sync',"-source:contentPath='$([IO.Path]::GetFullPath($publish))'","-dest:$destination",'-enableRule:AppOffline','-skip:objectName=dirPath,absolutePath=App_Data')
  # Do not emit provider arguments or tool output; they can contain credentials.
  $result = & $deploy @arguments 2>&1
  if ($LASTEXITCODE -ne 0) { throw 'Web Deploy failed. Inspect the protected host deployment logs.' }
  Write-Host 'API deployment completed.'
} finally {
  # Restore the artifact copy immediately so no generated secrets remain in web.config.
  $asp.RemoveChild($variables) | Out-Null
  $config.Save($webConfig)
}
