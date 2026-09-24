import { defineStore } from 'pinia'
import { computed, ref, shallowRef } from 'vue'
import { flattenOperations, exampleForSchema, resolveSchema } from '~/utils/apiDocs'
import { isWriteOperation, isDeniedOperation } from '~/utils/docsPolicy'

export const useApiDocsStore = defineStore('api-docs', () => {
  const definition = ref('parceiros')
  const environment = ref('atual')
  const section = ref('visao-geral')
  const operationId = ref('')
  const tab = ref('informacoes')
  const language = ref('curl')
  const search = ref('')
  const spec = shallowRef<any>(null)
  const environments = ref<any[]>([])
  const supportUrl = ref('')
  const context = ref<any>(null)
  const loading = ref(false)
  const error = ref('')
  const running = ref(false)
  const result = shallowRef<any>(null)
  const tryOpen = ref(false)
  const body = ref('')
  const contentType = ref('application/json')
  const parameters = ref<Record<string, Record<string, string>>>({ path: {}, query: {}, headers: {} })
  const responseCode = ref('200')
  const credentialRevision = ref(0)
  // Kept outside Pinia state: never included in SSR hydration, cookies or persistent storage.
  const tokens = new Map<string, string>()
  let controller: AbortController | undefined
  let generation = 0
  const operations = computed<any[]>(() => spec.value ? flattenOperations(spec.value) : [])
  const selected = computed(() => operations.value.find(op => op.id === operationId.value))
  const visibleOperations = computed(() => {
    const q = search.value.trim().toLocaleLowerCase('pt-BR')
    return operations.value.filter(op => !q || `${op.method} ${op.path} ${op.summary} ${op.description} ${op.tag}`.toLocaleLowerCase('pt-BR').includes(q))
  })
  const publicBase = computed(() => environments.value.find(e => e.id === environment.value)?.publicBaseUrl || '')
  const credentialKey = () => `${environment.value}:${definition.value}`
  const authorized = computed(() => { void credentialRevision.value; return !!tokens.get(credentialKey()) })
  const requiresToken = computed(() => !!selected.value?.security?.length)
  const writeOperation = computed(() => !!selected.value && isWriteOperation(selected.value))
  const externalOperation = computed(() => !!selected.value && isDeniedOperation({ ...selected.value, 'x-docs-safe-try': selected.value.raw?.['x-docs-safe-try'] }))
  const canExecute = computed(() => !!selected.value && !externalOperation.value && (!writeOperation.value || (environment.value === 'homologacao' && context.value?.allowWrites === true && context.value?.environment === 'Homologacao')))

  function setToken(value: string) {
    const token = value.replace(/^Bearer\s+/i, '').trim()
    if (token) tokens.set(credentialKey(), token)
    else tokens.delete(credentialKey())
    credentialRevision.value++
  }
  function clearCredentials() { tokens.clear(); credentialRevision.value++; result.value = null }
  function headers(webToken = ''): Record<string, string> {
    const h: Record<string, string> = {}
    const token = tokens.get(credentialKey()) || (definition.value !== 'parceiros' && environment.value === 'atual' ? webToken : '')
    if (token) h.Authorization = `Bearer ${token}`
    if (webToken) h['X-Docs-Web-Authorization'] = `Bearer ${webToken}`
    return h
  }
  async function load(fetcher: typeof $fetch, webToken = '') {
    const current = ++generation
    loading.value = true; error.value = ''; spec.value = null; context.value = null; result.value = null
    cancel()
    try {
      const config = await fetcher<any>('/api/documentacao/config')
      if (current !== generation) return
      environments.value = config.environments
      supportUrl.value = config.supportUrl || ''
      const root = `/api/documentacao/${environment.value}`
      const [document, ctx] = await Promise.all([
        fetcher<any>(`${root}/${definition.value}/openapi`, { headers: headers(webToken) }),
        fetcher<any>(`${root}/contexto`, { headers: headers(webToken) }).catch(() => null)
      ])
      if (current !== generation) return
      spec.value = document; context.value = ctx
      if (operationId.value && !operations.value.some(op => op.id === operationId.value)) operationId.value = ''
      initializeForm()
    } catch (e: any) {
      if (current === generation) error.value = e?.data?.statusMessage || e?.data?.message || 'Não foi possível carregar esta definição. Verifique o ambiente e sua autenticação.'
    } finally { if (current === generation) loading.value = false }
  }
  function initializeForm() {
    result.value = null; tryOpen.value = false
    parameters.value = { path: {}, query: {}, headers: {} }
    const op = selected.value
    if (!op) return
    for (const raw of op.parameters || []) {
      const p = resolveSchema(spec.value, raw)
      const location = p.in === 'header' ? 'headers' : p.in
      if (!parameters.value[location]) continue
      const value = p.schema?.default ?? (p.required ? p.example ?? p.schema?.example : undefined)
      if (value !== undefined) parameters.value[location][p.name] = String(value)
    }
    const request = resolveSchema(spec.value, op.requestBody || {})
    contentType.value = Object.keys(request.content || {})[0] || 'application/json'
    const media = request.content?.[contentType.value]
    body.value = media ? JSON.stringify(media.example ?? exampleForSchema(spec.value, media.schema || {}), null, 2) : ''
    responseCode.value = Object.keys(op.responses || {}).find(code => /^2\d\d$/.test(code)) || Object.keys(op.responses || {})[0] || '200'
  }
  function selectOperation(id: string) { cancel(); operationId.value = id; section.value = 'endpoints'; tab.value = 'informacoes'; initializeForm() }
  function cancel() { controller?.abort(); controller = undefined; running.value = false }
  async function execute(webToken: string, files: Record<string, File[]>, confirmedWrite: boolean) {
    if (!selected.value || !canExecute.value || running.value) return
    controller = new AbortController()
    const requestController = controller
    running.value = true; result.value = null; error.value = ''
    const form = new FormData()
    form.set('operationId', selected.value.id)
    form.set('parameters', JSON.stringify(parameters.value))
    form.set('body', body.value)
    form.set('contentType', contentType.value)
    form.set('confirmedWrite', String(confirmedWrite))
    for (const [name, items] of Object.entries(files)) for (const file of items) form.append(`file:${name}`, file)
    try {
      const response = await $fetch<any>(`/api/documentacao/${environment.value}/${definition.value}/executar`, { method: 'POST', headers: headers(webToken), body: form, signal: requestController.signal })
      if (!requestController.signal.aborted) result.value = response
    } catch (e: any) {
      if (!requestController.signal.aborted) result.value = { status: e.statusCode || 0, body: e?.data?.statusMessage || e?.data?.message || 'Falha de conexão. Tente novamente.', headers: {}, durationMs: 0 }
    } finally { if (controller === requestController) { running.value = false; controller = undefined } }
  }
  async function obtainToken(password: string) {
    const op = operations.value.find(op => op.path.endsWith('/token') && op.method.toLowerCase() === 'post')
    if (!op) throw new Error('Selecione a definição de Parceiros para obter um token.')
    const form = new FormData()
    form.set('operationId', op.id); form.set('parameters', '{}'); form.set('body', JSON.stringify({ senha: password })); form.set('contentType', 'application/json')
    const response = await $fetch<any>(`/api/documentacao/${environment.value}/${definition.value}/executar`, { method: 'POST', body: form })
    const data = typeof response.body === 'string' ? JSON.parse(response.body) : response.body
    if (response.status !== 200 || !data.token) throw new Error(data.message || data.error || 'Não foi possível autorizar.')
    setToken(data.token)
    return data.token as string
  }
  return { definition, environment, section, operationId, tab, language, search, spec, environments, supportUrl, context, loading, error, running, result, tryOpen, body, parameters, contentType, responseCode, operations, selected, visibleOperations, publicBase, authorized, requiresToken, writeOperation, externalOperation, canExecute, setToken, clearCredentials, load, selectOperation, initializeForm, execute, cancel, obtainToken }
})
