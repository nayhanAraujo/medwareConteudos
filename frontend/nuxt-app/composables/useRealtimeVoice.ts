import { HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr'
import type { HubConnection } from '@microsoft/signalr'

export function useRealtimeVoice() {
  const config = useRuntimeConfig()
  const auth = useAuthStore()
  const store = useVoiceSessionStore()
  const connected = ref(false)
  const listening = ref(false)
  const starting = ref(false)
  const saving = ref(false)
  const error = ref('')
  const speaking = ref(false)
  const revision = ref(0)
  const activity = ref<string[]>([])
  const proposal = ref<{ proposalId: string; revision: number; modeloId: number; nome: string } | null>(null)
  let connection: HubConnection | null = null
  let stream: MediaStream | null = null
  let context: AudioContext | null = null
  let node: AudioWorkletNode | null = null
  let muted: GainNode | null = null
  let sequence = 0
  let inFlight = 0
  let disposed = false
  let capturing = false
  let pausing = false
  let sessionId: string | null = null
  const uploads = new Set<Promise<void>>()
  const note = (s: string) => { activity.value = [s, ...activity.value].slice(0, 15) }
  const belongs = (e: { sessionId: string }) => e.sessionId === sessionId && store.sessionId === sessionId

  function audio(frame: ArrayBuffer) {
    if (!connection || !sessionId || !connected.value) return
    if (inFlight >= 16) {
      error.value = 'O envio de áudio ficou atrasado. A sessão foi interrompida; o rascunho foi preservado.'
      void stop()
      return
    }
    const bytes = new Uint8Array(frame)
    const base64 = btoa(String.fromCharCode(...bytes))
    inFlight++
    const pending: Promise<void> = connection.invoke<void>('AppendAudio', { sessionId, sequence: sequence++, audio: base64 })
      .catch(() => { error.value = 'O áudio foi interrompido. Retome a sessão; seu rascunho foi preservado.'; void stop() })
      .finally(() => { inFlight--; uploads.delete(pending) })
    uploads.add(pending)
  }

  async function releaseMicrophone() {
    listening.value = false
    node?.disconnect(); muted?.disconnect()
    stream?.getTracks().forEach(t => t.stop())
    stream = null; node = null; muted = null
    const old = context; context = null
    if (old && old.state !== 'closed') await old.close()
  }

  async function listen() {
    if (!connected.value || listening.value || capturing || pausing) return
    capturing = true
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true } })
      if (disposed || !connected.value) { await releaseMicrophone(); return }
      context = new AudioContext()
      await context.audioWorklet.addModule('/audio/realtime-worklet.js')
      if (disposed || !connected.value || !context) { await releaseMicrophone(); return }
      await context.resume()
      node = new AudioWorkletNode(context, 'studio-realtime-pcm')
      node.port.onmessage = e => { if (e.data instanceof ArrayBuffer) audio(e.data) }
      muted = context.createGain(); muted.gain.value = 0
      context.createMediaStreamSource(stream).connect(node).connect(muted).connect(context.destination)
      listening.value = true
    } catch {
      await releaseMicrophone()
      error.value = 'Não foi possível abrir o microfone. Verifique a permissão e utilize HTTPS.'
    } finally { capturing = false }
  }

  async function pause() {
    if (!listening.value || !node || pausing) return
    pausing = true
    const worklet = node
    await new Promise<void>(resolve => {
      const timeout = setTimeout(resolve, 500)
      worklet.port.onmessage = e => {
        if (e.data instanceof ArrayBuffer) audio(e.data)
        if (e.data === 'flushed') { clearTimeout(timeout); resolve() }
      }
      worklet.port.postMessage('flush')
    })
    await releaseMicrophone()
    // Feed trailing silence so server VAD can finish the last phrase without manual commit.
    for (let i = 0; i < 15 && connected.value; i++) {
      audio(new ArrayBuffer(1920))
      await new Promise(resolve => setTimeout(resolve, 40))
    }
    note('Microfone pausado. As ações pendentes continuam sendo processadas.')
    pausing = false
  }

  async function start(modeloId: number | null = null) {
    if (starting.value || connected.value || !store.sessionId) return
    starting.value = true; error.value = ''; activity.value = []; proposal.value = null
    sequence = 0; revision.value = 0; sessionId = store.sessionId
    const base = String(config.public.apiBase || '').replace(/\/$/, '')
    const hub = new HubConnectionBuilder()
      .withUrl(`${base}/api/voice/realtime/hub`, { transport: HttpTransportType.WebSockets, accessTokenFactory: () => auth.token || '' })
      .configureLogging(LogLevel.None) // Avoid bearer tokens/query strings in browser logs.
      .build()
    connection = hub
    hub.on('SessionReady', e => {
      if (!belongs(e)) return
      connected.value = true; revision.value = e.revision
      store.updateCamposScript(e.camposScriptJson)
    })
    hub.on('DraftUpdated', e => {
      if (!belongs(e) || e.revision <= revision.value) return
      const parsed = JSON.parse(e.camposScriptJson)
      if (!Array.isArray(parsed.camposScript)) { error.value = 'Resposta de rascunho inválida.'; void stop(); return }
      revision.value = e.revision; store.updateCamposScript(e.camposScriptJson); proposal.value = null
    })
    hub.on('SpeechState', e => { if (belongs(e)) speaking.value = e.speaking })
    hub.on('Transcript', e => { if (belongs(e)) store.addHistory({ at: new Date().toISOString(), transcript: e.text, sttSource: 'server', summary: 'Fala recebida em tempo real' }) })
    hub.on('ToolStarted', e => { if (belongs(e)) note(`Executando: ${e.name}`) })
    hub.on('ToolCompleted', e => { if (belongs(e)) note(`Concluído: ${e.name}`) })
    hub.on('ToolFailed', e => { if (belongs(e)) note(e.result?.message || 'Não foi possível aplicar a ação; o rascunho foi preservado.') })
    hub.on('SaveProposed', e => { if (belongs(e) && e.revision === revision.value) proposal.value = e })
    hub.on('SaveCompleted', e => { if (belongs(e)) { saving.value = false; proposal.value = null; note('Modelo salvo no banco.') } })
    hub.on('SaveFailed', e => { if (belongs(e)) { saving.value = false; error.value = e.message } })
    hub.on('SessionClosed', e => { if (belongs(e)) { error.value = `${e.message} Código: ${e.code}`; void stop() } })
    hub.onclose(() => {
      if (connection !== hub) return
      connection = null; connected.value = false; speaking.value = false; proposal.value = null; saving.value = false
      error.value ||= 'Conexão encerrada. Seu rascunho foi preservado; conecte novamente para continuar.'
      void releaseMicrophone()
    })
    try {
      await hub.start()
      if (disposed) { await hub.stop(); return }
      await hub.invoke('StartSession', { sessionId, modeloId })
      if (!connected.value) throw new Error('A sessão não confirmou sua configuração.')
      await listen()
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Não foi possível conectar ao agente.'
      await stop()
    } finally { starting.value = false }
  }

  async function confirmSave() {
    const value = proposal.value
    if (!connection || !value || !connected.value || saving.value) return
    saving.value = true
    try { await connection.invoke('ConfirmSave', { sessionId, proposalId: value.proposalId, revision: value.revision }) }
    catch (e) { saving.value = false; error.value = e instanceof Error ? e.message : 'Falha ao confirmar salvamento.' }
  }

  async function stop() {
    const hub = connection
    connection = null; connected.value = false; proposal.value = null; speaking.value = false; saving.value = false
    await releaseMicrophone()
    // No reconnect/replay: losing this connection cancels unfinished operations on the server.
    if (hub) { try { await hub.stop() } catch { /* Already disconnected. */ } }
  }
  onBeforeUnmount(() => { disposed = true; void stop() })
  return { connected, listening, starting, saving, speaking, error, activity, proposal, revision, start, pause, listen, stop, confirmSave }
}
