export type CuradoriaTeamsTheme = 'default' | 'dark' | 'contrast'
export type CuradoriaTeamsKind = 'configuration' | 'content'
export interface CuradoriaTeamsDiagnostic {
  host: 'Teams' | 'TeamsModern' | 'outro' | 'não informado'
  frame: 'settings' | 'content' | 'outro' | 'não informado'
  configurationSupported: boolean
  chatIdPresent: boolean
  teamIdPresent: boolean
  channelIdPresent: boolean
  meetingIdPresent: boolean
  pilotCheck: 'not-needed' | 'not-checked' | 'checking' | 'matched' | 'not-confirmed'
}
export interface CuradoriaTeamsView {
  mode: 'loading' | 'browser' | 'unsupported' | 'ready' | 'saving' | 'saved' | 'error'
  message: string
  theme: CuradoriaTeamsTheme
  confirmed: boolean
  canConfigure: boolean
  diagnostic: CuradoriaTeamsDiagnostic | null
}

// Fixed public destinations. Context, query strings and identifiers never enter configuration URLs.
export const CURADORIA_TAB_CONFIG = Object.freeze({
  entityId: 'medware-curadoria-conhecimento',
  suggestedDisplayName: 'Curadoria',
  contentUrl: 'https://conteudo.medware.com.br/curadoria/teams/aba',
  websiteUrl: 'https://conteudo.medware.com.br/curadoria/teams/aba'
})

interface SaveEvent { notifySuccess(): void; notifyFailure(reason?: string): void }
interface TeamsContext {
  app: { theme: string; host: { name: string } }
  page: { frameContext: string }
  chat?: { id?: string }
  team?: { internalId?: string; groupId?: string }
  channel?: { id?: string }
  meeting?: { id?: string }
}
export interface CuradoriaTeamsSdk {
  app: {
    initialize(): Promise<void>
    getContext(): Promise<TeamsContext>
    registerOnThemeChangeHandler(handler: (theme: string) => void): void
  }
  pages: { config: {
    isSupported(): boolean
    setValidityState(valid: boolean): void
    registerOnSaveHandler(handler: (event: SaveEvent) => void): void
    setConfig(config: typeof CURADORIA_TAB_CONFIG): Promise<void>
  } }
}

export function initialCuradoriaTeamsView(): CuradoriaTeamsView {
  return { mode: 'loading', message: 'Verificando o ambiente do Teams…', theme: 'default', confirmed: false, canConfigure: false, diagnostic: null }
}

function hasIdentifier(value: unknown): boolean {
  return typeof value === 'string' && value.trim().length > 0
}

function theme(value: string): CuradoriaTeamsTheme {
  return value === 'dark' || value === 'contrast' ? value : 'default'
}
function bounded<T>(work: Promise<T>, milliseconds: number): Promise<T> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('Teams timeout')), milliseconds)
    work.then(value => { clearTimeout(timer); resolve(value) }, error => { clearTimeout(timer); reject(error) })
  })
}

// This controller adapts presentation only. Teams context is NOT authentication or proof of RSC access.
// No Graph/authentication SDK calls, storage, telemetry, raw context or identifiers are retained.
export function createCuradoriaTeamsController(
  kind: CuradoriaTeamsKind,
  loadSdk: () => Promise<CuradoriaTeamsSdk>,
  update: (view: CuradoriaTeamsView) => void,
  timeoutMs = 8000,
  checkPilot: (chatId: string) => Promise<boolean> = async () => false
) {
  const state = initialCuradoriaTeamsView()
  let sdk: CuradoriaTeamsSdk | undefined
  let alive = true
  let started = false
  let registered = false
  let pilotRequired = false
  const emit = () => { if (alive) update({ ...state }) }
  const failure = (event: SaveEvent, message: string) => { try { event.notifyFailure(message) } catch { /* Host may already have closed the frame. */ } }
  const validity = (valid: boolean) => { if (registered && sdk) sdk.pages.config.setValidityState(valid) }

  async function save(event: SaveEvent) {
    if (!alive || !state.canConfigure || !state.confirmed || !['ready', 'error'].includes(state.mode) || !sdk) {
      failure(event, 'Confirme o chat autorizado e aguarde a configuração ficar pronta antes de salvar.')
      return
    }
    state.mode = 'saving'
    state.message = 'Salvando somente a configuração da aba no Teams…'
    emit()
    try {
      validity(false)
      // Revalidate the scoped exception at save time, including a changed SDK context
      // or a pin removed/changed by the operator since this dialog was opened.
      {
        let permitted = false
        try {
          permitted = await bounded((async () => {
            const current = await sdk!.app.getContext()
            const compatible = ['Teams', 'TeamsModern'].includes(current.app.host.name) && current.page.frameContext === 'settings' &&
              sdk!.pages.config.isSupported() && !hasIdentifier(current.team?.internalId) && !hasIdentifier(current.team?.groupId) &&
              !hasIdentifier(current.channel?.id)
            if (!compatible) return false
            if (pilotRequired || hasIdentifier(current.meeting?.id)) {
              return hasIdentifier(current.chat?.id) && await checkPilot(current.chat!.id!) === true
            }
            return true
          })(), timeoutMs)
        } catch { /* A failed check must not restore the previous validity. */ }
        if (!alive) { failure(event, 'A página foi fechada.'); return }
        if (!permitted) {
          state.canConfigure = false
          state.confirmed = false
          state.mode = 'unsupported'
          if (state.diagnostic) state.diagnostic = { ...state.diagnostic, pilotCheck: 'not-confirmed' }
          state.message = 'O chat do piloto não pôde ser confirmado novamente. Reabra a configuração após conferir a configuração privada do Nuxt.'
          emit()
          failure(event, 'O chat do piloto não foi confirmado. A aba não foi salva.')
          return
        }
      }
      await bounded(sdk.pages.config.setConfig({ ...CURADORIA_TAB_CONFIG }), timeoutMs)
      if (!alive) { failure(event, 'A página foi fechada. Confira a aba no Teams antes de tentar novamente.'); return }
      state.mode = 'saved'
      state.message = 'Configuração da aba salva. Isso não comprova permissão Graph e não ativa a coleta de mensagens.'
      emit()
      event.notifySuccess()
    } catch {
      if (alive) {
        state.mode = 'error'
        state.message = 'Não foi possível confirmar o salvamento da aba. Confira o Teams antes de tentar novamente. Nenhuma coleta foi ativada por esta página.'
        try { validity(state.confirmed) } catch { state.canConfigure = false }
        emit()
      }
      failure(event, 'Não foi possível confirmar o salvamento. Confira a aba no Teams e tente novamente se necessário.')
    }
  }

  return {
    async start() {
      if (started || !alive) return
      started = true
      emit()
      try {
        const prepared = await bounded((async () => {
          const loaded = await loadSdk()
          await loaded.app.initialize()
          return { loaded, context: await loaded.app.getContext() }
        })(), timeoutMs)
        if (!alive) return
        sdk = prepared.loaded
        const context = prepared.context
        const host = context.app.host.name
        const frame = context.page.frameContext
        const teamsHost = host === 'Teams' || host === 'TeamsModern'
        const chatIdPresent = hasIdentifier(context.chat?.id)
        const teamIdPresent = hasIdentifier(context.team?.internalId) || hasIdentifier(context.team?.groupId)
        const channelIdPresent = hasIdentifier(context.channel?.id)
        const meetingIdPresent = hasIdentifier(context.meeting?.id)
        state.theme = theme(context.app.theme)
        sdk.app.registerOnThemeChangeHandler(value => { if (alive) { state.theme = theme(value); emit() } })

        if (kind === 'configuration') {
          const settings = frame === 'settings'
          const supported = sdk.pages.config.isSupported()
          state.diagnostic = Object.freeze({
            host: teamsHost ? host : host ? 'outro' : 'não informado',
            frame: frame === 'settings' || frame === 'content' ? frame : frame ? 'outro' : 'não informado',
            configurationSupported: supported,
            chatIdPresent, teamIdPresent, channelIdPresent, meetingIdPresent,
            pilotCheck: meetingIdPresent ? 'not-checked' : 'not-needed'
          })
          if (settings && supported) sdk.pages.config.setValidityState(false)
          if (teamsHost && settings && supported && chatIdPresent && meetingIdPresent && !teamIdPresent && !channelIdPresent) {
            state.diagnostic = { ...state.diagnostic, pilotCheck: 'checking' }
            state.message = 'Conferindo a exceção restrita ao chat do piloto…'
            emit()
            try { pilotRequired = await bounded(checkPilot(context.chat!.id!), timeoutMs) === true } catch { pilotRequired = false }
            if (!alive) return
            state.diagnostic = { ...state.diagnostic, pilotCheck: pilotRequired ? 'matched' : 'not-confirmed' }
          }
          // A settings dialog may not supply chat.id before the tab exists. Empty optional
          // objects are not evidence of a team/channel/meeting. Keep the positive host,
          // settings and capability checks; reject concrete incompatible identifiers
          // except the privately pinned, previously verified pilot group chat.
          // This is presentation only: manifesto scope + explicit operator confirmation
          // select the pilot chat. Neither this context nor its absence grants RSC access.
          if (!teamsHost || teamIdPresent || channelIdPresent || (meetingIdPresent && !pilotRequired) || !settings || !supported) {
            state.mode = 'unsupported'
            state.message = 'Esta configuração deve ser aberta ao adicionar uma aba ao chat em grupo autorizado no Teams. Equipes, canais, reuniões e outros aplicativos não fazem parte deste piloto.'
            if (state.diagnostic.pilotCheck === 'not-confirmed') state.message = 'O Teams informou uma reunião, mas o chat não foi confirmado pela exceção privada do piloto. Confira a configuração do Nuxt; outros chats continuam bloqueados.'
          } else {
            sdk.pages.config.registerOnSaveHandler(event => { void save(event) })
            registered = true
            state.canConfigure = true
            state.mode = 'ready'
            state.message = 'Confirme abaixo o destino do piloto. Depois use Salvar na janela do Teams para adicionar a aba informativa.'
          }
        } else {
          let pilotContent = false
          if (teamsHost && frame === 'content' && chatIdPresent && meetingIdPresent && !teamIdPresent && !channelIdPresent) {
            try { pilotContent = await bounded(checkPilot(context.chat!.id!), timeoutMs) === true } catch { /* Keep generic informational mode. */ }
            if (!alive) return
          }
          state.mode = teamsHost && chatIdPresent && !teamIdPresent && !channelIdPresent && (!meetingIdPresent || pilotContent) && frame === 'content' ? 'ready' : 'unsupported'
          state.message = state.mode === 'ready'
            ? 'Aba informativa exibida no Teams. Nenhuma leitura de mensagens é realizada por esta página.'
            : 'Página informativa do piloto. Sua exibição não comprova instalação, consentimento ou acesso a mensagens.'
        }
        emit()
      } catch {
        if (!alive) return
        state.mode = 'browser'
        state.canConfigure = false
        state.message = kind === 'configuration'
          ? 'Não foi possível estabelecer a comunicação com o Teams. Abra esta configuração dentro do Teams, ao adicionar a aba no chat em grupo autorizado. No navegador comum não é possível instalar ou salvar a aba.'
          : 'Você pode ler esta página no navegador. Instalação, consentimento e acesso às mensagens devem ser verificados separadamente na organização.'
        emit()
      }
    },
    setConfirmed(confirmed: boolean) {
      if (!alive || !state.canConfigure || state.mode === 'saving' || state.mode === 'saved') return
      state.confirmed = confirmed
      try { validity(confirmed) } catch {
        state.mode = 'error'
        state.canConfigure = false
        state.message = 'A comunicação com o Teams foi interrompida. Reabra a configuração dentro do chat autorizado.'
      }
      emit()
    },
    dispose() {
      alive = false
      try { validity(false) } catch { /* Closing a frame must not create an unhandled SDK error. */ }
    }
  }
}
