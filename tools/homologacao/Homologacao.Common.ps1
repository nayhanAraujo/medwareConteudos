#requires -Version 7.2
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-LocalPath {
    param([Parameter(Mandatory)][string]$Path, [string]$Within, [switch]$MustExist)
    if (-not [IO.Path]::IsPathFullyQualified($Path) -or $Path.StartsWith('\\') -or $Path.StartsWith('//')) {
        throw 'Caminho deve ser absoluto, local e sem UNC/device path.'
    }
    $full = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Path))
    if ($full -eq [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetPathRoot($full)) -or
        $full.Substring(2).Contains(':') -or $full.Contains('~')) { throw 'Raiz de volume, ADS ou nome alternativo não permitido.' }
    if ($Within) {
        $parent = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Within))
        if (-not $full.StartsWith($parent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Destino não está estritamente dentro da raiz autorizada.'
        }
    }
    for ($current = $full; $current; $current = [IO.Path]::GetDirectoryName($current)) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $item.LinkType -eq 'HardLink') {
                throw 'Symlink, junction e hardlink não são permitidos.'
            }
        }
    }
    if ($MustExist -and -not (Test-Path -LiteralPath $full)) { throw 'Artefato obrigatório ausente.' }
    return $full
}

function Get-HomologacaoWorkspace {
    $root = Assert-LocalPath ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) -MustExist
    if (-not (Test-Path -LiteralPath (Join-Path $root 'backend/MdwConteudos.Api/Program.cs')) -or
        -not (Test-Path -LiteralPath (Join-Path $root 'frontend/nuxt-app'))) { throw 'Workspace não reconhecido.' }
    return $root
}

function Merge-HomologacaoMap {
    param([System.Collections.IDictionary]$Target, [System.Collections.IDictionary]$Source)
    foreach ($key in $Source.Keys) {
        if ($Source[$key] -is [System.Collections.IDictionary]) {
            if ($Target[$key] -isnot [System.Collections.IDictionary]) { $Target[$key] = @{} }
            Merge-HomologacaoMap $Target[$key] $Source[$key]
        } else { $Target[$key] = $Source[$key] }
    }
}

function Read-HomologacaoSource {
    param([string]$Workspace, [string]$SourceEnvironment = 'Development')
    if ($SourceEnvironment -notmatch '^[A-Za-z][A-Za-z0-9_-]*$' -or $SourceEnvironment -eq 'Homologacao') {
        throw 'SourceEnvironment deve identificar o perfil de origem, nunca Homologacao.'
    }
    $api = Join-Path $Workspace 'backend/MdwConteudos.Api'
    $config = @{}
    foreach ($name in @('appsettings.json', "appsettings.$SourceEnvironment.json", "appsettings.$SourceEnvironment.local.json")) {
        $path = Assert-LocalPath (Join-Path $api $name) -Within $Workspace
        if (Test-Path -LiteralPath $path) {
            try { Merge-HomologacaoMap $config (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable) }
            catch { throw 'JSON de origem inválido; conteúdo omitido para proteger credenciais.' }
        }
    }
    $environmentValues = @{}
    foreach ($entry in [Environment]::GetEnvironmentVariables().GetEnumerator()) { $environmentValues[$entry.Key] = $entry.Value }
    # Mesma busca ancestral do loader normal. Nunca escreve no processo nem no .env.
    $directory = $api
    for ($depth = 0; $depth -lt 6 -and $directory; $depth++) {
        $envPath = Join-Path $directory '.env'
        if (Test-Path -LiteralPath $envPath) {
            $null = Assert-LocalPath $envPath -MustExist
            foreach ($line in [IO.File]::ReadAllLines($envPath)) {
                $trimmed = $line.Trim()
                if (-not $trimmed -or $trimmed.StartsWith('#')) { continue }
                $equals = $trimmed.IndexOf('=')
                if ($equals -gt 0) { $environmentValues[$trimmed.Substring(0,$equals).Trim()] = $trimmed.Substring($equals+1).Trim().Trim('"') }
            }
            break
        }
        $directory = [IO.Path]::GetDirectoryName($directory)
    }
    foreach ($section in @('Firebird', 'AssistantFirebird')) {
        if (-not $config.Contains($section)) { throw 'Seção Firebird obrigatória ausente na origem.' }
        foreach ($property in @('Host','Port','Database','User','Password','Charset')) {
            $key = "${section}__$property"
            if ($environmentValues.ContainsKey($key)) { $config[$section][$property] = $environmentValues[$key] }
        }
    }
    $mappings = @{
        FIREBIRD_HOST='Firebird:Host'; FIREBIRD_PORT='Firebird:Port'; FIREBIRD_DB='Firebird:Database';
        FIREBIRD_PASSWORD='Firebird:Password'; ASSISTENTE_FIREBIRD_HOST='AssistantFirebird:Host';
        ASSISTENTE_FIREBIRD_PORT='AssistantFirebird:Port'; ASSISTENTE_FIREBIRD_DATABASE='AssistantFirebird:Database';
        ASSISTENTE_FIREBIRD_USER='AssistantFirebird:User'; ASSISTENTE_FIREBIRD_PASSWORD='AssistantFirebird:Password';
        ASSISTENTE_FIREBIRD_CHARSET='AssistantFirebird:Charset'
    }
    if (-not [string]::IsNullOrWhiteSpace($environmentValues['LOCAL_DB_PASSWORD'])) { $config.Firebird.Password = $environmentValues['LOCAL_DB_PASSWORD'] }
    foreach ($key in $mappings.Keys) {
        if (-not [string]::IsNullOrWhiteSpace($environmentValues[$key])) {
            $parts = $mappings[$key].Split(':'); $config[$parts[0]][$parts[1]] = $environmentValues[$key]
        }
    }
    if ($environmentValues['FIREBIRD_USER'] -and $environmentValues['FIREBIRD_USER'] -ne $config.Firebird.User) {
        throw 'FIREBIRD_USER faz o loader legado divergir da fábrica padrão. Alinhe a configuração antes da preparação.'
    }
    foreach ($section in @('Firebird','AssistantFirebird')) {
        $db = $config[$section]
        if ($db.Host -notin @('127.0.0.1','localhost')) { throw 'Somente servidores Firebird locais são aceitos.' }
        $port = 0
        if (-not [int]::TryParse([string]$db.Port, [ref]$port) -or $port -lt 1 -or $port -gt 65535) { throw 'Porta Firebird inválida.' }
        $db.Port = $port
        if ([string]::IsNullOrWhiteSpace($db.Database) -or [string]::IsNullOrWhiteSpace($db.User) -or
            [string]::IsNullOrWhiteSpace($db.Password)) { throw 'Origem requer caminho, usuário e senha Firebird explícitos.' }
        $path = if ([IO.Path]::IsPathFullyQualified($db.Database)) { $db.Database } else { Join-Path $Workspace $db.Database }
        $db.Database = Assert-LocalPath $path -MustExist
        if ($db.Database.StartsWith((Join-Path $Workspace '.homologacao'), [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Não é permitido preparar uma homologação a partir de outra homologação.'
        }
        if ([IO.Path]::GetExtension($db.Database) -ine '.FDB') { throw 'Origem deve ser um arquivo FDB explícito, não alias.' }
    }
    if ($config.AssistantFirebird.Charset -ine 'ISO8859_1') { throw 'ASSISTENTE requer ISO8859_1.' }
    if ($config.Firebird.Database -eq $config.AssistantFirebird.Database) { throw 'REFERENCIAS e ASSISTENTE não podem ser o mesmo arquivo.' }
    # Não devolve segredos dos outros módulos nem os publica em stdout.
    return @{ Firebird=$config.Firebird; AssistantFirebird=$config.AssistantFirebird }
}

function Find-HomologacaoGbak {
    param([int]$Port, [string]$ExplicitPath)
    if ($ExplicitPath) {
        $path = Assert-LocalPath $ExplicitPath -MustExist
        if ([IO.Path]::GetFileName($path) -ine 'gbak.exe') { throw 'Executável deve ser gbak.exe.' }
        return $path
    }
    $owners = @(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)
    $services = @(Get-CimInstance Win32_Service -Filter "Name LIKE '%Firebird%'" | Where-Object { $_.ProcessId -in $owners -and $_.ProcessId -ne 0 })
    if ($services.Count -ne 1 -or $services[0].PathName -notmatch '^"([^"]+\.exe)"') {
        throw 'Não foi possível identificar o gbak do servidor desta porta. Informe o caminho explícito do gbak correspondente.'
    }
    $path = Join-Path ([IO.Path]::GetDirectoryName($matches[1])) 'gbak.exe'
    # ACLs geradas permitem usuário atual, SYSTEM e Administradores. Não amplia ACL de origem.
    if ($services[0].StartName -notin @('LocalSystem','NT AUTHORITY\SYSTEM')) {
        throw 'Servidor usa conta personalizada. Coordenar ACL local antes de preparar; não serão ampliadas permissões automaticamente.'
    }
    return Assert-LocalPath $path -MustExist
}

function Set-HomologacaoPrivateAcl {
    param([string]$Path, [switch]$Directory)
    $null = Assert-LocalPath $Path -MustExist
    $acl = if ($Directory) { [Security.AccessControl.DirectorySecurity]::new() } else { [Security.AccessControl.FileSecurity]::new() }
    $acl.SetAccessRuleProtection($true, $false)
    $identities = @([Security.Principal.WindowsIdentity]::GetCurrent().User,
        [Security.Principal.SecurityIdentifier]::new('S-1-5-18'), [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
    foreach ($identity in $identities) {
        $inheritance = if ($Directory) { [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit' } else { [Security.AccessControl.InheritanceFlags]::None }
        $rule = [Security.AccessControl.FileSystemAccessRule]::new($identity, 'FullControl', $inheritance, 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $Path -AclObject $acl
}

function New-HomologacaoProcessInfo {
    param([string]$Executable, [string[]]$Arguments, [string]$WorkingDirectory)
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = Assert-LocalPath $Executable -MustExist
    $info.WorkingDirectory = Assert-LocalPath $WorkingDirectory -MustExist
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.Environment.Clear()
    foreach ($name in @('SystemRoot','WINDIR','SystemDrive','COMSPEC','TEMP','TMP','USERPROFILE','LOCALAPPDATA','APPDATA',
            'ProgramFiles','ProgramFiles(x86)','ProgramW6432','ProgramData','PATH','PATHEXT','DOTNET_ROOT')) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if ($null -ne $value) { $info.Environment[$name] = $value }
    }
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    return $info
}

function Invoke-HomologacaoGbak {
    param([string]$Executable, [string[]]$Arguments, [System.Collections.IDictionary]$Database, [string]$WorkingDirectory, [string]$Stage)
    $info = New-HomologacaoProcessInfo $Executable $Arguments $WorkingDirectory
    # Nunca usar -password nem escrever senha em arquivos/logs/linha de comando.
    $info.Environment['ISC_USER'] = $Database.User
    $info.Environment['ISC_PASSWORD'] = $Database.Password
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
    try {
        $null = $process.Start()
        # Drenar ambos os streams evita deadlock; não registrar texto potencialmente sensível.
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        while (-not $process.WaitForExit(1000)) { }
        $null = $stdout.GetAwaiter().GetResult(); $null = $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "gbak falhou na etapa $Stage (exit $($process.ExitCode)); saída bruta omitida." }
    } finally {
        if ($process.Id -and -not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose(); $info.Environment.Remove('ISC_PASSWORD') | Out-Null
    }
}

function Copy-HomologacaoTree {
    param([string]$Source, [string]$Destination, [string]$Sandbox)
    $destinationPath = Assert-LocalPath $Destination -Within $Sandbox
    $null = [IO.Directory]::CreateDirectory($destinationPath)
    if (-not (Test-Path -LiteralPath $Source)) { return 0 }
    $sourcePath = Assert-LocalPath $Source -MustExist
    $count = 0
    foreach ($item in Get-ChildItem -LiteralPath $sourcePath -Force) {
        $sourceItem = Assert-LocalPath $item.FullName -Within $sourcePath -MustExist
        $target = Assert-LocalPath (Join-Path $destinationPath $item.Name) -Within $Sandbox
        if ($item.PSIsContainer) { $count += Copy-HomologacaoTree $sourceItem $target $Sandbox }
        else {
            # A cópia é um arquivo independente (sem links); nenhum arquivo de destino é substituído.
            $inputStream = [IO.File]::Open($sourceItem, 'Open', 'Read', 'Read')
            try {
                $outputStream = [IO.File]::Open($target, 'CreateNew', 'Write', 'None')
                try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
            } finally { $inputStream.Dispose() }
            $count++
        }
    }
    return $count
}

function Get-HomologacaoTreeStamp {
    param([string]$Root)
    if (-not (Test-Path -LiteralPath $Root)) { return @() }
    $null = Assert-LocalPath $Root -MustExist
    $entries = [Collections.Generic.List[string]]::new()
    foreach ($item in Get-ChildItem -LiteralPath $Root -Force) {
        $null = Assert-LocalPath $item.FullName -Within $Root -MustExist
        $entries.Add("$($item.FullName)|$($item.LastWriteTimeUtc.Ticks)")
        if ($item.PSIsContainer) { foreach ($entry in @(Get-HomologacaoTreeStamp $item.FullName)) { $entries.Add($entry) } }
        else { $entries.Add("$($item.FullName)|$($item.Length)") }
    }
    return @($entries | Sort-Object)
}

function New-HomologacaoSecret {
    return 'homol-' + [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
}

function Write-HomologacaoJson {
    param([string]$Path, [object]$Value, [string]$Within)
    $validated = Assert-LocalPath $Path -Within $Within
    if (Test-Path -LiteralPath $validated) { throw 'Artefato da execução já existe; substituição recusada.' }
    [IO.File]::WriteAllText($validated, ($Value | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    Set-HomologacaoPrivateAcl $validated
}

function Read-HomologacaoPrepared {
    param([string]$Workspace)
    $path = Assert-LocalPath (Join-Path $Workspace 'backend/MdwConteudos.Api/appsettings.Homologacao.local.json') -Within $Workspace -MustExist
    try { $config = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable }
    catch { throw 'Configuração local inválida; conteúdo omitido.' }
    $root = Assert-LocalPath $config.Homologacao.Root -Within (Join-Path $Workspace '.homologacao') -MustExist
    if ([IO.Path]::GetDirectoryName($root) -ne (Join-Path $Workspace '.homologacao') -or
        [IO.Path]::GetFileName($root) -notmatch '^\d{8}-\d{6}-\d{3}-[a-f0-9]{8}$' -or
        $config.Homologacao.WorkspaceRoot -ne $Workspace) { throw 'Raiz da preparação inválida.' }
    $manifestPath = Assert-LocalPath (Join-Path $root 'manifest.json') -Within $root -MustExist
    try { $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable }
    catch { throw 'Manifesto inválido.' }
    if (-not $manifest.prepared -or $manifest.schemaVersion -ne 1 -or $manifest.root -ne $root -or
        $manifest.instanceId -ne $config.Documentation.InstanceId -or
        $config.Documentation.InstanceId -notmatch '^[a-f0-9]{32}$') { throw 'Preparação não concluída ou identidade divergente.' }
    foreach ($pair in @(@('Firebird','referencias','REFERENCIAS.FDB'),@('AssistantFirebird','assistente','ASSISTENTE.FDB'))) {
        $expected = Join-Path $root "databases/$($pair[2])"
        if ($config[$pair[0]].Database -ne $expected -or $manifest[$pair[1]] -ne $expected) { throw 'Banco configurado não corresponde à cópia esperada.' }
        $null = Assert-LocalPath $expected -Within $root -MustExist
        if ((Get-Item -LiteralPath $expected).Length -eq 0) { throw 'Cópia Firebird vazia.' }
    }
    if ($config.PublicacaoAssistente.Enabled -ne $false -or $config.Homologacao.Enabled -ne $true -or
        $config.MigrationPaths.Root -ne (Join-Path $root 'files')) { throw 'Flags ou raiz de arquivos inválidos.' }
    foreach ($directory in @('files/static/uploads','files/uploads')) { $null = Assert-LocalPath (Join-Path $root $directory) -Within $root -MustExist }
    return @{ Config=$config; Manifest=$manifest; Path=$path }
}
