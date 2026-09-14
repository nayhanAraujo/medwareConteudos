#requires -Version 7.2
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$SourceEnvironment = 'Development',
    [string]$ReferenciasGbakPath,
    [string]$AssistenteGbakPath,
    [ValidateRange(1024,65535)][int]$Port = 5081
)
. (Join-Path $PSScriptRoot 'Homologacao.Common.ps1')
if (-not $IsWindows) { throw 'Esta preparação suporta Windows local.' }
$workspace = Get-HomologacaoWorkspace
$source = Read-HomologacaoSource $workspace $SourceEnvironment
$referenceGbak = Find-HomologacaoGbak $source.Firebird.Port $ReferenciasGbakPath
$assistantGbak = Find-HomologacaoGbak $source.AssistantFirebird.Port $AssistenteGbakPath
$sandboxParent = Assert-LocalPath (Join-Path $workspace '.homologacao') -Within $workspace
$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$sandbox = Assert-LocalPath (Join-Path $sandboxParent $runId) -Within $sandboxParent
$localConfigPath = Assert-LocalPath (Join-Path $workspace 'backend/MdwConteudos.Api/appsettings.Homologacao.local.json') -Within $workspace
# Falhar antes de gravar qualquer segredo se as regras de exclusão não estiverem prontas.
& git -C $workspace check-ignore --quiet --no-index -- '.homologacao/probe/manifest.json'
if ($LASTEXITCODE -ne 0) { throw 'Inclua .homologacao/ no .gitignore antes da preparação.' }
& git -C $workspace check-ignore --quiet --no-index -- 'backend/MdwConteudos.Api/appsettings.Homologacao.local.json'
if ($LASTEXITCODE -ne 0) { throw 'Configuração local precisa estar ignorada pelo Git.' }
Write-Host "REFERENCIAS: servidor local porta $($source.Firebird.Port); gbak $referenceGbak"
Write-Host "ASSISTENTE: servidor local porta $($source.AssistantFirebird.Port); gbak $assistantGbak"
Write-Host "Destino novo: $sandbox"
if (-not $PSCmdlet.ShouldProcess($sandbox, 'Backup/restore de dois bancos e cópia isolada de arquivos; sem iniciar API')) { return }
if (Test-Path -LiteralPath $sandbox) { throw 'Destino já existe; execute novamente para gerar outro timestamp.' }
$null = New-Item -ItemType Directory -Path $sandbox
Set-HomologacaoPrivateAcl $sandbox -Directory
foreach ($relative in @('databases','backups','files','files/static','files/static/uploads','files/uploads')) {
    $destination = Assert-LocalPath (Join-Path $sandbox $relative) -Within $sandbox
    $null = [IO.Directory]::CreateDirectory($destination)
}
$startedAt = [DateTime]::UtcNow.ToString('o')
try {
    $backupMetadata = @()
    foreach ($item in @(@{Name='REFERENCIAS';Options=$source.Firebird;Gbak=$referenceGbak},
                         @{Name='ASSISTENTE';Options=$source.AssistantFirebird;Gbak=$assistantGbak})) {
        $db = $item.Options
        $backup = Assert-LocalPath (Join-Path $sandbox "backups/$($item.Name).fbk") -Within $sandbox
        $restored = Assert-LocalPath (Join-Path $sandbox "databases/$($item.Name).FDB") -Within $sandbox
        if ((Test-Path -LiteralPath $backup) -or (Test-Path -LiteralPath $restored) -or $restored -eq $db.Database) {
            throw 'Backup/restore exige destinos novos, distintos dos originais.'
        }
        $sourceDsn = "$($db.Host)/$($db.Port):$($db.Database)"
        $destinationDsn = "$($db.Host)/$($db.Port):$restored"
        $backupStart = [DateTime]::UtcNow.ToString('o')
        Write-Host "Backup consistente $($item.Name), sem coleta de lixo na origem..."
        # -g inibe GC; -convert materializa tabelas EXTERNAL no backup para não referenciar arquivos originais.
        Invoke-HomologacaoGbak $item.Gbak @('-backup_database','-garbage_collect','-convert',$sourceDsn,$backup) $db $sandbox "backup $($item.Name)"
        Write-Host "Restore exclusivo $($item.Name)..."
        Invoke-HomologacaoGbak $item.Gbak @('-create_database',$backup,$destinationDsn) $db $sandbox "restore $($item.Name)"
        $null = Assert-LocalPath $restored -Within $sandbox -MustExist
        if ((Get-Item -LiteralPath $restored).Length -eq 0) { throw 'Restore retornou banco vazio.' }
        $backupMetadata += @{name=$item.Name; startedAtUtc=$backupStart; completedAtUtc=[DateTime]::UtcNow.ToString('o');
            gbak=$item.Gbak; port=$db.Port; backupSha256=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash;
            restoredBytes=(Get-Item -LiteralPath $restored).Length}
    }
    Write-Host 'Copiando static e uploads para arquivos independentes...'
    $fileCounts = @{}
    foreach ($tree in @('static','uploads')) {
        $originalTree = Join-Path $workspace $tree
        $before = @(Get-HomologacaoTreeStamp $originalTree)
        $fileCounts[$tree] = Copy-HomologacaoTree $originalTree (Join-Path $sandbox "files/$tree") $sandbox
        $after = @(Get-HomologacaoTreeStamp $originalTree)
        if (($before -join "`0") -cne ($after -join "`0")) { throw 'Arquivos de origem mudaram durante a cópia. Repetir com uploads pausados.' }
    }
    $instanceId = [Guid]::NewGuid().ToString('N')
    $references = Join-Path $sandbox 'databases/REFERENCIAS.FDB'
    $assistant = Join-Path $sandbox 'databases/ASSISTENTE.FDB'
    $config = @{
        Urls="http://127.0.0.1:$Port"; AllowedHosts='localhost;127.0.0.1';
        Homologacao=@{Enabled=$true;WorkspaceRoot=$workspace;Root=$sandbox;OriginalReferencias=$source.Firebird.Database;OriginalAssistente=$source.AssistantFirebird.Database};
        MigrationPaths=@{Root=(Join-Path $sandbox 'files')}; LegacyPaths=@{RepoRoot=''};
        Documentation=@{AllowWrites=$true;InstanceId=$instanceId};
        Firebird=@{Host=$source.Firebird.Host;Port=$source.Firebird.Port;Database=$references;User=$source.Firebird.User;Password=$source.Firebird.Password;Charset=$source.Firebird.Charset};
        AssistantFirebird=@{Host=$source.AssistantFirebird.Host;Port=$source.AssistantFirebird.Port;Database=$assistant;User=$source.AssistantFirebird.User;Password=$source.AssistantFirebird.Password;Charset='ISO8859_1'};
        PublicacaoAssistente=@{Enabled=$false;SourceKey="homol-$instanceId"};
        WebAuth=@{JwtSecret=(New-HomologacaoSecret);Issuer='MdwConteudos.Homologacao';Audience='MdwConteudos.Homologacao.Web';
            DevUserEnabled=$true;DevUsername='homologacao';DevPassword=(New-HomologacaoSecret);DevName='Homologação local';DevRole='admin'};
        ApiPartner=@{JwtSecret=(New-HomologacaoSecret);JwtPassword=(New-HomologacaoSecret)};
        Conversion=@{Provider='Mock'};Voice=@{Provider='Mock';TranscriptionProvider='Browser';OpenAiApiKey=''};
        Cursor=@{ApiKey='';RepoRoot='';BridgeScriptPath=''};AzureOpenAI=@{ApiKey='';Endpoint=''}
    }
    $manifest = @{schemaVersion=1;prepared=$true;instanceId=$instanceId;root=$sandbox;referencias=$references;assistente=$assistant;
        originalReferencias=$source.Firebird.Database;originalAssistente=$source.AssistantFirebird.Database;
        startedAtUtc=$startedAt;completedAtUtc=[DateTime]::UtcNow.ToString('o');backups=$backupMetadata;copiedFiles=$fileCounts;
        consistency='Backup transacional individual por banco; sem atomicidade entre bancos e uploads.'}
    Write-HomologacaoJson (Join-Path $sandbox 'manifest.json') $manifest $sandbox
    $snapshotConfig = Join-Path $sandbox 'appsettings.Homologacao.local.json'
    Write-HomologacaoJson $snapshotConfig $config $sandbox
    # Promove somente depois dos dois restores e cópias. Execuções anteriores permanecem recuperáveis.
    $staged = Assert-LocalPath (Join-Path $workspace "backend/MdwConteudos.Api/appsettings.Homologacao-$runId.local.json") -Within $workspace
    Write-HomologacaoJson $staged $config $workspace
    $null = Assert-LocalPath $localConfigPath -Within $workspace
    [IO.File]::Move($staged, $localConfigPath, $true)
    $null = Read-HomologacaoPrepared $workspace
    Write-Host "Preparação concluída: $sandbox"
    Write-Host "InstanceId (não secreto): $instanceId"
    Write-Host 'Nenhum serviço iniciado. Program/guard/middleware devem estar integrados e compilados antes de Start-Homologacao.ps1.'
} catch {
    # Sem rollback destrutivo: mantém apenas artefatos da tentativa para inspeção local.
    Write-Warning "Preparação incompleta preservada em $sandbox. Não usar esta execução. A configuração anterior só é promovida após sucesso."
    throw
}
