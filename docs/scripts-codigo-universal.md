# Codigo universal dos modelos

Cada SCRIPTLAUDO recebe CODUNIVERSAL, por exemplo MOD-000001. O identificador
interno CODSCRIPTLAUDO e todos os vinculos existentes permanecem inalterados.
As versoes consultam o mesmo codigo do script; nao possuem outra sequencia.

## Regras

- VARCHAR(24) CHARACTER SET ASCII, NOT NULL e constraint UNQ_SCRIPTLAUDO_CODUNIVERSAL.
- GEN_SCRIPTLAUDO_UNIVERSAL gera o numero global. Seis digitos sao o minimo;
  depois de MOD-999999 vem MOD-1000000, sem truncar o numero.
- A trigger SCRIPTLAUDO_CODUNIVERSAL gera o codigo no INSERT, mesmo quando
  o cliente informa outro valor, e rejeita alteracoes no UPDATE.
- Exclusoes e transacoes revertidas nao liberam numeros para reutilizacao.
  Lacunas na numeracao sao esperadas. Nao reiniciar a sequencia manualmente.
- A migracao preenche registros sem codigo na ordem de CODSCRIPTLAUDO.
  Repetir o migrador preserva os codigos e a sequencia existentes.
- Nome, pacote, arquivos e versoes podem mudar sem mudar o codigo.

## Publicacao

1. Fazer backup consistente e conferir a conexao Firebird do ambiente alvo.
2. Suspender cadastros/alteracoes de scripts nos componentes envolvidos.
3. Executar o migrador existente com --migrate-database antes de iniciar a API nova.
   Esse comando executa todas as migracoes registradas, nao somente esta.
4. Conferir preenchimento e unicidade, iniciar a API atualizada e validar cadastro,
   busca por MOD- e consulta de versoes. Nao executar o SQL bruto em isql:
   backend/sql/script-codigo-universal.sql e consumido em blocos pelo migrador.

As fases sao confirmadas separadamente para tornar os metadados visiveis.
Em falha, manter os gravadores suspensos e corrigir/repetir o migrador; nao remover
codigos ja distribuidos. Para desfazer integralmente, restaurar o backup em janela
controlada, considerando qualquer alteracao realizada depois dele.

## Contratos

A API web retorna codUniversal nas listagens, detalhes do script e detalhes da
versao. O campo nao e aceito nos formularios. A busca por nome tambem aceita o
codigo (sem diferenciar maiusculas/minusculas).

A API parceira retorna coduniversal na listagem completa, enxuta e no ultimo
verificado. Os campos e identificadores anteriores continuam presentes.

Na rota /scripts o codigo aparece nos cards, tabela e detalhes; tambem aparece
na edicao do script e nos detalhes de uma versao.

## Sincronizacao com o Assistente

O Assistente copia exatamente SCRIPTLAUDO.CODUNIVERSAL de Conteudos. Seu
CODSCRIPTLAUDO continua sendo um identificador local; os dois numeros nao
precisam coincidir. Versoes e transferencias de pacote nao geram outro codigo.

- No Assistente a coluna tem VARCHAR(24) CHARACTER SET ASCII, permite NULL e
  usa UNQ_SCRIPTLAUDO_CODUNIVERSAL. A nulabilidade e intencional: modelos
  nativos, origens excluidas e modelos sem mapeamento confiavel nao recebem
  identificadores MOD- inventados. Nao ha gerador de codigos no destino.
- O preenchimento usa exclusivamente CON_PUBLICACAO da SourceKey configurada,
  associando CODORIGEM ao CODSCRIPTLAUDO da origem e CODDESTINO ao do destino.
  Nao procura por titulo nem altera IDs, conteudo, status, especialidades ou MRDs.
  Inclui modelos mapeados suspensos/inativos, sem retomar sua publicacao.
- Um codigo ja atribuido nao pode ser trocado ou apagado, inclusive pelo
  publicador. Um nativo so recebe seu primeiro codigo durante conciliacao
  explicita. A trigger CON_SCRIPT_CODUNIVERSAL exige contexto de publicacao.
- Codigos conflitantes abortam o preenchimento inteiro em uma transacao.
  O migrador nao funde modelos, nao sobrescreve codigos e nao muda mapeamentos.
  A constraint unique tambem protege publicacoes concorrentes.
- Instancias de Conteudos com sequencias independentes podem produzir o mesmo
  MOD-. Antes de usar varias origens no mesmo Assistente, os codigos precisam
  pertencer ao mesmo catalogo global. A colisao sera recusada, nunca renumerada.
- APIs de publicacao e regras por script retornam codUniversal e aceitam busca
  pelo MOD-. A API de dominios do Assistente preserva CODUNIVERSAL nos dados;
  o cliente o normaliza para codUniversal e exibe nos cards.

### Aplicacao no ambiente autorizado

1. Obter autorizacao explicita para os dois bancos alvo e fazer backup de ambos.
2. Parar a API/publicador e os gravadores de scripts envolvidos; nao parar o
   servico Firebird nem componentes de outros sistemas.
3. Aplicar a migracao da origem primeiro (--migrate-database), caso ainda nao
   aplicada. Conferir CODUNIVERSAL preenchido e unico. O comando tambem executa
   as outras migracoes registradas da origem.
4. Conferir Firebird, AssistantFirebird e PublicacaoAssistente:SourceKey. A chave
   deve ser a mesma que ja identifica essa origem em CON_PUBLICACAO.
5. Com a API ainda parada, executar a API compilada com
   --migrate-assistente-universal-code. Esse comando prepara somente o esquema
   de codigo do Assistente e preenche os mapeamentos dessa origem. Nao inicia
   o worker e nao executa migracoes gerais no Assistente. A migracao nao e
   automatica no startup nem foi adicionada a --migrate-database.
6. O SQL backend/sql/assistente-codigo-universal.sql e um recurso do migrador,
   com blocos NEXT. Nao executar seu texto bruto em isql. Ele nao faz sozinho
   o preenchimento entre bancos; essa etapa pertence ao migrador C#.
7. Conferir codigos dos modelos mapeados, nativos NULL e conflitos reportados.
   As fases de metadados sao confirmadas separadamente; o preenchimento e
   atomico. Corrigir conflitos e repetir o mesmo comando preserva codigos.
8. Iniciar a API nova somente depois das duas migracoes. Validar publicacao,
   conciliacao, atualizacao, inativacao e troca de pacote. Se preciso restaurar,
   manter os gravadores parados e restaurar os backups coerentes das duas bases;
   nao remover/regerar codigos que ja foram distribuidos.

Nenhuma migracao deve ser executada contra banco operacional como efeito da
implementacao ou dos testes. Os testes usam arquivos Firebird temporarios.
