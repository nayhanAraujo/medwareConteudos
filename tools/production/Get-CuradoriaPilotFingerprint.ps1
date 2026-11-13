#requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
# Read-only: never install modules, sign in, request consent or change a service here.
if (-not (Get-Command Get-MgContext -ErrorAction SilentlyContinue) -or
    -not (Get-Command Get-MgChat -ErrorAction SilentlyContinue)) {
    throw 'Os comandos Microsoft Graph precisam estar disponiveis nesta janela.'
}
$graphContext = Get-MgContext
if (-not $graphContext -or $graphContext.AuthType -ne 'Delegated') {
    throw 'Use uma sessao Graph delegada ja autorizada nesta janela.'
}
$acceptedScopes = @('Chat.ReadBasic', 'Chat.Read', 'Chat.ReadWrite')
if (-not @($graphContext.Scopes | Where-Object { $_ -in $acceptedScopes }).Count) {
    throw 'A sessao existente nao informa uma permissao delegada de leitura basica de chats.'
}
$chats = @(Get-MgChat -All -ErrorAction Stop | Where-Object { $_.Topic -eq 'Desenvolvimento | Demandas' })
if ($chats.Count -ne 1) { throw 'Esperado exatamente um chat com o nome do piloto. Confira duplicidades antes de continuar.' }
if ([string]$chats[0].ChatType -ne 'group') { throw 'O chat do piloto precisa ter ChatType group. Nenhuma impressao digital foi gerada.' }
$chatIdentifier = [string]$chats[0].Id
if ([string]::IsNullOrWhiteSpace($chatIdentifier) -or $chatIdentifier.Length -gt 2048) { throw 'Identificador de chat ausente ou invalido.' }
$algorithm = [Security.Cryptography.SHA256]::Create()
try {
    $bytes = [Text.Encoding]::UTF8.GetBytes($chatIdentifier)
    $fingerprint = ([BitConverter]::ToString($algorithm.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    # Configure privately in the Nuxt service, never in Git, URLs or runtimeConfig.public.
    Write-Output "NUXT_CURADORIA_PILOT_CHAT_SHA256=sha256:$fingerprint"
} finally {
    $algorithm.Dispose()
    $chatIdentifier = $null
    $chats = $null
    $bytes = $null
}
