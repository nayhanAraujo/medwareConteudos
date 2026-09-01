<template>
  <div>
    <DsPageHeader :title="title" :subtitle="subtitle" :icon="icon">
      <template #actions>
        <DsButton variant="secondary" size="sm" to="/assistente">Voltar</DsButton>
        <DsButton v-if="auth.isAdmin && !hideCreate" variant="success" size="sm" icon="plus-lg" @click="openEditor()">Novo</DsButton>
      </template>
    </DsPageHeader>
    <DsPageShell>
      <AssistenteNav />
      <DsAlert v-if="!auth.isAdmin" variant="info" class="mb-4">Consulta liberada. Alterações são exclusivas de administradores.</DsAlert>
      <DsAlert v-if="error" variant="error" class="mb-4">{{ error }}</DsAlert>
      <div class="mb-4 flex flex-wrap items-end justify-between gap-3">
        <DsSearchInput v-model="search" class="min-w-72 flex-1" placeholder="Pesquisar..." @enter="load(1)" />
        <div class="flex flex-wrap items-end gap-2">
          <DsButton
            v-for="action in bulkActions || []"
            :key="action.key"
            :variant="action.variant || 'secondary'"
            size="sm"
            :icon="action.icon"
            :disabled="selectedRows.length === 0 || action.disabled?.(selectedRows)"
            @click="action.handler(selectedRows)"
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
          <td v-for="column in columns" :key="column.key"><span :class="column.primary ? 'font-semibold' : ''">{{ display(row, column) }}</span></td>
          <td><DsBadge :variant="isActive(row) ? 'success' : 'neutral'">{{ isActive(row) ? 'Ativo' : 'Inativo' }}</DsBadge></td>
          <td><div class="flex justify-end gap-2">
            <DsButton variant="secondary" size="sm" icon="diagram-3" @click="openLinks(row)">Vínculos</DsButton>
            <DsButton
              v-for="action in visibleRowActions(row)"
              :key="action.key"
              :variant="action.variant || 'secondary'"
              size="sm"
              :icon="action.icon"
              @click="action.handler(row)"
            >
              {{ action.label }}
            </DsButton>
            <template v-if="auth.isAdmin">
            <DsButton variant="secondary" size="sm" @click="openEditor(row)">Editar</DsButton>
            <DsButton variant="secondary" size="sm" @click="toggle(row)">{{ isActive(row) ? 'Inativar' : 'Ativar' }}</DsButton>
            <DsButton variant="danger" size="sm" @click="remove(row)">Excluir</DsButton>
            </template>
          </div></td>
        </tr>
      </DsTable>
      <div v-if="total > pageSize" class="mt-4 flex items-center justify-between gap-3 text-sm text-gray-600">
        <span>{{ total }} registros</span><div class="flex gap-2">
          <DsButton variant="secondary" size="sm" :disabled="page <= 1" @click="load(page - 1)">Anterior</DsButton>
          <span class="px-2 py-2">Página {{ page }} de {{ pages }}</span>
          <DsButton variant="secondary" size="sm" :disabled="page >= pages" @click="load(page + 1)">Próxima</DsButton>
        </div>
      </div>
    </DsPageShell>

    <DsModal v-model="editorOpen" :title="editingId ? `Editar ${singular}` : `Novo ${singular}`" size="xl">
      <p class="mb-4 text-sm text-gray-600"><span class="font-semibold text-red-600">*</span> Campos obrigatórios</p>
      <div class="grid gap-4 md:grid-cols-2">
        <template v-for="field in fields" :key="field.key">
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
          {{ deleteTotal > 0 ? 'Revise os vínculos abaixo antes de confirmar a exclusão. A API pode bloquear registros ainda vinculados.' : 'Este registro não possui vínculos cadastrados.' }}
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
        <DsButton variant="danger" :loading="deleting" :disabled="deleteLoading || !!deleteError || !deleteRow" @click="confirmRemove">Excluir</DsButton>
      </template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
import type { AssistenteEntity, AssistenteLinksDetail, AssistenteOption } from '~/composables/useAssistenteApi'
export interface AssistenteField { key: string; apiKey?: string; label: string; kind?: 'text' | 'number' | 'textarea' | 'select' | 'multi'; required?: boolean; defaultValue?: string | number; options?: { label: string; value: string | number }[]; optionsDomain?: string }
export interface AssistenteColumn { key: string; label: string; primary?: boolean; format?: (value: unknown, row: AssistenteEntity) => string }
export interface AssistenteRowAction { key: string; label: string; icon?: string; variant?: 'primary' | 'secondary' | 'ghost' | 'danger' | 'success'; visible?: (row: AssistenteEntity) => boolean; handler: (row: AssistenteEntity) => void | Promise<void> }
export interface AssistenteBulkAction { key: string; label: string; icon?: string; variant?: 'primary' | 'secondary' | 'ghost' | 'danger' | 'success'; disabled?: (rows: AssistenteEntity[]) => boolean; handler: (rows: AssistenteEntity[]) => void | Promise<void> }
const props = defineProps<{ domain: string; title: string; singular: string; subtitle: string; icon?: string; fields: AssistenteField[]; columns: AssistenteColumn[]; hideCreate?: boolean; rowActions?: AssistenteRowAction[]; bulkActions?: AssistenteBulkAction[]; selectable?: boolean }>()
const auth = useAuthStore(); const api = useAssistenteApi(); const swal = useSwal()
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
function fieldLabel(field: AssistenteField) { return `${field.label}${field.required ? ' *' : ''}` }
function fieldOptions(field: AssistenteField) { return field.options || (relationOptions[field.optionsDomain || ''] || []).map(option => ({ label: option.nome, value: option.id })) }
function resetForm(row?: AssistenteEntity) { props.fields.forEach(field => { const value = row?.[field.key]; form[field.key] = field.kind === 'multi' ? (Array.isArray(value) ? value.map(item => Number(typeof item === 'object' ? item.id : item)) : []) : (value ?? field.defaultValue ?? '') }) }
function requestBody() { return Object.fromEntries(props.fields.map(field => [field.apiKey || field.key, form[field.key]])) }
async function load(target = page.value) { loading.value = true; error.value = ''; try { const result = await api.list(props.domain, target, Number(pageSize.value), search.value); rows.value = result.items || []; selectedIds.value = selectedIds.value.filter(id => rows.value.some(row => entityId(row) === id)); total.value = result.total ?? rows.value.length; page.value = result.page || target } catch (reason) { error.value = message(reason); rows.value = [] } finally { loading.value = false } }
async function loadRelations() { const domains = [...new Set(props.fields.filter(f => (f.kind === 'multi' || f.kind === 'select') && f.optionsDomain).map(f => f.optionsDomain).filter(Boolean))] as string[]; await Promise.all(domains.map(async domain => { if (!relationOptions[domain]) relationOptions[domain] = await api.options(domain) })) }
async function openEditor(row?: AssistenteEntity) { editingId.value = row ? entityId(row) : null; let detail = row; if (editingId.value) { try { detail = await api.get(props.domain, editingId.value) } catch { /* use list row */ } } resetForm(detail); await loadRelations(); editorOpen.value = true }
function openLinks(row: AssistenteEntity) { linkedId.value = entityId(row); linksOpen.value = true }
async function save() { const missing = props.fields.find(field => field.required && (form[field.key] === '' || form[field.key] === null || form[field.key] === undefined || (Array.isArray(form[field.key]) && !form[field.key].length))); if (missing) return void swal.toast(`Preencha ${missing.label}.`, 'warning'); saving.value = true; try { const body = requestBody(); if (editingId.value) await api.update(props.domain, editingId.value, body); else await api.create(props.domain, body); editorOpen.value = false; await load(); await swal.toast(`${props.singular} salvo com sucesso.`) } catch (reason) { await swal.toast(message(reason), 'error') } finally { saving.value = false } }
async function toggle(row: AssistenteEntity) { try { await api.setStatus(props.domain, entityId(row), !isActive(row)); await load() } catch (reason) { await swal.toast(message(reason), 'error') } }
async function remove(row: AssistenteEntity) { deleteRow.value = row; deleteDetail.value = null; deleteError.value = ''; deleteOpen.value = true; deleteLoading.value = true; try { deleteDetail.value = await api.links(props.domain, entityId(row)) } catch (reason) { deleteError.value = reason instanceof Error ? reason.message : 'Não foi possível consultar os vínculos.' } finally { deleteLoading.value = false } }
async function confirmRemove() { if (!deleteRow.value) return; deleting.value = true; try { await api.remove(props.domain, entityId(deleteRow.value)); deleteOpen.value = false; deleteRow.value = null; deleteDetail.value = null; await load(); await swal.toast(`${props.singular} excluído com sucesso.`) } catch (reason) { await swal.toast(message(reason), 'error') } finally { deleting.value = false } }
onMounted(() => load())
watch(search, () => { clearTimeout(searchTimer); searchTimer = setTimeout(() => load(1), 350) })
onUnmounted(() => clearTimeout(searchTimer))
</script>
