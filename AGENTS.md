# Regras do projeto para o Codex

## Banco de dados Firebird

Antes de criar ou alterar tabelas, colunas, índices ou constraints, leia integralmente `docs/PADRAO_BANCO_DADOS.md`. Aplique as regras ao DDL, SQL da aplicação, migradores, testes e documentação da mesma entrega.

- Identificadores físicos em português, maiúsculos, sem acentos e sem aspas delimitadoras.
- Entidades e satélites: nomes compostos sem `_`, como `SCRIPTLAUDO`, `ORCAMENTOREVISAO` e `CLIENTECOMERCIAL`.
- `_` em nomes de tabelas somente para relações N:N, como `ORCAMENTO_USUARIO`. Não tratar uma tabela de histórico ou anexo como relação N:N apenas por possuir FK.
- Chaves identificadoras usam `COD` + entidade, nunca `ID` genérico ou `<ENTIDADE>_ID`. A FK mantém o nome e o tipo da chave referenciada. PK composta é permitida quando representa a identidade natural da relação/item.
- Colunas novas usam nomes compostos sem `_`, em português: `VERSAO`, `VALORFINAL`, `NOMEAUTOR`. Data/hora de criação: `DTHRCRIACAO`; última modificação: `DTHRULTMODIFICACAO`. Não renomear contratos JSON por consequência do nome físico: usar aliases/DTOs.
- Nomear constraints explicitamente: `PK_<TABELA>`, `FK_<TABELA>_<COLUNA>`, `UNQ_<TABELA>_<COLUNA>`. Usar abreviações documentadas quando necessário; não deixar nomes automáticos `INTEG_*`. Índices: `IX_<TABELA>_<COLUNAS>`; checks: `CK_<TABELA>_<REGRA>`.
- Para SQL compartilhado entre Firebird 4 e 5, manter identificadores até 31 caracteres como convenção conservadora do projeto, com nomes únicos e abreviações consistentes.
- Não usar ponto flutuante para dinheiro. Declarar precisão/escala, nulabilidade, tamanhos, charset e regras de integridade conscientemente. Manter o padrão de flags de cada domínio existente.
- Não alterar tabelas legadas fora do escopo nem presumir que exemplos antigos inconsistentes revogam estas regras. Em dúvida, verificar o DDL/documentação e explicitar a decisão antes de migrar.

## Migrações e segurança operacional

- Gerar scripts versionados em `docs/sql/scripts` ou no local já estabelecido para o módulo e integrar ao migrador quando aplicável.
- Firebird não possui `RENAME TABLE`. Planejar recriação, dependências, preservação de dados, identities, índices e constraints; nunca simular renomeação com um comando inexistente.
- Uma recriação baseada em tabela vazia deve conferir essa condição no próprio script e abortar antes de qualquer remoção se houver dados. Distinguir registros comerciais de seeds iniciais e preservar estes últimos.
- Não executar migração, exclusão ou recriação em banco local operacional, homologação ou produção sem autorização explícita para executar naquele alvo. Autorização para gerar um script não autoriza executá-lo.
- Orientar backup, parada apenas dos componentes envolvidos, conferência do banco alvo, transação/rollback e compatibilidade entre API e esquema. Não desligar serviços globais do servidor.
- Testar em banco temporário isolado: instalação limpa, repetição do migrador, correção do esquema anterior, proteção contra dados existentes e integridade das PKs/FKs. Não usar o banco do usuário como fixture.
