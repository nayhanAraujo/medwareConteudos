#requires -Version 5.1
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9.-]+$')][string]$Domain,
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{40}$')][string]$CertificateThumbprint,
    [Parameter(Mandatory)][string]$ReleaseDirectory,
    [Parameter(Mandatory)][string]$DataDirectory,
    [Parameter(Mandatory)][string]$ServiceDirectory,
    [Parameter(Mandatory)][string]$SecretsFile,
    [Parameter(Mandatory)][string]$NssmPath,
    [Parameter(Mandatory)][string]$NodePath,
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function FullPath([string]$Value) {
    # This installer runs on the Windows Server inbox shell (Windows PowerShell 5.1).
    # Path.IsPathFullyQualified is unavailable in its .NET Framework runtime.
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch '^[A-Za-z]:\\' -or $Value.StartsWith('\\')) { throw 'Use absolute local paths.' }
    $result = [IO.Path]::GetFullPath($Value).TrimEnd('\')
    if ($result -eq [IO.Path]::GetPathRoot($result).TrimEnd('\')) { throw 'Volume roots are not valid targets.' }
    return $result
}
function RelativePath([string]$BasePath, [string]$ChildPath) {
    $base = (FullPath $BasePath).TrimEnd('\\')
    $child = [IO.Path]::GetFullPath($ChildPath)
    if (-not $child.StartsWith($base + '\\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Expected a child path inside its base directory.' }
    return $child.Substring($base.Length).TrimStart('\\')
}
$package = (Resolve-Path -LiteralPath $PackageDirectory).Path
$release = FullPath $ReleaseDirectory
$data = FullPath $DataDirectory
$service = FullPath $ServiceDirectory
$secret = (Resolve-Path -LiteralPath $SecretsFile).Path
foreach ($pair in @(@($release,$data),@($release,$service),@($data,$service),@($package,$release),@($package,$data),@($package,$service))) {
    if ($pair[0] -eq $pair[1] -or $pair[0].StartsWith($pair[1]+'\',[StringComparison]::OrdinalIgnoreCase) -or $pair[1].StartsWith($pair[0]+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Package, release, data and service locations must not overlap.' }
}
if (Test-Path $release) { throw 'Release must be a new directory. Existing releases are never overwritten.' }
if (Test-Path $service) { throw 'ServiceDirectory must be new; use the documented controlled release switch for upgrades.' }
foreach ($executable in @($NssmPath,$NodePath)) {
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf) -or [IO.Path]::GetExtension($executable) -ne '.exe') { throw "Missing executable: $executable" }
}
$NssmPath = (Resolve-Path $NssmPath).Path
$NodePath = (Resolve-Path $NodePath).Path
if ((& $NodePath --version) -notmatch '^v22\.' -or $LASTEXITCODE -ne 0) { throw 'Node 22 required.' }
# NSSM 2.24 writes its help banner to stderr and exits nonzero. Windows
# PowerShell 5.1 promotes that stderr record to an exception when ErrorAction
# is Stop, so validate the executable's version resource without running it.
$nssmVersionInfo = (Get-Item -LiteralPath $NssmPath).VersionInfo
$nssmIdentity = "$($nssmVersionInfo.ProductName) $($nssmVersionInfo.FileDescription) $($nssmVersionInfo.OriginalFilename)"
$nssmVersionText = "$($nssmVersionInfo.ProductVersion) $($nssmVersionInfo.FileVersion)"
if ($nssmIdentity -notmatch '(?i)NSSM|non-sucking service manager' -or $nssmVersionText -notmatch '\d+\.\d+') { throw 'Invalid NSSM executable; supply the server-approved NSSM binary.' }
foreach ($required in @('api/MdwConteudos.Api.dll','api/web.config','nuxt/server/index.mjs','manifest.json','deployment/templates/public.web.config')) {
    if (-not (Test-Path "$package/$required" -PathType Leaf)) { throw "Incomplete package: $required" }
}
foreach ($entry in (Get-Content "$package/manifest.json" -Raw | ConvertFrom-Json)) {
    $path = [IO.Path]::GetFullPath((Join-Path $package $entry.Path))
    if (-not $path.StartsWith($package.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe manifest path.' }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.SHA256) { throw "Package integrity failure: $($entry.Path)" }
}
$raw = Get-Content -LiteralPath $secret -Raw
$config = $raw | ConvertFrom-Json
if ($raw -match 'REQUIRED|mdw-web-dev') { throw 'Complete the production secrets template manually.' }
if ($config.DataPaths.Root -ne $data -or $config.AllowedHosts -ne "$Domain;127.0.0.1;localhost") { throw 'DataPaths:Root must match DataDirectory; AllowedHosts must be domain;127.0.0.1;localhost.' }
if (-not (Test-Path -LiteralPath $data -PathType Container)) { throw 'DataPaths:Root must already exist; provision persistent data before installation.' }
$portal = $null
if (-not [uri]::TryCreate($config.Documentation.PortalUrl, [UriKind]::Absolute, [ref]$portal) -or $portal.Scheme -ne 'https' -or $portal.IsLoopback -or $config.Documentation.AllowWrites -ne $false) { throw 'Documentation requires a public HTTPS PortalUrl and AllowWrites=false.' }
function Test-ProductionSecret([string]$Value) {
    return -not ([string]::IsNullOrWhiteSpace($Value) -or $Value -in @('masterkey','mdw-api-jwt-conteudos-secret','mdw-web-dev-secret-change-me-2026-local-migration-only','Medware!111096','altere-para-segredo-forte') -or $Value -match 'CHANGE_ME|^homol-')
}
if (-not (Test-ProductionSecret $config.WebAuth.JwtSecret)) { throw 'WebAuth credentials cannot be empty, legacy or development defaults.' }
$allowLegacyPartnerCredentials = $config.Security.AllowLegacyPartnerCredentials -eq $true
$acceptedLegacyPartnerPair = $allowLegacyPartnerCredentials -and $config.ApiPartner.JwtSecret -ceq 'mdw-api-jwt-conteudos-secret' -and $config.ApiPartner.JwtPassword -ceq 'Medware!111096'
$acceptedOwnPartnerPair = (Test-ProductionSecret $config.ApiPartner.JwtSecret) -and (Test-ProductionSecret $config.ApiPartner.JwtPassword)
if (-not $acceptedLegacyPartnerPair -and -not $acceptedOwnPartnerPair) { throw 'ApiPartner credentials are blocked legacy/default values; enable compatibility for the exact Python pair or rotate both values.' }
if ($allowLegacyPartnerCredentials) { Write-Warning 'Legacy partner API credentials are enabled for compatibility. Plan a coordinated credential rotation.' }
$allowDefaultFirebirdPassword = $config.Security.AllowDefaultFirebirdPassword -eq $true
foreach ($value in @($config.Firebird.Password,$config.AssistantFirebird.Password)) {
    $acceptedDefault = $allowDefaultFirebirdPassword -and $value -ieq 'masterkey'
    if (-not $acceptedDefault -and -not (Test-ProductionSecret $value)) { throw 'Firebird credentials cannot use defaults unless Security:AllowDefaultFirebirdPassword is explicitly true.' }
}
if ($allowDefaultFirebirdPassword) { Write-Warning 'Firebird masterkey opt-in is enabled. Restrict ports 3050/3052 and plan credential rotation.' }
if ($config.WebAuth.DevUserEnabled -ne $false -or [Text.Encoding]::UTF8.GetByteCount($config.WebAuth.JwtSecret) -lt 32 -or $config.WebAuth.JwtSecret -ceq $config.ApiPartner.JwtSecret -or $config.ApiPartner.JwtSecret -ceq $config.ApiPartner.JwtPassword) { throw 'Invalid production authentication settings; use independent credentials and a web key of at least 32 UTF-8 bytes.' }
if ((FullPath $config.Cursor.RepoRoot) -ne $release -or (FullPath $config.Cursor.BridgeScriptPath) -ne (Join-Path $release 'api/agent-bridge/convert.mjs') -or (FullPath $config.Voice.VoiceBridgeScriptPath) -ne (Join-Path $release 'api/agent-bridge/voice.mjs')) { throw 'Cursor:RepoRoot must be the release root; both bridge paths must be absolute paths in its api/agent-bridge directory.' }
if (@($config.Security.CorsOrigins) -notcontains "https://$Domain" -or @($config.Security.CorsOrigins) -contains '*') { throw 'Explicit HTTPS CORS origin required.' }
$trustedProxies = @($config.Security.TrustedProxies)
if ($trustedProxies.Count -ne 2 -or $trustedProxies -notcontains '127.0.0.1' -or $trustedProxies -notcontains '::1') {
    throw 'Security:TrustedProxies must contain exactly 127.0.0.1 and ::1.'
}
foreach ($db in @($config.Firebird,$config.AssistantFirebird)) {
    if ($db.Host -ne '127.0.0.1' -or [string]::IsNullOrWhiteSpace($db.Database) -or $db.Database -notmatch '^[A-Za-z]:\\' -or $db.Database.StartsWith('\\') -or [string]::IsNullOrWhiteSpace($db.Password)) { throw 'Firebird must use localhost, absolute database paths and a password.' }
}
Import-Module WebAdministration
$cert = Get-Item "Cert:\LocalMachine\My\$CertificateThumbprint"
if (-not $cert.HasPrivateKey -or $cert.NotAfter -le (Get-Date)) { throw 'Certificate must be current and include its private key.' }
if (-not (Get-WebGlobalModule | Where-Object Name -eq 'RewriteModule') -or -not (Get-WebGlobalModule | Where-Object Name -eq 'AspNetCoreModuleV2')) { throw 'Install URL Rewrite and .NET 10 Hosting Bundle first.' }
$null = Get-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter 'system.webServer/proxy' -ErrorAction Stop
if (-not ((& dotnet --list-runtimes) -match 'Microsoft.AspNetCore.App 10\.')) { throw '.NET 10 ASP.NET runtime required.' }
if (Get-Service MdwNuxt -ErrorAction SilentlyContinue) { throw 'MdwNuxt already exists; follow upgrade procedure.' }
foreach ($name in @('MdwApi','MdwPublic')) {
    if ((Test-Path "IIS:\Sites\$name") -or (Test-Path "IIS:\AppPools\$name")) { throw "IIS object already exists: $name" }
}
if (Get-NetTCPConnection -LocalPort 5080,3000 -State Listen -ErrorAction SilentlyContinue) { throw 'Ports 5080/3000 must be free.' }
Write-Host 'Validation complete. Verify certificate DNS/SAN, existing HTTPS bindings, firewall and backup before applying.'
if (-not $Apply) { Write-Host 'Validation only: no installation changes made. Use -Apply to install locally.'; return }
if (-not $PSCmdlet.ShouldProcess($release, 'Install local IIS sites and Nuxt service through NSSM; enable server-wide ARR proxy with 360-second timeout')) { return }
function Checked([string]$File, [string[]]$Arguments) {
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$File failed ($LASTEXITCODE); inspect partial installation before retrying." }
}
function Protect([string]$Path, [string[]]$Grants) {
    Checked icacls.exe (@($Path,'/inheritance:r','/grant:r','*S-1-5-18:(OI)(CI)F','*S-1-5-32-544:(OI)(CI)F') + $Grants)
}
New-Item -ItemType Directory -Path $release,$service -Force | Out-Null
Protect $release @()
Protect $service @()
Copy-Item "$package/*" $release -Recurse
foreach ($name in @('MdwApi','MdwPublic')) {
    New-WebAppPool $name | Out-Null
    Set-ItemProperty "IIS:\AppPools\$name" managedRuntimeVersion ''
    Set-ItemProperty "IIS:\AppPools\$name" startMode 'AlwaysRunning'
    Set-ItemProperty "IIS:\AppPools\$name" processModel.idleTimeout ([TimeSpan]::Zero)
    Set-ItemProperty "IIS:\AppPools\$name" recycling.periodicRestart.time ([TimeSpan]::Zero)
}
Protect $release @('IIS AppPool\MdwApi:(OI)(CI)RX','*S-1-5-19:(OI)(CI)RX','IIS AppPool\MdwPublic:(OI)(CI)RX')
# Do not recursively replace existing data ACLs; preserve Firebird service access.
Checked icacls.exe @($data,'/grant','IIS AppPool\MdwApi:(OI)(CI)M')
foreach ($folder in @('static','static/uploads','uploads','logs')) {
    New-Item -ItemType Directory -Path (Join-Path $data $folder) -Force | Out-Null
}
if (Test-Path "$release/static-assets") {
    foreach ($file in Get-ChildItem "$release/static-assets" -Recurse -File) {
        $relative = RelativePath "$release/static-assets" $file.FullName
        $destination = Join-Path "$data/static" $relative
        if (Test-Path -LiteralPath $destination) { continue }
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
}
Copy-Item -LiteralPath $secret -Destination "$release/api/appsettings.Production.local.json"
Checked icacls.exe @("$release/api/appsettings.Production.local.json",'/inheritance:r','/grant:r','*S-1-5-18:F','*S-1-5-32-544:F','IIS AppPool\MdwApi:R')
New-Item -ItemType Directory "$release/public" | Out-Null
(Get-Content "$release/deployment/templates/public.web.config" -Raw).Replace('__DOMAIN__',$Domain) | Set-Content "$release/public/web.config" -Encoding UTF8
New-Item -ItemType Directory "$service/logs" | Out-Null
Protect $service @('*S-1-5-19:(OI)(CI)RX')
Checked icacls.exe @("$service/logs",'/grant','*S-1-5-19:(OI)(CI)M')
$nuxtEntry = "$release/nuxt/server/index.mjs"
$nuxtLogs = Join-Path $service 'logs'
Checked $NssmPath @('install','MdwNuxt',$NodePath,$nuxtEntry)
Checked $NssmPath @('set','MdwNuxt','DisplayName','MDW Nuxt SSR')
Checked $NssmPath @('set','MdwNuxt','Description','Production Nuxt Node 22')
Checked $NssmPath @('set','MdwNuxt','AppDirectory',"$release/nuxt")
Checked $NssmPath @('set','MdwNuxt','AppEnvironmentExtra',
    'NODE_ENV=production','NITRO_HOST=127.0.0.1','NITRO_PORT=3000',
    'NUXT_API_SERVER_BASE=http://127.0.0.1:5080','NUXT_DOCS_API_BASE=http://127.0.0.1:5080',
    'NUXT_PUBLIC_API_BASE=/api-dotnet',"NUXT_DOCS_PUBLIC_BASE=https://$Domain",
    'NUXT_DOCS_SANDBOX_BASE=http://127.0.0.1:9',"NUXT_DOCS_SANDBOX_PUBLIC_BASE=https://$Domain/sandbox-disabled")
Checked $NssmPath @('set','MdwNuxt','AppStdout',(Join-Path $nuxtLogs 'nuxt.stdout.log'))
Checked $NssmPath @('set','MdwNuxt','AppStderr',(Join-Path $nuxtLogs 'nuxt.stderr.log'))
Checked $NssmPath @('set','MdwNuxt','AppStdoutCreationDisposition','4')
Checked $NssmPath @('set','MdwNuxt','AppStderrCreationDisposition','4')
Checked $NssmPath @('set','MdwNuxt','AppRotateFiles','1')
Checked $NssmPath @('set','MdwNuxt','AppRotateOnline','0')
Checked $NssmPath @('set','MdwNuxt','AppRotateBytes','10485760')
Checked $NssmPath @('set','MdwNuxt','AppExit','Default','Restart')
Checked $NssmPath @('set','MdwNuxt','AppRestartDelay','10000')
Checked $NssmPath @('set','MdwNuxt','AppThrottle','1500')
Checked sc.exe @('config','MdwNuxt','obj=','NT AUTHORITY\LocalService','password=','')
Set-Service -Name MdwNuxt -StartupType Automatic
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter 'system.webServer/proxy' -Name enabled -Value $true
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter 'system.webServer/proxy' -Name preserveHostHeader -Value $true
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter 'system.webServer/proxy' -Name timeout -Value '00:06:00'
New-Website -Name MdwApi -PhysicalPath "$release/api" -Port 5080 -IPAddress '127.0.0.1' -ApplicationPool MdwApi | Out-Null
New-Website -Name MdwPublic -PhysicalPath "$release/public" -Port 80 -HostHeader $Domain -ApplicationPool MdwPublic | Out-Null
if (-not (Get-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Location MdwPublic -Filter "system.webServer/rewrite/allowedServerVariables/add[@name='HTTP_X_FORWARDED_PROTO']")) {
    Add-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location MdwPublic -Filter 'system.webServer/rewrite/allowedServerVariables' -Name '.' -Value @{name='HTTP_X_FORWARDED_PROTO'}
}
Set-ItemProperty 'IIS:\Sites\MdwApi' -Name applicationDefaults.preloadEnabled -Value $true
New-WebBinding -Name MdwPublic -Protocol https -Port 443 -HostHeader $Domain -SslFlags 1
(Get-WebBinding -Name MdwPublic -Protocol https).AddSslCertificate($CertificateThumbprint,'My')
Checked $NssmPath @('start','MdwNuxt')
Write-Host 'Installed locally. Run Test-Health.ps1 and functional acceptance before DNS cutover. No database modifications performed.'
