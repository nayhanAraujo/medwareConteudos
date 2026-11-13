# Voz em tempo real no Studio

O modo opcional de `/studio/voz` conecta o navegador ao Hub SignalR em
`/api/voice/realtime/hub`. O .NET mantém um `ClientWebSocket` com a xAI,
executa Tools e publica alterações estruturadas no mesmo rascunho de voz.
O modo convencional e seus endpoints continuam disponíveis.

## Configuração e validação antes de habilitar

Adicionar ao JSON privado da API, preservando as demais configurações:

```json
{
  "RealtimeVoice": {
    "Enabled": false,
    "ApiKey": "CONFIGURAR_SOMENTE_NO_ARQUIVO_PRIVADO",
    "Model": "grok-voice-latest",
    "SilenceDurationMs": 400,
    "MaxSessions": 20,
    "MaxSessionMinutes": 20
  }
}
```

Alternativamente, configurar `XAI_API_KEY` no ambiente do processo da API.
Nunca adicionar a chave ao Nuxt, Git, linha de comando ou logs.

O recurso permanece desabilitado por padrão. Antes de habilitá-lo, executar,
de um ambiente autorizado com a credencial já configurada no processo:

```powershell
powershell.exe -NoProfile -File tools/production/tests/Test-XaiRealtimeCapabilities.ps1
```

Esse teste **acessa o serviço externo** e pode gerar cobrança. Não envia áudio
nem executa Tools comerciais. Ele verifica o reconhecimento explícito de
`modalities: ["text"]` e `silence_duration_ms: 400` em `session.updated`.
Falha ou ausência desses campos impede a ativação: não há fallback que apenas
silencie áudio e alegue que a geração de áudio foi desativada. Cada sessão real
repete essa validação antes de liberar o microfone.

O probe não substitui o teste de fala real: conferir pausas naturais, ruído,
termos médicos, ausência de áudio de saída e latência no ambiente final.
Nenhuma credencial disponível significa que a compatibilidade real ainda
está pendente; testes simulados não a comprovam.

## Uso e persistência

1. Iniciar uma sessão de voz existente ou nova e marcar o modo em tempo real.
2. Opcionalmente escolher um modelo existente, privado ao usuário, como destino.
3. Conectar o agente e autorizar o microfone em HTTPS.
4. Pedir busca, criação de seções, inclusão/movimentação de medidas ou desfazer.
5. Usar **Pausar microfone** para enviar o final da fala e deixar as ações terminarem.
6. Pedir preparação do salvamento e conferir a revisão e o destino na interface.
7. O botão de confirmação substitui a composição do modelo de destino pelo rascunho.

Sem destino selecionado, o agente apenas edita o rascunho. Não cria modelos
persistentes implicitamente. Campos de sessões convencionais sem vínculo
com o catálogo precisam ser corrigidos antes de salvar pelo montador.
Não há Tool que confirme gravação nem execute exclusão persistente.
O salvamento reutiliza a transação e as validações do serviço de modelos;
requer permissão `formulas/editar`, além de `studio/visualizar` e `studio/voz`.

**Encerrar e cancelar pendências** ou desconectar cancela operações em andamento.
O rascunho aplicado é preservado no armazenamento atual de sessões de voz,
em memória, sujeito à expiração e ao reinício da API. Não há replay automático
de áudio ou gravações. Reconectar exige uma ação explícita. Na primeira entrega,
usar uma instância de API; múltiplas instâncias exigem afinidade e estratégia
compartilhada para o armazenamento de sessões.

## Protocolo e consistência

No editor integrado, uma busca ambígua abre sugestões e pausa as alterações do
agente até a escolha explícita ou **Cancelar sugestões**. Consultas paralelas não
podem substituir a pergunta nem incluir automaticamente uma das candidatas.
O áudio continua sendo recebido; não há continuação automática do agente enquanto
a pergunta aguarda decisão. Após escolher, prossiga com a próxima instrução de voz.

A janela não fecha por atualização periódica ou mudança da revisão. Se outra
edição invalidar seu contexto, ela permanece visível com aviso e exige cancelar
e solicitar novas sugestões. Erros de aplicação ficam dentro da janela; repetir
a tentativa usa o mesmo identificador, evitando inclusão duplicada. A seção
escolhida na janela é independente de mudanças de seleção no canvas.

`POST /api/voice/rascunhos/{id}/escolhas/cancelar` recebe `{token}` e libera a
pergunta da sessão ativa somente após conferir a propriedade do rascunho.
A seleção continua usando ticket privado, candidatos autorizados e revisão esperada.

- Hub: `StartSession({sessionId, modeloId?})`, `AppendAudio({sessionId, sequence, audio})`,
  `ConfirmSave({sessionId, proposalId, revision})`, `StopSession()`.
- Áudio: blocos de 40 ms = 960 amostras PCM16 little-endian mono a 24 kHz,
  1920 bytes antes de Base64; sequência inicia em zero e não pode ter lacunas.
- UI: `SessionReady`, `SpeechState`, `Transcript`, `ToolStarted`, `ToolCompleted`,
  `ToolFailed`, `DraftUpdated`, `SaveProposed`, `SaveCompleted`, `SaveFailed`, `SessionClosed`.
- Tools: `consultar_rascunho`, `buscar_medidas`, `criar_secao`, `organizar_secao`,
  `incluir_medidas`, `mover_medida`, `desfazer_alteracao`, `preparar_salvamento`.
- Correções reutilizam `grupoOperacao` e `substituirOperacao: true`.
  Nova fala não invalida todas as operações anteriores. O agente deve identificar
  qual intenção está sendo corrigida; não se promete inferência perfeita.
- Chamadas repetidas pelo `call_id` são deduplicadas dentro da sessão.
- Consultas independentes podem executar em paralelo; alterações usam exclusão mútua.
  Há um único escritor WebSocket e filas limitadas; saturação encerra a sessão.
- Resultados das Tools precedem uma única continuação por lote fechado por
  `response.done`. Cancelar geração do modelo não desfaz transações já confirmadas.
- Desfazer recusa sobrescrever itens modificados posteriormente ou deixar campos
  sem seção. Não restaura indiscriminadamente um snapshot completo.

## IIS, autenticação e diagnóstico

O prefixo público existente `/api-dotnet` encaminha o Hub à API. WebSocket deve
estar disponível no IIS público/ARR e na API; o Hub não usa long polling.
O caminho esperado no navegador é
`wss://DOMINIO/api-dotnet/api/voice/realtime/hub?...`, sem redirecionamento ao Nuxt.
Validar primeiro a negociação autenticada e depois o upgrade HTTP 101.
Não editar configurações globais de outros sites nem reiniciar serviços globais.

O JWT em `access_token` é aceito **somente** no caminho do Hub. Não registrar
query strings desse caminho no IIS, ARR ou observabilidade; remover o campo
`cs-uri-query` do log do site envolvido antes do teste real. Não basta ocultá-lo
apenas no log da aplicação. O Hub encerra conexões no vencimento do token e
revalida permissões em cada Tool e na confirmação.

Logs da API incluem sessão, conexão, chamada, resposta, nome da Tool e duração,
sem áudio, transcrição, argumentos ou corpo da resposta do provedor.
Correlacionar `SessionClosed.code` com `SessionId` nos logs. Conferir a saída do
probe em falhas de capacidade. Estado antigo do rascunho e previews não são prova
de que uma Tool pendente concluiu.

## Verificações

Sem build:

```powershell
node --test frontend/nuxt-app/tests/realtimePcm.test.mjs frontend/nuxt-app/tests/realtimeVoice.test.mjs frontend/nuxt-app/tests/voiceSession.test.mjs
git diff --check
```

Na compilação/testes manuais, executar `RealtimeVoiceTests` e
`RealtimeVoiceProtocolTests` no projeto .NET. Eles usam transporte, permissões e
catálogo simulados; nenhum banco operacional ou xAI real é acessado.

Em homologação, verificar usuário não autorizado, rascunho de outro usuário,
expiração JWT, micrófone negado, pausas, consulta lenta com áudio contínuo,
correções sucessivas, proposta invalidada por outra edição, confirmação duplicada,
queda da conexão e saturação da rede. Conferir o download JSON/TXT após a edição.
Não é necessária migração de banco de dados.
