# Automações — ChatFast (fase 1)

> Atualização 1.1: consulte a seção final **Contrato ampliado e demonstração local**. Os cinco indicadores descritos na fase inicial continuam compatíveis com snapshots 1.0; snapshots 1.1 usam indicadores específicos e detalhes estruturados. Nenhuma nova tabela é necessária.

## Mapa da documentação

- **Entrega e limites operacionais / Banco e ativação:** arquitetura inicial, responsabilidade dos componentes e operação segura.
- **Contrato das respostas / Relatórios:** Power Automate, bot e idempotência.
- **Contrato ampliado e demonstração local — 1.1:** contrato atual, separação público/privado, exemplos e demonstração sem gravação.
- **Dicionário físico e dependências do banco:** colunas, PKs, FKs, índices, conteúdo dos BLOBs e rastreabilidade.
- **Futura transferência para outro Firebird:** desenho necessário, dependência de usuários, cópia, validação e retorno. É planejamento, não uma funcionalidade já implementada.
- **Preparação da integração automática — 22/11/2026:** entrega ao agente responsável e critérios para autorizar a ativação futura.

Implementa persistência das respostas dos gestores e relatórios estruturados no MDW Conteúdo. Não substitui análise, consultas GET ao ChatFast, agendamentos, cartões, lembretes, e-mails ou máquina de estados do bot. Receber uma marcação de tratado **não** significa resolver/encerrar um caso.

## Entrega e limites operacionais

- Público, sem login: `/automacoes/chatfast/diario` e `/automacoes/chatfast/semanal`.
- Privado: `/automacoes` → `/automacoes/chatfast/gestao`, apenas administradores ativos. Token antigo de administrador desativado/rebaixado é recusado.
- Nenhuma página incorpora HTML/JavaScript gerado pelo bot. O HTML vem de componentes Vue; o produtor envia JSON versionado.
- Público contém exclusivamente tipo/período, instante de geração, métricas globais e indicação de desatualização. Não possui filtros de gestor/equipe/cliente, nomes, observações, IDs de conversas ou links de atendimento. `details` é privado e nunca compõe o DTO público.
- Indicador desconhecido é `null`, exibido como `—`, nunca convertido para zero.
- A integração nasce desabilitada. Esta entrega não executa migração no banco operacional, altera o Power Automate, publica relatório real, envia cartão ou ativa cron.

## Banco e ativação (somente mediante autorização do alvo)

Alvo: mesmo banco REFERENCIAS já configurado na API. Tabelas novas:

| Tabela | Finalidade |
| --- | --- |
| `CHATFASTRESPOSTA` | Envelope imutável de resposta, origem, instante UTC e hash |
| `CHATFASTRESPOSTAITEM` | Marcação por identificador externo `tratado_<id8>`; não inventa associação com sessão |
| `CHATFASTIMPORTACAO` | Hash do XLSX, nome, administrador responsável e instante da importação |
| `CHATFASTRELATORIO` | Snapshot imutável, versão de contrato, período, completude e detalhes privados |

PKs: `CODCHATFASTRESPOSTA`, `CODCHATFASTIMPORTACAO`, `CODCHATFASTRELATORIO`; item usa `(CODCHATFASTRESPOSTA, POSICAO)`. FK de importação usa `USUARIO.CODUSUARIO INTEGER`. As abreviações de constraints `CF`, `CFITEM`, `CFRESPOSTA`, `CFIMPORTACAO` e `CFRELATORIO` estão documentadas no SQL versionado. Novos textos usam UTF8; datas físicas são UTC. Não há DELETE, recriação ou alteração de tabelas legadas.

Antes da ativação: conferir máquina, ambiente e caminho real de REFERENCIAS, fazer backup consistente, validar restauração e combinar janela. Não copiar um `.fdb` aberto como substituto de backup. Parar somente os componentes MDW envolvidos se a política operacional exigir; nunca `iisreset` ou serviços globais.

A partir da pasta **da API da release correta**, com a configuração privada correta e autorização explícita para aquele alvo:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
dotnet .\MdwConteudos.Api.dll --migrate-chatfast
```

O comando cria apenas estruturas ausentes, adiciona FK/índice próprios e valida colunas, tipos, comprimentos, nulabilidade e nomes de constraints na mesma transação. É repetível. Esquema incompatível aborta: não remove dados para “consertar”. Referência SQL versionada: `docs/sql/scripts/add-chatfast-20261120.sql`; a CLI é o caminho preferencial pela validação adicional. Se ocorrer falha, preservar logs sem conteúdo pessoal e corrigir a causa antes de repetir.

O migrador geral `--migrate-database` inclui ChatFast **somente** se `AutomacoesChatfast:MigrateWithDatabase=true`. Manter `false` até autorizar essa ativação; habilitar depois permite que os próximos deploys executem a etapa do módulo. Não é necessário executar SQL manual e CLI juntos.

Mesclar esta seção no JSON **privado** (`secrets` no servidor, copiado pelo deploy para a API), sem substituir as outras seções:

```json
"AutomacoesChatfast": {
  "Enabled": false,
  "MigrateWithDatabase": false,
  "ResponsesKey": "",
  "ReportsKey": ""
}
```

Gerar duas chaves aleatórias diferentes, com pelo menos 32 caracteres, por ambiente. Uma fica exclusivamente na conexão HTTP do Power Automate; outra no ambiente privado do bot. Não usar JWT de usuário/parceiro nem chave Cursor. Não colocá-las em Git, planilhas, query string, HTML, exemplos ou logs. Validar em ambiente de teste com banco descartável antes de definir `Enabled=true`, autorizar migrações no deploy e reciclar **somente** o pool da API. Nenhuma configuração global do IIS ou de outros sites precisa mudar.

Verificar HTTPS, health existentes e endpoints abaixo após publicação. O proxy público existente `/api-dotnet` já encaminha esses caminhos à API; não criar novo binding/porta pública.

## Contrato das respostas — Power Automate

`POST /api/integracoes/chatfast/v1/respostas`, cabeçalho privado `X-Chatfast-Key: <ResponsesKey>`, HTTPS, JSON de até 1 MB. Externamente a URL inclui o proxy: `https://conteudo.medware.com.br/api-dotnet/api/integracoes/chatfast/v1/respostas`.

Corpo da ação HTTP: `tools/production/chatfast/power-automate-response.example.json`. O nome interno da espera foi conferido no ZIP exportado:

```json
{
  "schemaVersion": "1.0",
  "flowId": "@workflow()['name']",
  "runId": "@workflow()['run']['name']",
  "response": "@body('Postar_cartão_adaptável_e_aguardar_uma_resposta')"
}
```

As expressões acima são avaliadas pelo Power Automate: `response` deve chegar como **objeto**, não como string contendo JSON. O objeto contém `responseTime` com fuso, `responder`, `messageId` se disponível e `data` original. Em `data`, reconhecemos `rodada`, `gestor`, `obs`, `acao`, `caso_id`, `versao` e as propriedades `tratado_*` (`"1"`, `"0"`, booleanos ou números 1/0). Metadados ausentes ficam desconhecidos. Não deduzir sessão/cliente pelo sufixo de oito caracteres.

Identidade: `flowId + runId`. Reenvio com mesma identidade e conteúdo retorna `200` com `duplicate=true`; conteúdo diferente retorna `409`, sem sobrescrever o anterior. Reordenação de propriedades e espaços do JSON não muda seu hash canônico. Não gerar novo runId manual para esconder conflito. Se futuramente houver mais de uma resposta dentro da mesma execução, versionar o contrato antes de habilitar esse cenário.

### Alteração proposta no fluxo, ainda não aplicada

1. Trabalhar em cópia de teste do fluxo **chatfast-cardcomresposta**. O fluxo simples de postar mensagem/cartão permanece intacto.
2. Preservar espera, destinatários, cartão, conexões e `timeout = coalesce(triggerBody()?['timeout'], 'PT4H')`. Timeout sem resposta não vira marcação de tratado.
3. Após sucesso de `Postar_cartão_adaptável_e_aguardar_uma_resposta`, abrir ramos independentes: (a) persistência HTTP; (b) envio do e-mail atual. Manter Excel em um terceiro ramo durante a transição. No fluxo exportado, o e-mail dependia do sucesso do Excel: retirar **essa dependência** na cópia, sem fazê-lo depender da nova HTTP. Falha em persistir não deve impedir e-mail; falha no e-mail não deve impedir persistência.
4. HTTP: `Content-Type: application/json`, chave como segredo da conexão/variável segura. Habilitar Secure Inputs/Outputs na ação; não registrar o corpo. Política de repetição exponencial limitada para 408/429/5xx, mantendo identidade/corpo. 400/401/409 pedem correção, não reenvio com novo identificador. Confirmar licença/conector HTTP disponível no tenant antes da ativação.
5. Conservar e-mail com JSON completo entre `##DADOS##` e `##FIM##`, link e resumo atuais: o bot ainda o consome. Não duplicar a espera ou a postagem para testar persistência.
6. Comparar Excel/API/e-mail por pelo menos **cinco dias úteis**, incluindo uma execução semanal. Validar reenvio idempotente, itens, observações, datas/fuso, responsável e ausência de cartões duplicados. Só retirar Excel após aceite explícito. Se falhar, desativar o ramo HTTP e manter Excel/e-mail; não apagar registros já recebidos.

Retorno: `{ "success": true, "id": "...", "duplicate": false }`. Rate limit retorna `429` e `Retry-After`. Chave inválida `401`, integração desligada `503`, conteúdo inválido `400`, conflito `409`. Não interpretar `200` como encerramento de caso.

## Relatórios — adaptador do bot

`POST /api/integracoes/chatfast/v1/relatorios`, cabeçalho `X-Chatfast-Key: <ReportsKey>`. Exemplo completo fictício: `tools/production/chatfast/report.example.json`.

- `schemaVersion="1.0"`, `batchId` estável por snapshot; `type="diario"|"semanal"`.
- `periodStart` inclusivo e `periodEnd` exclusivo com fuso explícito; `generatedAt` com fuso. Diário até 25 horas; semanal até 8 dias. Geração não anterior ao fim do período. Adaptar os agregados **já produzidos** pelo bot; não somar categorias que possam se sobrepor.
- `metrics`: `analyzed`, `waiting`, `deviations`, `critical`, `treated`, cada um inteiro não negativo ou `null`.
- `complete=false` persiste apenas para gestão. Somente snapshots completos podem ser públicos. Uma correção recebe novo `batchId`; o snapshot anterior permanece imutável.
- Seleção pública ordena pelo fim do período e depois pela geração, não pela hora de chegada. Lote atrasado de período antigo não substitui o atual. Atualização incompleta mais recente mantém o último completo e sinaliza desatualização. Após 48h no diário ou 192h no semanal, mostra atualização pendente.
- `details` opcional é JSON privado; nunca HTML executável. Não enviar tokens/credenciais ou transcrições desnecessárias. A fase 1 não resolve a associação de item com sessão: esse mapeamento continua pertencendo ao bot.

Helper Node 22, sem SDK ou dependência nova. Executar no ambiente do bot **após aprovação e integração explícita** da chamada, não neste repositório contra produção:

```powershell
# CHATFAST_API_BASE e CHATFAST_REPORTS_KEY devem vir do ambiente privado do bot.
# Base esperada: https://conteudo.medware.com.br/api-dotnet
node tools/production/chatfast/publish.mjs --file caminho-do-relatorio.json
# A linha anterior não transmite nada. Só após conferir o alvo:
node tools/production/chatfast/publish.mjs --file caminho-do-relatorio.json --send
```

O adaptador não busca atendimentos, não gera HTML, não instala cron nem altera estados locais do bot. Ele transmite o mesmo arquivo/identidade, com timeout de 20s e até três tentativas para transporte/408/429/5xx. Não segue redirecionamentos com credencial. Falha 409 interrompe e pede conferência. Log só contém resultado/duplicidade ou resumo técnico; não imprime corpo/chave. Para testes HTTP locais, é preciso `--allow-local-http` e host loopback. A API aceita HTTP de integração apenas no ambiente Development.

## API pública e gestão

- `GET /api/publico/chatfast/v1/relatorios/diario` (ou `semanal`), opcional `?date=yyyy-MM-dd` para início do período em Brasília. Sem snapshot completo, `data=null`; indisponibilidade é erro, nunca “zero casos”. Resposta pública não depende de login.
- Admin: `GET /api/web/automacoes/chatfast/respostas` e `/relatorios`, paginação por `?before=<id>` (50 por página). Requer JWT de administrador **e** confirmação de conta ativa no banco. Detalhes permanecem no servidor/API privada.
- Admin: `POST /api/web/automacoes/chatfast/importacoes`, multipart `file` e `confirm=false|true`. A prévia não grava; confirmação reenvia o mesmo arquivo. A tela invalida a prévia se o arquivo for trocado. Dados não válidos impedem a confirmação integral.

## Histórico XLSX

Usar a planilha exportada com uma única aba de cabeçalhos `data, rodada, gestor, itens_tratados, observacao, respondido_por, hora`, nessa ordem e na primeira linha. Suporta strings compartilhadas/inline e seriais de data/hora Excel padrão 1900; formato 1904 é recusado para evitar conversão incorreta. Não executa fórmulas. Até 5 MB compactados, 20 MB descompactados, 200 entradas ZIP, 5.000 registros e 10.000 marcações por lote. XML externo/DTD é proibido; não extrai anexos no disco.

O servidor calcula SHA-256 dos bytes, não confia em hash enviado pelo cliente. Importação confirmada é transacional. Repetir **o mesmo arquivo** não duplica registros. Não deduplica arquivos reexportados com bytes diferentes por heurística: a planilha não tem identidade original de execução. Não importar versões sobrepostas sem reconciliação administrativa. Antes de confirmar, conferir contagens, intervalo de datas e sobreposição com eventos já recebidos da API.

Origem `excel`, arquivo/hash/linha e administrador são preservados. Hora legada é Brasília → UTC; ambiguidade/ausência de data rejeita a linha. Campos inexistentes (sessão, mensagem, estado do caso) continuam desconhecidos. A importação não alimenta a máquina de estados, não publica relatórios, não dispara e-mails/cartões e não é fixture de testes.

## Testes e aceite

Testes sintéticos: contratos, allowlist pública, isolamento de produtores, idempotência/concorrência, 409, projeção sem dados pessoais, importação, datas, constraints, instalação limpa/repetida e proteção de dados com esquema incompatível. Os testes Firebird criam exclusivamente arquivos aleatórios temporários embedded; não leem appsettings ou bancos do usuário. `PUBLICACAO_TEST_FBCLIENT` seleciona só a biblioteca embedded, não um banco.

```powershell
dotnet test backend/ConversorHtml.Tests/ConversorHtml.Tests.csproj --filter FullyQualifiedName~Chatfast
# Em frontend/nuxt-app:
npm run test:automacoes
```

O empacotador executa os testes de Automações e inclui este guia, o SQL de referência e o helper. Para reverter ativação: desativar o ramo novo do fluxo/publicação, definir `Enabled=false` e `MigrateWithDatabase=false` no JSON privado e reciclar somente a API. Preservar tabelas/histórico; rollback de código não desfaz dados. Remoção de dados exige plano e autorização separados.

### Registro de validação da implementação — 20/11/2026

- Build .NET e build de produção Nuxt com Node 22.20.0 concluídos.
- 19 testes focados ChatFast aprovados, incluindo nove de integração/HTTP em bancos Firebird 5 temporários embedded. Quatro testes frontend/adaptador aprovados também no Node 22.
- Suíte geral .NET: 496 aprovados / 105 ignorados por condições da suíte, sem falhas. Suites frontend de documentação, permissões, curadoria, auditoria e orçamentos também passaram; helper de processos do deploy passou.
- Conferência em navegador headless com dados sintéticos e API simulada: desktop/celular, estados normal/vazio/erro/desatualizado, redirecionamento de usuário anônimo e gestão administrativa. Layout móvel ajustado e conferido novamente; sem erros JavaScript.
- Teste adicional com Firebird 4 não pôde ser concluído: conflito de memória compartilhada/runtime na instalação local, inclusive na tentativa de isolamento. Nenhum serviço instalado foi parado/reconfigurado para contornar isso. Não tratar essa tentativa como validação de compatibilidade runtime 4.
- Permanecem deliberadamente pendentes de autorização: migração em banco operacional, configuração das chaves reais, deploy, alteração dos fluxos/bot, importação da planilha real e período de operação paralela/aceite. Nenhuma dessas ações foi executada nesta implementação.

## Contrato ampliado e demonstração local — 1.1

O contrato de relatórios aceita `schemaVersion: "1.0"` e `"1.1"`. Respostas de cartões continuam em 1.0. A rota de integração continua `/api/integracoes/chatfast/v1/relatorios`: a versão do documento não altera a rota. Snapshot 1.0 conserva a serialização e o hash anterior (o campo `summary` ausente não é introduzido como `null`). Não reutilizar o `batchId` de um documento 1.0 ao convertê-lo para 1.1.

Não há DDL novo nem migração adicional: o documento ampliado continua no BLOB UTF8 de `CHATFASTRELATORIO`. Os testes em banco temporário cobrem persistência, reenvio e projeção. **Não executar novamente migração no banco do usuário para esta ampliação.**

Exemplo inteiramente fictício: `tools/production/chatfast/report-1.1.example.json`. Os arquivos reais da demonstração não fazem parte do Git nem do pacote de produção.

### Parte pública: summary

Em 1.1, `summary` é obrigatório. Contém somente:

- `statistics`: até 30 elementos `{code, value, previousValue}`. `previousValue` é opcional/nulo; nenhuma ausência é convertida para zero.
- `dailyVolume`: até oito pares `{date, events}` no período reportado, sem datas repetidas. Volume de eventos não é volume de conversas.
- `trend`: até 12 períodos `{start, end, deviationsPer100}`. O índice é calculado na origem, não pela soma de taxas de técnicos/equipes. Períodos de até oito dias e não posteriores ao snapshot.
- `methodCodes` e `limitationCodes`: códigos fixos; o texto exibido pertence à aplicação, não ao produtor.

Códigos inteiros de estatísticas: `analyzedConversations`, `handledConversations`, `newConversations`, `outOfPatternConversations`, `longWaitOver60Snapshot`, `criticalSelections`, `deduplicatedDeviations`, `deviationEvents`, `events`, `recurrenceEvents`, `noReplyNewConversations`, `firstReplyMeasurements`, `audioMessages`, `businessDays`.

Códigos decimais: `deviationsPer100`, `medianFirstReplyMinutes`, `meanFirstReplyMinutes`. Valores de 0 a 1 bilhão ou `null`; não aceitar frações para contagens. Estatísticas repetidas são recusadas. Propriedades adicionais no `summary`, nomes, URLs, rótulos e textos livres são recusados — não basta esconder campos no frontend.

Métodos aceitos: `businessHours08001800`, `uniqueTechnicianConversation`, `newClientConversationFirstReply`, `globalConversationDeduplication`, `criticalSelectionsNotUniqueCases`, `longWaitSnapshot`, `dailyOutOfPatternWindows`, `botAuthoritativeState`.

Limitações aceitas: `unequalBusinessDays`, `partialCollectionLastDay`, `missingDeploymentSummary`, `incompleteCriticalEventExtraction`, `recurrenceCoverageChanged`, `limitedFirstReplySample`, `audioTranscriptionPending`, `deviationsOnlyMonitoringWindows`, `snapshotNotLive`, `sourceFileNotRawAudit`, `noTreatmentEvidence`, `trackerEmptyNotNoCriticalCases`.

Apenas `summary` tipado, os cinco `metrics` legados e metadados de período/geração são projetados no endpoint público. Nenhum detalhe individual, nome/foto, equipe, recomendação específica ou link é devolvido. Para dados cuja semântica difere dos cinco indicadores iniciais, manter esses cinco como `null` e usar os códigos específicos de `summary`.

### Parte privada: details

Em 1.1, `details` pode ser `null` (envio só de agregados). Se preenchido, o formato é:

```json
{
  "tables": [{ "title": "Título privado", "columns": ["Coluna"], "rows": [["Valor"]] }],
  "cases": [{ "client": "Exemplo fictício", "technicians": "Pessoa de teste", "team": "Equipe fictícia", "status": "Não conferido", "description": "Relato de teste", "dates": "19/11/2026", "occurrences": null, "severity": "Espera longa" }],
  "recommendations": [],
  "positiveExamples": [],
  "sources": [],
  "tracker": null
}
```

Todos os arrays acima são obrigatórios, mesmo vazios; `tracker` é opcional/nulo ou `{count, updatedAt, note}`. Fontes usam `{file, sha256}`, sem caminho absoluto. Limites: 20 tabelas, 20 colunas por tabela, 500 linhas por tabela, 500 seleções, 100 recomendações/exemplos e 10 fontes, dentro de 1 MB do envelope. Linhas devem ter o mesmo número de células do cabeçalho; células são strings/nulas. Texto é escapado na interface, nunca interpretado como HTML. URLs/fotos dos TSX não são importadas para esta demonstração.

A gestão oferece seções de indicadores/método, técnicos/equipes, atendimentos selecionados e recomendações/exemplos. Pesquisa atua só sobre tabelas/seleções privadas e não recalcula agregados. A lista de seleções não se torna cadastro/estado de casos. Mantém JWT e verificação de administrador ativo no banco, inclusive para demonstração.

### Dados reais dos anexos de 19/11

O conversor foi preparado especificamente para os snapshots fornecidos: diário 19/11/2026, semanal 16–19/11 e acompanhamento vazio atualizado em 19/11. Faz parsing AST dos TSX; **não importa/executa os TSX, não invoca callbacks e não acessa ChatFast/MCP**. A dependência `@babel/parser` é explícita em desenvolvimento. Não utilizar o conversor como extrator genérico de outras versões sem revisar os mapeamentos.

Os dados do diário dão 204 conversas analisadas, 31 conversas fora do padrão, oito atendimentos críticos e duas esperas superiores a uma hora no instante da coleta. A lista privada possui dez selecionados: oito críticos **e duas esperas longas**. Não chamar os dez de dez críticos.

O semanal separa 633 conversas atendidas, 644 novas, 151 desvios deduplicados por técnico/conversa, 160 eventos de desvio, 238 eventos totais e 47 seleções críticas (que podem repetir o mesmo caso em datas distintas). O arquivo possui 37 entradas agrupadas na lista privada; não afirmar 47 casos únicos. Índice global 23,9/100 e primeira resposta mediana 9,2 min, média 16,3 min e 354 medições são medidas diferentes. Nenhum valor de tratados foi inferido. Acompanhamento formal com zero registros não contradiz as seleções críticas.

As fontes são os TSX fornecidos, com seus hashes registrados nos detalhes privados. Não houve reconsulta/auditoria da base ChatFast. Os horários de cobertura e limitações (transcrição pendente, janelas de monitoramento, semanas com quantidades diferentes de dias úteis etc.) aparecem na página. `complete=true` representa o snapshot histórico fechado apresentado pelo arquivo, não cobertura contínua nem transcrição completa de todos os áudios.

### Preparar e visualizar sem gravar no banco

Na raiz do repositório, com dependências do frontend instaladas, o conversor exige uma pasta de saída **nova e privada, fora do repositório**:

```powershell
node tools/production/chatfast/prepare-demo.mjs --source 'C:\Users\Nayhan.MEDWARE\Documents\automacoes' --output 'C:\Users\Nayhan.MEDWARE\Documents\automacoes\chatfast-demo-OUTRA-PASTA-NOVA'
```

Uma demonstração já foi preparada nesta implementação em:

`C:\Users\Nayhan.MEDWARE\Documents\automacoes\chatfast-demo-20261120-final`

Ela contém `diario.json` e `semanal.json`, com detalhes privados, **não copiar para static/public, Git, pasta de deploy ou links públicos**. Uma versão preliminar também foi gerada nas pastas `chatfast-demo-20261120` e `chatfast-demo-20261120-v11`; usar somente a pasta `-final` acima.

No terminal local da API, depois de encerrar **somente o processo local da API** com Ctrl+C, a partir da raiz:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:AutomacoesChatfast__DemoDirectory = 'C:\Users\Nayhan.MEDWARE\Documents\automacoes\chatfast-demo-20261120-final'
dotnet run --project backend\MdwConteudos.Api\MdwConteudos.Api.csproj --no-launch-profile -- --urls http://127.0.0.1:5080
```

Não é necessário editar o JSON versionado nem mudar chaves, tabelas ou serviços. Essas variáveis afetam somente essa janela/processo. Não removê-las de outros ambientes nem editar NSSM/IIS.

Com o frontend local em `http://localhost:3000`, acessar:

- Diário de demonstração: `http://localhost:3000/automacoes/chatfast/diario?demo=1`.
- Semanal de demonstração: `http://localhost:3000/automacoes/chatfast/semanal?demo=1`.
- Gestão: `http://localhost:3000/automacoes/chatfast/gestao` → **Abrir diário real** ou **Abrir semanal real**. Requer login de administrador.

Endpoints de demonstração: `GET /api/publico/chatfast/v1/demonstracao/{type}` (projeção pública) e `GET /api/web/automacoes/chatfast/demonstracao/{type}` (documento privado autorizado). Funcionam exclusivamente em `Development` com diretório explicitamente configurado; em Production/Homologacao retornam 404. O frontend só disponibiliza controles de demo no servidor de desenvolvimento Nuxt. Em build de produção, `?demo=1` não seleciona a fonte de demonstração.

Não há fallback silencioso entre fontes: sem `?demo=1`, a página continua lendo o banco. Ler demonstração não insere nada, não habilita o módulo em produção, não envia relatório ao bot e não altera a máquina de estados. Para parar de usar, fechar o processo ou remover a variável **da mesma janela local** e reiniciar a API local. Eventual persistência destes snapshots pela API de integração fica para autorização separada.

### Validação da ampliação

- Contratos 1.0/1.1, códigos permitidos, rejeição de estrutura malformada, projeção pública, persistência/reenvio em Firebird 5 temporário, autorização de administrador e bloqueio de demo em produção: 29 testes focados aprovados.
- Suíte geral da API: 504 aprovados, 107 ignorados pelas condições da suíte. Seis testes frontend/conversor aprovados em Node 22.20.0; regressões de permissões, curadoria e documentação também passaram.
- Build de produção Nuxt aprovado. Conferência final em navegador sobre build isolado, com API simulada e snapshots reais locais: diário/semanal, desktop/celular, ausência de dados individuais no público, redirecionamento de anônimo, tabelas privadas, pesquisa e seleções. Sem erros JavaScript; layout do painel privado corrigido para não herdar a largura da barra lateral. Os endpoints reais foram testados separadamente com autenticação e banco temporário.
- Os dois JSON reais também foram desserializados/validados pelo código .NET de contrato e projetados no DTO público sem detalhes privados. Não houve envio desses arquivos ao banco nem a serviço externo.
- O usuário já confirmou migração das quatro tabelas e envio fictício no banco **local** antes desta ampliação. Nenhuma migração adicional foi executada. Produção e fluxos continuam sem ativação por esta entrega.

## Dicionário físico e dependências do banco

Inventário conferido no código em 22/11/2026. Reflete o esquema implementado, não uma inspeção de produção. A fonte canônica é `backend/MdwConteudos.Api/Modules/Automacoes/ChatfastSchema.cs`; o SQL versionado correspondente está em `docs/sql/scripts/add-chatfast-20261120.sql`. Constraints/índices usam abreviações CF = ChatFast, CFRESPOSTA = CHATFASTRESPOSTA, CFITEM = CHATFASTRESPOSTAITEM, CFIMPORTACAO = CHATFASTIMPORTACAO e CFRELATORIO = CHATFASTRELATORIO.

### Arquitetura e propriedade dos dados

O serviço `ChatfastService` recebe `IFirebirdConnectionFactory`, a mesma fábrica utilizada pelo banco principal REFERENCIAS. **Hoje não existe conexão independente configurável para o armazenamento ChatFast.** Trocar `Firebird:Database` ou `FIREBIRD_DB` mudaria o banco principal de outros módulos também.

O Grok bot produz os snapshots e continua sendo responsável por análise, estados e agendamentos. O Power Automate produz as respostas dos cartões. A API autentica o produtor, valida, persiste em transação e projeta a visualização pública. O frontend não decide quais campos privados retirar: o servidor só devolve o DTO público permitido. A gestão exige JWT e consulta de administrador ativo na tabela central `USUARIO` pelo `ActiveAuditAdminFilter`.

Relações implementadas (sem exclusão em cascata):

```text
USUARIO (banco principal)
  └── CHATFASTIMPORTACAO.CODUSUARIO
        └── CHATFASTRESPOSTA.CODCHATFASTIMPORTACAO (opcional)
              └── CHATFASTRESPOSTAITEM.CODCHATFASTRESPOSTA

CHATFASTRELATORIO — snapshots independentes das respostas/importações
```

Relatório não é calculado automaticamente somando respostas ou importações. Recebimento de tratado não altera o estado dos casos e não gera relatório por conta própria. Os estados apresentados nos documentos são snapshots do bot, não comandos executados pela API.

### CHATFASTRESPOSTA

Uma linha por evento de resposta do Power Automate ou por linha importada do XLSX.

| Coluna | Tipo físico | Obrigatória | Significado |
| --- | --- | --- | --- |
| `CODCHATFASTRESPOSTA` | `BIGINT GENERATED BY DEFAULT AS IDENTITY` | Sim | Chave identificadora local do evento |
| `CHAVE` | `VARCHAR(64) CHARACTER SET ASCII` | Sim | SHA-256 da identidade lógica, com unicidade |
| `SHA256` | `VARCHAR(64) CHARACTER SET ASCII` | Sim | Hash do conteúdo da resposta/linha normalizada para conferência de repetição |
| `ORIGEM` | `VARCHAR(20) CHARACTER SET ASCII` | Sim | Valores produzidos hoje: `powerautomate` ou `excel`; não há CHECK físico restringindo esses textos |
| `DTHRRESPOSTA` | `TIMESTAMP` | Não | Instante original da resposta convertido para UTC |
| `DTHRCRIACAO` | `TIMESTAMP` | Sim | Instante de recebimento/importação pela aplicação, UTC |
| `CODCHATFASTIMPORTACAO` | `BIGINT` | Não | Vínculo com o lote XLSX; nulo para resposta recebida diretamente |
| `LINHA` | `INTEGER` | Não | Linha original do XLSX; nula para recebimento direto |
| `DOCUMENTO` | `BLOB SUB_TYPE TEXT CHARACTER SET UTF8` | Sim | JSON normalizado da resposta com metadados e documento de origem |

Constraints:

- `PK_CHATFASTRESPOSTA` sobre `CODCHATFASTRESPOSTA`.
- `UNQ_CFRESPOSTA_CHAVE` sobre `CHAVE`.
- `FK_CFRESPOSTA_IMPORTACAO`: `CODCHATFASTIMPORTACAO` → `CHATFASTIMPORTACAO.CODCHATFASTIMPORTACAO`.

Para Power Automate, a identidade é SHA-256 do texto `powerautomate\n{flowId}\n{runId}`; o hash do conteúdo usa a resposta original em JSON canônico. Para Excel, a identidade é SHA-256 de `excel\n{hashDoArquivo}\n{numeroDaLinha}`; o hash de conteúdo é o da serialização da linha legada. **O hash de conteúdo desta tabela não é necessariamente o SHA-256 de todo o BLOB armazenado.** Não recalculá-lo dessa forma numa transferência.

`DOCUMENTO` guarda o registro `ChatfastResponse`: `eventKey`, `hash`, `respondedAtUtc`, `messageId`, `round`, `manager`, `responder`, `observation`, `action`, `caseId`, `cardVersion`, `source`, `items`, `document` e, quando conhecidos, `flowId` e `runId`. O campo interno `document` é uma string JSON com a origem. Há conteúdo pessoal/operacional; não publicar o BLOB como arquivo estático.

### CHATFASTRESPOSTAITEM

Tabela subordinada de marcações da resposta; não é uma relação N:N e não recebe identificador genérico redundante.

| Coluna | Tipo físico | Obrigatória | Significado |
| --- | --- | --- | --- |
| `CODCHATFASTRESPOSTA` | `BIGINT` | Sim | Mesma chave/tipo da resposta pai |
| `POSICAO` | `INTEGER` | Sim | Ordem do item na resposta, gerada pela aplicação a partir de 1 |
| `CODIGOEXTERNO` | `VARCHAR(120) CHARACTER SET UTF8` | Sim | Nome original do item `tratado_*`; não representa uma FK de sessão ChatFast |
| `TRATADO` | `SMALLINT` | Sim | `1` para marcado e `0` para não marcado |

Constraints: `PK_CFRESPOSTAITEM(CODCHATFASTRESPOSTA, POSICAO)`, `FK_CFITEM_RESPOSTA` para a resposta e `CK_CFITEM_TRATADO CHECK(TRATADO IN (0,1))`. A posição é validada/produzida pela aplicação; não há CHECK físico `POSICAO>0`. Não inferir vínculo com cliente/sessão pelo sufixo de oito caracteres do código externo.

### CHATFASTIMPORTACAO

Uma linha por arquivo XLSX confirmado. A prévia de importação não grava esta tabela.

| Coluna | Tipo físico | Obrigatória | Significado |
| --- | --- | --- | --- |
| `CODCHATFASTIMPORTACAO` | `BIGINT GENERATED BY DEFAULT AS IDENTITY` | Sim | Chave identificadora do lote |
| `SHA256` | `VARCHAR(64) CHARACTER SET ASCII` | Sim | Hash calculado pelo servidor sobre os bytes do XLSX |
| `NOMEARQUIVO` | `VARCHAR(255) CHARACTER SET UTF8` | Sim | Nome do arquivo de origem |
| `CODUSUARIO` | `INTEGER` | Sim | Administrador que confirmou a importação |
| `DTHRCRIACAO` | `TIMESTAMP` | Sim | Instante de confirmação, UTC |

Constraints: `PK_CHATFASTIMPORTACAO`, `UNQ_CFIMPORTACAO_SHA256(SHA256)` e **`FK_CFIMPORTACAO_USUARIO(CODUSUARIO) REFERENCES USUARIO(CODUSUARIO)`**.

Essa última FK é a dependência externa mais importante para uma futura separação do banco. O migrador pressupõe que `USUARIO` já exista no mesmo banco e possua chave compatível `INTEGER`. Não cria usuários e não faz replicação de identidades. A exclusão de usuário referenciado pode ser impedida pela FK; retenção/anonimização precisa de política específica, sem apagar o histórico por conveniência.

O mesmo arquivo não duplica o lote. Arquivos reexportados com bytes diferentes não são deduplicados entre si por heurística, mesmo que algumas linhas pareçam iguais. A linha, o hash e o lote são parte da proveniência que deve ser preservada.

### CHATFASTRELATORIO

Uma linha por snapshot do bot, completo ou incompleto. A versão 1.1 é armazenada no mesmo esquema da 1.0.

| Coluna | Tipo físico | Obrigatória | Significado |
| --- | --- | --- | --- |
| `CODCHATFASTRELATORIO` | `BIGINT GENERATED BY DEFAULT AS IDENTITY` | Sim | Chave identificadora do snapshot |
| `CHAVE` | `VARCHAR(64) CHARACTER SET ASCII` | Sim | Identidade lógica do snapshot, com unicidade |
| `SHA256` | `VARCHAR(64) CHARACTER SET ASCII` | Sim | SHA-256 do documento de relatório em JSON canônico |
| `TIPO` | `VARCHAR(10) CHARACTER SET ASCII` | Sim | `diario` ou `semanal` |
| `INICIO` | `TIMESTAMP` | Sim | Início inclusivo da cobertura, UTC |
| `FIM` | `TIMESTAMP` | Sim | Limite exclusivo da cobertura, UTC |
| `COMPLETO` | `SMALLINT` | Sim | `1` completo; `0` incompleto |
| `DTHRGERACAO` | `TIMESTAMP` | Sim | Instante de geração informado pelo bot, UTC |
| `DTHRCRIACAO` | `TIMESTAMP` | Sim | Instante de recebimento pelo MDW, UTC |
| `DOCUMENTO` | `BLOB SUB_TYPE TEXT CHARACTER SET UTF8` | Sim | Envelope JSON completo, incluindo `summary` público e `details` privado |

Constraints: `PK_CHATFASTRELATORIO`, `UNQ_CFRELATORIO_CHAVE(CHAVE)`, `CK_CFRELATORIO_COMPLETO CHECK(COMPLETO IN (0,1))`, `CK_CFRELATORIO_TIPO CHECK(TIPO IN ('diario','semanal'))` e `CK_CFRELATORIO_PERIODO CHECK(FIM>INICIO)`.

Índice adicional: `IX_CFRELATORIO_TIPO_GERACAO(TIPO,DTHRGERACAO)`. PKs e uniques já possuem índices associados; não criar cópias redundantes. A consulta pública também ordena por `FIM`, geração e chave; revisar o plano com o volume real antes de propor índice novo.

Identidade: SHA-256 do texto `report\n{type}\n{batchId}`. `batchId` e `schemaVersion` ficam dentro de `DOCUMENTO`, não em colunas separadas. Não há FK de relatório para `USUARIO`, respostas ou importações. Mesmo assim, `details` contém dados restritos: **o BLOB inteiro nunca é um documento público**.

A seleção pública usa apenas `COMPLETO=1`, tipo/data solicitados, fim do período mais recente e depois geração/chave. Documento atrasado de período antigo não substitui o período atual. Incompleto mais recente mantém o último completo e sinaliza desatualização. Correções geram novo `batchId`; não há endpoint de edição/exclusão de snapshots.

### Regras transversais de armazenamento e acesso

- São três identities BIGINT independentes (resposta, importação, relatório), e uma PK composta no item. Preservar todas em eventual cópia.
- `TIMESTAMP` não armazena automaticamente um fuso: o contrato da aplicação é UTC, com conversão de apresentação para Brasília. Não aplicar deslocamento outra vez aos dados copiados.
- Texto internacional novo usa UTF8 no DDL, sem mudar o charset global do banco legado. Conservar conteúdo, charset/subtipo dos BLOBs e hashes. O esquema não implementa criptografia dos BLOBs por campo; acessos, backups e transporte devem seguir política de segurança.
- Escrita de uma resposta e seus itens é transacional. Confirmação XLSX grava lote, respostas e itens na mesma transação. Cada relatório é transacional. Não existe transação distribuída já implementada entre dois bancos.
- A importação registrada não guarda o XLSX binário: guarda hash/nome, linhas e JSON normalizado. Se for necessário arquivar o original, definir armazenamento privado e retenção separados.
- Não existe expurgo automático ou processo de movimentação entre bancos. Geração do DDL não concede autorização para aplicá-lo ou remover dados.
- O acesso administrativo e a identidade do usuário pertencem ao sistema central; não confundir `ReportsKey`/`ResponsesKey` com identidade de usuário ou senha Firebird.

### Consultas de conferência — somente leitura

Executar no banco correto, sem exportar BLOBs privados em logs. Contagens em bases grandes podem exigir janela apropriada.

```sql
SELECT 'CHATFASTIMPORTACAO' AS TABELA, COUNT(*) AS QUANTIDADE FROM CHATFASTIMPORTACAO
UNION ALL
SELECT 'CHATFASTRESPOSTA', COUNT(*) FROM CHATFASTRESPOSTA
UNION ALL
SELECT 'CHATFASTRESPOSTAITEM', COUNT(*) FROM CHATFASTRESPOSTAITEM
UNION ALL
SELECT 'CHATFASTRELATORIO', COUNT(*) FROM CHATFASTRELATORIO;

SELECT TIPO, COMPLETO, COUNT(*) AS QUANTIDADE
FROM CHATFASTRELATORIO
GROUP BY TIPO, COMPLETO;
```

Conferência completa deve incluir PKs/FKs, hashes, datas, documentos e identities, não apenas contagem. O migrador valida estrutura básica/nulabilidade/comprimentos e existência de constraints/índice próprios; isso não substitui reconciliação de dados nem auditoria completa do destino.

## Futura transferência para outro Firebird — planejamento, não implementação

### O que não funciona por simples configuração

Hoje o módulo usa a conexão principal. Não existe seção `ChatfastFirebird` funcional. **Não criar essa seção esperando que a aplicação já a utilize**, nem trocar a conexão principal para o novo banco. Também não basta copiar as quatro tabelas: a FK para `USUARIO` e a consulta de administrador ativo precisam ser consideradas.

O bot e o Power Automate não devem acessar Firebird diretamente; os produtores continuam chamando a mesma API. Uma separação de armazenamento não precisa mudar seus contratos, URLs, chaves lógicas de evento ou `batchId`.

### Desenho necessário antes de executar

1. Definir se o objetivo é arquivamento histórico, transferência integral ou operação permanente em banco dedicado. Evitar dividir respostas/lotes/itens entre bancos enquanto mantêm FKs entre si. Definir política de consulta pública/gestão sobre histórico.
2. Introduzir uma fábrica/repositório dedicado para armazenamento ChatFast, sem substituir globalmente `IFirebirdConnectionFactory`. Manter autenticação e verificação de administrador no banco central. Rever inicialização de sessão/auditoria e outros hooks da fábrica atual antes de reaproveitá-los no destino.
3. Resolver explicitamente a identidade do autor da importação. A FK atual é local ao banco: não aponta automaticamente para o `USUARIO` de outro arquivo/servidor. Alternativas a avaliar: banco de destino já possuir cadastro central compatível; referência de ator própria com mapeamento e integridade definidos; ou revisão autorizada dessa dependência, preservando a identidade/proveniência original e validação pela aplicação. **Não copiar senhas/tokens de usuários nem criar tabela `USUARIO` fictícia apenas para o DDL passar.** Nenhuma alternativa foi implementada nesta entrega.
4. Revisar migrador e scripts versionados para o alvo dedicado. O `--migrate-chatfast` atual abre o banco principal e depende de `USUARIO`; não é um instalador pronto para um banco dedicado vazio. Não executar o migrador geral do sistema contra o destino só para criar a dependência, pois ele envolve outros módulos.
5. Definir comportamento de falha entre banco central (autorização) e banco do módulo (dados). Falhar fechado quando não for possível validar administrador. Não prometer atomicidade distribuída: garantir transações locais das quatro tabelas e um protocolo de corte/reconciliação.
6. Testar em banco temporário isolado, inclusive instalação limpa, reexecução, dados existentes, dependências ausentes, repetição concorrente, autorização revogada e documentos 1.0/1.1. Verificar versão Firebird/ODS/driver/charset. A validação runtime realizada nesta entrega foi Firebird 5; a tentativa 4 ficou bloqueada pelo runtime local e não comprova compatibilidade operacional 4.

### Plano de movimentação a elaborar e autorizar

Este checklist não autoriza executar uma transferência e não contém scripts destrutivos prontos.

1. Identificar exatamente origem/destino, responsável, versões, caminhos/aliases, permissões e janela. Inventariar volumes, identities, FKs, índices, contratos existentes e política de retenção. Fazer backup consistente e testar restauração; não copiar arquivo FDB aberto como procedimento de backup.
2. Preparar esquema do destino e dependência de ator após decisão arquitetural. Não alterar schemas legados fora do escopo. Instalar constraints/índices com nomes explícitos e preservar UTF8/ASCII. Não existe `RENAME TABLE` no Firebird.
3. Combinar corte de gravação somente do módulo/entregas envolvidas, sem `iisreset` ou parada global. Preparar fila/outbox do produtor para retenção de snapshots durante a janela, sem ativá-la agora; conservar mesmo `batchId` e corpo nas tentativas. Não modificar o fluxo de cartões/e-mails para testar uma cópia de dados.
4. Copiar preservando PKs, relações, `CHAVE`, `SHA256`, datas UTC, origem, lote/linha e texto dos documentos. Não regenerar IDs, não refazer canonicalização de snapshots e não converter desconhecidos para zero. Ordem lógica após dependência de ator: importações → respostas → itens; relatórios podem ser copiados independentemente. Na estratégia futura de DDL, respeitar a ordem real de criação das FKs.
5. Reposicionar os geradores/identities do destino acima dos maiores códigos copiados, com procedimento validado para a versão Firebird utilizada. Testar um novo recebimento e nova importação sem colisão; preservar PK composta/ordem dos itens.
6. Reconciliar contagens totais e por origem/tipo/completude, faixas de chaves/datas, vínculos e hashes. Confrontar os documentos, verificando que o hash de resposta não é o hash do BLOB inteiro. Demonstrar que o relatório público mais recente, o filtro de data e os detalhes privados são iguais nos dois lados.
7. Testar repetição do mesmo evento/lote (duplicado, sem nova linha), alteração sob mesma identidade (409), correção com novo lote, documento incompleto e reenvio atrasado. Testar anônimo/usuário comum/administrador desativado, dados privados ausentes no público e restauração de falhas transacionais.
8. Só após aceite, apontar a fábrica dedicada para o destino e retomar as escritas do módulo. Não operar escrita dupla informal: ela exige desenho próprio de consistência e recuperação. Fazer um envio supervisionado autorizado e observar erros/contagens antes de liberar automação.
9. Manter origem preservada e sem novas escritas durante a janela de retorno. Se for preciso voltar, reconciliar antes os registros que chegaram ao destino após o corte; não basta apontar de volta e perder dados novos. Backup e preservação da origem são parte do plano de recuperação.
10. Exclusão de tabelas/arquivos antigos somente depois da retenção/aceite e de **nova autorização específica**. Não remover a origem como parte automática da cópia ou do rollback de código.

### Dependências para transportar com o histórico

Além das quatro tabelas: DDL versionado, contratos e códigos de `summary`, mapeamento de atores que vier a ser aprovado, configuração privada da conexão dedicada, políticas de autenticação/segurança, procedimento de backup/restauração e relatório de reconciliação. Não transportar arquivos de segredos nos pacotes de dados. Demos locais não são histórico oficial do banco e não devem entrar na cópia por acidente.

## Preparação da integração automática — 22/11/2026

Nayhan solicitou encaminhar o contrato 1.1 ao agente **Medware - Chatfast Monitoramento** e preparar a integração com o Grok bot, **sem ativar o envio automático**. O contrato e referências de código foram encaminhados com essa restrição. O usuário confirmou visualização local da demonstração real; isso não significa deploy/aceite de produção.

Preparação solicitada: mapeamento dos agregados atuais, produtor JSON 1.1, chamada/adaptador desabilitado por padrão, configuração futura da `ReportsKey` por canal privado, armazenamento estável do snapshot para retries, testes sintéticos e roteiro do primeiro envio manual supervisionado. Não instalar cron, alterar horários/estados/fluxos, enviar cartões/e-mails/relatórios reais nem desativar páginas antigas nesta etapa. Se o agente não tiver canal direto ao bot, deve entregar pacote/mensagem para encaminhamento manual e registrar essa limitação, sem alegar envio ao bot.

Critérios antes da ativação: versão compatível publicada e validada no servidor correto; segredo de produção provisionado sem exposição; JSON validado com definições/limitações; uma transmissão real manual separadamente autorizada; conferência do snapshot e da projeção pública/gestão; repetição idempotente e recuperação de erro; aceite explícito para acrescentar a chamada às rotinas diária/semanal existentes. O armazenamento permanece no REFERENCIAS até uma separação futura ser aprovada e implementada.

Retorno do agente responsável em 22/11/2026: preparou um pacote em `C:\Users\Nayhan.MEDWARE\Documents\ChatGPT\Controle GrokBot\output\chatfast-integracao-1.1.zip`, com instruções/mapeamento, referências do contrato, preparação de snapshots e envio manual bloqueado por padrão. Relatou dois testes com transporte simulado aprovados, sem chamadas reais. **Não instalou ou ativou nada no bot e não dispõe de canal direto identificado para encaminhá-lo**; o pacote depende de repasse pelo usuário. A entrega é preparação, não comprovação de adaptação às fontes operacionais atuais nem aceite de produção. Permanecem pendentes o adaptador sobre essas fontes, validação integral dos JSON, deploy, provisionamento privado da chave e autorização do primeiro envio supervisionado. O guia neste repositório é a referência documental atual; cópias dentro de pacotes externos são snapshots e podem precisar ser atualizadas.
