# Homologação local isolada

## Preparar

Requer Windows, PowerShell 7.2+, Firebird local e `gbak.exe` correspondente a cada servidor. O script identifica o serviço pela porta (neste ambiente, REFERENCIAS 3052 e ASSISTENTE 3050). Contas de serviço personalizadas exigem revisão de ACL; o script não amplia permissões automaticamente.

Na raiz do checkout:

```powershell
pwsh -NoProfile -File tools/homologacao/Prepare-Homologacao.ps1
# Caso a detecção não encontre o executável:
# acrescentar -ReferenciasGbakPath 'C:\...\gbak.exe' -AssistenteGbakPath 'C:\...\gbak.exe'
```

O processo executa backup transacional sem coleta de lixo na origem e restore em arquivos novos, materializa tabelas externas, copia `static` e `uploads` sem links e verifica se os arquivos mudaram durante a cópia. Cada banco é consistente individualmente; não há atomicidade global entre os dois bancos e os uploads. Pause uploads enquanto prepara uma referência conjunta.

Saída: `.homologacao/<data-id>/` com `databases`, `backups`, `files` e manifesto. Somente após concluir, promove `backend/MdwConteudos.Api/appsettings.Homologacao.local.json`. Essas pastas/configurações ficam fora do Git, com ACL local restrita. Não compartilhe esse JSON: contém credenciais. O InstanceId exibido não é segredo.

Para renovar, pare a instância de homologação e execute Prepare novamente. A execução anterior é preservada; nenhum original nem snapshot anterior é apagado. Não copie um FDB aberto com Explorer/Copy-Item.

## Iniciar

```powershell
dotnet build backend/MdwConteudos.Api/MdwConteudos.Api.csproj --no-restore
pwsh -NoProfile -File tools/homologacao/Start-Homologacao.ps1 -IntegrationConfirmed
```

Start valida artefatos e atualidade do build; não compila. O perfil é **Homologacao**, HTTP loopback **5081**. Também pode ser selecionado no Visual Studio após a preparação. O guard executa antes de `EnsureSchemaAsync`, rejeita originais, symlinks/hardlinks, configurações divergentes e fallback para o Flask. O `.env` ancestral não é carregado; valores são congelados para que hot reload não troque os destinos.

Publicação automática, e-mails, aprovação por link e integrações externas ficam desabilitados. Caminhos absolutos legados de anexos são resolvidos somente para cópias; arquivo não copiado fica indisponível, nunca usa o original como fallback. O usuário **homologacao** é criado somente na cópia, com senha de teste própria definida no JSON local; a senha de parceiro também é própria. Não reutilize credenciais de produção para autenticar o console de testes.

Configure no terminal que iniciará o Nuxt:

```powershell
$prepared = Get-Content backend/MdwConteudos.Api/appsettings.Homologacao.local.json -Raw | ConvertFrom-Json
$env:NUXT_DOCS_SANDBOX_BASE = 'http://127.0.0.1:5081'
$env:NUXT_DOCS_SANDBOX_PUBLIC_BASE = 'http://127.0.0.1:5081'
$env:NUXT_DOCS_SANDBOX_INSTANCE_ID = $prepared.Documentation.InstanceId
cd frontend/nuxt-app
npm run dev
```

O destino atual permanece separado, normalmente 5080. A autenticação administrativa do portal usa esse destino atual. Não sobrescreva seu appsettings normal nem suas variáveis de produção para testar.

## Verificar

```powershell
pwsh -NoProfile -File tools/homologacao/Test-Homologacao.ps1 -CheckContext
pwsh -NoProfile -File tools/homologacao/Test-Portal.ps1 -PortalBase http://localhost:3000
# Inclui PUT conservando os valores existentes e criação de usuário comum descartável na cópia:
pwsh -NoProfile -File tools/homologacao/Test-Portal.ps1 -PortalBase http://localhost:3000 -AllowCopyWrites
```

O smoke Test-Portal é voltado a uma sessão QA em que o login administrativo do portal reconhece o usuário da cópia (para QA integral sem produção, configure `NUXT_DOCS_API_BASE=http://localhost:5081` e `NUXT_DOCS_SANDBOX_BASE=http://127.0.0.1:5081`; ambos apontam deliberadamente à cópia, mantendo os modos de execução separados). Para uso cotidiano com 5080 real, faça login administrativo normal no portal e informe o JWT da cópia em Autorizar; não execute o smoke com credenciais reais embutidas.

O script valida a identidade antes de escrever, não imprime tokens e não abre bancos originais. Testa consultas/aliases, envelopes, ZIP real, 401/403 e política de escrita. Os testes antigos de publicação dependentes de fixture externa continuam opt-in e não são automaticamente habilitados.

As cópias contêm dados do projeto e devem permanecer locais, fora do versionamento. Interromper os processos com Ctrl+C não remove as cópias. O cutover de produção continua sujeito à validação dos consumidores reais, casos de borda e downloads de todas as famílias de conteúdo.
