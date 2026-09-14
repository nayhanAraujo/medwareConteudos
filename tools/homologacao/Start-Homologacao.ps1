#requires -Version 7.2
[CmdletBinding()]
param([switch]$IntegrationConfirmed, [ValidateSet('Debug','Release')][string]$Configuration = 'Debug')
. (Join-Path $PSScriptRoot 'Homologacao.Common.ps1')
if (-not $IntegrationConfirmed) { throw 'Confirme o build e a integração do isolamento passando -IntegrationConfirmed; consulte docs/API_HOMOLOGACAO.md.' }
$workspace = Get-HomologacaoWorkspace
$prepared = Read-HomologacaoPrepared $workspace
$api = Join-Path $workspace 'backend/MdwConteudos.Api'
$program = [IO.File]::ReadAllText((Join-Path $api 'Program.cs'))
if ($program -notmatch 'HomologacaoGuard\.Configure\(' -or $program -notmatch 'HomologacaoGuard\.Validate\(' -or
    $program.IndexOf('HomologacaoGuard.Validate(') -gt $program.IndexOf('EnsureSchemaAsync(')) {
    throw 'Program ainda não tem o contrato Configure/Validate antes do schema.'
}
$assembly = Assert-LocalPath (Join-Path $api "bin/$Configuration/net10.0/MdwConteudos.Api.dll") -Within $workspace -MustExist
$assemblyTime = (Get-Item -LiteralPath $assembly).LastWriteTimeUtc
$newer = Get-ChildItem -LiteralPath (Join-Path $workspace 'backend') -Filter '*.cs' -Recurse |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.LastWriteTimeUtc -gt $assemblyTime } |
    Select-Object -First 1
if ($newer) { throw 'Build está desatualizado. Compile o backend novamente; Start nunca faz build.' }
$dotnet = Assert-LocalPath (Join-Path $env:ProgramFiles 'dotnet/dotnet.exe') -MustExist
$info = New-HomologacaoProcessInfo $dotnet @($assembly) $api
$info.Environment['DOTNET_ENVIRONMENT'] = 'Homologacao'
$info.Environment['ASPNETCORE_ENVIRONMENT'] = 'Homologacao'
# Contexto esperado deve ser passado também ao processo Nuxt (não é segredo).
$info.Environment['NUXT_DOCS_SANDBOX_INSTANCE_ID'] = $prepared.Config.Documentation.InstanceId
Write-Host "API em primeiro plano: $($prepared.Config.Urls)"
Write-Host "Nuxt deve usar NUXT_DOCS_SANDBOX_INSTANCE_ID=$($prepared.Config.Documentation.InstanceId)"
$process = [Diagnostics.Process]::new(); $process.StartInfo = $info
try {
    $null = $process.Start()
    while (-not $process.WaitForExit(1000)) { }
    if ($process.ExitCode -ne 0) { throw "API finalizada com exit $($process.ExitCode)." }
} finally {
    if ($process.Id -and -not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
    $process.Dispose()
}
