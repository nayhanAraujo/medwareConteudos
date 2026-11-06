#requires -Version 7.0
# Exercises the real staging branches without running the package/build commands.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scriptPath = Join-Path $PSScriptRoot '../Build-Package.ps1'
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    (Resolve-Path $scriptPath).Path, [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw "Invalid package script: $errors" }
$filter = $ast.Find({ param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Is-SourceAllowed'
}, $false)
$staging = $ast.Find({ param($node)
    $node -is [Management.Automation.Language.IfStatementAst] -and
    $node.Clauses[0].Item1.Extent.Text -eq '$WorkingTree'
}, $false)
if (-not $filter -or -not $staging) { throw 'Package filter/staging branches not found.' }
. ([scriptblock]::Create($filter.Extent.Text))
function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "$Command failed ($LASTEXITCODE)" }
}
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('mdw-source-test-' + [guid]::NewGuid())
$repo = Join-Path $fixture 'repo'
New-Item -ItemType Directory -Path $repo | Out-Null
$resource = 'docs/sql/scripts/add-studio-rascunhos.sql'
$allowed = @($resource, 'docs/PRODUCAO_IIS.md', 'backend/MdwConteudos.Api/Example.cs')
$blocked = @('docs/usuariosMedware.html', 'docs/sql/scripts/private.sql',
    'docs/sql/scripts/add-studio-rascunhos.sql.bak', '.env', 'backend/.env.local',
    'backend/MdwConteudos.Api/appsettings.Development.local.json',
    'backend/bd/REFERENCIAS.FDB', 'backend/private.fdb', 'backend/secret.key')
foreach ($relative in @($allowed) + @($blocked)) {
    $path = Join-Path $repo $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
    Set-Content -LiteralPath $path -Value "Fixture only: $relative" -Encoding utf8
}
Invoke-Checked git @('-C', $repo, 'init', '--quiet')
Invoke-Checked git @('-C', $repo, 'add', '--all')
Invoke-Checked git @('-C', $repo, '-c', 'user.name=Teste local', '-c', 'user.email=teste@example.invalid',
    '-c', 'commit.gpgsign=false', 'commit', '--quiet', '-m', 'Cria fixture isolada')
$untracked = 'backend/UntrackedExample.cs'
Set-Content -LiteralPath (Join-Path $repo $untracked) -Value 'Fixture untracked source'
foreach ($WorkingTree in @($false, $true)) {
    $stage = Join-Path $fixture $(if ($WorkingTree) { 'working-tree' } else { 'archive' })
    $source = Join-Path $stage 'source'
    New-Item -ItemType Directory -Path $source | Out-Null
    # Executes the unchanged branches from Build-Package.ps1 against the fixture repository.
    . ([scriptblock]::Create($staging.Extent.Text))
    foreach ($relative in $allowed) {
        $destination = Join-Path $source $relative
        if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) {
            throw "Missing source ($WorkingTree): $relative"
        }
        if ((Get-FileHash $destination).Hash -ne (Get-FileHash (Join-Path $repo $relative)).Hash) {
            throw "Source content changed: $relative"
        }
    }
    foreach ($relative in $blocked) {
        if (Test-Path -LiteralPath (Join-Path $source $relative)) {
            throw "Forbidden source ($WorkingTree): $relative"
        }
    }
    if ((Test-Path -LiteralPath (Join-Path $source $untracked)) -ne $WorkingTree) {
        throw 'WorkingTree must include untracked source; archive must use HEAD only.'
    }
    if (-not (Is-SourceAllowed 'docs\sql\scripts\add-studio-rascunhos.sql')) {
        throw 'Windows resource path rejected.'
    }
    Write-Host "PASS: staging WorkingTree=$WorkingTree includes SQL and excludes private files."
}
Write-Host "Isolated fixture retained: $fixture. No build or database operation executed."
