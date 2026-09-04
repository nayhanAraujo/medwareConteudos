<template>
  <AssistenteDomainCrud
    domain="scripts"
    title="Scripts de laudo"
    singular="script"
    subtitle="Scripts importados de /scripts/pacotes"
    icon="code-square"
    :hide-create="true"
    :columns="columns"
    :fields="fields"
    :row-actions="rowActions"
    :bulk-actions="bulkActions"
    :list-filters="listFilters"
    search-placeholder="Pesquisar em Títulos, Códigos ou Conteúdo..."
    compact-layout
    selectable
  >
    <template #filters>
      <DsSelect v-model="tipoFiltro" input-class="min-w-44 !rounded-xl !py-2.5">
        <option value="">Filtrar por Tipo: [Todos]</option>
        <option value="2">Filtrar por Tipo: [C#]</option>
        <option value="1">Filtrar por Tipo: [VB legado]</option>
        <option value="3">Filtrar por Tipo: [JSON]</option>
      </DsSelect>
      <DsSelect v-model="especialidadeFiltro" input-class="min-w-56 !rounded-xl !py-2.5">
        <option value="">Filtrar por Especialidade: [Todos]</option>
        <option v-for="option in especialidades" :key="option.id" :value="String(option.id)">
          Filtrar por Especialidade: [{{ option.nome }}]
        </option>
      </DsSelect>
      <DsSelect v-model="statusFiltro" input-class="min-w-44 !rounded-xl !py-2.5">
        <option value="-1">Filtrar por Status: [Ativo]</option>
        <option value="0">Filtrar por Status: [Inativo]</option>
        <option value="">Filtrar por Status: [Todos]</option>
      </DsSelect>
      <DsButton variant="secondary" size="sm" icon="sliders" @click="clearFilters">Limpar filtros</DsButton>
    </template>
  </AssistenteDomainCrud>
</template>
<script setup lang="ts">
import type { AssistenteEntity, AssistenteOption } from '~/composables/useAssistenteApi'

definePageMeta({ layout: 'default' })

const api = useAssistenteApi()
const swal = useSwal()
const tipoFiltro = ref('')
const especialidadeFiltro = ref('')
const statusFiltro = ref('-1')
const especialidades = ref<AssistenteOption[]>([])

const listFilters = computed(() => ({
  tipoScript: tipoFiltro.value || undefined,
  especialidade: especialidadeFiltro.value || undefined,
  status: statusFiltro.value || undefined
}))

function formatTipo(value: unknown) {
  const map: Record<number, string> = { 1: 'VB (legado)', 2: 'C#', 3: 'JSON' }
  const n = Number(value)
  return map[n] ?? String(value ?? '—')
}

function formatTipoVariant(value: unknown): 'default' | 'primary' | 'warning' | 'purple' {
  const map: Record<number, 'primary' | 'warning' | 'purple'> = { 1: 'warning', 2: 'primary', 3: 'purple' }
  return map[Number(value)] || 'default'
}

function clearFilters() {
  tipoFiltro.value = ''
  especialidadeFiltro.value = ''
  statusFiltro.value = ''
}

function scriptId(row: AssistenteEntity) {
  return Number(row.id ?? row.codigo ?? row.codigoscriptlaudo ?? row.CODSCRIPTLAUDO ?? 0)
}

function safeFileName(value: unknown) {
  const name = String(value || 'script').trim() || 'script'
  return name.replace(/[\\/:*?"<>|]/g, '_')
}

function isExportableScript(row: AssistenteEntity) {
  return [1, 2, 3].includes(Number(row.tipoScript ?? row.TIPOSCRIPT))
}

async function exportScript(row: AssistenteEntity) {
  const id = scriptId(row)
  if (!id) return void swal.toast('Script inválido para exportação.', 'error')
  try {
    await api.exportScriptPackage(id, `${safeFileName(row.titulo ?? row.TITULO)}.zip`)
  } catch (reason) {
    await swal.toast(reason instanceof Error ? reason.message : 'Não foi possível exportar o script.', 'error')
  }
}

async function exportSelectedScripts(rows: AssistenteEntity[]) {
  const ids = rows.filter(isExportableScript).map(scriptId).filter(Boolean)
  if (!ids.length) return void swal.toast('Selecione ao menos um script VB legado, C# ou JSON.', 'warning')
  try {
    await api.exportScriptsPackage(ids, 'scripts_assistente.zip')
    await swal.toast(`Exportação iniciada (${ids.length} script${ids.length === 1 ? '' : 's'}).`, 'success')
  } catch (reason) {
    await swal.toast(reason instanceof Error ? reason.message : 'Não foi possível exportar os scripts.', 'error')
  }
}

const columns = [
  { key: 'codigo', label: 'Código' },
  { key: 'titulo', label: 'Título', primary: true },
  { key: 'tipoScript', label: 'Tipo', format: formatTipo, badgeVariant: formatTipoVariant },
  { key: 'especialidades', label: 'Especialidade' }
]

const fields = [
  { key: 'titulo', label: 'Título', required: true },
  { key: 'tipoScript', label: 'Tipo', kind: 'select' as const, required: true, options: [{ label: 'VB (legado)', value: 1 }, { label: 'C#', value: 2 }, { label: 'JSON', value: 3 }] },
  { key: 'status', label: 'Status', kind: 'select' as const, required: true, defaultValue: -1, options: [{ label: 'Ativo', value: -1 }, { label: 'Inativo', value: 0 }] },
  { key: 'especialidades', label: 'Especialidades', kind: 'multi' as const, optionsDomain: 'especialidades' },
  { key: 'procedimentos', label: 'Procedimentos', kind: 'multi' as const, optionsDomain: 'procedimentos' }
]

const rowActions = [
  {
    key: 'export-script',
    label: 'Exportar',
    icon: 'download',
    visible: isExportableScript,
    handler: exportScript
  }
]

const bulkActions = [
  {
    key: 'export-selected',
    label: 'Exportar selecionados',
    icon: 'file-earmark-zip',
    disabled: (rows: AssistenteEntity[]) => !rows.some(isExportableScript),
    handler: exportSelectedScripts
  }
]

onMounted(async () => {
  try {
    especialidades.value = await api.options('especialidades')
  } catch {
    especialidades.value = []
  }
})
</script>
