#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot '..\Get-CuradoriaPilotFingerprint.ps1'
$script:session = [PSCustomObject]@{ AuthType = 'Delegated'; Scopes = @('Chat.ReadBasic') }
$script:chats = @([PSCustomObject]@{ Topic = 'Desenvolvimento | Demandas'; ChatType = 'group'; Id = 'fixture-chat' })
function Get-MgContext { $global:curadoriaFixtureSession }
function Get-MgChat { $global:curadoriaFixtureChats }
$global:curadoriaFixtureSession = $script:session
$global:curadoriaFixtureChats = $script:chats
$expected = 'NUXT_CURADORIA_PILOT_CHAT_SHA256=sha256:'
$algorithm = [Security.Cryptography.SHA256]::Create()
try { $expected += ([BitConverter]::ToString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes('fixture-chat')))).Replace('-', '').ToLowerInvariant() }
finally { $algorithm.Dispose() }
$result = & $helper
if ($result -ne $expected -or $result.Contains('fixture-chat')) { throw 'Fingerprint output mismatch.' }
$baseSession = $script:session
$baseChats = $script:chats
foreach ($scenario in @('missing', 'duplicate', 'meeting', 'empty-id', 'no-session', 'app-only', 'no-scope')) {
    $script:session = $baseSession
    $script:chats = @([PSCustomObject]@{ Topic = 'Desenvolvimento | Demandas'; ChatType = 'group'; Id = 'fixture-chat' })
    switch ($scenario) {
        'missing' { $script:chats = @() }
        'duplicate' { $script:chats = @($baseChats[0], $baseChats[0]) }
        'meeting' { $script:chats[0].ChatType = 'meeting' }
        'empty-id' { $script:chats[0].Id = '' }
        'no-session' { $script:session = $null }
        'app-only' { $script:session = [PSCustomObject]@{ AuthType = 'AppOnly'; Scopes = @('Chat.ReadBasic.All') } }
        'no-scope' { $script:session = [PSCustomObject]@{ AuthType = 'Delegated'; Scopes = @('User.Read') } }
    }
    $global:curadoriaFixtureSession = $script:session
    $global:curadoriaFixtureChats = $script:chats
    $rejected = $false
    try { $null = & $helper } catch { $rejected = $true }
    if (-not $rejected) { throw "Unsafe scenario accepted: $scenario" }
}
Write-Output 'PASS: unique group only; no Graph session, API, consent or service used (all commands mocked).'
