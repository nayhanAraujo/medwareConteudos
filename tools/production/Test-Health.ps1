[CmdletBinding()]
param([Parameter(Mandatory)][uri]$PublicBase)
$ErrorActionPreference = 'Stop'
if ($PublicBase.Scheme -ne 'https') { throw 'PublicBase must use HTTPS; certificates are verified.' }
$checks = @(
    'http://127.0.0.1:5080/health/live', 'http://127.0.0.1:5080/health/ready',
    'http://127.0.0.1:3000/login', "$($PublicBase.AbsoluteUri.TrimEnd('/'))/health/live",
    "$($PublicBase.AbsoluteUri.TrimEnd('/'))/health/ready", "$($PublicBase.AbsoluteUri.TrimEnd('/'))/api-dotnet/health/ready",
    "$($PublicBase.AbsoluteUri.TrimEnd('/'))/apiconteudos/docs"
)
foreach ($url in $checks) {
    $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 30 -MaximumRedirection 0
    if ($response.StatusCode -ne 200) { throw "Smoke failed: $url" }
    Write-Host "OK $url"
}
