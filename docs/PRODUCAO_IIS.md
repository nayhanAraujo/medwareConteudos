# Produção IIS — nova VM Azure Windows

Procedimento vigente: IIS + ARR + URL Rewrite, API ASP.NET Core 10 pelo Hosting Bundle, Nuxt SSR Node **22** via **NSSM** e Firebird no mesmo host. Nenhum script instala remotamente ou baixa NSSM. Para operações IIS no servidor, use Windows PowerShell **5.1** elevado com WebAdministration; para o empacotamento, use PowerShell **7**. Verifique certificado com chave privada em LocalMachine/My, SAN do domínio e renovação. Use o binário NSSM já aprovado no servidor.

## Diretórios e isolamento

Usar `D:\Mdw\releases\VERSAO` para código, `D:\Mdw\data` para dados persistentes, `D:\Mdw\service` para serviço/logs e `D:\Mdw\secrets` para configuração restrita. Nenhum deles pode estar dentro de outro. Backups devem ficar em volume separado, com cópia externa criptografada. Preservar identidade e ACL do serviço Firebird nos bancos. API recebe Modify nos dados; Nuxt não precisa de acesso aos dados nem segredos API.

API IIS vinculada exclusivamente a 127.0.0.1:5080; Node a 127.0.0.1:3000. Abrir somente 80/443 externamente, fechar portas internas e Firebird em NSG/firewall. Confirmar versões Firebird compatíveis e portas 3052/3050 do template. Node deve ser legível por LocalService e pool API, também disponível no PATH de máquina para o bridge.

## Build

```powershell
./tools/production/Build-Package.ps1 -OutputDirectory D:\Artifacts\mdw-20260923-01
```

Executar em agente Windows limpo, SDK .NET 10 e Node **22.20.0** (mesma versão homologada no servidor), sem variáveis de aplicação/segredos herdadas. Por padrão o build usa **HEAD commitado**; `-WorkingTree` permite validar alterações pendentes, copiando apenas arquivos conhecidos do Git e não ignorados. Cada execução usa staging novo, testes .NET, dotnet publish, npm ci, testes do portal e Nuxt node-server SSR. O bridge inclui dependências de produção e os três manuais. Configuração base e Production permanecem; .env, arquivos locais, bancos, backups, uploads, homologação e caches ficam excluídos. Assets estáticos de aplicação vão em `static-assets`, separados dos anexos. Manifesto SHA256 e `build-info.json` registram integridade e versões. Proteger transporte e manifesto; o staging temporário fica retido para diagnóstico.

## Configurar e validar

O exemplo de instalação abaixo é para uma instância nova (objetos `MdwApi`, `MdwPublic` e `MdwNuxt`). A produção existente usa `MdwConteudoApi`, `MdwConteudo` e `MdwConteudoNuxt`; para atualizá-la, use o pipeline, sem executar o instalador novamente.

Copiar `tools/production/templates/appsettings.Production.local.json.example` para diretório secreto e preencher todos os REQUIRED manualmente. Usar senhas dedicadas e JWTs fortes distintos. Não versionar segredos nem incluí-los em pipeline, argumentos ou logs. Fonte JSON deve permitir somente Administradores/SYSTEM. O instalador protege a cópia publicada permitindo leitura ao pool API.

Contrato: `DataPaths:Root` absoluto (não `Production:DataRoot`), `AllowedHosts` no formato `dominio;127.0.0.1;localhost`, `Security:CorsOrigins` array HTTPS explícito, `Security:TrustedProxies` exatamente `["127.0.0.1","::1"]`. ARR preserva Host; loopback é autorizado para SSR/health internos. Firebird localhost e caminhos absolutos. Backend lê `api/appsettings.Production.local.json`; guards de produção e opções das integrações são responsabilidade do backend. Não publicar appsettings de desenvolvimento. Confirmar integrações obrigatórias e credenciais com seus responsáveis. O uso de `masterkey` permanece bloqueado por padrão; quando a rotação não for possível, `Security:AllowDefaultFirebirdPassword=true` registra uma exceção explícita e gera aviso, exigindo que as portas 3050/3052 permaneçam restritas pelo firewall. O par legado exato da API Python também permanece bloqueado por padrão; `Security:AllowLegacyPartnerCredentials=true` habilita compatibilidade temporária somente com esse par, gera aviso e deve ser removido após uma rotação coordenada com os consumidores.

```powershell
$parameters = @{
  PackageDirectory = 'D:\Artifacts\mdw-20260923-01'
  Domain = 'conteudos.exemplo.com.br'
  CertificateThumbprint = 'SUBSTITUIR_POR_THUMBPRINT_40_HEX'
  ReleaseDirectory = 'D:\Mdw\releases\20260923-01'
  DataDirectory = 'D:\Mdw\data'
  ServiceDirectory = 'D:\Mdw\service'
  SecretsFile = 'D:\Mdw\secrets\appsettings.Production.local.json'
  NssmPath = 'C:\caminho\do\nssm.exe'
  NodePath = 'C:\Program Files\nodejs\node.exe'
}
./tools/production/Install-Iis.ps1 @parameters
./tools/production/Install-Iis.ps1 @parameters -Apply -WhatIf
./tools/production/Install-Iis.ps1 @parameters -Apply
./tools/production/Test-Health.ps1 -PublicBase https://conteudos.exemplo.com.br
```

Sem Apply: somente validação e consultas de versão. Apply usa ShouldProcess. Instalação inicial rejeita objetos existentes e diretórios release/service existentes. Não é transacional: em falha inspecionar instalação parcial, não repetir cegamente nem apagar dados. Fazer backup IIS com `appcmd add backup` e registrar objetos criados. ARR habilitado com timeout **360 segundos globalmente**: revisar impacto sobre outros sites/bindings antes de aplicar.

API usa ApplicationPoolIdentity; Nuxt usa LocalService. Release é leitura/execução. JSON publicado retira herança, autoriza apenas SYSTEM, Administradores e leitura pool API. O executável NSSM precisa estar em caminho protegido e legível por LocalService; a configuração do serviço fica no Registro do Windows e os logs são graváveis somente pelo serviço. Dados existentes recebem Modify para API sem substituir recursivamente ACLs: operador deve remover acessos amplos previamente e verificar herança, preservando Firebird.

## Proxy, limites e smoke

Após HTTPS, ordem com stopProcessing:

1. `/apiconteudos/docs`, `/docs`, `/api/documentacao` → Node.
2. `/api-dotnet` → API removendo prefixo.
3. `/api`, `/apiconteudos/v1`, `/swagger`, `/health` → API preservando caminho.
4. `/static` → API com arquivos persistentes de runtime.
5. Restante → Nuxt SSR.

Queries preservadas. Swagger respeita política backend; não é ativado automaticamente. Ambos sites limitam requests a **115343360 bytes (110 MiB)**; alinhar limites multipart/backend. Nunca copiar uploads para `.output/public`. Nuxt recebe ambiente production e bases API internas/públicas via XML. Sandbox aponta para destino desabilitado, nunca produção: confirmar interface sem sandbox utilizável.

Smoke exige HTTP 200, certificado válido, sem aceitar redirects: live/ready local e público, prefixo API e SSR local/portal. `/health/ready` deve verificar dependências reais conforme backend. Antes do DNS, usar hosts autorizado para testar domínio/certificado. Complementar com login, CORS, handlers documentação, parceiros, arquivo conhecido `/static`, upload/download, bridge/manuais e limites/timeouts. Testes de escrita somente em cópias isoladas. Verificar cookies, URLs HTTPS e bloqueio externo das portas internas. Smoke não substitui aceite funcional.

## Preparação do catálogo e aceite de segurança

O deploy executa `--migrate-database` usando a DLL e o JSON restrito do novo release, depois de parar os componentes MDW e antes de trocar os caminhos IIS/NSSM. O processo filho recebe ambiente Production e diretório de conteúdo explícitos; variáveis de configuração herdadas do agente são removidas somente desse processo. O limite padrão é de 300 segundos (`-MigrationTimeoutSeconds` permite ajustar). Os logs identificam cada etapa e o deploy só inicia a API após a conclusão com código zero.

Em instalação manual, após backup consistente dos bancos de destino, execute explicitamente no diretório `release/api`, com a configuração local preenchida e o pool API parado:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
dotnet MdwConteudos.Api.dll --migrate-database
```

Esse comando prepara permissões, ícones de pacotes, ícones dos módulos de relatórios e avaliações, valida o catálogo e encerra sem iniciar HTTP ou serviços de publicação. As etapas são repetíveis; ícones existentes e concessões/exceções de acesso são preservados. O instalador inicial não executa o comando. Conceder novas permissões explicitamente aos perfis autorizados.

Toda futura migração necessária para iniciar uma versão deve ser registrada em `Infrastructure/DatabaseMigrations.cs`, na ordem das dependências, e testada em banco temporário, inclusive na segunda execução. O deploy usa sempre esse registro da DLL nova; não descobre nem executa arquivos SQL arbitrários. Registrar somente migrações aditivas compatíveis com o release anterior. Alterações destrutivas precisam de procedimento específico, backup e plano de compatibilidade. Em instalações novas, revisar também os scripts de base fornecidos em `sql` conforme o schema restaurado.

Em falha ou timeout de migração, o deploy restaura os componentes e caminhos anteriores e informa erro. O rollback é de código: etapas de banco já confirmadas permanecem aplicadas. Não existe transação única abrangendo todas as etapas; por isso cada migração precisa suportar repetição e rollback de código. Não restaurar bancos automaticamente. A preparação de backups continua sendo responsabilidade da operação antes da aprovação do Environment.

As cinco frentes corrigidas são recuperação anônima de senha, autenticação da API interna, autorização/posse de sessões Studio, segredos/configuração de produção e controles de abuso/exposição de erros. `/api/v1` agora exige JWT; comunicar a mudança aos integradores. Credenciais padrão anteriormente utilizadas devem ser rotacionadas, inclusive nos bancos; remover do arquivo atual não remove o histórico Git.

**Pendência adicional encontrada na auditoria:** `@cursor/sdk@1.0.23` → `@connectrpc/connect-node@1.7.0` → `undici@5.29.0` apresenta vulnerabilidades (uma alta, dois alertas moderados na árvore, `npm audit --omit=dev`). O SDK não oferece correção automática nesse lockfile. Não foi aplicado override para outro major sem validação. Não liberar Studio para uso em produção antes de corrigir/validar essa dependência ou aprovar formalmente a mitigação com o responsável de segurança. O pacote gerado não representa aprovação de entrada em produção.

Em 24/09/2026: 304 testes .NET aprovados, 16 testes de integração Firebird ignorados por ausência da configuração isolada; 24 testes frontend do portal aprovados e build Nuxt concluído. Smoke IIS/ARR, instalação NSSM, restauração e testes funcionais com bancos copiados precisam ocorrer na VM antes do corte. Nenhum banco original foi alterado durante essa validação.

## Procedimento de backup

Definir RPO/RTO e ensaiar restauração antes do corte. Pausar todos os escritores, serviços/jobs e integrações; registrar horário. Para cada banco usar `gbak` da versão Firebird compatível, modo backup **-b**, conexão localhost, conta autorizada e destino `.fbk` novo. Não copiar `.fdb` aberto. Não passar senha em linha de comando/histórico: usar mecanismo seguro da versão instalada, por exemplo arquivo restrito com `-fetch_password` após conferir `gbak -?`. Verificar saída e logs sem credenciais.

Na mesma janela sem gravações, fazer backup de todos os arquivos persistentes, uploads/static, JSON criptografado, configuração NSSM do serviço (`nssm dump MdwNuxt`) quando existente, certificado conforme política e configuração IIS. Registrar hashes, ACLs, versão Firebird, release e horário. Copiar para armazenamento externo protegido com retenção; verificar restauração, não apenas existência do backup.

Restaurar com gbak modo create **-c** para outro caminho/banco, nunca substituir original com -replace. Restaurar arquivos do mesmo ponto consistente; conceder acesso à identidade Firebird, validar integridade, contagens, charset e fluxos em ambiente isolado. Só apontar aplicação para banco restaurado na janela autorizada. Preservar original até aceite. Instalador não executa backups/restores nem altera bancos.

## Atualização e rollback

Script automatiza primeira instalação. Atualização controlada: preparar release nova, verificar hashes, reproduzir ACLs e copiar JSON restrito; manutenção, parar MdwNuxt/pool API; registrar physicalPaths e salvar `nssm dump MdwNuxt`. Atualizar `Application`, `AppDirectory` e os caminhos dependentes do serviço NSSM para a nova release, mantendo o executável NSSM estável. Reiniciar e executar smoke/aceite. Manter DataPaths:Root. Troca coordenada com tráfego suspenso, não atômica entre IIS e Node.

O pipeline versionado em `/azure-pipelines.yml` automatiza esse fluxo após push em `main`: o build ocorre em agente hospedado limpo, publica um artifact sem segredos e o deployment job usa o pool self-hosted `MedwareConteudo`. O Environment `MdwConteudo-Producao` deve ser criado manualmente no Azure DevOps com aprovação e bloqueio exclusivo. O deploy chama `Deploy-IisRelease.ps1`, cria uma pasta nova em `C:\MdwConteudo\releases`, copia o JSON a partir de `C:\MdwConteudo\secrets`, preserva `C:\MdwConteudo\conteudo`, não altera bindings/certificado/ARR e reverte os caminhos IIS/NSSM se o smoke falhar. O agente do servidor precisa executar elevado; limitar o uso desse pool e do Environment ao pipeline autorizado.

Configuração inicial no Azure DevOps:

1. Criar o pool privado `MedwareConteudo` e instalar nele um agente Windows como serviço na VM de produção. A conta do serviço precisa conseguir controlar somente os sites/pools `MdwConteudo` e `MdwConteudoApi`, o serviço `MdwConteudoNuxt` e os diretórios usados pelo deploy; a versão inicial do script exige token administrativo.
2. Criar o Environment `MdwConteudo-Producao`, autorizar somente este pipeline e adicionar **Approval** e **Exclusive lock** em *Approvals and checks*.
3. Criar um pipeline YAML no repositório `Conteudos_migracao`, apontando para `/azure-pipelines.yml`. Não cadastrar senhas Firebird, JWTs ou conteúdo do JSON em variáveis do pipeline.
4. Executar a primeira rodada manualmente, revisar o artifact e aprovar o Environment. Após o aceite, pushes em `main` iniciam build automaticamente; produção continua aguardando a aprovação configurada.
5. Confirmar no servidor que `C:\MdwConteudo\secrets\appsettings.Production.local.json` e os bancos em `C:\MdwConteudo\conteudo\bd` são legíveis pelas identidades corretas antes de aprovar o deploy.

O pipeline não observa arquivos do computador local: alterações precisam ser commitadas e enviadas para `main`. Pull requests executam somente a validação; não implantam. O script valida manifesto SHA256, JSON, bancos, Node/NSSM e objetos IIS antes de interromper os componentes. O corte executa smoke tests em API, Nuxt e domínio público; falhas restauram os caminhos e o estado anterior dos componentes.

Rollback de **código**: manutenção, parar serviço/pool, repor physicalPaths/XML/configuração compatível anteriores e testar. Migrações de banco já confirmadas permanecem aplicadas. Rollback de **banco** é decisão separada: autorização, perda de escritas desde backup explicitamente aceita e restauração coordenada banco+arquivos sem escritores. Não restaurar banco por falha de smoke. Se código anterior não suporta schema atual, manter manutenção e envolver DBA. Preservar releases antigas pela retenção.

### Rotação de credenciais da API

No servidor em produção, a fonte do pipeline é
`C:\MdwConteudo\secrets\appsettings.Production.local.json`. Durante o deploy,
o script copia esse arquivo para `api\appsettings.Production.local.json` da
nova release e restringe sua ACL. Build, commit ou cópia somente no PC não
atualizam a API em execução. Em rollback, a API volta para o JSON da release
anterior; uma chave revogada pode deixar de funcionar nessa release.

Para o Cursor, rotacione apenas `Cursor:ApiKey` se a intenção for manter modelo
e bridge. Use uma chave do Cursor com autorização para executar agentes e teste
uma conversão com dados fictícios, não apenas a autenticação. Isso é independente
de `RealtimeVoice:ApiKey`/`XAI_API_KEY`, usados pela voz em tempo real.
Não registre chaves em Git, artefatos, logs ou mensagens.

### Falha de inicialização do Nuxt

Não repita o deploy nem execute `iisreset` sem verificar o estado da release
ativa. Consulte `sc.exe qc MdwConteudoNuxt`, o `ObjectName` no Registro,
`Application`, `AppDirectory` e `AppParameters` em `Parameters`, e os
eventos System/Application no horário da falha. Compare com a última release
funcional e valide os caminhos dos executáveis. Uma vez iniciado, confirme
`http://127.0.0.1:3000/login` e o acesso público. Não exponha o ambiente do
serviço em um dump compartilhado.

O rollback restaura caminhos e estados anteriores, mas não desfaz mudanças
manuais no ambiente ou na identidade do NSSM. Se apenas o deploy falhou, uma
nova tentativa do job reutiliza o pacote original; o pipeline usa
`build-<BuildId>-tentativa<JobAttempt>` para não sobrescrever a release.
Para publicar um commit posterior, inicie uma nova execução do pipeline.

## Exceção do chat piloto da Curadoria no Teams

O Teams pode informar `meeting.id` no diálogo do chat **Desenvolvimento | Demandas**, já verificado pelo responsável como `ChatType=group`. A exceção não libera reuniões em geral: exige correspondência exata com `NUXT_CURADORIA_PILOT_CHAT_SHA256=sha256:<64 caracteres hexadecimais minúsculos>`, configurada **somente no ambiente do serviço Nuxt** (neste servidor, `MdwConteudoNuxt`). Não é uma chave do JSON da API .NET e não deve ser versionada ou colocada em configuração pública.

Na máquina e na mesma janela PowerShell que já possui a sessão Graph delegada autorizada, executar `& '.\tools\production\Get-CuradoriaPilotFingerprint.ps1'` a partir da raiz do repositório. No pacote, o helper está em `deployment/Get-CuradoriaPilotFingerprint.ps1`. Ele consulta metadados, exige um único chat `group` com o nome do piloto e devolve apenas a linha de ambiente, sem exibir o ID. Não instala módulos, não conecta nem solicita consentimento.

No servidor, com PowerShell elevado e sem deploy em andamento, atualize somente o valor `AppEnvironmentExtra` do serviço. Evite salvar pela interface do NSSM. No incidente de 13/11/2026, a conta estava gravada com nome localizado e o serviço retornava 1068; voltou a iniciar após restabelecer `NT AUTHORITY\LocalService`. Também foram encontrados caminhos truncados. Não há evidência de que o pin tenha causado a falha.

```powershell
$chaveNuxt = 'HKLM:\SYSTEM\CurrentControlSet\Services\MdwConteudoNuxt\Parameters'
$linhaPiloto = (Read-Host 'Cole a linha completa gerada pelo helper').Trim()
if ($linhaPiloto -cnotmatch '^NUXT_CURADORIA_PILOT_CHAT_SHA256=sha256:[a-f0-9]{64}$') {
    throw 'Formato invalido. Nenhuma alteracao realizada.'
}
if ((Get-Item -LiteralPath $chaveNuxt -ErrorAction Stop).GetValueKind('AppEnvironmentExtra') -ne [Microsoft.Win32.RegistryValueKind]::MultiString) {
    throw 'Tipo inesperado. Nenhuma alteracao realizada.'
}
$ambienteAntes = [string[]](Get-ItemPropertyValue -LiteralPath $chaveNuxt -Name AppEnvironmentExtra -ErrorAction Stop)
$ambienteDepois = [string[]]@(
    $ambienteAntes | Where-Object { $_ -notmatch '^NUXT_CURADORIA_PILOT_CHAT_SHA256=' }
    $linhaPiloto
)
Set-ItemProperty -LiteralPath $chaveNuxt -Name AppEnvironmentExtra -Value $ambienteDepois -ErrorAction Stop
@((Get-ItemPropertyValue -LiteralPath $chaveNuxt -Name AppEnvironmentExtra -ErrorAction Stop) | Where-Object { $_ -ceq $linhaPiloto }).Count
```

No prompt, cole a linha inteira **sem aspas**. A contagem final deve ser `1`; não imprima a lista de variáveis. A alteração não muda a conta, os caminhos ou o IIS. O deploy normal preserva esse valor e reinicia o Nuxt; se a release correta já estiver ativa, carregue o valor reiniciando somente `MdwConteudoNuxt` em janela autorizada. Não faça os dois reinícios.

O template público inclui uma regra exata para `/api/curadoria/teams/piloto` antes da regra da API .NET, encaminhando-a ao Nuxt. Não é necessário alterar ARR, bindings ou regras globais. Sem pin válido, com pin diferente ou em falha de rede, a exceção continua bloqueada sem impedir o restante do sistema. O diálogo informa somente o resultado da conferência; a confirmação humana e a revalidação antes de salvar permanecem obrigatórias. Esta checagem de apresentação não autentica usuários nem concede acesso Graph/RSC. A validação no Teams real deve ocorrer após o deploy autorizado.
