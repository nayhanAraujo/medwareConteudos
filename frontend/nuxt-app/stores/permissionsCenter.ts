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
  // Nuxt composables must be created synchronously while the store itself is
  // instantiated from a component setup. Actions reuse these clients later.
  const permissionsApi = usePermissoesApi()
  const api = useApi()

  const catalog = ref<PermissionCatalogItem[]>([])
  const profiles = ref<string[]>([])
  const selectedProfile = ref('comum')
  const profilePermissions = ref<string[]>([])
  const users = ref<PermissionUserItem[]>([])
  const selectedUserId = ref(0)
  const userState = ref<UserPermissionState | null>(null)
  const userOverrides = ref<UserPermissionOverride[]>([])
  const expanded = ref<string[]>(['biblioteca'])
  const loading = ref(false)
  const saving = ref(false)
  const error = ref('')

  const modules = computed<PermissionModuleConfig[]>(() => {
    const available = new Set(catalog.value.map((item) => item.chave.toLowerCase()))
    return permissionModules
      .map((module) => ({
        ...module,
        actions: module.actions.filter((action) => available.has(`${module.domain}.${action}`))
      }))
      .filter((module) => module.actions.length > 0)
  })
  const allPermissionKeys = computed(() => catalog.value.map((item) => item.chave.toLowerCase()))
  const isAdminProfile = computed(() => selectedProfile.value === 'admin')

  function actionLabel(action: string) {
    return actionLabels[action] || action
  }

  async function load() {
    loading.value = true
    error.value = ''
    try {
      const [catalogRes, profilesRes] = await Promise.all([
        permissionsApi.catalogo(),
        permissionsApi.perfis()
      ])
      catalog.value = (catalogRes.data || [])
        .map((item) => normalizeCatalogItem(item))
        .filter((item): item is PermissionCatalogItem => !!item)
      profiles.value = (profilesRes.data || []).map(normalizeProfile).filter(Boolean)
      if (!profiles.value.includes(selectedProfile.value)) {
        selectedProfile.value = profiles.value.includes('comum') ? 'comum' : profiles.value[0] || 'comum'
      }

      try {
        const usersRes = await api.get<{ data: PermissionUserItem[] }>('/api/web/users')
        users.value = usersRes.data || []
      } catch {
        users.value = []
      }

      await loadProfile()
    } catch (reason) {
      error.value = reason instanceof Error ? reason.message : 'Erro ao carregar permissões.'
    } finally {
      loading.value = false
    }
  }

  async function loadProfile() {
    if (!selectedProfile.value) return
    if (selectedProfile.value === 'admin') {
      profilePermissions.value = [...allPermissionKeys.value]
      return
    }
    const res = await permissionsApi.perfil(selectedProfile.value)
    profilePermissions.value = (res.data?.permissoes || []).map((item) => item.toLowerCase())
  }

  async function loadUser() {
    userState.value = null
    userOverrides.value = []
    if (!selectedUserId.value) return
    const res = await permissionsApi.usuario(Number(selectedUserId.value))
    userState.value = res.data
    userOverrides.value = (res.data?.excecoes || []).map((item) => ({ chave: item.chave.toLowerCase(), modo: item.modo }))
  }

  function profileHas(key: string) {
    return selectedProfile.value === 'admin' || profilePermissions.value.includes(key.toLowerCase())
  }

  function userEffectiveHas(key: string) {
    return !!userState.value?.efetivas?.some((item) => item.toLowerCase() === key.toLowerCase())
  }

  function userOverride(key: string) {
    return userOverrides.value.find((item) => item.chave.toLowerCase() === key.toLowerCase())?.modo || ''
  }

  function toggleExpanded(domain: string) {
    expanded.value = expanded.value.includes(domain)
      ? expanded.value.filter((item) => item !== domain)
      : [...expanded.value, domain]
  }

  function toggleProfilePermission(key: string, checked: boolean) {
    if (selectedProfile.value === 'admin') return
    const normalized = key.toLowerCase()
    const next = new Set(profilePermissions.value)
    if (checked) next.add(normalized)
    else next.delete(normalized)
    profilePermissions.value = [...next]
  }

  function setUserOverride(key: string, modo: '' | 'PERMITIR' | 'NEGAR') {
    const normalized = key.toLowerCase()
    const next = userOverrides.value.filter((item) => item.chave.toLowerCase() !== normalized)
    if (modo) next.push({ chave: normalized, modo })
    userOverrides.value = next
  }

  function toggleUserPermission(key: string, checked: boolean) {
    const inherited = userEffectiveHas(key)
    if (checked === inherited) setUserOverride(key, '')
    else setUserOverride(key, checked ? 'PERMITIR' : 'NEGAR')
  }

  function permitAll() {
    if (selectedProfile.value !== 'admin') profilePermissions.value = [...allPermissionKeys.value]
  }

  function denyAll() {
    if (selectedProfile.value !== 'admin') profilePermissions.value = []
  }

  function userPermitAll() {
    userOverrides.value = allPermissionKeys.value.map((chave) => ({ chave, modo: 'PERMITIR' }))
  }

  function userDenyAll() {
    userOverrides.value = allPermissionKeys.value.map((chave) => ({ chave, modo: 'NEGAR' }))
  }

  async function saveProfile() {
    if (selectedProfile.value === 'admin') return
    saving.value = true
    try {
      await permissionsApi.salvarPerfil(selectedProfile.value, profilePermissions.value)
    } finally {
      saving.value = false
    }
  }

  async function saveUser() {
    if (!selectedUserId.value) return
    saving.value = true
    try {
      await permissionsApi.salvarUsuario(Number(selectedUserId.value), userOverrides.value)
      await loadUser()
    } finally {
      saving.value = false
    }
  }

  return {
    catalog, profiles, selectedProfile, profilePermissions, users, selectedUserId,
    userState, userOverrides, expanded, loading, saving, error,
    modules, allPermissionKeys, isAdminProfile,
    actionLabel, load, loadProfile, loadUser, profileHas, userEffectiveHas,
    userOverride, toggleExpanded, toggleProfilePermission, setUserOverride,
    toggleUserPermission, permitAll, denyAll, userPermitAll, userDenyAll,
    saveProfile, saveUser
  }
})
