#requires -Version 7.2
[CmdletBinding()]
param([switch]$CheckContext)
. (Join-Path $PSScriptRoot 'Homologacao.Common.ps1')
$workspace = Get-HomologacaoWorkspace
$prepared = Read-HomologacaoPrepared $workspace
Write-Host "Artefatos e identidade válidos: $($prepared.Manifest.root)"
if ($CheckContext) {
    $uri = [Uri]$prepared.Config.Urls
    if ($uri.Scheme -ne 'http' -or $uri.Host -notin @('127.0.0.1','localhost') -or $uri.AbsolutePath -ne '/') {
        throw 'Endpoint de contexto deve usar HTTP loopback.'
    }
    $context = Invoke-RestMethod -Uri ($uri.AbsoluteUri.TrimEnd('/') + '/api/documentacao/contexto') -TimeoutSec 10 -MaximumRedirection 0
    if ($context.environment -cne 'Homologacao' -or $context.allowWrites -ne $true -or
        $context.instanceId -cne $prepared.Config.Documentation.InstanceId) { throw 'Contexto não corresponde à sandbox preparada.' }
    Write-Host 'Contexto HTTP validado; nenhuma escrita HTTP executada.'
}
