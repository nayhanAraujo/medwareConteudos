# Clientes ausentes: resolução local/Admin e importação confirmada

Integração para o bot **Gerar Orçamento**. O bot consulta identidade mínima, mas não cadastra clientes. Uma pessoa autorizada importa ou vincula o cadastro oficial; a mensagem do Outlook fica pendente de identificação até existir um cliente local confiável. O restante do fluxo de orçamento, aprovação independente e autorização de envio permanece inalterado.

## Estado da validação externa

O caminho informado pelo usuário é **`/cliente/{codigoAdmin}`**, no singular. A integração não foi autenticada na API real nesta entrega: não há conta técnica `AdminDirectory` configurada nos arquivos locais verificados. Os contratos de resposta de `Auth/login` e `cliente/{codigoAdmin}` são configuráveis e foram validados com HTTP simulado, não com produção. **Não marcar `ContractConfirmed` como verdadeiro antes de conferir os campos/token/estado com a conta técnica.** Nenhuma credencial pessoal foi copiada para código/documentação.

## Fluxo

1. O bot usa código Admin confiável. Nome produz somente sugestões locais, mesmo quando retorna um único nome. Documento informado não restringe a consulta por código.
2. O backend consulta o Admin e confere o código retornado. CPF/CNPJ ausente ou inválido não bloqueia a exibição: impede somente a importação. O bot recebe somente código, nome, CPF/CNPJ, estado e orientação; não recebe token Admin, contatos, mensalidade ou contratos.
3. Se o cliente oficial estiver ausente e não houver conflito local, a resposta indica `encontrado_admin`. O bot mantém a mensagem pendente e solicita ação humana, sem utilizar o cliente padrão e sem gerar outro código.
4. A pessoa acessa **Orçamentos → Resolver cliente ausente no Admin** ou **Configurações → Clientes do Admin**, consulta o código, confere os dados e registra justificativa/confirmação.
5. A consulta humana gera um identificador vinculado ao usuário, válido por 15 minutos. O POST de confirmação contém apenas esse identificador, a confirmação e o motivo: código/nome/CNPJ editáveis não são aceitos como fonte de importação.
6. O backend consulta novamente o Admin. Mudanças desde a pré-visualização, conflito de identidade ou expiração impedem a gravação.
7. O cadastro é criado com `CLIENTES.CODCLIENTE = codigoAdmin` e nome oficial, ou recebe somente um vínculo comprovado se já existia localmente. Uma transação guarda vínculo, consumo da consulta e auditoria. Mensalidade, condições comerciais, destinatário e dados de `CLIENTECOMERCIAL` não são preenchidos ou substituídos.
8. Após o cliente estar disponível, o bot pode retomar a importação do e-mail. A confirmação humana do escopo/destinatário continua obrigatória; importar cliente não cria nem aprova orçamento e não envia e-mail.

## Rotas e permissões

Relativas a `/api/web/orcamentos`; no IIS externo, usar `/api-dotnet/api/web/orcamentos`. Autenticação JWT web.

| Método/rota | Permissão | Observação |
|---|---|---|
| `POST /integracoes/emails/clientes/resolver` | `orcamentos.consultar_clientes_admin` | Leitura; não emite autorização de importação |
| `POST /clientes/admin/consultar` | `orcamentos.visualizar` + `orcamentos.importar_cliente_admin` | Pré-visualização humana com consulta de 15 minutos |
| `POST /clientes/importar-admin` | As mesmas permissões humanas | Reconferência oficial e persistência transacional |

Adicionar **somente** `consultar_clientes_admin` à conta não administradora `bot.triagem.orcamentos` se a gestão autorizar a consulta. Manter as permissões existentes `importar_email` e `enviar_email_confirmado`. Não conceder `visualizar`, `importar_cliente_admin`, `elaborar`, `aprovar`, CRUD de Painéis ou configuração ao bot. Permissões novas não são atribuídas automaticamente a usuários comuns.

Resolução:

```json
{"codigoAdmin":13687,"cnpj":null,"name":null}
```

Também aceita CPF/CNPJ sem código para consultar vínculos locais, ou `name` com ao menos três caracteres para sugestões. **Não foi presumida uma rota de pesquisa remota por CPF/CNPJ/nome**: seu contrato ainda não foi fornecido. Se não houver código e o CPF/CNPJ não resolver localmente, pedir identificação humana/código Admin em vez de inventar uma rota ou varrer todos os clientes.

Retorno: `{success:true,data:{state,message,candidate,localId,canImport,suggestions}}`. `canImport` indica elegibilidade, não permissão para o bot cadastrar. Estados:

- `encontrado_local`: vínculo verificado, ou cadastro legado que pode ser vinculado por um humano quando código, nome e CPF/CNPJ comercial independente corresponderem ao Admin. Conferir `canImport`/mensagem. Consulta local por CPF/CNPJ não verifica a atividade atual no Admin em tempo real.
- `encontrado_admin`: ausente localmente, elegível à confirmação humana.
- `revisao_necessaria`: nome ou CPF/CNPJ local sem identidade Admin suficientemente comprovada, ou ambiguidade.
- `conflito`: código ocupado, documentos divergentes, mesmo CPF/CNPJ em outro código, nome igual em outro código, cliente padrão, tamanho incompatível etc. Não sobrescrever, fundir, renumerar ou tentar outro código artificial.
- `inativo`: cliente inativo no Admin; não cadastrar ou reativar automaticamente.
- `nao_encontrado`: pedir informação adicional; manter o e-mail pendente.

Consulta humana: `{"codigoAdmin":13687,"cnpj":null}`. Retorna `resolution`, `consultationId` e `expiresAtUtc`; consultas bloqueadas não emitem identificador de importação.

Confirmação humana:

```json
{"consultationId":"11111111-1111-4111-8111-111111111111","confirm":true,"reason":"Código e CNPJ conferidos pelo responsável."}
```

Retorna `{success:true,data:{id,codigoAdmin,created}}`. Repetir a mesma consulta consumida retorna o cliente sem duplicar gravação/auditoria, se o vínculo continua íntegro. Consulta de outro usuário retorna 403; consulta expirada, alterada ou conflito retorna 409. Colisões concorrentes são recusadas por PK/unique/transação; reconferir/repetir a mesma consulta é seguro. Falhas técnicas retornam 502/503 sem corpo remoto, senha ou token.

## Configuração segura do backend

Exemplo ilustrativo para o arquivo **local não versionado do ambiente correto**. Não copiar credenciais pessoais. Reiniciar somente a API envolvida, sob controle do operador, após configurar e validar o contrato:

```json
"AdminDirectory": {
  "Enabled": false,
  "ContractConfirmed": false,
  "BaseUrl": "https://apiv2.medware.com.br/",
  "LoginPath": "Auth/login",
  "ClientPath": "cliente/{codigoAdmin}",
  "Username": "CONTA_TECNICA_A_CONFIGURAR",
  "Password": "SEGREDO_FORA_DO_REPOSITORIO",
  "TokenPointer": "/token",
  "ClientPointer": "",
  "CodeField": "codigoAdmin",
  "NameField": "nome",
  "CnpjField": "cnpj",
  "ActiveField": "ativo",
  "ActiveValues": ["true", "1"],
  "InactiveValues": ["false", "0"]
}
```

Os mapeamentos acima são **exemplos, não confirmação da resposta remota**. Se token/cadastro estiverem em envelopes, configurar ponteiros JSON, como `/data/token` e `/data`. Os nomes dos campos são exatos. Código divergente, payload excessivo ou estrutura incompatível bloqueiam a consulta. Documento inválido ou estado não reconhecido não impedem exibição, mas não autorizam importação nem presunção de cliente ativo. Login envia `usuario`, `senha` e `registroAplicacao: "ADMIN"`. Usa Bearer apenas no GET fixo configurado; não aceita URL do bot, não segue redirecionamentos, não compartilha token em cabeçalhos globais e limita a operação HTTP a 15 segundos/1 MiB por resposta. Sem retentativa automática. `Enabled` e `ContractConfirmed` permanecem falsos por padrão.

## Esquema e compatibilidade

Migração aditiva integrada a `--migrate-database`; SQL em `docs/sql/scripts/add-clienteadmin.sql`:

- `CLIENTEADMIN`: satélite 1:1 de `CLIENTES`, PK/FK `CODCLIENTE INTEGER`, CNPJ normalizado único, snapshot oficial UTF8 BLOB, autor e criação UTC. Não usa `_` no nome de entidade.
- `CLIENTEADMINCONSULTA`: consulta humana com UUID `CODCLIENTEADMINCONSULTA`, autor, snapshot/fingerprint/justificativa em BLOB, criação/expiração UTC e flag `CONSUMIDA` 0/1. Não autoriza importação por si só: a permissão humana é conferida no endpoint. Autoria histórica segue o padrão do módulo, sem FK de usuário. `CK_CLIADMCONS_CONSUMIDA` é abreviação documentada para respeitar 31 caracteres.

O DDL operacional de `CLIENTES` não foi obtido nem alterado nesta entrega. O código inspeciona metadados efetivos antes de inserir: exige `CODCLIENTE INTEGER`, respeita o tamanho de `NOME` e recusa campos obrigatórios extras sem default ainda não mapeados. Para identity `GENERATED ALWAYS`, usa `OVERRIDING SYSTEM VALUE`; para `BY DEFAULT`/PK comum, fornece o código explicitamente. Confere `RETURNING CODCLIENTE` e o registro gravado; se um trigger mudar código/nome/estado, desfaz a transação. Não remove triggers nem reajusta geradores. [Referência Firebird](https://www.firebirdsql.org/file/documentation/chunk/en/refdocs/fblangref40/fblangref40-dml-insert.html).

**Códigos legados gerados localmente podem coincidir com códigos Admin sem representar o mesmo cliente.** Nome/código iguais não são prova suficiente. Não há adoção automática de códigos legados sem CPF/CNPJ comercial correspondente ou vínculo Admin já comprovado. Campos comerciais existentes nunca são sobrescritos; cadastros vinculados não podem ser excluídos pelo formulário genérico. Homônimos e múltiplos códigos para um mesmo CPF/CNPJ exigem revisão de gestão, não uma fusão automática.

O espaço de códigos continua compartilhado com cadastros locais. Não há realinhamento automático de sequência após inserção externa; um gerador local que futuramente alcançar código já ocupado poderá produzir conflito de PK, que deve ser tratado pelo operador sem renumerar registros. Antes de ativar em produção, conferir identity, triggers, charset, defaults, tamanho e a política de criação local de novos clientes. Não afirmar equivalência global de códigos sem esse levantamento.

CNPJ é validado com dígitos verificadores numéricos/alfanuméricos, sem presumir identidade por máscara ou nome; CPF não é convertido em CNPJ. A validação de formato/DV não comprova titularidade: essa prova vem da consulta ao Admin e revisão humana. [Documentação técnica da Receita Federal](https://www.gov.br/receitafederal/pt-br/centrais-de-conteudo/publicacoes/documentos-tecnicos/cnpj).

## Implantação e aceite

Backup consistente, conferir alvo/DDL, parar apenas a API envolvida, executar migrador, publicar API/frontend compatíveis e conceder permissões explicitamente. Migração não altera `CLIENTES`, códigos, geradores ou snapshots anteriores. Não remover tabelas/vínculos já utilizados para reverter código. Nenhum migrador ou cadastro deve ser executado em banco operacional sem autorização para esse alvo.

Testar em Firebird temporário: instalação limpa e repetição, script aditivo, PK/FK/unique, conflitos, repetição/concorrência, proof expirado/de outro usuário, dados oficiais alterados, identity ALWAYS/BY DEFAULT e trigger que sobrescreve código. HTTP simulado verifica autenticação, caminho singular, mapeamento, rejeição de resposta incompatível/redirecionamentos e segredo não exposto. Testar UI, permissões do bot e preservação de dados comerciais/propostas já aprovadas. **Validação real do Admin e levantamento operacional de CLIENTES permanecem pendentes.**
# Consulta somente por Código Admin

## Vínculos com CPF ou CNPJ

A importação aceita CPF (11 dígitos) e CNPJ (14 posições), normalizados e com dígitos verificadores válidos. O Código Admin permanece como `CODCLIENTE`. O contrato JSON legado `cnpj` representa ambos os documentos para compatibilidade. Documento inválido não impede consulta, mas impede importação.

Migração: `docs/sql/scripts/add-clienteadmin-documentofiscal.sql`, integrada a `--migrate-database`, após a criação de CLIENTEADMIN. Acrescenta `DOCUMENTOFISCAL VARCHAR(14) CHARACTER SET ASCII NOT NULL` e `UNQ_CLIENTEADMIN_DOCFISCAL`; preenche com os CNPJs anteriores sem alterar snapshots, autoria, PK/FK ou geradores. A coluna legada CNPJ permanece, mas aceita NULL para vínculos CPF. Novos vínculos CNPJ preenchem ambas as colunas. Não remover a coluna legada nesta entrega.

Antes de executar no banco operacional, conferir alvo, backup consistente e parar apenas a API envolvida. Publicar código e esquema juntos; versões antigas da API não suportam vínculos CPF. Há duas etapas confirmadas: adicionar a coluna e depois preencher/endurecer as constraints. Em erro, reverter a transação atual; a coluna já confirmada pode permanecer e a execução pode ser retomada. Investigar sem apagar vínculos. A implementação não autoriza executar a migração nos bancos do usuário.

A tela de consulta solicita e envia somente `codigoAdmin`. O retorno oficial deve conter o mesmo código positivo solicitado. CPF, CNPJ ausente ou documento fiscal inválido não impedem a exibição do cliente; dados incompatíveis com a importação retornam `revisao_necessaria`, com o candidato visível e sem autorização de importação. A importação continua separada, explícita e protegida pelas validações fiscais, conflitos locais e reconferência do snapshot. O campo JSON legado `cnpj` permanece compatível, mas é ignorado quando há `codigoAdmin`.

