<template>
  <div class="permissions-page">
    <DsPageHeader title="Centro de Permissões" subtitle="Perfis de acesso e exceções individuais" icon="shield-lock" :show-user-info="false">
      <template #actions><DsButton variant="secondary" size="sm" icon="arrow-left" to="/biblioteca">Voltar</DsButton></template>
    </DsPageHeader>

    <main v-if="canView" class="permissions-center" :class="{ 'has-pending': store.dirtyCount > 0 }">
      <section class="context-toolbar" aria-label="Contexto das permissões">
        <div class="mode-tabs" role="group" aria-label="Visão de permissões">
          <button type="button" :aria-pressed="store.mode === 'perfil'" :disabled="contextLocked" @click="switchContext('perfil')"><i class="bi bi-diagram-3" aria-hidden="true" />Por perfil</button>
          <button type="button" :aria-pressed="store.mode === 'usuario'" :disabled="contextLocked" @click="switchContext('usuario')"><i class="bi bi-person-gear" aria-hidden="true" />Por usuário</button>
        </div>
        <div v-if="store.mode === 'perfil'" class="context-picker">
          <label for="permission-profile">Perfil</label>
          <select id="permission-profile" :value="store.selectedProfile" :disabled="contextLocked" @change="selectProfile">
            <option v-for="profile in store.profiles" :key="profile" :value="profile">{{ profileLabel(profile) }}</option>
          </select>
        </div>
        <PermissionUserPicker v-else :users="store.users" :model-value="store.selectedUserId" :disabled="contextLocked" @select="id => switchContext('usuario', undefined, id)" />
        <button type="button" class="command primary" :disabled="!canSave" @click="saveChanges"><i class="bi bi-save" aria-hidden="true" />{{ store.saving ? 'Salvando...' : 'Salvar' }}</button>
      </section>

      <div v-if="store.error" class="load-error" role="alert">{{ store.error }}<button type="button" class="command" :disabled="contextLocked" @click="retry">Tentar novamente</button></div>
      <div v-if="store.mode === 'usuario' && store.usersError" class="load-error" role="alert">{{ store.usersError }}<button type="button" class="command" @click="store.loadUsers">Recarregar usuários</button></div>
      <div v-if="store.ready && store.isAdmin" class="context-note"><i class="bi bi-lock" aria-hidden="true" />Administrador: acesso total por regra fixa.</div>
      <div v-else-if="store.mode === 'usuario' && store.userState" class="context-note">Perfil herdado: <strong>{{ profileLabel(store.userState.perfil) }}</strong></div>
      <div v-if="!canEdit" class="context-note"><i class="bi bi-eye" aria-hidden="true" />Somente leitura</div>

      <section class="filter-toolbar" aria-label="Filtros de permissões">
        <div class="search-field"><i class="bi bi-search" aria-hidden="true" /><input v-model="search" aria-label="Buscar módulo ou permissão" placeholder="Buscar módulo ou permissão" type="search" /></div>
        <label class="filter-field"><span>Exibir</span><select v-model="filter" aria-label="Filtrar permissões">
          <option value="todas">Todas</option><option value="permitidas">Permitidas</option><option value="negadas">Negadas</option>
          <option v-if="store.mode === 'usuario'" value="excecoes">Exceções</option><option value="alteradas">Alteradas</option>
        </select></label>
        <span class="result-count" aria-live="polite">{{ resultCount }} permissões</span>
      </section>

      <div v-if="store.loading" class="loading-state" role="status"><i class="bi bi-arrow-repeat" aria-hidden="true" />Carregando permissões...</div>
      <div v-else-if="store.mode === 'usuario' && !store.selectedUserId" class="empty-state"><i class="bi bi-person-gear" aria-hidden="true" /><h2>Selecione um usuário</h2></div>
      <div v-else-if="!store.ready && store.error" class="empty-state">Não foi possível carregar este cadastro.</div>
      <div v-else-if="!store.modules.length" class="empty-state"><h2>Nenhuma permissão disponível</h2></div>
      <div v-else-if="!visibleModules.length" class="empty-state"><i class="bi bi-search" aria-hidden="true" /><h2>Nenhuma permissão encontrada</h2><button type="button" class="command" @click="clearFilters">Limpar filtros</button></div>
      <div v-else class="permissions-workspace">
        <nav class="module-navigation" aria-label="Módulos de permissões">
          <button v-for="item in visibleModules" :key="item.module.domain" type="button" :aria-current="selectedModule === item.module.domain ? 'true' : undefined" @click="selectedModule = item.module.domain">
            <i :class="['bi', item.module.icon]" aria-hidden="true" /><span>{{ item.module.title }}<small v-if="store.changedCount(item.module)">{{ store.changedCount(item.module) }} alteradas</small></span>
            <span class="module-count">{{ store.enabledCount(item.module) }}/{{ item.module.actions.length }}</span>
          </button>
        </nav>
        <div class="module-content">
          <PermissionModuleCard v-for="item in visibleModules" :key="item.module.domain" :module="item.module" :actions="item.actions"
            :editable="editable" :expanded="expanded.includes(item.module.domain)" :class="{ 'not-selected': selectedModule !== item.module.domain }"
            @expand="toggleExpanded(item.module.domain)" @bulk="action => bulk(item.module, action)" />
        </div>
      </div>

      <div v-if="store.dirtyCount" class="pending-bar" role="region" aria-label="Alterações pendentes">
        <span role="status"><i class="bi bi-pencil" aria-hidden="true" /><strong>{{ store.dirtyCount }}</strong> {{ store.dirtyCount === 1 ? 'alteração pendente' : 'alterações pendentes' }}</span>
        <div><button type="button" class="command" :disabled="contextLocked" @click="store.discard"><i class="bi bi-arrow-counterclockwise" aria-hidden="true" />Descartar</button>
        <button type="button" class="command primary" :disabled="!canSave" @click="saveChanges"><i class="bi bi-save" aria-hidden="true" />{{ store.saving ? 'Salvando...' : 'Salvar alterações' }}</button></div>
      </div>
    </main>
    <DsAlert v-else variant="error">Você não possui permissão para visualizar configurações.</DsAlert>
  </div>
</template>

<script setup lang="ts">
import PermissionModuleCard from '~/components/permissions/PermissionModuleCard.vue'
import PermissionUserPicker from '~/components/permissions/PermissionUserPicker.vue'
import type { PermissionModuleConfig, PermissionViewMode } from '~/stores/permissionsCenter'
import { usePermissionsCenterStore } from '~/stores/permissionsCenter'
import { compactDialogOptions } from '~/utils/swalOptions'

definePageMeta({ layout: 'default', permissionsWorkspace: true })
const auth = useAuthStore(), store = usePermissionsCenterStore(), swal = useSwal()
const search = ref(''), filter = ref('todas'), selectedModule = ref(''), expanded = ref<string[]>([])
const transitioning = ref(false)
let mounted = false, prompting = false
const canView = computed(() => auth.isAdmin || auth.can('configuracoes', 'visualizar'))
const canEdit = computed(() => auth.isAdmin || auth.can('configuracoes', 'editar'))
const contextLocked = computed(() => store.loading || store.saving || transitioning.value)
const editable = computed(() => canEdit.value && store.canMutate && !transitioning.value)
const canSave = computed(() => canEdit.value && store.canMutate && store.dirtyCount > 0 && !transitioning.value)
function norm(value: string) { return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim() }
function profileLabel(profile: string) { return profile === 'admin' ? 'Administrador' : profile === 'comum' ? 'Usuário comum' : profile === 'usuario' ? 'Usuário padrão' : profile }
const visibleModules = computed(() => store.modules.map(module => ({
  module,
  actions: module.actions.filter(action => {
    const key = module.domain + '.' + action
    if (!norm(module.title + ' ' + module.domain + ' ' + store.actionLabel(action) + ' ' + store.description(key)).includes(norm(search.value))) return false
    if (filter.value === 'permitidas') return store.effectiveHas(key)
    if (filter.value === 'negadas') return !store.effectiveHas(key)
    if (filter.value === 'excecoes') return store.mode === 'usuario' && !!store.userOverride(key)
    if (filter.value === 'alteradas') return store.changed(key)
    return true
  })
})).filter(item => item.actions.length > 0))
const resultCount = computed(() => store.ready ? visibleModules.value.reduce((sum, item) => sum + item.actions.length, 0) : 0)
watch(visibleModules, items => {
  if (!items.some(item => item.module.domain === selectedModule.value)) selectedModule.value = items[0]?.module.domain || ''
  if (search.value.trim() || filter.value !== 'todas') expanded.value = items.map(item => item.module.domain)
}, { immediate: true })
watch(() => store.mode, () => { if (filter.value === 'excecoes') filter.value = 'todas' })
watch(canView, allowed => { if (mounted && allowed) store.load() })
function toggleExpanded(domain: string) { expanded.value = expanded.value.includes(domain) ? expanded.value.filter(item => item !== domain) : [...expanded.value, domain] }
function clearFilters() { search.value = ''; filter.value = 'todas' }

async function saveChanges() {
  if (!canEdit.value || !store.canMutate || !store.dirtyCount) return !store.dirtyCount
  try { await store.save(); await swal.toast('Permissões salvas.'); return true }
  catch (reason) { await swal.error('Erro ao salvar permissões', reason instanceof Error ? reason.message : String(reason)); return false }
}
async function guardPending() {
  if (store.saving || prompting) return false
  if (!store.dirtyCount) return true
  prompting = true
  try {
    const { default: Swal } = await import('sweetalert2')
    const result = await Swal.fire({
      ...compactDialogOptions, titleText: 'Alterações pendentes',
      text: store.dirtyCount + ' alterações em ' + contextLabel.value + '.',
      showCancelButton: true, showDenyButton: true, showConfirmButton: canEdit.value,
      confirmButtonText: 'Salvar e continuar', denyButtonText: 'Descartar e continuar', cancelButtonText: 'Cancelar',
      allowOutsideClick: false
    })
    if (result.isConfirmed) return await saveChanges()
    if (result.isDenied) { store.discard(); return true }
    return false
  } finally { prompting = false }
}
const contextLabel = computed(() => store.mode === 'perfil' ? 'perfil ' + profileLabel(store.selectedProfile) : 'usuário ' + (store.users.find(user => user.codUsuario === store.selectedUserId)?.nome || store.selectedUserId))
async function switchContext(mode: PermissionViewMode, profile = store.selectedProfile, userId = store.selectedUserId) {
  if (contextLocked.value || (mode === store.mode && profile === store.selectedProfile && userId === store.selectedUserId)) return
  transitioning.value = true
  try {
    if (!await guardPending()) return
    await store.selectContext(mode, profile, userId)
  } finally { transitioning.value = false }
}
function selectProfile(event: Event) {
  const el = event.target as HTMLSelectElement, value = el.value
  el.value = store.selectedProfile
  void switchContext('perfil', value)
}
async function retry() {
  if (contextLocked.value) return
  if (!store.catalog.length) await store.load()
  else await store.selectContext(store.mode)
}
async function bulk(module: PermissionModuleConfig, action: 'permitir' | 'negar' | 'herdar') {
  if (!editable.value) return
  const mode = store.mode, profile = store.selectedProfile, userId = store.selectedUserId
  const label = action === 'permitir' ? 'Permitir tudo' : action === 'negar' ? 'Negar tudo' : 'Restaurar herança'
  transitioning.value = true
  try {
    const result = await swal.confirm(label, contextLabel.value + ' · ' + module.title + ': ' + module.actions.length + ' permissões, incluindo as ocultas pelos filtros.')
    if (result?.isConfirmed && canEdit.value && mode === store.mode && profile === store.selectedProfile && userId === store.selectedUserId) store.applyModule(module, action)
  } finally { transitioning.value = false }
}
function beforeUnload(event: BeforeUnloadEvent) {
  if (store.dirtyCount || store.saving) { event.preventDefault(); event.returnValue = '' }
}
onBeforeRouteLeave(async () => {
  if (transitioning.value) return false
  return await guardPending()
})
onMounted(() => {
  mounted = true
  window.addEventListener('beforeunload', beforeUnload)
  if (canView.value) store.load()
})
onBeforeUnmount(() => { mounted = false; window.removeEventListener('beforeunload', beforeUnload); store.invalidate() })
</script>

<style scoped>
.permissions-center { margin: 24px 0; background: #fff; border: 1px solid #dce0e5; border-radius: 8px; min-width: 0; color: #252b36; }
.context-toolbar { display: grid; grid-template-columns: auto minmax(220px, 1fr) auto; align-items: end; gap: 20px; padding: 20px; border-bottom: 1px solid #e5e7eb; }
.mode-tabs { display: flex; border-bottom: 1px solid #dce0e5; height: 40px; }
.mode-tabs button { display: flex; align-items: center; justify-content: center; gap: 8px; border: 0; border-bottom: 2px solid transparent; background: white; color: #667085; padding: 0 14px; white-space: nowrap; font-size: 14px; }
.mode-tabs button[aria-pressed=true] { border-bottom-color: #2563eb; color: #1d4ed8; font-weight: 600; }
.context-picker label { display: block; font-size: 12px; font-weight: 600; color: #4b5563; margin-bottom: 4px; }
select { font-size: 14px; border: 1px solid #cbd0d7; border-radius: 6px; padding: 8px 10px; background: white; height: 40px; }
.context-picker select { width: 100%; }
.command { display: inline-flex; justify-content: center; align-items: center; gap: 7px; border: 1px solid #cbd0d7; border-radius: 6px; padding: 9px 13px; background: white; font-size: 13px; font-weight: 500; min-height: 40px; color: #374151; }
.command.primary { background: #2563eb; border-color: #2563eb; color: white; }
.command:hover:not(:disabled) { filter: brightness(.96); }
:disabled { opacity: .55; cursor: not-allowed; }
button:focus-visible, select:focus-visible, input:focus-visible { outline: 2px solid #2563eb; outline-offset: 2px; }
.context-note { display: flex; align-items: center; gap: 8px; font-size: 13px; color: #4b5563; padding: 10px 20px; background: #f7f8fa; border-bottom: 1px solid #e5e7eb; }
.context-note strong { font-weight: 600; }
.load-error { display: flex; justify-content: space-between; align-items: center; gap: 12px; background: #fff1f2; color: #9f2442; padding: 12px 20px; font-size: 13px; }
.filter-toolbar { display: flex; align-items: center; gap: 16px; padding: 16px 20px; border-bottom: 1px solid #e5e7eb; flex-wrap: wrap; }
.search-field { display: flex; align-items: center; gap: 8px; flex: 1; min-width: 200px; color: #667085; }
.search-field input { border: 1px solid #cbd0d7; border-radius: 6px; padding: 9px 10px; font-size: 14px; width: 100%; min-width: 0; height: 40px; }
.filter-field { display: flex; align-items: center; gap: 8px; font-size: 13px; color: #667085; }
.filter-field select { min-width: 130px; }
.result-count { font-size: 12px; color: #667085; white-space: nowrap; }
.permissions-workspace { display: grid; grid-template-columns: 240px minmax(0, 1fr); }
.module-navigation { border-right: 1px solid #e5e7eb; padding: 12px 8px; background: #fafbfc; border-radius: 0 0 0 8px; }
.module-navigation button { display: flex; width: 100%; align-items: center; gap: 10px; padding: 11px 10px; border: 0; border-radius: 6px; background: transparent; text-align: left; color: #475467; font-size: 13px; }
.module-navigation button:hover { background: #f0f2f5; }
.module-navigation button[aria-current=true] { background: #eaf1ff; color: #1d4ed8; font-weight: 600; }
.module-navigation button > span:first-of-type { flex: 1; min-width: 0; overflow-wrap: anywhere; }
.module-navigation small { display: block; color: #906407; font-size: 11px; margin-top: 3px; }
.module-count { font-size: 12px; font-variant-numeric: tabular-nums; }
.module-content { min-width: 0; padding: 20px; }
.empty-state, .loading-state { display: flex; align-items: center; justify-content: center; flex-direction: column; gap: 12px; min-height: 240px; padding: 24px; color: #667085; text-align: center; font-size: 14px; }
.empty-state > i { font-size: 28px; }
.empty-state h2 { margin: 0; font-size: 16px; font-weight: 500; color: #344054; }
.has-pending { padding-bottom: 90px; }
.pending-bar { position: fixed; bottom: 16px; left: 304px; right: 32px; z-index: 25; display: flex; align-items: center; justify-content: space-between; gap: 16px; border: 1px solid #d5cba8; border-radius: 8px; background: #fffdf5; padding: 12px 16px; box-shadow: 0 2px 8px #0000000a; }
.pending-bar > span { display: flex; align-items: center; gap: 6px; font-size: 13px; }
.pending-bar > div { display: flex; gap: 8px; }
@media (min-width: 1024px) { .module-content > .not-selected { display: none; } }
@media (max-width: 1200px) { .context-toolbar { gap: 12px; } .mode-tabs button { padding: 0 10px; } .permissions-workspace { grid-template-columns: 200px minmax(0, 1fr); } .module-content { padding: 12px; } }
@media (max-width: 1023px) {
  .context-toolbar { grid-template-columns: minmax(0, 1fr) auto; padding: 16px; }
  .mode-tabs { grid-column: 1 / -1; }
  .mode-tabs button { flex: 1; }
  .permissions-workspace { display: block; }
  .module-navigation { display: none; }
  .module-content { display: flex; flex-direction: column; gap: 12px; }
  .pending-bar { left: 68px; right: 12px; }
}
@media (max-width: 600px) {
  .mode-tabs button { min-width: 0; padding: 0 6px; gap: 5px; }
  .context-toolbar { gap: 12px; padding: 12px; }
  .filter-toolbar { padding: 12px; gap: 10px; }
  .search-field { flex-basis: 100%; }
  .filter-field { flex: 1; }
  .result-count { margin-left: auto; }
  .pending-bar { flex-direction: column; align-items: stretch; gap: 10px; padding: 10px; bottom: 8px; left: 68px; right: 12px; }
  .pending-bar > div { display: grid; grid-template-columns: 1fr 1fr; }
  .pending-bar > span { justify-content: center; }
  .pending-bar .command { padding: 9px; }
  .has-pending { padding-bottom: 140px; }
  .load-error { align-items: flex-start; flex-direction: column; }
  :deep(header h1) { font-size: 20px; }
}
</style>
