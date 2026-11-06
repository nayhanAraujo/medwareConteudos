import type { StudioDraft, StudioRevision, StudioVariable } from '~/types/studioDraft'

export function useStudioDraft() {
  const api = useApi()
  const auth = useAuthStore()
  const draft = ref<StudioDraft | null>(null)
  const entries = ref<StudioDraft[]>([])
  const history = ref<StudioRevision[]>([])
  const catalog = ref<StudioVariable[]>([])
  const problems = ref<Array<{code: string; message: string; itemId?: string}>>([])
  const busy = ref(false)
  const error = ref('')
  const sync = ref('Rascunho salvo')
  const pending = ref<{ commandId: string; expectedRevision: number; type: string; payload: unknown; group?: string } | null>(null)
  const selectedSection = ref<string | null>(null)
  const selectedItem = ref<string | null>(null)
  const preview = ref<{content: string; format: string; revision: number; metadataHash: string} | null>(null)
  const metadataChanged = ref(false)
  const outdated = computed(() => !!preview.value && (preview.value.revision !== draft.value?.revision || metadataChanged.value))
  let poll: ReturnType<typeof setInterval> | undefined
  let disposed = false
  let checking = false
  const pendingKey = (id: string) => `studio-pending:${auth.user?.codusuario}:${id}`
  watch(pending, value => {
    if (!import.meta.client || !draft.value) return
    try { if (value) sessionStorage.setItem(pendingKey(draft.value.id), JSON.stringify(value)); else sessionStorage.removeItem(pendingKey(draft.value.id)) } catch { /* Storage unavailable: keep the pending command in memory. */ }
  }, {flush: 'sync'})
  function accept(value: StudioDraft) {
    if (draft.value && draft.value.id === value.id && value.revision < draft.value.revision) return
    const changed = draft.value?.id !== value.id || draft.value?.revision !== value.revision
    draft.value = value
    if (selectedSection.value && !value.document.secoes.some(s => s.id === selectedSection.value)) selectedSection.value = null
    if (selectedItem.value && !value.document.secoes.some(s => s.itens.some(i => i.id === selectedItem.value))) selectedItem.value = null
    if (changed) void refreshDetails()
  }
  async function refreshDetails() {
    if (!draft.value) return
    const id = draft.value.id
    try {
      const [versions, validation] = await Promise.all([
        api.get<StudioRevision[]>(`/api/voice/rascunhos/${id}/historico`),
        api.get<{revision: number; metadataHash: string; problems: typeof problems.value}>(`/api/voice/rascunhos/${id}/validacao`)
      ])
      if (draft.value?.id !== id) return
      history.value = versions
      problems.value = validation.problems
      metadataChanged.value = !!preview.value && (validation.metadataHash !== preview.value.metadataHash || validation.revision !== preview.value.revision)
      return true
    } catch (e) { error.value = e instanceof Error ? e.message : 'Não foi possível validar o rascunho.'; return false }
  }
  async function list(archived = false) { entries.value = await api.get<StudioDraft[]>(`/api/voice/rascunhos?archived=${archived}`) }
  async function open(id: string) {
    if (pending.value) throw new Error('Confira o comando pendente antes de trocar de rascunho.')
    preview.value = null; accept(await api.get<StudioDraft>(`/api/voice/rascunhos/${id}`))
    if (import.meta.client) {
      try { const saved = sessionStorage.getItem(pendingKey(id)); if (saved) { pending.value = JSON.parse(saved); sync.value = 'Comando pendente — consulte seu resultado antes de editar' } } catch {}
    }
  }
  async function create(nome: string, modeloId: number | null = null, voiceSessionId?: string) {
    accept(await api.post<StudioDraft>('/api/voice/rascunhos', { nome, modeloId, voiceSessionId })); await list()
  }
  async function send(type: string, payload: unknown, group?: string) {
    if (!draft.value || busy.value || pending.value) return
    const request = { commandId: crypto.randomUUID(), expectedRevision: draft.value.revision, type, payload, group }
    pending.value = request; busy.value = true; error.value = ''; sync.value = 'Sincronizando'
    try {
      accept(await api.post<StudioDraft>(`/api/voice/rascunhos/${draft.value.id}/comandos`, request))
      pending.value = null; sync.value = 'Rascunho salvo'
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Falha de sincronização'
      sync.value = 'Falha de sincronização — confira o comando pendente'
    } finally { busy.value = false }
  }
  async function reconcile(retry: boolean) {
    if (!pending.value || !draft.value || busy.value) return
    busy.value = true
    try {
      let confirmed = false
      try { await api.get(`/api/voice/rascunhos/${draft.value.id}/comandos/${pending.value.commandId}`); confirmed = true } catch { /* It may not have committed. */ }
      const current = await api.get<StudioDraft>(`/api/voice/rascunhos/${draft.value.id}`)
      if (confirmed) { pending.value = null; accept(current); sync.value = 'Rascunho salvo'; error.value = ''; return true }
      if (retry && current.revision === pending.value.expectedRevision) {
        accept(await api.post<StudioDraft>(`/api/voice/rascunhos/${current.id}/comandos`, pending.value))
        pending.value = null; sync.value = 'Rascunho salvo'; error.value = ''
      } else { accept(current); error.value = 'O comando não foi confirmado. Descarte a tentativa e aplique novamente sobre o estado atual.' }
      return true
    } catch (e) { error.value = e instanceof Error ? e.message : 'Falha ao conferir o comando.'; return false }
    finally { busy.value = false }
  }
  async function discardPending() { if (await reconcile(false)) { pending.value = null; sync.value = 'Rascunho salvo'; error.value = '' } }
  async function generate(format: string) {
    if (!draft.value || busy.value || pending.value) return
    busy.value = true
    try {
      preview.value = await api.post(`/api/voice/rascunhos/${draft.value.id}/exportar`, { revision: draft.value.revision, format })
      metadataChanged.value = false
    } catch (e) { error.value = e instanceof Error ? e.message : 'Falha ao gerar.' } finally { busy.value = false }
  }
  onMounted(async () => {
    try { await list(); const data = await api.get<{data: StudioVariable[]}>('/api/web/modelos/variaveis'); catalog.value = data.data } catch (e) { error.value = e instanceof Error ? e.message : 'Falha ao abrir o editor.' }
    poll = setInterval(async () => {
      if (disposed || checking || busy.value || pending.value || !draft.value) return
      checking = true
      try { const id = draft.value.id; const latest = await api.get<StudioDraft>(`/api/voice/rascunhos/${id}`); if (draft.value?.id === id) accept(latest); await refreshDetails() }
      catch { /* Preserve the last confirmed document during network failures. */ }
      finally { checking = false }
    }, 5000)
  })
  onBeforeUnmount(() => { disposed = true; if (poll) clearInterval(poll) })
  return { api, draft, entries, history, catalog, problems, busy, error, sync, pending, selectedSection, selectedItem, preview, outdated, accept, refreshDetails, list, open, create, send, reconcile, discardPending, generate }
}
