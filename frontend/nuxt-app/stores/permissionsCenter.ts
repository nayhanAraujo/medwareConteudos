import { defineStore } from 'pinia'
import type { PermissionCatalogItem, UserPermissionOverride, UserPermissionState } from '~/composables/usePermissoesApi'

export type PermissionViewMode = 'perfil' | 'usuario'

export interface PermissionModuleConfig {
  domain: string
  title: string
  icon: string
  actions: string[]
}

export interface PermissionUserItem {
  codUsuario: number
  nome: string
  identificacao: string
  perfil: string
  status: number
}

export const permissionModules: PermissionModuleConfig[] = [
  { domain: 'studio', title: 'Studio', icon: 'bi-magic', actions: ['visualizar', 'converter', 'voz'] },
  { domain: 'orcamentos', title: 'Central de Orçamentos', icon: 'bi-receipt', actions: ['visualizar', 'elaborar', 'usar_ia', 'aprovar', 'registrar_marcos', 'configurar_precos', 'configurar_clientes', 'configurar_regras', 'importar_email', 'confirmar_email', 'enviar_email_confirmado', 'consultar_clientes_admin', 'importar_cliente_admin'] },
  { domain: 'biblioteca', title: 'Biblioteca', icon: 'bi-folder2-open', actions: ['visualizar'] },
  { domain: 'aprovacao-conteudo', title: 'Aprovação de Conteúdo', icon: 'bi-file-earmark-check', actions: ['visualizar', 'aprovar'] },
  { domain: 'conteudos', title: 'Conteúdos', icon: 'bi-folder', actions: ['visualizar', 'criar', 'editar', 'excluir', 'importar', 'exportar', 'ativar'] },
  { domain: 'relatorios', title: 'Relatórios', icon: 'bi-file-earmark-bar-graph', actions: ['visualizar', 'criar', 'editar', 'excluir', 'importar', 'exportar', 'aprovar', 'ativar'] },
  { domain: 'paineis', title: 'Painéis', icon: 'bi-window-stack', actions: ['visualizar', 'criar', 'editar', 'excluir', 'exportar', 'ativar'] },
  { domain: 'assistente', title: 'Assistente', icon: 'bi-wrench-adjustable', actions: ['visualizar', 'criar', 'editar', 'excluir', 'importar', 'exportar', 'aprovar', 'vincular', 'ativar'] },
  { domain: 'usuarios', title: 'Usuários', icon: 'bi-people', actions: ['visualizar', 'criar', 'editar', 'excluir', 'ativar'] },
  { domain: 'configuracoes', title: 'Configurações', icon: 'bi-gear', actions: ['visualizar', 'criar', 'editar', 'excluir'] },
  { domain: 'variaveis', title: 'Variáveis', icon: 'bi-sliders2-vertical', actions: ['visualizar', 'criar', 'editar', 'excluir', 'importar', 'exportar', 'vincular', 'ativar'] },
  { domain: 'formulas', title: 'Fórmulas', icon: 'bi-flask', actions: ['visualizar', 'criar', 'editar', 'excluir', 'vincular'] },
  { domain: 'referencias', title: 'Referências', icon: 'bi-diagram-3', actions: ['visualizar', 'criar', 'editar', 'excluir', 'importar', 'exportar', 'vincular'] },
  { domain: 'scripts', title: 'Scripts', icon: 'bi-journal-code', actions: ['visualizar', 'criar', 'editar', 'excluir', 'importar', 'exportar', 'aprovar', 'vincular', 'ativar'] },
  { domain: 'pacotes', title: 'Pacotes', icon: 'bi-box-seam', actions: ['visualizar', 'criar', 'editar', 'excluir', 'vincular', 'ativar'] }
]

const actionLabels: Record<string, string> = {
  consultar_clientes_admin:'Consultar identidade de clientes no Admin', importar_cliente_admin:'Confirmar importação de cliente Admin',
  importar_email: 'Importar solicitações de e-mail', confirmar_email: 'Confirmar origem e autorizar envio por bot', enviar_email_confirmado: 'Executar envio autorizado de proposta',
  elaborar: 'Elaborar/editar', usar_ia: 'Usar IA', registrar_marcos: 'Registrar marcos comerciais', configurar_precos: 'Configurar preços', configurar_clientes: 'Configurar clientes', configurar_regras: 'Configurar regras técnicas',
  converter: 'Converter',
  voz: 'Usar voz',
  visualizar: 'Visualizar',
  criar: 'Criar',
  editar: 'Editar',
  excluir: 'Excluir',
  importar: 'Importar',
  exportar: 'Exportar',
  aprovar: 'Aprovar',
  vincular: 'Vincular',
  ativar: 'Ativar/Inativar'
}

function normalizeCatalogItem(item: PermissionCatalogItem | Record<string, unknown>): PermissionCatalogItem | null {
  const source = item as Record<string, unknown>
  const chave = String(source.chave ?? source.Chave ?? '').trim().toLowerCase()
  const dominio = String(source.dominio ?? source.Dominio ?? '').trim().toLowerCase()
  const acao = String(source.acao ?? source.Acao ?? '').trim().toLowerCase()
  const descricao = String(source.descricao ?? source.Descricao ?? '')
  const rawStatus = source.status ?? source.Status ?? -1
  const status = typeof rawStatus === 'number' ? rawStatus : Number(rawStatus)

  if (!chave || !dominio || !acao) return null
  return { chave, dominio, acao, descricao, status: Number.isFinite(status) ? status : -1 }
}

function normalizeProfile(value: string) {
  return String(value || '').trim().toLowerCase()
}

export const usePermissionsCenterStore = defineStore('permissionsCenter', () => {
  const permissionsApi = usePermissoesApi()
  const api = useApi()
  const mode = ref<PermissionViewMode>('perfil')
  const catalog = ref<PermissionCatalogItem[]>([])
  const profiles = ref<string[]>([])
  const selectedProfile = ref('comum')
  const selectedUserId = ref(0)
  const users = ref<PermissionUserItem[]>([])
  const userState = ref<UserPermissionState | null>(null)
  const profilePermissions = ref<string[]>([])
  const inheritedPermissions = ref<string[]>([])
  const userOverrides = ref<UserPermissionOverride[]>([])
  const savedProfile = ref<string[]>([])
  const savedOverrides = ref<UserPermissionOverride[]>([])
  const loading = ref(false), saving = ref(false), ready = ref(false)
  const error = ref(''), usersError = ref('')
  let revision = 0

  const modules = computed<PermissionModuleConfig[]>(() => {
    const available = new Set(catalog.value.map(item => item.chave))
    return permissionModules.map(module => ({
      ...module, actions: module.actions.filter(action => available.has(module.domain + '.' + action))
    })).filter(module => module.actions.length > 0)
  })
  const allPermissionKeys = computed(() => catalog.value.map(item => item.chave))
  const isAdminProfile = computed(() => selectedProfile.value === 'admin')
  const isAdmin = computed(() => mode.value === 'perfil' ? isAdminProfile.value : userState.value?.perfil === 'admin')
  const changedKeys = computed(() => {
    if (!ready.value || isAdmin.value) return []
    if (mode.value === 'perfil') {
      const before = new Set(savedProfile.value), after = new Set(profilePermissions.value)
      return [...new Set([...before, ...after])].filter(key => before.has(key) !== after.has(key))
    }
    const before = new Map(savedOverrides.value.map(item => [item.chave, item.modo]))
    const after = new Map(userOverrides.value.map(item => [item.chave, item.modo]))
    return [...new Set([...before.keys(), ...after.keys()])].filter(key => before.get(key) !== after.get(key))
  })
  const dirtyCount = computed(() => changedKeys.value.length)
  const canMutate = computed(() => ready.value && !loading.value && !saving.value && !isAdmin.value)

  function actionLabel(action: string) { return actionLabels[action] || action }
  function description(key: string) { return catalog.value.find(item => item.chave === key)?.descricao || '' }
  function profileHas(key: string) { return isAdminProfile.value || profilePermissions.value.includes(key.toLowerCase()) }
  function userOverride(key: string) { return userOverrides.value.find(item => item.chave === key.toLowerCase())?.modo || '' }
  function userEffectiveHas(key: string) {
    if (userState.value?.perfil === 'admin') return true
    const override = userOverride(key)
    return override ? override === 'PERMITIR' : inheritedPermissions.value.includes(key.toLowerCase())
  }
  function effectiveHas(key: string) { return mode.value === 'perfil' ? profileHas(key) : userEffectiveHas(key) }
  function changed(key: string) { return changedKeys.value.includes(key) }
  function enabledCount(module: PermissionModuleConfig) { return module.actions.filter(action => effectiveHas(module.domain + '.' + action)).length }
  function changedCount(module: PermissionModuleConfig) { return module.actions.filter(action => changed(module.domain + '.' + action)).length }
  function resultLabel(key: string) {
    if (isAdmin.value) return 'Permitido · administrador'
    if (mode.value === 'usuario' && userOverride(key)) return userOverride(key) === 'PERMITIR' ? 'Permitido por exceção' : 'Negado por exceção'
    return effectiveHas(key) ? 'Permitido pelo perfil' : 'Negado pelo perfil'
  }
  function copyOverrides(items: UserPermissionOverride[]) { return items.map(item => ({ ...item })) }

  async function loadUsers() {
    usersError.value = ''
    try { users.value = (await api.get<{ data: PermissionUserItem[] }>('/api/web/users')).data || [] }
    catch (reason) { users.value = []; usersError.value = reason instanceof Error ? reason.message : 'Erro ao carregar usuários.' }
  }
  async function load() {
    const request = ++revision
    loading.value = true; ready.value = false; error.value = ''
    try {
      const [c, p] = await Promise.all([permissionsApi.catalogo(), permissionsApi.perfis(), loadUsers()])
      if (request !== revision) return
      catalog.value = (c.data || []).map(normalizeCatalogItem).filter((item): item is PermissionCatalogItem => !!item && item.status === -1)
      profiles.value = [...new Set((p.data || []).map(normalizeProfile).filter(Boolean))]
      if (!profiles.value.includes(selectedProfile.value)) selectedProfile.value = profiles.value.includes('comum') ? 'comum' : profiles.value[0] || ''
      await selectContext(mode.value, selectedProfile.value, selectedUserId.value)
    } catch (reason) {
      if (request === revision) { error.value = reason instanceof Error ? reason.message : 'Erro ao carregar permissões.'; loading.value = false }
    }
  }
  async function selectContext(nextMode: PermissionViewMode, profile = selectedProfile.value, userId = selectedUserId.value) {
    if (saving.value) return
    const request = ++revision
    mode.value = nextMode; selectedProfile.value = profile; selectedUserId.value = userId
    loading.value = true; ready.value = false; error.value = ''
    userState.value = null; profilePermissions.value = []; inheritedPermissions.value = []; userOverrides.value = []
    savedProfile.value = []; savedOverrides.value = []
    try {
      if (nextMode === 'perfil') {
        if (!profile) return
        const keys = profile === 'admin' ? allPermissionKeys.value : (await permissionsApi.perfil(profile)).data?.permissoes || []
        if (request !== revision) return
        profilePermissions.value = [...new Set(keys.map(key => key.toLowerCase()))]
        savedProfile.value = [...profilePermissions.value]
      } else {
        if (!userId) return
        const state = (await permissionsApi.usuario(userId)).data
        if (!state) throw new Error('Usuário não encontrado.')
        const base = normalizeProfile(state.perfil) === 'admin' ? allPermissionKeys.value : (await permissionsApi.perfil(state.perfil)).data?.permissoes || []
        if (request !== revision) return
        userState.value = { ...state, perfil: normalizeProfile(state.perfil) }
        inheritedPermissions.value = base.map(key => key.toLowerCase())
        userOverrides.value = state.excecoes.map(item => ({ chave: item.chave.toLowerCase(), modo: item.modo }))
        savedOverrides.value = copyOverrides(userOverrides.value)
      }
      ready.value = true
    } catch (reason) {
      if (request === revision) error.value = reason instanceof Error ? reason.message : 'Erro ao carregar permissões.'
    } finally { if (request === revision) loading.value = false }
  }
  function toggleProfilePermission(key: string, checked: boolean) {
    if (!canMutate.value) return
    const next = new Set(profilePermissions.value)
    if (checked) next.add(key); else next.delete(key)
    profilePermissions.value = [...next]
  }
  function setUserOverride(key: string, value: '' | 'PERMITIR' | 'NEGAR') {
    if (!canMutate.value) return
    userOverrides.value = userOverrides.value.filter(item => item.chave !== key)
    if (value) userOverrides.value.push({ chave: key, modo: value })
  }
  function applyModule(module: PermissionModuleConfig, action: 'permitir' | 'negar' | 'herdar') {
    if (!canMutate.value) return
    for (const item of module.actions) {
      const key = module.domain + '.' + item
      if (mode.value === 'perfil') {
        if (action !== 'herdar') toggleProfilePermission(key, action === 'permitir')
      } else setUserOverride(key, action === 'herdar' ? '' : action === 'permitir' ? 'PERMITIR' : 'NEGAR')
    }
  }
  function discard() {
    if (saving.value) return
    profilePermissions.value = [...savedProfile.value]
    userOverrides.value = copyOverrides(savedOverrides.value)
  }
  async function save() {
    if (!canMutate.value || !dirtyCount.value) return
    const request = revision, profile = selectedProfile.value, userId = selectedUserId.value
    const keys = [...profilePermissions.value], overrides = copyOverrides(userOverrides.value)
    saving.value = true
    try {
      if (mode.value === 'perfil') await permissionsApi.salvarPerfil(profile, keys)
      else await permissionsApi.salvarUsuario(userId, overrides)
      if (request === revision) { savedProfile.value = keys; savedOverrides.value = overrides }
    } finally { saving.value = false }
  }
  function invalidate() { revision++; loading.value = false; ready.value = false }

  return {
    mode, catalog, profiles, selectedProfile, selectedUserId, users, userState, profilePermissions, inheritedPermissions, userOverrides,
    loading, saving, ready, error, usersError, modules, allPermissionKeys, isAdminProfile, isAdmin, changedKeys, dirtyCount, canMutate,
    actionLabel, description, profileHas, userOverride, userEffectiveHas, effectiveHas, changed, enabledCount, changedCount, resultLabel,
    load, loadUsers, selectContext, toggleProfilePermission, setUserOverride, applyModule, discard, save, invalidate
  }
})
