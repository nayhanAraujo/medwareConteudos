<template>
  <div>
    <DsPageShell panel-class="!border-0 !bg-transparent !p-0 !shadow-none !ring-0">
      <AssistenteNav />
      <DsAlert v-if="!canWrite" variant="info" class="mb-4">Consulta liberada. Alterações exigem permissão no módulo Assistente.</DsAlert>
      <DsAlert v-if="error" variant="error" class="mb-4">{{ error }}</DsAlert>

      <AssistenteScriptFilterBar
        v-model:search="search"
        v-model:tipo="tipoFiltro"
        v-model:especialidade="especialidadeFiltro"
        v-model:status="statusFiltro"
        :especialidades="especialidades"
        @clear="clearFilters"
        @search="loadGroups"
      />

      <div v-if="loadingGroups" class="py-12 text-center text-gray-500">Carregando especialidades...</div>
      <DsEmptyState v-else-if="!visibleGroups.length" title="Nenhum script encontrado" message="Ajuste a pesquisa ou os filtros para localizar outros modelos." icon="code-square" />
      <div v-else class="lg:grid lg:grid-cols-[minmax(320px,38%)_minmax(0,1fr)] lg:gap-6" :class="selectedRows.length ? 'pb-6' : ''">
        <aside :class="mobileDetailOpen ? 'hidden lg:block' : 'block'">
          <div class="mb-3">
            <h2 class="text-sm font-semibold uppercase text-ds-text">Especialidades</h2>
            <p class="mt-0.5 text-xs text-gray-500">{{ visibleGroups.length }} grupo{{ visibleGroups.length === 1 ? '' : 's' }} encontrado{{ visibleGroups.length === 1 ? '' : 's' }}</p>
          </div>
          <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-1 2xl:grid-cols-2">
            <AssistenteSpecialtyCard
              v-for="group in visibleGroups"
              :key="group.id"
              :group="group"
              :selected="selectedGroupId === group.id"
              @select="selectGroup(group.id)"
            />
          </div>
        </aside>

        <main :class="mobileDetailOpen ? 'block' : 'hidden lg:block'" class="min-w-0">
          <template v-if="selectedGroup && selectedGroupState">
            <button type="button" class="mb-4 inline-flex items-center gap-2 text-sm font-medium text-gray-600 hover:text-gray-900 lg:hidden" @click="mobileDetailOpen = false">
              <i class="bi bi-arrow-left" /> Voltar para especialidades
            </button>

            <section class="border-b border-gray-200 pb-4">
              <div class="flex flex-wrap items-start justify-between gap-3">
                <div class="flex min-w-0 items-center gap-3">
                  <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-gray-100 text-gray-700"><i class="bi bi-folder2-open" /></span>
                  <div class="min-w-0">
                    <h2 class="break-words text-base font-semibold uppercase text-ds-text">{{ selectedGroup.nome }}</h2>
                    <p class="mt-0.5 text-xs text-gray-500">
                      {{ selectedGroup.totalScripts }} modelo{{ selectedGroup.totalScripts === 1 ? '' : 's' }}
                      <span v-if="selectedGroupState.items.length"> · {{ selectedGroupState.items.length }} carregado{{ selectedGroupState.items.length === 1 ? '' : 's' }}</span>
                    </p>
                  </div>
                </div>
                <DsButton v-if="canCreate" :to="addScriptTarget" variant="primary" size="sm" icon="plus-lg">Adicionar novo script</DsButton>
              </div>

              <div class="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-gray-100 pt-4">
                <label class="flex cursor-pointer items-center gap-2 text-sm text-gray-700">
                  <input type="checkbox" class="rounded border-gray-300 text-ds-primary focus:ring-ds-primary" :checked="isGroupSelected(selectedGroup.id)" :disabled="!selectedGroupState.items.length" @change="toggleGroupSelection(selectedGroup.id)">
                  Selecionar exibidos
                </label>
                <div class="flex flex-wrap items-center gap-2">
                  <span v-if="selectedRows.length" class="text-xs text-gray-500">{{ selectedRows.length }} selecionado{{ selectedRows.length === 1 ? '' : 's' }} no total</span>
                  <DsButton variant="secondary" size="sm" icon="file-earmark-zip" :disabled="!selectedRows.length" @click="exportSelectedScripts">Exportar selecionados</DsButton>
                  <DsButton v-if="canDelete" variant="danger" size="sm" icon="trash" :disabled="!selectedRows.length" @click="deleteSelectedScripts">Excluir selecionados</DsButton>
                </div>
              </div>
            </section>

            <div v-if="selectedGroupState.loading && !selectedGroupState.items.length" class="py-12 text-center text-sm text-gray-500">Carregando modelos...</div>
            <div v-else-if="selectedGroupState.error && !selectedGroupState.items.length" class="space-y-3 py-5">
              <DsAlert variant="error">{{ selectedGroupState.error }}</DsAlert>
              <DsButton variant="secondary" size="sm" icon="arrow-clockwise" @click="loadGroup(selectedGroup.id, true)">Tentar novamente</DsButton>
            </div>
            <div v-else-if="selectedGroupState.items.length" class="mt-4 space-y-3">
              <AssistenteScriptItem
                v-for="script in selectedGroupState.items"
                :key="scriptId(script)"
                :script="script"
                :selected="selectedIds.includes(scriptId(script))"
                :admin="canWrite"
                @toggle-selection="toggleScriptSelection(script)"
                @links="openLinks(script)"
                @export="exportScript(script)"
                @edit="openEditor(script)"
                @toggle-status="toggleStatus(script)"
                @delete="openDelete(script)"
              />
            </div>
            <DsEmptyState v-else title="Nenhum modelo nesta especialidade" icon="code-square" />
            <DsAlert v-if="selectedGroupState.error && selectedGroupState.items.length" variant="error" class="mt-4">{{ selectedGroupState.error }}</DsAlert>
            <div v-if="selectedGroupState.items.length < selectedGroupState.total" class="mt-4 flex justify-center">
              <DsButton variant="secondary" size="sm" icon="chevron-down" :loading="selectedGroupState.loading" @click="loadGroup(selectedGroup.id)">Carregar mais</DsButton>
            </div>
          </template>
        </main>
      </div>
    </DsPageShell>

    <DsModal v-model="editorOpen" title="Editar script" size="xl" :close-on-backdrop="!saving">
      <p class="mb-4 text-sm text-gray-600"><span class="font-semibold text-red-600">*</span> Campos obrigatórios</p>
      <div class="grid gap-4 md:grid-cols-2">
        <DsInput v-model="form.titulo" label="Título *" required />
        <DsSelect v-model="form.tipoScript" label="Tipo *" required><option :value="1">VB (legado)</option><option :value="2">C#</option><option :value="3">JSON</option></DsSelect>
        <DsSelect v-model="form.status" label="Status *" required><option :value="-1">Ativo</option><option :value="0">Inativo</option></DsSelect>
        <div class="hidden md:block" />
        <AssistenteMultiSelect v-model="form.especialidades" label="Especialidades" :options="especialidades" class="md:col-span-2" />
        <AssistenteMultiSelect v-model="form.procedimentos" label="Procedimentos" :options="procedimentos" class="md:col-span-2" />
      </div>
      <template #footer><DsButton variant="secondary" :disabled="saving" @click="editorOpen = false">Cancelar</DsButton><DsButton :loading="saving" :disabled="!canEdit" @click="saveEditor">Salvar</DsButton></template>
    </DsModal>

    <DsModal v-model="linksOpen" title="Vínculos do registro" size="xl"><AssistenteVinculosPanel v-if="linkedId" domain="scripts" :id="linkedId" @updated="refreshAll" /></DsModal>

    <DsModal v-model="deleteOpen" title="Excluir script" size="xl" :close-on-backdrop="!deleting">
      <div v-if="deleteLoading" class="py-10 text-center text-gray-500">Consultando vínculos...</div>
      <DsAlert v-else-if="deleteError" variant="error">{{ deleteError }}</DsAlert>
      <template v-else-if="deleteDetail">
        <div class="mb-4 rounded-lg bg-gray-50 p-4"><div class="text-xs uppercase text-gray-500">Script #{{ deleteDetail.id }}</div><div class="font-semibold text-ds-text">{{ deleteDetail.nome }}</div><div class="mt-2 text-sm text-gray-600">{{ deleteTotal }} vínculo{{ deleteTotal === 1 ? '' : 's' }} encontrado{{ deleteTotal === 1 ? '' : 's' }}.</div></div>
        <DsAlert :variant="deleteTotal ? 'warning' : 'info'" class="mb-4">{{ deleteTotal ? 'Os vínculos abaixo serão removidos. Os registros vinculados não serão excluídos.' : 'Este script não possui vínculos cadastrados.' }}</DsAlert>
        <div v-if="deleteLinkedGroups.length" class="space-y-3">
          <div v-for="group in deleteLinkedGroups" :key="group.relacao" class="rounded-lg border border-gray-200 p-4">
            <div class="mb-2 flex items-center justify-between gap-3"><h4 class="font-semibold">{{ group.titulo }}</h4><DsBadge variant="neutral">{{ group.itens.length }}</DsBadge></div>
            <ul class="space-y-1 text-sm text-gray-700"><li v-for="item in group.itens.slice(0, 5)" :key="item.id">{{ item.nome }} <span class="text-gray-400">#{{ item.id }}</span></li></ul>
            <p v-if="group.itens.length > 5" class="mt-2 text-xs text-gray-500">+{{ group.itens.length - 5 }} vínculo{{ group.itens.length - 5 === 1 ? '' : 's' }}</p>
          </div>
        </div>
      </template>
      <template #footer><DsButton variant="secondary" :disabled="deleting" @click="deleteOpen = false">Cancelar</DsButton><DsButton variant="danger" :loading="deleting" :disabled="deleteLoading || !!deleteError || !deleteRow || !canDelete" @click="confirmDelete">Excluir</DsButton></template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
import type { AssistenteEntity, AssistenteLinksDetail, AssistenteOption, AssistenteScriptEspecialidadeGroup } from '~/composables/useAssistenteApi'

definePageMeta({ layout: 'default' })

interface GroupState { items: AssistenteEntity[]; page: number; total: number; loading: boolean; loaded: boolean; error: string }

const PAGE_SIZE = 20
const api = useAssistenteApi()
const auth = useAuthStore()
const swal = useSwal()
const canCreate = computed(() => auth.can('assistente', 'criar'))
const canEdit = computed(() => auth.can('assistente', 'editar'))
const canDelete = computed(() => auth.can('assistente', 'excluir'))
const canActivate = computed(() => auth.can('assistente', 'ativar'))
const canWrite = computed(() => canCreate.value || canEdit.value || canDelete.value || canActivate.value)
const search = ref(String(useRoute().query.search || ''))
const tipoFiltro = ref('')
const especialidadeFiltro = ref('')
const statusFiltro = ref('-1')
const especialidades = ref<AssistenteOption[]>([])
const procedimentos = ref<AssistenteOption[]>([])
const groups = ref<AssistenteScriptEspecialidadeGroup[]>([])
const selectedGroupId = ref<number | null>(null)
const mobileDetailOpen = ref(false)
const selectedIds = ref<number[]>([])
const groupStates = reactive<Record<number, GroupState>>({})
const loadingGroups = ref(false)
const error = ref('')
const editorOpen = ref(false)
const editingId = ref<number | null>(null)
const saving = ref(false)
const linksOpen = ref(false)
const linkedId = ref<number | null>(null)
const deleteOpen = ref(false)
const deleteLoading = ref(false)
const deleting = ref(false)
const deleteError = ref('')
const deleteRow = ref<AssistenteEntity | null>(null)
const deleteDetail = ref<AssistenteLinksDetail | null>(null)
const form = reactive({ titulo: '', tipoScript: 2, status: -1, especialidades: [] as number[], procedimentos: [] as number[] })
let searchTimer: ReturnType<typeof setTimeout> | undefined
let loadVersion = 0

const visibleGroups = computed(() => {
  const filtered = especialidadeFiltro.value === '' ? groups.value : groups.value.filter(group => group.id === Number(especialidadeFiltro.value))
  return [...filtered].sort((left, right) => {
    if (left.id === 0) return 1
    if (right.id === 0) return -1
    if (left.totalScripts !== right.totalScripts) return right.totalScripts - left.totalScripts
    return left.nome.localeCompare(right.nome, 'pt-BR')
  })
})
const selectedGroup = computed(() => visibleGroups.value.find(group => group.id === selectedGroupId.value) ?? null)
const selectedGroupState = computed(() => selectedGroupId.value === null ? null : stateFor(selectedGroupId.value))
const loadedScripts = computed(() => {
  const unique = new Map<number, AssistenteEntity>()
  Object.values(groupStates).flatMap(state => state.items).forEach(row => unique.set(scriptId(row), row))
  return unique
})
const selectedRows = computed(() => selectedIds.value.map(id => loadedScripts.value.get(id)).filter(Boolean) as AssistenteEntity[])
const deleteLinkedGroups = computed(() => (deleteDetail.value?.relacoes || []).filter(group => group.itens.length))
const deleteTotal = computed(() => deleteLinkedGroups.value.reduce((sum, group) => sum + group.itens.length, 0))
const addScriptTarget = computed(() => selectedGroup.value && selectedGroup.value.id > 0
  ? { path: '/assistente/modelos/importar', query: { especialidade: String(selectedGroup.value.id) } }
  : { path: '/assistente/modelos/importar' })

function message(reason: unknown) { return reason instanceof Error ? reason.message : 'Não foi possível concluir a operação.' }
function scriptId(row: AssistenteEntity) { return Number(row.id ?? row.codigo ?? row.codigoscriptlaudo ?? row.CODSCRIPTLAUDO ?? 0) }
function isActive(row: AssistenteEntity) { return row.status === undefined || row.status === true || Number(row.status) === -1 || Number(row.status) === 1 }
function typeValue(row: AssistenteEntity) { return Number(row.tipoScript ?? row.TIPOSCRIPT) }
function isExportableScript(row: AssistenteEntity) { return [1, 2, 3].includes(typeValue(row)) }
function safeFileName(value: unknown) { return (String(value || 'script').trim() || 'script').replace(/[\\/:*?"<>|]/g, '_') }
function stateFor(id: number) { return groupStates[id] || (groupStates[id] = { items: [], page: 0, total: 0, loading: false, loaded: false, error: '' }) }
function clearStates() { Object.keys(groupStates).forEach(key => delete groupStates[Number(key)]) }
function clearFilters() { search.value = ''; tipoFiltro.value = ''; especialidadeFiltro.value = ''; statusFiltro.value = '' }
function isGroupSelected(id: number) { const ids = stateFor(id).items.map(scriptId); return ids.length > 0 && ids.every(value => selectedIds.value.includes(value)) }
function toggleGroupSelection(id: number) { const ids = stateFor(id).items.map(scriptId); selectedIds.value = isGroupSelected(id) ? selectedIds.value.filter(value => !ids.includes(value)) : [...new Set([...selectedIds.value, ...ids])] }
function toggleScriptSelection(row: AssistenteEntity) { const id = scriptId(row); selectedIds.value = selectedIds.value.includes(id) ? selectedIds.value.filter(value => value !== id) : [...selectedIds.value, id] }

async function loadGroups() {
  clearTimeout(searchTimer)
  const version = ++loadVersion
  loadingGroups.value = true
  error.value = ''
  selectedIds.value = []
  try {
    const result = await api.scriptSpecialtyGroups(search.value, tipoFiltro.value || undefined, statusFiltro.value || undefined)
    if (version !== loadVersion) return
    groups.value = result
    clearStates()
    const available = new Set(visibleGroups.value.map(group => group.id))
    if (selectedGroupId.value === null || !available.has(selectedGroupId.value)) {
      selectedGroupId.value = visibleGroups.value[0]?.id ?? null
      mobileDetailOpen.value = false
    }
    if (selectedGroupId.value !== null) await loadGroup(selectedGroupId.value, true, version)
  } catch (reason) {
    if (version === loadVersion) { groups.value = []; selectedGroupId.value = null; error.value = message(reason) }
  } finally {
    if (version === loadVersion) loadingGroups.value = false
  }
}

async function loadGroup(id: number, reset = false, version = loadVersion) {
  const state = stateFor(id)
  if (state.loading) return
  if (reset) { state.items = []; state.page = 0; state.total = 0; state.loaded = false; state.error = '' }
  state.loading = true
  state.error = ''
  try {
    const result = await api.list('scripts', state.page + 1, PAGE_SIZE, search.value, { tipoScript: tipoFiltro.value || undefined, status: statusFiltro.value || undefined, especialidade: id })
    if (canEdit.value) {
      const origins = await useApi().post<Record<string, number>[]>('/api/web/assistente/publicacao/origens', result.items.map(scriptId))
      const byId = new Map(origins.map(row => [Number(row.ID ?? row.id), Number(row.ORIGEM ?? row.origem)]))
      result.items.forEach(row => { row.origemConteudos = byId.get(scriptId(row)) })
    }
    if (version !== loadVersion) return
    const unique = new Map(state.items.map(row => [scriptId(row), row]))
    result.items.forEach(row => unique.set(scriptId(row), row))
    state.items = [...unique.values()]
    state.page = result.page
    state.total = result.total
    state.loaded = true
  } catch (reason) {
    if (version === loadVersion) state.error = message(reason)
  } finally {
    if (version === loadVersion) state.loading = false
  }
}

async function selectGroup(id: number) { selectedGroupId.value = id; mobileDetailOpen.value = true; if (!stateFor(id).loaded) await loadGroup(id, true) }
async function refreshAll() { await loadGroups() }
function openLinks(row: AssistenteEntity) { linkedId.value = scriptId(row); linksOpen.value = true }
async function exportScript(row: AssistenteEntity) { try { await api.exportScriptPackage(scriptId(row), `${safeFileName(row.titulo ?? row.TITULO)}.zip`) } catch (reason) { await swal.toast(message(reason), 'error') } }
async function exportSelectedScripts() { const ids = [...new Set(selectedRows.value.filter(isExportableScript).map(scriptId))]; if (!ids.length) return void swal.toast('Selecione ao menos um script VB legado, C# ou JSON.', 'warning'); try { await api.exportScriptsPackage(ids, 'scripts_assistente.zip'); await swal.toast(`Exportação iniciada (${ids.length} script${ids.length === 1 ? '' : 's'}).`) } catch (reason) { await swal.toast(message(reason), 'error') } }
async function deleteSelectedScripts() { if (!canDelete.value) return; const ids = [...new Set(selectedRows.value.map(scriptId))]; if (!ids.length) return; const confirmation = await swal.confirm('Excluir scripts selecionados?', ids.length === 1 ? 'O script será excluído e seus vínculos serão removidos. Os registros vinculados serão mantidos.' : `${ids.length} scripts serão excluídos e seus vínculos serão removidos. Os registros vinculados serão mantidos.`); if (!confirmation?.isConfirmed) return; try { await api.removeMany('scripts', ids); await swal.toast(`${ids.length} script${ids.length === 1 ? '' : 's'} excluído${ids.length === 1 ? '' : 's'} com sucesso.`); await refreshAll() } catch (reason) { await swal.toast(message(reason), 'error') } }
async function toggleStatus(row: AssistenteEntity) { if (!canActivate.value) return; const active = !isActive(row); try { await api.setStatus('scripts', scriptId(row), active); await refreshAll(); await swal.toast(`Script ${active ? 'ativado' : 'inativado'} com sucesso.`) } catch (reason) { await swal.toast(message(reason), 'error') } }

async function openEditor(row: AssistenteEntity) {
  if (!canEdit.value) return
  editingId.value = scriptId(row)
  try {
    const [detail, options] = await Promise.all([api.get('scripts', editingId.value), procedimentos.value.length ? Promise.resolve(procedimentos.value) : api.options('procedimentos')])
    procedimentos.value = options
    form.titulo = String(detail.titulo ?? detail.TITULO ?? '')
    form.tipoScript = Number(detail.tipoScript ?? detail.TIPOSCRIPT ?? 2)
    form.status = isActive(detail) ? -1 : 0
    form.especialidades = Array.isArray(detail.especialidades) ? detail.especialidades.map(Number) : []
    form.procedimentos = Array.isArray(detail.procedimentos) ? detail.procedimentos.map(Number) : []
    editorOpen.value = true
  } catch (reason) { await swal.toast(message(reason), 'error') }
}

async function saveEditor() {
  if (!canEdit.value) return
  if (!editingId.value) return
  if (!form.titulo.trim()) return void swal.toast('Preencha Título.', 'warning')
  saving.value = true
  try {
    await api.update('scripts', editingId.value, { titulo: form.titulo, tipoScript: form.tipoScript, status: form.status, especialidades: form.especialidades, procedimentos: form.procedimentos })
    editorOpen.value = false
    await refreshAll()
    await swal.toast('Script salvo com sucesso.')
  } catch (reason) { await swal.toast(message(reason), 'error') } finally { saving.value = false }
}

async function openDelete(row: AssistenteEntity) { if (!canDelete.value) return; deleteRow.value = row; deleteDetail.value = null; deleteError.value = ''; deleteOpen.value = true; deleteLoading.value = true; try { deleteDetail.value = await api.links('scripts', scriptId(row)) } catch (reason) { deleteError.value = message(reason) } finally { deleteLoading.value = false } }
async function confirmDelete() { if (!deleteRow.value || !canDelete.value) return; deleting.value = true; try { await api.remove('scripts', scriptId(deleteRow.value)); deleteOpen.value = false; deleteRow.value = null; deleteDetail.value = null; await refreshAll(); await swal.toast('Script excluído com sucesso.') } catch (reason) { await swal.toast(message(reason), 'error') } finally { deleting.value = false } }

onMounted(async () => { try { especialidades.value = await api.options('especialidades') } catch { especialidades.value = [] }; await loadGroups() })
watch(search, () => { clearTimeout(searchTimer); searchTimer = setTimeout(loadGroups, 350) })
watch([tipoFiltro, especialidadeFiltro, statusFiltro], loadGroups)
onUnmounted(() => clearTimeout(searchTimer))
</script>
