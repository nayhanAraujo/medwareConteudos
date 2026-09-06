<template>
  <div>
    <DsPageHeader v-if="!compactLayout" :title="title" :subtitle="subtitle" :icon="icon">
      <template #actions>
        <DsButton variant="secondary" size="sm" to="/assistente">Voltar</DsButton>
        <DsButton v-if="canCreate && !hideCreate" variant="success" size="sm" icon="plus-lg" @click="openEditor()">{{ createLabel }}</DsButton>
      </template>
    </DsPageHeader>
    <DsPageShell :panel-class="compactLayout ? '!bg-transparent !shadow-none !border-0 !ring-0 !p-0' : undefined">
      <AssistenteNav />
      <DsAlert v-if="!canWrite" variant="info" class="mb-4">Consulta liberada. Alterações exigem permissão no módulo Assistente.</DsAlert>
      <DsAlert v-if="error" variant="error" class="mb-4">{{ error }}</DsAlert>
      <div v-if="compactLayout" class="mb-4 grid grid-cols-1 gap-3">
        <div class="flex flex-wrap items-center gap-2">
          <slot name="filters" />
          <DsButton v-if="canCreate && !hideCreate" variant="success" size="sm" icon="plus-lg" @click="openEditor()">{{ createLabel }}</DsButton>
        </div>
        <div class="flex flex-wrap items-end justify-between gap-3">
          <DsSearchInput v-model="search" class="min-w-72 flex-1" wrapper-class="!mb-0" :placeholder="searchPlaceholder" @enter="load(1)" />
          <div class="flex flex-wrap items-end gap-2">
            <DsButton
              v-for="action in bulkActions || []"
              :key="action.key"
              :variant="action.variant || 'secondary'"
              size="sm"
              :icon="action.icon"
              :disabled="selectedRows.length === 0 || action.disabled?.(selectedRows)"
              @click="runBulkAction(action)"
            >
              {{ action.label }}
            </DsButton>
            <DsSelect v-model="pageSize" label="Itens por página" input-class="min-w-24" @update:model-value="load(1)">
              <option :value="10">10</option><option :value="20">20</option><option :value="50">50</option>
            </DsSelect>
          </div>
        </div>
      </div>
      <div v-else class="mb-4 flex flex-wrap items-end justify-between gap-3">
        <DsSearchInput v-model="search" class="min-w-72 flex-1" wrapper-class="!mb-0" :placeholder="searchPlaceholder" @enter="load(1)" />
        <div class="flex flex-wrap items-end gap-2">
          <DsButton
            v-for="action in bulkActions || []"
            :key="action.key"
            :variant="action.variant || 'secondary'"
            size="sm"
            :icon="action.icon"
            :disabled="selectedRows.length === 0 || action.disabled?.(selectedRows)"
            @click="runBulkAction(action)"
          >
            {{ action.label }}
          </DsButton>
          <DsSelect v-model="pageSize" label="Itens por página" input-class="min-w-28" @update:model-value="load(1)">
            <option :value="10">10</option><option :value="20">20</option><option :value="50">50</option>
          </DsSelect>
        </div>
      </div>
      <div v-if="loading" class="py-12 text-center text-gray-500">Carregando...</div>
      <DsEmptyState v-else-if="!rows.length" title="Nenhum registro encontrado" description="Ajuste a pesquisa ou crie um novo cadastro." />
      <DsTable v-else>
        <template #head><tr><th v-if="selectable" class="w-10"><input :checked="allRowsSelected" type="checkbox" class="rounded" @change="toggleAllRows" /></th><th v-for="column in columns" :key="column.key">{{ column.label }}</th><th>Status</th><th class="text-right">Ações</th></tr></template>
        <tr v-for="row in rows" :key="entityId(row)">
          <td v-if="selectable"><input v-model="selectedIds" type="checkbox" class="rounded" :value="entityId(row)" /></td>
          <td v-for="column in columns" :key="column.key">
            <DsBadge v-if="column.badgeVariant?.(row[column.key], row)" :variant="column.badgeVariant(row[column.key], row)">
              {{ display(row, column) }}
            </DsBadge>
            <span v-else :class="column.primary ? 'font-semibold' : ''">{{ display(row, column) }}</span>
          </td>
          <td><DsBadge :variant="isActive(row) ? 'success' : 'neutral'">{{ isActive(row) ? 'Ativo' : 'Inativo' }}</DsBadge></td>
          <td>
            <div class="flex justify-end">
              <DsDropdown align="right">
                <template #trigger>
                  <span class="inline-flex items-center gap-2 rounded-full border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-ds-text shadow-sm hover:bg-gray-50">
                    Gerenciar
                    <i class="bi bi-chevron-down text-[10px]" />
                  </span>
                </template>
                <template #default="{ close }">
                  <DsDropdownItem @click="openLinks(row); close()">
                    <i class="bi bi-diagram-3" />
                    Vínculos
                  </DsDropdownItem>
                  <DsDropdownItem
                    v-for="action in visibleRowActions(row)"
                    :key="action.key"
                    @click="action.handler(row); close()"
                  >
                    <i v-if="action.icon" :class="`bi bi-${action.icon}`" />
                    {{ action.label }}
                  </DsDropdownItem>
                  <DsDropdownDivider v-if="canWrite" />
                  <template v-if="canWrite">
                    <DsDropdownItem v-if="canEdit" @click="openEditor(row); close()">
                      <i class="bi bi-pencil" />
                      Editar
                    </DsDropdownItem>
                    <DsDropdownItem v-if="canActivate" @click="toggle(row); close()">
                      <i :class="`bi bi-${isActive(row) ? 'slash-circle' : 'check-circle'}`" />
                      {{ isActive(row) ? 'Inativar' : 'Ativar' }}
                    </DsDropdownItem>
                    <DsDropdownItem v-if="canDelete" danger @click="remove(row); close()">
                      <i class="bi bi-trash" />
                      Excluir
                    </DsDropdownItem>
                  </template>
                </template>
              </DsDropdown>
            </div>
          </td>
        </tr>
      </DsTable>
      <div v-if="total > pageSize" class="mt-4 flex items-center justify-between gap-3 text-sm text-gray-600">
        <span>{{ total }} registros</span><div class="flex gap-2">
          <DsButton variant="secondary" size="sm" :disabled="page <= 1" @click="load(page - 1)">Anterior</DsButton>
          <span class="px-2 py-2">Página {{ page }} de {{ pages }}</span>
          <DsButton variant="secondary" size="sm" :disabled="page >= pages" @click="load(page + 1)">Próxima</DsButton>
        </div>
      </div>
      <div v-if="compactLayout && selectable && selectedRows.length" class="fixed inset-x-0 bottom-0 z-40 border-t border-gray-200 bg-white/95 px-6 py-3 shadow-lg backdrop-blur">
        <div class="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-3 text-sm">
          <span>{{ selectedRows.length }} {{ selectedRows.length === 1 ? 'item selecionado' : 'itens selecionados' }}</span>
          <div class="flex flex-wrap items-center gap-2">
            <span class="text-gray-500">Ações em Massa:</span>
            <DsButton
              v-for="action in bulkActions || []"
              :key="`bottom-${action.key}`"
              :variant="action.variant || 'secondary'"
              size="sm"
              :icon="action.icon"
              :disabled="action.disabled?.(selectedRows)"
              @click="runBulkAction(action)"
            >
              {{ action.label }}
            </DsButton>
          </div>
        </div>
      </div>
    </DsPageShell>

    <DsModal v-model="editorOpen" :title="editingId ? `Editar ${singular}` : `Novo ${singular}`" size="xl">
      <p class="mb-4 text-sm text-gray-600"><span class="font-semibold text-red-600">*</span> Campos obrigatórios</p>
      <div class="grid gap-4 md:grid-cols-2">
        <template v-for="field in editableFields" :key="field.key">
          <DsTextarea v-if="field.kind === 'textarea'" v-model="form[field.key]" :label="fieldLabel(field)" :required="field.required" class="md:col-span-2" />
          <DsSelect v-else-if="field.kind === 'select'" v-model="form[field.key]" :label="fieldLabel(field)" :required="field.required">
            <option value="">Selecione</option><option v-for="option in fieldOptions(field)" :key="option.value" :value="option.value">{{ option.label }}</option>
          </DsSelect>
          <AssistenteMultiSelect v-else-if="field.kind === 'multi'" v-model="form[field.key]" :label="fieldLabel(field)" :options="relationOptions[field.optionsDomain || ''] || []" class="md:col-span-2" />
          <DsInput v-else v-model="form[field.key]" :type="field.kind === 'number' ? 'number' : 'text'" :label="fieldLabel(field)" :required="field.required" />
        </template>
      </div>
      <template #footer><DsButton variant="secondary" @click="editorOpen = false">Cancelar</DsButton><DsButton :loading="saving" @click="save">Salvar</DsButton></template>
    </DsModal>
    <DsModal v-model="linksOpen" title="Vínculos do registro" size="xl">
      <AssistenteVinculosPanel v-if="linkedId" :domain="domain" :id="linkedId" @updated="load()" />
    </DsModal>
    <DsModal v-model="deleteOpen" :title="`Excluir ${singular}`" size="xl" :close-on-backdrop="!deleting">
      <div v-if="deleteLoading" class="py-10 text-center text-gray-500">Consultando vínculos...</div>
      <DsAlert v-else-if="deleteError" variant="error">{{ deleteError }}</DsAlert>
      <template v-else-if="deleteDetail">
        <div class="mb-4 rounded-xl bg-gray-50 p-4">
          <div class="text-xs uppercase tracking-wide text-gray-500">{{ singular }} #{{ deleteDetail.id }}</div>
          <div class="font-semibold text-ds-text">{{ deleteDetail.nome }}</div>
          <div class="mt-2 text-sm text-gray-600">{{ deleteTotal }} vínculo{{ deleteTotal === 1 ? '' : 's' }} encontrado{{ deleteTotal === 1 ? '' : 's' }}.</div>
        </div>
        <DsAlert :variant="deleteTotal > 0 ? 'warning' : 'info'" class="mb-4">
          {{ deleteMessage }}
        </DsAlert>
        <div v-if="deleteLinkedGroups.length" class="space-y-3">
          <div v-for="group in deleteLinkedGroups" :key="group.relacao" class="rounded-xl border border-gray-200 p-4">
            <div class="mb-2 flex items-center justify-between gap-3">
              <h4 class="font-semibold text-ds-text">{{ group.titulo }}</h4>
              <DsBadge variant="neutral">{{ group.itens.length }}</DsBadge>
            </div>
            <ul class="space-y-1 text-sm text-gray-700">
              <li v-for="item in group.itens.slice(0, 5)" :key="item.id">
                {{ item.nome }} <span class="text-gray-400">#{{ item.id }}</span>
              </li>
            </ul>
            <p v-if="group.itens.length > 5" class="mt-2 text-xs text-gray-500">+{{ group.itens.length - 5 }} vínculo{{ group.itens.length - 5 === 1 ? '' : 's' }}</p>
          </div>
        </div>
      </template>
      <template #footer>
        <DsButton variant="secondary" :disabled="deleting" @click="deleteOpen = false">Cancelar</DsButton>
        <DsButton variant="danger" :loading="deleting" :disabled="deleteLoading || !!deleteError || !deleteRow || !canDelete" @click="confirmRemove">Excluir</DsButton>
      </template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
import type { AssistenteEntity, AssistenteLinksDetail, AssistenteOption } from '~/composables/useAssistenteApi'
export interface AssistenteField { key: string; apiKey?: string; label: string; kind?: 'text' | 'number' | 'textarea' | 'select' | 'multi'; required?: boolean; defaultValue?: string | number; options?: { label: string; value: string | number }[]; optionsDomain?: string }
type AssistenteBadgeVariant = 'default' | 'neutral' | 'primary' | 'success' | 'warning' | 'danger' | 'dark' | 'purple'
export interface AssistenteColumn { key: string; label: string; primary?: boolean; format?: (value: unknown, row: AssistenteEntity) => string; badgeVariant?: (value: unknown, row: AssistenteEntity) => AssistenteBadgeVariant | undefined }
export interface AssistenteRowAction { key: string; label: string; icon?: string; variant?: 'primary' | 'secondary' | 'ghost' | 'danger' | 'success'; visible?: (row: AssistenteEntity) => boolean; handler: (row: AssistenteEntity) => void | Promise<void> }
export interface AssistenteBulkAction { key: string; label: string; icon?: string; variant?: 'primary' | 'secondary' | 'ghost' | 'danger' | 'success'; disabled?: (rows: AssistenteEntity[]) => boolean; refreshAfter?: boolean; handler: (rows: AssistenteEntity[]) => void | Promise<void> }
const props = withDefaults(defineProps<{ domain: string; title: string; singular: string; subtitle: string; icon?: string; fields: AssistenteField[]; columns: AssistenteColumn[]; hideCreate?: boolean; rowActions?: AssistenteRowAction[]; bulkActions?: AssistenteBulkAction[]; selectable?: boolean; compactLayout?: boolean; createLabel?: string; searchPlaceholder?: string; listFilters?: Record<string, string | number | boolean | undefined> }>(), { createLabel: 'Novo', searchPlaceholder: 'Pesquisar...' })
const auth = useAuthStore(); const api = useAssistenteApi(); const swal = useSwal()
const canCreate = computed(() => auth.can('assistente', 'criar'))
const canEdit = computed(() => auth.can('assistente', 'editar'))
const canDelete = computed(() => auth.can('assistente', 'excluir'))
const canActivate = computed(() => auth.can('assistente', 'ativar'))
const canWrite = computed(() => canCreate.value || canEdit.value || canDelete.value || canActivate.value)
const rows = ref<AssistenteEntity[]>([]); const search = ref(''); const page = ref(1); const pageSize = ref(20); const total = ref(0)
const loading = ref(false); const saving = ref(false); const error = ref(''); const editorOpen = ref(false); const editingId = ref<number | null>(null)
const linksOpen = ref(false); const linkedId = ref<number | null>(null)
const selectedIds = ref<number[]>([])
const deleteOpen = ref(false); const deleteLoading = ref(false); const deleting = ref(false); const deleteError = ref(''); const deleteRow = ref<AssistenteEntity | null>(null); const deleteDetail = ref<AssistenteLinksDetail | null>(null)
const form = reactive<Record<string, any>>({}); const relationOptions = reactive<Record<string, AssistenteOption[]>>({})
const pages = computed(() => Math.max(1, Math.ceil(total.value / Number(pageSize.value))))
const selectedRows = computed(() => rows.value.filter(row => selectedIds.value.includes(entityId(row))))
const allRowsSelected = computed(() => rows.value.length > 0 && selectedRows.value.length === rows.value.length)
const deleteLinkedGroups = computed(() => (deleteDetail.value?.relacoes || []).filter(group => group.itens.length > 0))
const deleteTotal = computed(() => deleteLinkedGroups.value.reduce((sum, group) => sum + group.itens.length, 0))
const deleteMessage = computed(() => {
  if (deleteTotal.value === 0) return 'Este registro não possui vínculos cadastrados.'
  if (props.domain === 'scripts') return 'Ao confirmar, os vínculos abaixo serão removidos e o script será excluído. Os registros vinculados não serão excluídos.'
  return 'Revise os vínculos abaixo antes de confirmar a exclusão. A API pode bloquear registros ainda vinculados.'
})
const editableFields = computed(() => props.domain === 'scripts' && editingId.value ? props.fields.filter(field => field.key !== 'estruturaScript') : props.fields)
let searchTimer: ReturnType<typeof setTimeout> | undefined
function message(reason: unknown) { return reason instanceof Error ? reason.message : 'Não foi possível concluir a operação.' }
function entityId(row: AssistenteEntity) { return Number(row.id ?? row.codigo ?? row.codigoscriptlaudo ?? row.codpagfotos ?? row.codprocedimento ?? 0) }
function isActive(row: AssistenteEntity) { return row.status === undefined || row.status === true || Number(row.status) === -1 || Number(row.status) === 1 }
function display(row: AssistenteEntity, column: AssistenteColumn) {
  if (column.format) return column.format(row[column.key], row)
  const value = row[column.key]
  if (Array.isArray(value)) return value.map(item => typeof item === 'object' ? (item.nome || item.titulo || item.id) : item).join(', ')
  return value ?? '—'
}
function visibleRowActions(row: AssistenteEntity) { return (props.rowActions || []).filter(action => !action.visible || action.visible(row)) }
function toggleAllRows() { selectedIds.value = allRowsSelected.value ? [] : rows.value.map(row => entityId(row)) }
async function runBulkAction(action: AssistenteBulkAction) { if (!canWrite.value) return; await action.handler(selectedRows.value); if (action.refreshAfter) await load() }
function fieldLabel(field: AssistenteField) { return `${field.label}${field.required ? ' *' : ''}` }
function fieldOptions(field: AssistenteField) { return field.options || (relationOptions[field.optionsDomain || ''] || []).map(option => ({ label: option.nome, value: option.id })) }
function resetForm(row?: AssistenteEntity) { editableFields.value.forEach(field => { const value = row?.[field.key]; form[field.key] = field.kind === 'multi' ? (Array.isArray(value) ? value.map(item => Number(typeof item === 'object' ? item.id : item)) : []) : (value ?? field.defaultValue ?? '') }) }
function requestBody() { return Object.fromEntries(editableFields.value.map(field => [field.apiKey || field.key, form[field.key]])) }
async function load(target = page.value) { loading.value = true; error.value = ''; try { const result = await api.list(props.domain, target, Number(pageSize.value), search.value, props.listFilters || {}); rows.value = result.items || []; selectedIds.value = selectedIds.value.filter(id => rows.value.some(row => entityId(row) === id)); total.value = result.total ?? rows.value.length; page.value = result.page || target } catch (reason) { error.value = message(reason); rows.value = [] } finally { loading.value = false } }
async function loadRelations() { const domains = [...new Set(editableFields.value.filter(f => (f.kind === 'multi' || f.kind === 'select') && f.optionsDomain).map(f => f.optionsDomain).filter(Boolean))] as string[]; await Promise.all(domains.map(async domain => { if (!relationOptions[domain]) relationOptions[domain] = await api.options(domain) })) }
async function openEditor(row?: AssistenteEntity) { if (row ? !canEdit.value : !canCreate.value) return; editingId.value = row ? entityId(row) : null; let detail = row; if (editingId.value) { try { detail = await api.get(props.domain, editingId.value) } catch { /* use list row */ } } resetForm(detail); await loadRelations(); editorOpen.value = true }
function openLinks(row: AssistenteEntity) { linkedId.value = entityId(row); linksOpen.value = true }
async function save() { if (editingId.value ? !canEdit.value : !canCreate.value) return; const missing = editableFields.value.find(field => field.required && (form[field.key] === '' || form[field.key] === null || form[field.key] === undefined || (Array.isArray(form[field.key]) && !form[field.key].length))); if (missing) return void swal.toast(`Preencha ${missing.label}.`, 'warning'); saving.value = true; try { const body = requestBody(); if (editingId.value) await api.update(props.domain, editingId.value, body); else await api.create(props.domain, body); editorOpen.value = false; await load(); await swal.toast(`${props.singular} salvo com sucesso.`) } catch (reason) { await swal.toast(message(reason), 'error') } finally { saving.value = false } }
async function toggle(row: AssistenteEntity) { if (!canActivate.value) return; const active = !isActive(row); try { await api.setStatus(props.domain, entityId(row), active); await load(); await swal.toast(`${props.singular} ${active ? 'ativado' : 'inativado'} com sucesso.`) } catch (reason) { await swal.toast(message(reason), 'error') } }
async function remove(row: AssistenteEntity) { if (!canDelete.value) return; deleteRow.value = row; deleteDetail.value = null; deleteError.value = ''; deleteOpen.value = true; deleteLoading.value = true; try { deleteDetail.value = await api.links(props.domain, entityId(row)) } catch (reason) { deleteError.value = reason instanceof Error ? reason.message : 'Não foi possível consultar os vínculos.' } finally { deleteLoading.value = false } }
async function confirmRemove() { if (!deleteRow.value || !canDelete.value) return; deleting.value = true; try { await api.remove(props.domain, entityId(deleteRow.value)); deleteOpen.value = false; deleteRow.value = null; deleteDetail.value = null; await load(); await swal.toast(`${props.singular} excluído com sucesso.`) } catch (reason) { await swal.toast(message(reason), 'error') } finally { deleting.value = false } }
onMounted(() => load())
watch(search, () => { clearTimeout(searchTimer); searchTimer = setTimeout(() => load(1), 350) })
watch(() => JSON.stringify(props.listFilters || {}), () => load(1))
onUnmounted(() => clearTimeout(searchTimer))
</script>
