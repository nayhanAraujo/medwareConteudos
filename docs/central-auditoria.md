# Central de Auditoria

A Central fica em **Biblioteca → Configurações → Central de Auditoria**, na rota `/biblioteca/configuracoes/auditoria`. Somente administradores ativos podem consultar a API. O card e a rota do frontend também restringem esse acesso, mas a autorização efetiva é feita no servidor.

## Consulta

O período inicial corresponde aos últimos sete dias. Os horários são exibidos no fuso do navegador; a API e o banco usam UTC. O início do filtro é inclusivo e o fim exclusivo. Filtros ficam na URL para compartilhar a consulta e atualizar a página. A navegação mostra 50 eventos por página em ordem decrescente de data e identificador; use a próxima página pelo cursor retornado, sem calcular números de página sobre um histórico em crescimento.

O detalhe reúne autor, registro, origem, código de correlação, requisição sem query string e diferenças dos campos permitidos. Não há edição, exclusão ou restauração pela Central. O histórico começa na implantação; não reconstrói alterações passadas nem substitui os históricos específicos de cada módulo.

## Contratos internos

- `GET /api/web/auditoria`: filtros opcionais `from`, `to`, `actorId`, `module`, `action`, `result`, `entityType`, `entityId`, `correlationId` e `cursor`; `take` de 1 a 100, padrão 50. Resposta `{ success, data, nextCursor }`. IDs bigint são strings no JSON para não perder precisão no JavaScript.
- `GET /api/web/auditoria/{id}`: resposta `{ success, data }`, incluindo `changes: [{ field, before, after }]` e o contexto permitido.
- `GET /api/web/auditoria/filtros`: catálogo `{ success, data: { modules, actions, results } }`.

Não autenticado retorna 401; usuário comum ou administrador inativo retorna 403. Eventos de download com resultado `iniciado` indicam disponibilização pelo servidor, não comprovação de recebimento ou salvamento no navegador.

## Proteção dos dados

Os campos auditados são permitidos explicitamente por entidade. Nunca registrar corpo HTTP bruto, senhas e seus hashes, tokens, cookies, chaves, binários, JSON completo de modelos ou dados clínicos. Arquivos são representados por metadados permitidos. Valores são exibidos como texto, sem interpretar HTML.

Autor e registro têm identificadores e nomes históricos preservados, mesmo após exclusão. Atores não autenticados ou automáticos são identificados como tais; não atribuir a um usuário fictício nem confiar em `criado_por` enviado por formulário. O IP só usa encaminhamento dos proxies confiáveis já configurados.

## Cobertura

O catálogo revisável está em `backend/MdwConteudos.Api/Modules/Auditoria/AuditEntities.cs`. A cobertura depende das tabelas presentes na versão do banco. Tabelas opcionais inexistentes não são criadas apenas para auditoria; tabelas presentes precisam ter identificadores compatíveis.

| Domínio | Registros instrumentados |
| --- | --- |
| Usuários e permissões | Usuários, permissões de perfil e exceções de usuário. |
| Scripts | Cadastro, versões, vínculos com variáveis, anexos e MRDs. |
| Painéis | Cadastro, versões, pacotes comerciais, imagens, arquivos, configurações API/Power BI, métricas, dimensões e fontes de dados. |
| Relatórios | Cadastro, módulos e sistemas vinculados, colunas, validações e filtros. |
| Variáveis e fórmulas | Cadastro, alternativas, nomes clínicos, classificações, códigos, especialidades, vínculos e equações. |
| Referências e cadastros | Referências, autores vinculados, anexos, normalidades, pacotes, unidades, linguagens e demais entidades explicitadas no catálogo. |
| Segurança e arquivos | Login, alteração/redefinição de senha, acesso negado, falhas e disponibilização de downloads/exports das rotas instrumentadas. |

Conteúdos excluídos da comparação textual podem ter somente resumo criptográfico e tamanho registrados para detectar mudanças sem copiar o conteúdo. Isso não significa que todo arquivo externo no disco tenha seu SHA256 calculado: a cobertura de arquivo depende dos metadados ou bytes disponíveis na entidade instrumentada. Não há inspeção de diretórios nem captura de alterações manuais em arquivos.

## Implantação e manutenção

O executor `--migrate-database` deve criar a estrutura de auditoria antes de iniciar a API nova. O pipeline executa esse comando no novo release com o JSON de produção. Faça backup consistente antes de aprovar o deploy, conforme o procedimento de produção.

A inicialização valida a estrutura em todos os ambientes e recusa banco sem a migração de auditoria. Para testar localmente, compile a versão nova e execute o migrador com o ambiente e as conexões da cópia pretendida antes de iniciar a API. Confira o destino efetivo; não reutilize configurações de produção para testar. Esta implementação não executa a migração automaticamente nos bancos locais existentes.

Na cópia de homologação já preparada, a partir da raiz do repositório:

```powershell
dotnet build backend\MdwConteudos.Api\MdwConteudos.Api.csproj
pwsh -NoProfile -File tools\homologacao\Start-Homologacao.ps1 -IntegrationConfirmed -MigrateDatabase
pwsh -NoProfile -File tools\homologacao\Start-Homologacao.ps1 -IntegrationConfirmed
```

Execute sem outra instância da API de homologação aberta. A opção `-MigrateDatabase` usa o mesmo isolamento e configuração verificada do iniciador; executa as migrações registradas e encerra, sem iniciar o servidor. Não substitui o preparo das cópias dos bancos e arquivos.

Alterações de negócio no REFERENCIAS e seus eventos de sucesso compartilham conexão e transação. A captura usa triggers nas entidades permitidas, habilitados pelo contexto confiável da conexão da API; não depende de valores enviados pelo formulário. O contexto é limpo e reconstruído em cada abertura de conexão para evitar reutilização de identidade pelo pool. Uma falha de gravação da auditoria impede o commit da alteração. Falhas e acessos negados são tratados separadamente, sem substituir o erro original quando a auditoria estiver indisponível. O rollback do deploy é de código e não apaga eventos ou reverte migrações confirmadas.

Sem limpeza automática nesta versão. A aplicação não disponibiliza exclusão de eventos; isso não significa proteção contra um administrador que controla diretamente o banco. Monitorar crescimento, espaço livre e desempenho antes de definir uma política de arquivamento.

Mudanças feitas por SQL direto, pelo Python e por aplicações externas não instrumentadas não são capturadas. O Assistente interno e as sessões de voz/Studio ficam para uma fase posterior. Ao adicionar ações, atualizar o catálogo explícito de entidades e campos permitidos, garantir uso da conexão instrumentada e da transação da ação inteira, e testar commit, rollback, repetição e proteção de segredos. Eventos de segurança que não representam DML usam o gravador próprio. Eventos da mesma operação podem corresponder a registros distintos (por exemplo, um script e seus vínculos); o identificador de operação permite agrupá-los.

## Validação

Execute `npm run test:auditoria` em `frontend/nuxt-app` para verificar períodos, filtros, UTC, IDs bigint, cursores e respostas fora de ordem na store. Esse comando também integra a geração do pacote de produção. Os testes backend de auditoria devem usar Firebird temporário, nunca os bancos configurados da aplicação. A verificação visual pode usar respostas HTTP simuladas para não gerar alterações nos bancos reais.

Os testes `AuditFirebirdTests` criam e removem bancos Embedded temporários. Informe `PUBLICACAO_TEST_FBCLIENT` com o caminho da biblioteca cliente Firebird compatível com Embedded no computador de testes e execute `dotnet test backend/ConversorHtml.Tests/ConversorHtml.Tests.csproj --filter FullyQualifiedName~AuditFirebirdTests`. Sem essa variável os testes de integração são ignorados, não considerados aprovados contra Firebird. Não aponte esses testes para um banco existente.
