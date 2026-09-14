#requires -Version 7.2
[CmdletBinding()]
param([string]$PortalBase = 'http://127.0.0.1:3001', [switch]$AllowCopyWrites)
. (Join-Path $PSScriptRoot 'Homologacao.Common.ps1')
$prepared = Read-HomologacaoPrepared (Get-HomologacaoWorkspace)
$apiBase = $prepared.Config.Urls.TrimEnd('/')
$portal = [Uri]$PortalBase
if ($portal.Scheme -ne 'http' -or $portal.Host -notin @('localhost','127.0.0.1')) { throw 'Portal deve ser local.' }
$context = Invoke-RestMethod "$apiBase/api/documentacao/contexto"
if ($context.environment -cne 'Homologacao' -or $context.instanceId -cne $prepared.Config.Documentation.InstanceId) { throw 'API não é a cópia preparada.' }
function Check([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message }; Write-Host "OK: $Message" }
function Request([string]$Url, [string]$Method = 'GET', [hashtable]$Headers = @{}, [object]$Body = $null) {
    $options = @{ Uri=$Url; Method=$Method; Headers=$Headers; SkipHttpErrorCheck=$true; TimeoutSec=30; MaximumRedirection=0 }
    if ($null -ne $Body) { $options.Body = $Body | ConvertTo-Json -Depth 10; $options.ContentType='application/json' }
    Invoke-WebRequest @options
}
function Execute([string]$Environment, [string]$Operation, [object]$Parameters, [string]$Body, [bool]$Confirm, [hashtable]$Headers) {
    Invoke-RestMethod "$PortalBase/api/documentacao/$Environment/parceiros/executar" -Method Post -Headers $Headers -Form @{
        operationId=$Operation; parameters=($Parameters | ConvertTo-Json -Depth 8 -Compress); body=$Body; contentType='application/json'; confirmedWrite=$Confirm.ToString().ToLowerInvariant()
    } -SkipHttpErrorCheck -TimeoutSec 30
}
$spec = Invoke-RestMethod "$PortalBase/api/documentacao/homologacao/parceiros/openapi"
Check ($spec.openapi.StartsWith('3.0.')) 'OpenAPI público 3.0'
foreach ($definition in @('api-v1','web')) {
    Check ((Request "$apiBase/swagger/$definition/swagger.json").StatusCode -eq 401) "JSON restrito $definition sem login retorna 401"
}
$tokenResponse = (Request "$apiBase/apiconteudos/v1/token" POST @{} @{senha=$prepared.Config.ApiPartner.JwtPassword}).Content | ConvertFrom-Json
Check ($tokenResponse.success -eq $true) 'Emissão de JWT parceiro'
$partnerHeaders = @{Authorization="Bearer $($tokenResponse.token)"}
$webResponse = (Request "$apiBase/api/web/auth/login" POST @{} @{usuario=$prepared.Config.WebAuth.DevUsername; senha=$prepared.Config.WebAuth.DevPassword}).Content | ConvertFrom-Json
Check ($webResponse.success -eq $true) 'Login administrativo exclusivo da cópia'
$webHeaders = @{Authorization="Bearer $($webResponse.token)"}
foreach ($definition in @('api-v1','web')) {
    Check ((Request "$apiBase/swagger/$definition/swagger.json" GET $webHeaders).StatusCode -eq 200) "JSON restrito $definition com admin retorna 200"
}
$docsHeaders = @{Authorization=$webHeaders.Authorization; 'X-Docs-Web-Authorization'=$webHeaders.Authorization}
foreach ($definition in @('interna','web')) {
    Check ((Request "$PortalBase/api/documentacao/homologacao/$definition/openapi" GET $docsHeaders).StatusCode -eq 200) "Proxy entrega definição $definition ao admin"
}
foreach ($path in @('health','variaveis','normalidades','formulas','referencias','especialidades','sistema/info','normalidades_ecodopplercardiograma','normalidades/ecodopplercardiograma/1','relatorios','scripts?incluir_arquivos=0')) {
    $partner = Request "$apiBase/apiconteudos/v1/$path" GET $partnerHeaders
    $alias = Request "$apiBase/api/v1/$path"
    Check ($partner.StatusCode -eq 200 -and $alias.StatusCode -eq 200) "Consulta e alias: $path"
    $p = $partner.Content | ConvertFrom-Json -AsHashtable
    $a = $alias.Content | ConvertFrom-Json -AsHashtable
    $p.Remove('timestamp'); $a.Remove('timestamp')
    if ($path -eq 'sistema/info') { $p.data.Remove('ultima_atualizacao'); $a.data.Remove('ultima_atualizacao') }
    Check (($p | ConvertTo-Json -Depth 70 -Compress) -ceq ($a | ConvertTo-Json -Depth 70 -Compress)) "Payload equivalente entre prefixos: $path"
}
Check ((Request "$apiBase/apiconteudos/v1/paineis" GET $partnerHeaders).StatusCode -eq 200) 'Consulta painéis'
$scripts = ((Request "$apiBase/apiconteudos/v1/scripts?incluir_arquivos=0" GET $partnerHeaders).Content | ConvertFrom-Json).data
$downloadVerified = $false
foreach ($script in $scripts) {
    $path = "scripts/$($script.codscriptlaudo)/download"
    $download = Request "$apiBase/apiconteudos/v1/$path" GET $partnerHeaders
    if ($download.StatusCode -ne 200) { continue }
    $alias = Request "$apiBase/api/v1/$path"
    Check ($alias.StatusCode -eq 200) 'Download ZIP disponível nos dois prefixos'
    $left = [IO.Compression.ZipArchive]::new([IO.MemoryStream]::new([byte[]]$download.Content), [IO.Compression.ZipArchiveMode]::Read)
    $right = [IO.Compression.ZipArchive]::new([IO.MemoryStream]::new([byte[]]$alias.Content), [IO.Compression.ZipArchiveMode]::Read)
    try {
        Check ($left.Entries.Count -eq $right.Entries.Count -and $left.Entries.Count -gt 0) 'ZIP com as mesmas entradas'
        foreach ($entry in $left.Entries) {
            $other = $right.GetEntry($entry.FullName)
            Check ($null -ne $other) 'Nome da entrada ZIP preservado'
            $l = $entry.Open(); $r = $other.Open()
            try { Check ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($l)) -ceq [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($r))) 'Bytes descompactados idênticos' }
            finally { $l.Dispose(); $r.Dispose() }
        }
        Check ($download.Headers['X-Script-Download-Source'][0] -in @('SCRIPT_VERSOES','SCRIPTLAUDO')) 'Header de origem legado preservado'
    } finally { $left.Dispose(); $right.Dispose() }
    $downloadVerified = $true; break
}
Check $downloadVerified 'Ao menos um pacote de script real validado na cópia'
Check ((Request "$apiBase/apiconteudos/v1/normalidades?referencia=invalida" GET $partnerHeaders).StatusCode -eq 400) 'Validação de tipo retorna 400'
$normalidades = ((Request "$apiBase/apiconteudos/v1/normalidades" GET $partnerHeaders).Content | ConvertFrom-Json).data
$item = $normalidades | Where-Object { $null -ne $_.valor_min -and $null -ne $_.valor_max } | Select-Object -First 1
Check ($null -ne $item) 'Normalidade disponível para round-trip na cópia'
$operation = $spec.paths.'/apiconteudos/v1/normalidades/{codnormalidade}'.put.operationId
$parameters = @{path=@{codnormalidade=[string]$item.codnormalidade}}
$body = @{valor_min=$item.valor_min;valor_max=$item.valor_max;sexo=$item.sexo;idade_min=$item.idade_min;idade_max=$item.idade_max} | ConvertTo-Json -Compress
$blocked = Execute 'atual' $operation $parameters $body $true $partnerHeaders
Check ($blocked.statusCode -eq 403) 'Proxy bloqueia escrita em ambiente atual mesmo confirmada'
$blocked = Execute 'homologacao' $operation $parameters $body $false $partnerHeaders
Check ($blocked.statusCode -eq 400 -and $blocked.error.code -eq 'DOCS_WRITE_CONFIRMATION_REQUIRED') 'Proxy exige confirmação de escrita na cópia'
if ($AllowCopyWrites) {
    $result = Execute 'homologacao' $operation $parameters $body $true $partnerHeaders
    Check ($result.status -eq 200) 'PUT confirmado na cópia preservando os valores originais'
    $username = 'testeportal' + [Guid]::NewGuid().ToString('N').Substring(0,8)
    $password = New-HomologacaoSecret
    $created = Request "$apiBase/api/web/users" POST $webHeaders @{nome='Teste portal comum';identificacao=$username;senha=$password;confirmarSenha=$password;perfil='usuario';status=-1}
    Check ($created.StatusCode -in @(200,201)) 'Usuário comum criado somente na cópia'
    $common = (Request "$apiBase/api/web/auth/login" POST @{} @{usuario=$username;senha=$password}).Content | ConvertFrom-Json
    $commonHeaders = @{Authorization="Bearer $($common.token)"; 'X-Docs-Web-Authorization'="Bearer $($common.token)"}
    Check ((Request "$apiBase/swagger/web/swagger.json" GET $commonHeaders).StatusCode -eq 403) 'JSON administrativo retorna 403 para usuário comum'
    Check ((Request "$PortalBase/api/documentacao/homologacao/web/openapi" GET $commonHeaders).StatusCode -eq 403) 'Proxy retorna 403 para usuário comum'
}
Write-Host 'Smoke concluído. Nenhuma credencial exibida; nenhuma conexão com banco original.'
