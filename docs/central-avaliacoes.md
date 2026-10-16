# Central de Avaliações de Scripts

## Preparação e implantação

A migração é aditiva e não altera scripts, versões ou avaliações existentes. Execute antes de habilitar os endpoints:

O pipeline de produção executa automaticamente `--migrate-database` antes de iniciar a API nova. Esse comando inclui avaliações e atualização do catálogo de permissões. Não é necessário executar migrações manualmente após um deploy concluído. O comando abaixo continua disponível para preparação manual de um ambiente:

```powershell
dotnet MdwConteudos.Api.dll --migrate-evaluations
```

Execute com o mesmo ambiente, diretório de conteúdo e configuração da instância de destino. O comando aplica o SQL incorporado de `backend/sql/script-avaliacoes.sql` em uma transação e registra as permissões. Pode ser executado novamente. Sem as duas tabelas, os endpoints respondem `503`; a aplicação não cria esse schema silenciosamente no startup.

Após a migração, publique/reinicie a API e o frontend. A Central fica em `/avaliacoes` e também em Visualizar Conteúdos. Administradores têm acesso; para outros perfis conceda `avaliacoes.visualizar`, `avaliacoes.acompanhar` e/ou `avaliacoes.moderar` nas configurações de permissões. Atualize a sessão web após alterar permissões. Não existe exclusão definitiva nem edição interna de notas/comentários.

## Autenticação e identidade

Use o JWT parceiro obtido no endpoint de token já utilizado pela integração, em `Authorization: Bearer <token>`. Todas as rotas abaixo exigem autenticação. Os aliases `/api/v1` têm o mesmo contrato e também aceitam JWT web com permissões: leitura exige `avaliacoes.visualizar`, envio exige `avaliacoes.acompanhar`. O prefixo `/api/web` aceita apenas a autenticação web.

O token parceiro autentica o sistema integrador, não o avaliador. O backend externo deve validar a sessão do usuário e determinar origem, clínica e usuário. Não exponha o token parceiro no navegador. Os identificadores são strings estáveis, não nomes ou e-mails. `origem` é normalizada para minúsculas; cliente e usuário preservam maiúsculas/minúsculas. Limites: origem 40, cliente/usuário 80, nomes 150 caracteres.

A chave lógica é origem + clínica + usuário + script + versão. Para compatibilidade com os limites de índices UTF-8 do Firebird, a identidade externa é indexada por SHA-256, e os identificadores originais também são comparados. `VERSAO_CHAVE=0` representa `CODVERSAO IS NULL`. Não crie registros diretamente nas tabelas.

## Selecionar o modelo avaliado

`GET /apiconteudos/v1/scripts` agora inclui `codversao`, mesmo com `incluir_arquivos=0`. O campo é o ID da versão ativa apresentada ou `null` para o principal. Capture esse valor junto ao modelo utilizado e devolva-o na avaliação; não volte a consultar a última versão no momento de enviar. Uma versão histórica é aceita se pertencer ao script. O recebimento não substitui a versão informada.

## Criar ou revisar

`PUT /apiconteudos/v1/scripts/20/avaliacoes`

```json
{
  "codversao": 30,
  "origem": "portal-modelos",
  "cliente_id": "clinica-123",
  "usuario_id": "usuario-456",
  "nome_avaliador": "João Silva",
  "nome_clinica": "Clínica Exemplo",
  "nota_geral": 4,
  "recomendacao": "sim",
  "facilidade_uso": 5,
  "qualidade_conteudo": 4,
  "completude": null,
  "aparencia": 5,
  "utilidade_clinica": 4,
  "titulo": "Bom modelo para a rotina",
  "comentario": "Conteúdo útil e fácil de usar."
}
```

`codversao` deve estar presente, inclusive como `null` para o principal; `0` não é aceito no PUT. Nota geral é obrigatória, inteira entre 1 e 5; recomendação é `sim`, `talvez` ou `nao`. Critérios são opcionais, inteiros de 1 a 5 ou `null`. Título e comentário são opcionais, até 150 e 1.000 caracteres. Textos são preservados em UTF-8 e espaços nas extremidades são removidos. Comentários são texto simples, não HTML.

Resposta: `201` na criação; `200` na revisão ou reenvio idêntico. Exemplo resumido:

```json
{
  "success": true,
  "data": {
    "id": 42,
    "criada": true,
    "atualizada": false,
    "avaliacao": {
      "id": 42,
      "codScriptLaudo": 20,
      "codVersao": 30,
      "numeroVersao": "1.0",
      "nomeAvaliador": "João Silva",
      "notaGeral": 4,
      "recomendacao": "sim"
    }
  }
}
```

O objeto `avaliacao` completo inclui os cinco critérios, título, comentário e datas `criadoEm`/`atualizadoEm` em UTC. Não inclui clínica, identidade externa, situação, visibilidade nem anotações. Repetir o payload atual não muda as datas nem cria evento. Para revisar, envie novamente o formulário completo com a mesma identidade; campos opcionais omitidos são considerados `null`. Alterar clínica/usuário/versão identifica outra avaliação.

## Consultas externas

```text
GET /apiconteudos/v1/scripts/20/avaliacoes?page=1&pageSize=25
GET /apiconteudos/v1/scripts/20/avaliacoes?codversao=30&page=1&pageSize=25
GET /apiconteudos/v1/scripts/20/avaliacoes/resumo?codversao=30
GET /apiconteudos/v1/scripts/20/avaliacoes/minha?codversao=30&origem=portal-modelos&cliente_id=clinica-123&usuario_id=usuario-456
```

Sem `codversao`, listagem e resumo consolidam todas as versões. Nas consultas, `codversao=0` seleciona apenas o principal. Em `minha`, o parâmetro é obrigatório; use `0` para o principal. Escape os parâmetros com um construtor de URL.

A listagem retorna `success`, `data`, `page`, `pageSize`, `totalItems`, `totalPages`, ordenada por atualização e ID decrescentes. Página começa em 1; tamanho entre 1 e 100. Retorna nome do avaliador, mas não clínica nem identificadores. Somente avaliações visíveis entram na listagem e no resumo.

`minha` permite ao backend externo recuperar o formulário atual, inclusive se a avaliação estiver oculta; retorna `404` quando ausente. Não revela motivos de ocultação ou histórico. O backend parceiro deve conferir que a identidade consultada pertence à sessão atual.

Resumo de exemplo:

```json
{
  "success": true,
  "data": {
    "quantidade": 1,
    "mediaGeral": 4,
    "facilidadeUso": 5,
    "qualidadeConteudo": 4,
    "completude": null,
    "aparencia": 5,
    "utilidadeClinica": 4,
    "distribuicaoNotas": { "1": 0, "2": 0, "3": 0, "4": 1, "5": 0 },
    "recomendacoes": { "sim": 1, "talvez": 0, "nao": 0 }
  }
}
```

Critérios não preenchidos não entram em suas médias. Sem respostas, as médias são `null` e os contadores são zero. A nota geral é a informada pelo usuário, não a média dos critérios. Uma avaliação por usuário/versão contribui uma vez; no consolidado, avaliações do mesmo usuário para versões diferentes contam separadamente.

## Gestão interna

```text
GET  /api/web/avaliacoes
GET  /api/web/avaliacoes/resumo
GET  /api/web/avaliacoes/{id}
PUT  /api/web/avaliacoes/{id}/acompanhamento  { "situacao": "em_analise" }
POST /api/web/avaliacoes/{id}/anotacoes      { "texto": "Conferir sugestão com a equipe." }
PUT  /api/web/avaliacoes/{id}/visibilidade  { "visivel": false, "justificativa": "Em revisão pela equipe." }
```

Filtros: `script`, `versao` (`0` para principal), `sistema`, `pacote`, `origem`, `clinica` (trecho do nome), `inicio`, `fim` (`YYYY-MM-DD`, inclusivos), `nota`, `recomendacao`, `situacao`, `visivel`, `page` e `pageSize`. O período considera a última atualização externa em UTC. O resumo considera os mesmos filtros e ignora paginação. Sem filtro de visibilidade, a Central inclui publicadas e ocultas.

Situações: `nova`, `em_analise`, `concluida`. Anotações e justificativas exigem texto não vazio de até 2.000 caracteres. Revisão externa volta à situação `nova` e preserva ocultação. Restaurar a publicação reinclui a avaliação nas médias. Toda ação gera evento com autor, data, observação e snapshots anterior/posterior. Os snapshots preservam notas, textos e nomes históricos; não são retornados à integração externa.

## Erros e validação

`400`: validação, versão incompatível ou justificativa vazia. `401`: token ausente/inválido. `403`: permissão web insuficiente. `404`: script/avaliação inexistente. `503`: banco ainda não migrado ou autenticação parceira não configurada. Falhas de validação MVC usam Problem Details; erros de negócio incluem `success=false`, `error` e `message`. Em falhas transitórias, reenvie o mesmo PUT; não gere outra identidade.

Os contratos e exemplos também estão no OpenAPI de parceiros e da API interna. Os testes usam bancos temporários Firebird embedded e não criam avaliações de demonstração no banco do usuário:

```powershell
$env:PUBLICACAO_TEST_FBCLIENT = 'C:/Program Files/Firebird/Firebird-5.0.2/fbclient.dll'
dotnet test backend/ConversorHtml.Tests/ConversorHtml.Tests.csproj --artifacts-path .codex-build/avaliacoes --filter FullyQualifiedName~Avaliacao
```
