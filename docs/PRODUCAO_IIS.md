# Produção IIS — nova VM Azure Windows

Procedimento vigente: IIS + ARR + URL Rewrite, API ASP.NET Core 10 pelo Hosting Bundle, Nuxt SSR Node **22** via **NSSM** e Firebird no mesmo host. Nenhum script instala remotamente ou baixa NSSM. Instalar previamente os componentes e usar PowerShell 7 elevado com WebAdministration disponível. Verificar certificado com chave privada em LocalMachine/My, SAN do domínio e renovação. Usar o NSSM já adotado pelo servidor, em caminho protegido e com versão confirmada por `nssm version`.

## Diretórios e isolamento

Usar `D:\Mdw\releases\VERSAO` para código, `D:\Mdw\data` para dados persistentes, `D:\Mdw\service` para serviço/logs e `D:\Mdw\secrets` para configuração restrita. Nenhum deles pode estar dentro de outro. Backups devem ficar em volume separado, com cópia externa criptografada. Preservar identidade e ACL do serviço Firebird nos bancos. API recebe Modify nos dados; Nuxt não precisa de acesso aos dados nem segredos API.

API IIS vinculada exclusivamente a 127.0.0.1:5080; Node a 127.0.0.1:3000. Abrir somente 80/443 externamente, fechar portas internas e Firebird em NSG/firewall. Confirmar versões Firebird compatíveis e portas 3052/3050 do template. Node deve ser legível por LocalService e pool API, também disponível no PATH de máquina para o bridge.

## Build

```powershell
./tools/production/Build-Package.ps1 -OutputDirectory D:\Artifacts\mdw-20260923-01
```

Executar em agente Windows limpo, SDK .NET 10 e Node **22.23.3**, sem variáveis de aplicação/segredos herdadas. Por padrão o build usa **HEAD commitado**; `-WorkingTree` permite validar alterações pendentes, copiando apenas arquivos conhecidos do Git e não ignorados. Cada execução usa staging novo, testes .NET, dotnet publish, npm ci, testes do portal e Nuxt node-server SSR. O bridge inclui dependências de produção e os três manuais. Configuração base e Production permanecem; .env, arquivos locais, bancos, backups, uploads, homologação e caches ficam excluídos. Assets estáticos de aplicação vão em `static-assets`, separados dos anexos. Manifesto SHA256 e `build-info.json` registram integridade e versões. Proteger transporte e manifesto; o staging temporário fica retido para diagnóstico.

## Configurar e validar

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

Production não cria automaticamente o catálogo de permissões. Após backup e restauração dos bancos de destino, execute explicitamente, no diretório `release/api`, com a configuração local preenchida:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
dotnet MdwConteudos.Api.dll --migrate-permissions
```

Esse comando altera o catálogo no banco configurado e encerra; confira os caminhos antes de executá-lo. O instalador não o executa. Em instalações novas, revisar também os scripts fornecidos em `sql` com o DBA, conforme o schema restaurado; não executar todos indiscriminadamente. Reiniciar o pool após preparar o catálogo. Conceder as novas permissões Studio explicitamente aos perfis autorizados.

As cinco frentes corrigidas são recuperação anônima de senha, autenticação da API interna, autorização/posse de sessões Studio, segredos/configuração de produção e controles de abuso/exposição de erros. `/api/v1` agora exige JWT; comunicar a mudança aos integradores. Credenciais padrão anteriormente utilizadas devem ser rotacionadas, inclusive nos bancos; remover do arquivo atual não remove o histórico Git.

**Pendência adicional encontrada na auditoria:** `@cursor/sdk@1.0.23` → `@connectrpc/connect-node@1.7.0` → `undici@5.29.0` apresenta vulnerabilidades (uma alta, dois alertas moderados na árvore, `npm audit --omit=dev`). O SDK não oferece correção automática nesse lockfile. Não foi aplicado override para outro major sem validação. Não liberar Studio para uso em produção antes de corrigir/validar essa dependência ou aprovar formalmente a mitigação com o responsável de segurança. O pacote gerado não representa aprovação de entrada em produção.

Em 24/09/2026: 304 testes .NET aprovados, 16 testes de integração Firebird ignorados por ausência da configuração isolada; 24 testes frontend do portal aprovados e build Nuxt concluído. Smoke IIS/ARR, instalação NSSM, restauração e testes funcionais com bancos copiados precisam ocorrer na VM antes do corte. Nenhum banco original foi alterado durante essa validação.

## Procedimento de backup

Definir RPO/RTO e ensaiar restauração antes do corte. Pausar todos os escritores, serviços/jobs e integrações; registrar horário. Para cada banco usar `gbak` da versão Firebird compatível, modo backup **-b**, conexão localhost, conta autorizada e destino `.fbk` novo. Não copiar `.fdb` aberto. Não passar senha em linha de comando/histórico: usar mecanismo seguro da versão instalada, por exemplo arquivo restrito com `-fetch_password` após conferir `gbak -?`. Verificar saída e logs sem credenciais.

Na mesma janela sem gravações, fazer backup de todos os arquivos persistentes, uploads/static, JSON criptografado, configuração NSSM do serviço (`nssm dump MdwNuxt`) quando existente, certificado conforme política e configuração IIS. Registrar hashes, ACLs, versão Firebird, release e horário. Copiar para armazenamento externo protegido com retenção; verificar restauração, não apenas existência do backup.

Restaurar com gbak modo create **-c** para outro caminho/banco, nunca substituir original com -replace. Restaurar arquivos do mesmo ponto consistente; conceder acesso à identidade Firebird, validar integridade, contagens, charset e fluxos em ambiente isolado. Só apontar aplicação para banco restaurado na janela autorizada. Preservar original até aceite. Instalador não executa backups/restores nem altera bancos.

## Atualização e rollback

Script automatiza primeira instalação. Atualização controlada: preparar release nova, verificar hashes, reproduzir ACLs e copiar JSON restrito; manutenção, parar MdwNuxt/pool API; registrar physicalPaths e salvar `nssm dump MdwNuxt`. Atualizar `Application`, `AppDirectory` e os caminhos dependentes do serviço NSSM para a nova release, mantendo o executável NSSM estável. Reiniciar e executar smoke/aceite. Manter DataPaths:Root. Troca coordenada com tráfego suspenso, não atômica entre IIS e Node.

Rollback de **código**: manutenção, parar serviço/pool, repor physicalPaths/XML/configuração compatível anteriores e testar. Dados intactos. Rollback de **banco** é decisão separada: autorização, perda de escritas desde backup explicitamente aceita e restauração coordenada banco+arquivos sem escritores. Não restaurar banco por falha de smoke. Se código anterior não suporta schema atual, manter manutenção e envolver DBA. Preservar releases antigas pela retenção.
