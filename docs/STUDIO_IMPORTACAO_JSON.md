# Importação de JSON Studio para edição por voz

O botão **Importar modelo JSON** em `/studio/voz` abre um trabalho separado, tanto
no modo convencional quanto no editor integrado. O trabalho anterior permanece
intacto até a confirmação e não é mesclado ao arquivo importado.

## Configuração e limites

- Aceita `.json` UTF-8 (com ou sem BOM), até 1 MiB e 500 elementos em `camposScript`.
- O documento canônico pode ocupar até 2 MiB, incluindo o envelope de edição.
- Requer autenticação e permissões `studio/visualizar` e `studio/voz`.
- No integrado, também exige `StudioDrafts:Enabled` e o esquema de rascunhos já
  instalado. Esta entrega não adiciona tabelas nem modifica a migração existente.
- A confirmação de importação expira em 15 minutos; há limite de oito revisões
  temporárias por usuário. Revisões temporárias não sobrevivem ao reinício da API.
- O modo convencional usa o provedor Cursor para converter a fala em comandos
  estruturados, ou o modo xAI em tempo real. Um provedor que não implemente esse
  contrato recusa a edição e preserva o modelo; não há substituição por JSON livre.

## Preservação e edição

O importador reconstrói vínculos por `etiquetaPai`, incluindo arquivos que
intercalam as duas colunas. Guarda as propriedades originais de cada elemento e
as propriedades adicionais da raiz no mesmo documento canônico. Campos do
catálogo são apenas associações: fórmulas, unidades e normalidades importadas
não são atualizadas automaticamente.

IDs e tokens do arquivo são separados dos identificadores internos de edição.
Renomear uma seção altera a descrição, preservando seu token. Mover/reordenar
atualiza vínculo e posicionamento; as linhas das colunas afetadas são recalculadas
para evitar sobreposição. Elementos sem coluna ou com coluna `-1` permanecem fora
do canvas. Botões, textos e funções auxiliares aparecem como componentes somente
para leitura. Não é permitido excluir uma seção que ainda possua esses componentes.

Funções, HTML e demais propriedades do arquivo são apresentados como texto,
sem execução no Studio. Problemas preexistentes de referências são avisos e não
impedem guardar ou baixar o JSON; alterações que introduzam referências ausentes
ou ciclos são recusadas. A validação identifica tokens `VR_*`, não variáveis locais.

O agente pode criar/organizar seções, incluir medidas do catálogo, mover medidas,
desfazer e refazer. Não pode editar fórmulas ou normalidades neste incremento.
No convencional, uma solicitação composta é aplicada atomicamente e guardada
como uma operação reversível. O integrado usa revisões, comandos e histórico
persistentes existentes. Em ambos os casos, falhas preservam o último estado aceito.

Publicação em modelos, exportação TXT/HTML e mesclagem por imagem ficam bloqueadas
para documentos importados, inclusive na API. O resultado suportado é o JSON
Studio completo e o rascunho. Sem edição, entrada e saída são semanticamente
equivalentes; espaços, escapes e ordem das propriedades podem mudar.

## APIs

- `POST /api/voice/importacoes/json`: multipart com `file`; retorna revisão privada,
  expiração, documento, contagens, avisos e candidatos para associação.
- `POST /api/voice/importacoes/json/{id}/confirmar`: JSON com `destination`
  (`conventional` ou `integrated`), `commandId` UUID, `nome` e `associations`
  opcionais (`UUID do item` → `codVariavel` entre os candidatos apresentados).
- A confirmação retorna `session` ou `draft`. Repetir a mesma confirmação devolve
  o mesmo trabalho; alterar sua identidade ou conteúdo após o uso retorna `409`.
- Conteúdo importado e falas não são registrados em logs deste fluxo. Transcrições
  permanecem somente na sessão convencional; o histórico persistente guarda comandos.

## Teste manual

1. Após compilação manual, abra `/studio/voz`, selecione **Importar modelo JSON**
   e escolha o exemplo de ecocardiograma. A revisão deve indicar 52 elementos,
   cinco seções, 42 medidas e cinco componentes preservados.
2. Cancele: o modelo anterior deve permanecer. Repita e confirme: deve abrir uma
   nova sessão. Baixe o JSON sem editar e compare todas as propriedades com a origem.
3. Diga “renomeie Dados do paciente para Informações do paciente” e “mova Peso
   para a seção Câmaras esquerdas”. Confira a descrição e o vínculo; tokens,
   unidades, fórmulas e normalidades devem permanecer. Teste desfazer e refazer.
4. Tente alterar uma fórmula por voz: nenhuma substituição de documento será aceita.
   Confirme que TXT, HTML e publicação não estão disponíveis para esse trabalho.
5. Ative o editor integrado, repita a importação, edite uma seção e aguarde salvar.
   Reabra o rascunho após atualizar a página ou reiniciar a API: conteúdo e
   componentes devem permanecer; confira também as revisões no histórico.
6. Teste JSON inválido, IDs duplicados, arquivo acima do limite, duas abas e
   interrupção de rede durante a confirmação. Repetir a confirmação não deve
   duplicar o trabalho. Outro usuário não deve acessar a revisão temporária.

O modo convencional continua sujeito à expiração e à perda da sessão ao reiniciar
a API; baixe o JSON para mantê-lo. O rascunho integrado permanece no banco.

## Validações automatizadas

`StudioJsonImportTests` cobre o arquivo de referência, preservação sem edição,
vínculos, UTF-8/BOM, entradas inválidas, comandos atômicos, conflitos, reversão,
dependências e confirmação privada/idempotente. A fixture é um modelo de
configuração sem dados de pacientes. Executar os testes .NET após compilação
manual ou no pipeline.

Sem compilar a aplicação, os testes frontend podem ser executados com:

```powershell
node --test frontend/nuxt-app/tests/studioJsonImport.test.mjs frontend/nuxt-app/tests/voiceSession.test.mjs frontend/nuxt-app/tests/studioDraft.test.mjs frontend/nuxt-app/tests/realtimeVoice.test.mjs frontend/nuxt-app/tests/realtimePcm.test.mjs
```

Testes reais de microfone, Cursor, xAI e recuperação no ambiente de destino ainda
devem ser realizados antes de considerar a integração homologada.
