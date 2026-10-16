#requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Load only the process helper, never the deploy's executable statements.
$scriptPath = Join-Path $PSScriptRoot '..\Deploy-IisRelease.ps1'
$parseErrors = $null
$tokens = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
$helper = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Invoke-ExternalWithTimeout' }, $true)
. ([scriptblock]::Create($helper.Extent.Text))
$nodePath = (Get-Command node.exe).Source

$output = Invoke-ExternalWithTimeout $nodePath @('-e', 'process.stdout.write(String.fromCharCode(120).repeat(131072));process.stderr.write(String.fromCharCode(121).repeat(131072))') 'Verbose child'
if ($output.Length -lt 262144) { throw 'Child output was truncated.' }

$processEnv = [Environment]::GetEnvironmentVariable('MDW_DEPLOY_PROCESS_TEST')
$output = Invoke-ExternalWithTimeout -File $nodePath -Arguments @('-e', 'console.log(process.env.MDW_DEPLOY_PROCESS_TEST);console.log(process.cwd())') -Description 'Child environment' -WorkingDirectory $PSScriptRoot -EnvironmentOverrides @{ MDW_DEPLOY_PROCESS_TEST = 'isolated' }
if (-not $output.Contains('isolated') -or -not $output.Contains($PSScriptRoot)) { throw 'Child environment/directory incorrect.' }
if ([Environment]::GetEnvironmentVariable('MDW_DEPLOY_PROCESS_TEST') -ne $processEnv) { throw 'Parent environment was changed.' }

try {
    Invoke-ExternalWithTimeout $nodePath @('-e', 'process.exit(7)') 'Failed child' | Out-Null
    throw 'Nonzero exit was ignored.'
}
catch { if ($_.Exception.Message -notmatch '7:') { throw } }
try {
    Invoke-ExternalWithTimeout $nodePath @('-e', 'setInterval(()=>{},1000)') 'Blocked child' 1 | Out-Null
    throw 'Timeout was ignored.'
}
catch { if ($_.Exception.Message -notmatch '1 segundos') { throw } }
Write-Host 'OK: verbose output, child environment/directory, nonzero exit and timeout.'
