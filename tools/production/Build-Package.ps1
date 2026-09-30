#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$BuildLabel,
    [switch]$WorkingTree
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$global:LASTEXITCODE = 0
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new OutputDirectory; existing releases are never overwritten.' }
$nodeVersion = & node --version
if ($LASTEXITCODE -ne 0 -or $nodeVersion -notmatch '^v22\.') { throw 'Build requires Node 22. Use the pinned CI runtime.' }
foreach ($item in Get-ChildItem Env:) {
    if ($item.Name -match '^(NUXT_|DOTNET_API_BASE$|FIREBIRD_|ASSISTENTE_FIREBIRD_|API_JWT_|CURSOR_|OPENAI_|AZURE_OPENAI_)') {
        throw "Build environment must not contain application variable $($item.Name). Use a clean build shell."
    }
}
$stage = Join-Path ([IO.Path]::GetTempPath()) ('mdw-package-' + [guid]::NewGuid())
$source = Join-Path $stage 'source'
New-Item -ItemType Directory -Path $source | Out-Null
function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    if ($Command -eq 'npm.cmd') {
        $npmCli = Join-Path (Split-Path (Get-Command npm.cmd).Source) 'node_modules/npm/bin/npm-cli.js'
        if (-not (Test-Path -LiteralPath $npmCli)) { throw 'Install npm with its standard CLI layout.' }
        & node $npmCli @Arguments
    } else { & $Command @Arguments }
    if ($LASTEXITCODE -ne 0) { throw "$Command failed ($LASTEXITCODE). Staging retained: $stage" }
}
function Is-SourceAllowed([string]$Path) {
    $p = $Path.Replace('\','/')
    if ($p -notmatch '^(backend/|frontend/nuxt-app/|tools/production/|global\.json$|docs/|static/(img|css|js|fonts)/)') { return $false }
    if ($p -match '(^|/)(node_modules|bin[^/]*|obj|\.nuxt|\.output|\.git|\.homologacao|uploads[^/]*|bd|TestResults)(/|$)') { return $false }
    if ($p -match '(^|/)(\.env[^/]*|appsettings\..*local\.json|appsettings\.(Development|Homologacao)\.json|secrets\.json|launchSettings\.json)$') { return $false }
    if ($p -match '\.(fdb|gdb|fbk|bak|pfx|key|pem|db|sqlite\d*)$') { return $false }
    if ($p -match '^docs/' -and $p -notmatch '(AGENTE-LAUDO-POR-VOZ|AGENTE-MODELOS-MODO-TEXTO|PRODUCAO_IIS)\.md$') { return $false }
    return $true
}
if ($WorkingTree) {
    $files = & git -c core.quotepath=false -C $repo ls-files --cached --others --exclude-standard
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate source.' }
    foreach ($relative in $files | Sort-Object -Unique) {
        if (-not (Is-SourceAllowed $relative)) { continue }
        $path = Join-Path $repo $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Source links are not allowed: $relative" }
        $destination = Join-Path $source $relative
        New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($destination)) | Out-Null
        Copy-Item -LiteralPath $path -Destination $destination
    }
} else {
    Invoke-Checked git @('-C',$repo,'archive','--format=zip',"--output=$stage/source.zip",'HEAD')
    Expand-Archive -LiteralPath "$stage/source.zip" -DestinationPath "$stage/archive"
    foreach ($file in Get-ChildItem "$stage/archive" -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath("$stage/archive",$file.FullName)
        if (-not (Is-SourceAllowed $relative)) { continue }
        $destination = Join-Path $source $relative
        New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($destination)) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
}
New-Item -ItemType Directory -Path $output | Out-Null
Push-Location $source
try {
    Invoke-Checked dotnet @('test','backend/ConversorHtml.Tests/ConversorHtml.Tests.csproj','-c','Release','--logger','console;verbosity=minimal')
    Invoke-Checked dotnet @('publish','backend/MdwConteudos.Api/MdwConteudos.Api.csproj','-c','Release','-o',"$output/api")
} finally { Pop-Location }
Push-Location "$source/frontend/nuxt-app"
$oldPreset = $env:NITRO_PRESET
$oldReleaseBuild = $env:NUXT_PUBLIC_RELEASE_BUILD
try {
    $env:NITRO_PRESET = 'node-server'
    if ($BuildLabel) { $env:NUXT_PUBLIC_RELEASE_BUILD = $BuildLabel }
    Invoke-Checked npm.cmd @('ci')
    Invoke-Checked npm.cmd @('run','test:docs')
    Invoke-Checked npm.cmd @('run','build')
    Copy-Item -LiteralPath '.output' -Destination "$output/nuxt" -Recurse
} finally {
    $env:NITRO_PRESET = $oldPreset
    $env:NUXT_PUBLIC_RELEASE_BUILD = $oldReleaseBuild
    Pop-Location
}
Copy-Item -LiteralPath "$source/backend/agent-bridge" -Destination "$output/api/agent-bridge" -Recurse
Push-Location "$output/api/agent-bridge"
try { Invoke-Checked npm.cmd @('ci','--omit=dev') } finally { Pop-Location }
Copy-Item -LiteralPath "$source/backend/manual_scripts_html_UX.md" -Destination $output
Copy-Item "$source/docs/AGENTE-LAUDO-POR-VOZ.md", "$source/docs/AGENTE-MODELOS-MODO-TEXTO.md" -Destination $output
Copy-Item -LiteralPath "$source/backend/sql" -Destination "$output/sql" -Recurse
if (Test-Path "$source/static") { Copy-Item -LiteralPath "$source/static" -Destination "$output/static-assets" -Recurse }
Copy-Item -LiteralPath "$source/tools/production" -Destination "$output/deployment" -Recurse
Copy-Item "$source/tools/production/templates/api.web.config" "$output/api/web.config" -Force
Copy-Item -LiteralPath "$source/docs/PRODUCAO_IIS.md" -Destination $output
foreach ($file in Get-ChildItem $output -Recurse -File) {
    if ($file.FullName -match '[\\/]node_modules[\\/]') { continue }
    if ($file.Name -match '^\.env($|\.)|^appsettings\.(Development|Homologacao).*\.json$|^appsettings\..*local\.json$|\.(fdb|fbk|pfx|bak)$') {
        throw "Forbidden file in package: $($file.Name)"
    }
}
foreach ($required in @('api/MdwConteudos.Api.dll','api/appsettings.json','api/appsettings.Production.json','api/agent-bridge/node_modules/@cursor/sdk','nuxt/server/index.mjs')) {
    if (-not (Test-Path "$output/$required")) { throw "Incomplete package: $required" }
}
$head = & git -C $repo rev-parse HEAD
@{ Commit=$head; BuildLabel=$BuildLabel; WorkingTree=[bool]$WorkingTree; Node=$nodeVersion; Dotnet=(& dotnet --version); BuiltAtUtc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json | Set-Content "$output/build-info.json" -Encoding UTF8
$files = Get-ChildItem $output -Recurse -File | ForEach-Object {
    [pscustomobject]@{ Path=[IO.Path]::GetRelativePath($output,$_.FullName); SHA256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash }
}
$files | ConvertTo-Json -Depth 4 | Set-Content "$output/manifest.json" -Encoding UTF8
Write-Host "Package validated: $output. Build staging retained: $stage"
