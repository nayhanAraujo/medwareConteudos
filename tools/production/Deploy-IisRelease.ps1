#requires -Version 5.1
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$ReleaseDirectory,
    [Parameter(Mandatory)][string]$DataDirectory,
    [Parameter(Mandatory)][string]$SecretsFile,
    [Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9.-]+$')][string]$Domain,
    [Parameter(Mandatory)][string]$NssmPath,
    [Parameter(Mandatory)][string]$NodePath,
    [string]$ApiSite = 'MdwConteudoApi',
    [string]$ApiPool = 'MdwConteudoApi',
    [string]$PublicSite = 'MdwConteudo',
    [string]$PublicPool = 'MdwConteudo',
    [string]$NuxtService = 'MdwConteudoNuxt',
    [switch]$RequireReady
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function FullPath([string]$Path) {
    return [IO.Path]::GetFullPath($Path).TrimEnd('\')
}

function Assert-ChildPath([string]$Parent, [string]$Child, [string]$Description) {
    $parentPath = FullPath $Parent
    $childPath = FullPath $Child
    if (-not $childPath.StartsWith($parentPath + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description deve estar dentro de $parentPath."
    }
}

function Invoke-Checked([string]$File, [string[]]$Arguments) {
    $global:LASTEXITCODE = 0
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$File falhou com o código $LASTEXITCODE."
    }
}

function Get-NssmValue([string]$Executable, [string]$Parameter) {
    $global:LASTEXITCODE = 0
    $value = (& $Executable get $NuxtService $Parameter 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Não foi possível consultar $Parameter do serviço $NuxtService." }
    return $value
}

function Wait-ServiceState([string]$Name, [string]$Expected, [int]$TimeoutSeconds = 45) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $state = (Get-Service -Name $Name -ErrorAction Stop).Status.ToString()
        if ($state -eq $Expected) { return }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    throw "Timeout aguardando o serviço $Name ficar $Expected."
}

function Stop-Components {
    Stop-Website -Name $PublicSite -ErrorAction SilentlyContinue
    Stop-WebAppPool -Name $PublicPool -ErrorAction SilentlyContinue
    if ((Get-Service -Name $NuxtService).Status -ne 'Stopped') {
        Stop-Service -Name $NuxtService -Force
        Wait-ServiceState $NuxtService 'Stopped'
    }
    Stop-Website -Name $ApiSite -ErrorAction SilentlyContinue
    Stop-WebAppPool -Name $ApiPool -ErrorAction SilentlyContinue
}

function Start-Components {
    if ((Get-WebAppPoolState -Name $ApiPool).Value -ne 'Started') { Start-WebAppPool -Name $ApiPool }
    if ((Get-Website -Name $ApiSite).State -ne 'Started') { Start-Website -Name $ApiSite }
    if ((Get-Service -Name $NuxtService).Status -ne 'Running') { Start-Service -Name $NuxtService }
    Wait-ServiceState $NuxtService 'Running'
    if ((Get-WebAppPoolState -Name $PublicPool).Value -ne 'Started') { Start-WebAppPool -Name $PublicPool }
    if ((Get-Website -Name $PublicSite).State -ne 'Started') { Start-Website -Name $PublicSite }
}

function Restore-ComponentState([System.Collections.IDictionary]$State) {
    if ($State.ApiPoolState -eq 'Started') { Start-WebAppPool -Name $ApiPool }
    if ($State.ApiSiteState -eq 'Started') { Start-Website -Name $ApiSite }
    if ($State.NuxtServiceState -eq 'Running') {
        Start-Service -Name $NuxtService
        Wait-ServiceState $NuxtService 'Running'
    }
    if ($State.PublicPoolState -eq 'Started') { Start-WebAppPool -Name $PublicPool }
    if ($State.PublicSiteState -eq 'Started') { Start-Website -Name $PublicSite }
}

function Test-Http([string]$Url, [string]$ExpectedStatus = $null, [int]$WaitSeconds = 90) {
    $deadline = (Get-Date).AddSeconds($WaitSeconds)
    $lastFailure = $null
    do {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 20
            if ($response.StatusCode -ne 200) { throw "HTTP $($response.StatusCode)" }
            if ($ExpectedStatus) {
                $body = $response.Content | ConvertFrom-Json
                if ($body.status -ne $ExpectedStatus) { throw "status '$($body.status)' diferente de '$ExpectedStatus'" }
            }
            return
        }
        catch {
            $lastFailure = $_.Exception.Message
            Start-Sleep -Seconds 2
        }
    } while ((Get-Date) -lt $deadline)
    throw "Smoke falhou em $Url após $WaitSeconds segundos: $lastFailure"
}

function Assert-ConfiguredDatabase([object]$Settings, [string]$Section, [string]$DataRoot) {
    $databaseSection = $Settings.$Section
    if (-not $databaseSection) { throw "Seção obrigatória ausente no JSON: $Section" }
    if ([string]::IsNullOrWhiteSpace([string]$databaseSection.Database)) { throw "$Section`:Database não foi configurado." }
    $databasePath = FullPath ([string]$databaseSection.Database)
    Assert-ChildPath $DataRoot $databasePath "$Section`:Database"
    if (-not (Test-Path -LiteralPath $databasePath -PathType Leaf)) {
        throw "Banco não encontrado para ${Section}: $databasePath"
    }
}

$package = FullPath (Resolve-Path -LiteralPath $PackageDirectory).Path
$release = FullPath $ReleaseDirectory
$data = FullPath (Resolve-Path -LiteralPath $DataDirectory).Path
$secret = FullPath (Resolve-Path -LiteralPath $SecretsFile).Path
$nssm = FullPath (Resolve-Path -LiteralPath $NssmPath).Path
$node = FullPath (Resolve-Path -LiteralPath $NodePath).Path
$releasesRoot = FullPath (Split-Path $release -Parent)

Assert-ChildPath $releasesRoot $release 'ReleaseDirectory'
try {
    $productionSettings = Get-Content -LiteralPath $secret -Raw | ConvertFrom-Json
}
catch {
    throw "JSON de produção inválido em ${secret}: $($_.Exception.Message)"
}
if (-not $productionSettings.DataPaths -or [string]::IsNullOrWhiteSpace([string]$productionSettings.DataPaths.Root)) {
    throw 'DataPaths:Root não foi configurado no JSON de produção.'
}
if ((FullPath ([string]$productionSettings.DataPaths.Root)) -ne $data) {
    throw "DataPaths:Root deve ser exatamente $data."
}
Assert-ConfiguredDatabase $productionSettings 'Firebird' $data
Assert-ConfiguredDatabase $productionSettings 'AssistantFirebird' $data
if ($productionSettings.AssistantFirebird.ClientLibrary) {
    $assistantClient = FullPath ([string]$productionSettings.AssistantFirebird.ClientLibrary)
    if (-not (Test-Path -LiteralPath $assistantClient -PathType Leaf)) {
        throw "ClientLibrary do Assistente não encontrada: $assistantClient"
    }
}

$nodeVersion = (& $node --version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $nodeVersion -notmatch '^v22\.') { throw "Node.js 22 obrigatório; encontrado '$nodeVersion'." }
$nssmVersion = (& $nssm version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $nssmVersion -notmatch '^2\.') { throw "NSSM inválido em $nssm." }

if (Test-Path -LiteralPath $release) { throw 'A release já existe; builds nunca são sobrescritos.' }
if (-not (Test-Path -LiteralPath (Join-Path $package 'manifest.json') -PathType Leaf)) { throw 'Manifesto ausente.' }
foreach ($required in @('api\MdwConteudos.Api.dll', 'api\web.config', 'nuxt\server\index.mjs', 'deployment\templates\public.web.config')) {
    if (-not (Test-Path -LiteralPath (Join-Path $package $required) -PathType Leaf)) { throw "Pacote incompleto: $required" }
}

# Valida a integridade antes de parar qualquer componente.
foreach ($entry in (Get-Content (Join-Path $package 'manifest.json') -Raw | ConvertFrom-Json)) {
    $path = FullPath (Join-Path $package $entry.Path)
    Assert-ChildPath $package $path 'Entrada do manifesto'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Arquivo ausente no pacote: $($entry.Path)" }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.SHA256) {
        throw "Falha de integridade no pacote: $($entry.Path)"
    }
}

Import-Module WebAdministration
foreach ($site in @($ApiSite, $PublicSite)) {
    if (-not (Test-Path "IIS:\Sites\$site")) { throw "Site IIS ausente: $site" }
}
foreach ($pool in @($ApiPool, $PublicPool)) {
    if (-not (Test-Path "IIS:\AppPools\$pool")) { throw "Pool IIS ausente: $pool" }
}
if (-not (Get-Service -Name $NuxtService -ErrorAction SilentlyContinue)) { throw "Serviço ausente: $NuxtService" }
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'O agente de deploy precisa executar elevado para controlar IIS e serviços.'
}

$old = [ordered]@{
    ApiPath = (Get-Item "IIS:\Sites\$ApiSite").physicalPath
    PublicPath = (Get-Item "IIS:\Sites\$PublicSite").physicalPath
    ApiSiteState = (Get-Website -Name $ApiSite).State.ToString()
    ApiPoolState = (Get-WebAppPoolState -Name $ApiPool).Value
    PublicSiteState = (Get-Website -Name $PublicSite).State.ToString()
    PublicPoolState = (Get-WebAppPoolState -Name $PublicPool).Value
    NuxtServiceState = (Get-Service -Name $NuxtService).Status.ToString()
    NuxtApplication = Get-NssmValue $nssm 'Application'
    NuxtParameters = Get-NssmValue $nssm 'AppParameters'
    NuxtDirectory = Get-NssmValue $nssm 'AppDirectory'
}

if (-not $PSCmdlet.ShouldProcess($release, 'Criar release, trocar caminhos IIS/NSSM e executar smoke tests com rollback')) { return }

New-Item -ItemType Directory -Path $releasesRoot -Force | Out-Null
New-Item -ItemType Directory -Path $release | Out-Null
Invoke-Checked 'icacls.exe' @($release, '/inheritance:r', '/grant:r', '*S-1-5-18:(OI)(CI)F', '*S-1-5-32-544:(OI)(CI)F', "IIS AppPool\${ApiPool}:(OI)(CI)RX", "IIS AppPool\${PublicPool}:(OI)(CI)RX", '*S-1-5-19:(OI)(CI)RX')
Get-ChildItem -LiteralPath $package -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $release -Recurse -Force
}

$publicDirectory = Join-Path $release 'public'
New-Item -ItemType Directory -Path $publicDirectory -Force | Out-Null
$publicConfig = (Get-Content (Join-Path $release 'deployment\templates\public.web.config') -Raw).Replace('__DOMAIN__', $Domain)
Set-Content -LiteralPath (Join-Path $publicDirectory 'web.config') -Value $publicConfig -Encoding UTF8

$publishedSecret = Join-Path $release 'api\appsettings.Production.local.json'
Copy-Item -LiteralPath $secret -Destination $publishedSecret -Force
Invoke-Checked 'icacls.exe' @($publishedSecret, '/inheritance:r', '/grant:r', '*S-1-5-18:F', '*S-1-5-32-544:F', "IIS AppPool\${ApiPool}:R")

$newApiPath = Join-Path $release 'api'
$newNuxtPath = Join-Path $release 'nuxt'
$newNuxtEntry = Join-Path $newNuxtPath 'server\index.mjs'

try {
    Stop-Components
    Set-ItemProperty "IIS:\Sites\$ApiSite" -Name physicalPath -Value $newApiPath
    Set-ItemProperty "IIS:\Sites\$PublicSite" -Name physicalPath -Value $publicDirectory
    Invoke-Checked $nssm @('set', $NuxtService, 'Application', $node)
    Invoke-Checked $nssm @('set', $NuxtService, 'AppParameters', $newNuxtEntry)
    Invoke-Checked $nssm @('set', $NuxtService, 'AppDirectory', $newNuxtPath)
    Start-Components

    Test-Http 'http://127.0.0.1:5080/health/live' 'healthy'
    if ($RequireReady) { Test-Http 'http://127.0.0.1:5080/health/ready' 'ready' }
    Test-Http 'http://127.0.0.1:3000/login'
    Test-Http "https://$Domain/login"
    Test-Http "https://$Domain/apiconteudos/docs"

    $stateDirectory = Join-Path (Split-Path $releasesRoot -Parent) 'deploy-state'
    New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
    [ordered]@{
        deployedAtUtc = [DateTime]::UtcNow.ToString('o')
        release = $release
        previous = $old
        build = if (Test-Path (Join-Path $release 'build-info.json')) { Get-Content (Join-Path $release 'build-info.json') -Raw | ConvertFrom-Json } else { $null }
    } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $stateDirectory 'current.json') -Encoding UTF8
    Write-Host "Deploy concluído: $release"
}
catch {
    $failure = $_
    Write-Warning "Deploy falhou; iniciando rollback de código: $($failure.Exception.Message)"
    try {
        Stop-Components
        Set-ItemProperty "IIS:\Sites\$ApiSite" -Name physicalPath -Value $old.ApiPath
        Set-ItemProperty "IIS:\Sites\$PublicSite" -Name physicalPath -Value $old.PublicPath
        Invoke-Checked $nssm @('set', $NuxtService, 'Application', $old.NuxtApplication)
        Invoke-Checked $nssm @('set', $NuxtService, 'AppParameters', $old.NuxtParameters)
        Invoke-Checked $nssm @('set', $NuxtService, 'AppDirectory', $old.NuxtDirectory)
        Restore-ComponentState $old
        if ($old.ApiSiteState -eq 'Started') { Test-Http 'http://127.0.0.1:5080/health/live' 'healthy' }
        if ($old.NuxtServiceState -eq 'Running') { Test-Http 'http://127.0.0.1:3000/login' }
    }
    catch {
        Write-Warning "Rollback também falhou: $($_.Exception.Message)"
    }
    throw $failure
}
