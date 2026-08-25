<script setup lang="ts">
definePageMeta({ layout: 'studio' })

interface VariableOption { codVariavel: number; nome: string; sigla: string; formula?: string | null; normalidade?: string | null; unidade?: string | null; abreviacao?: string | null; codGrupo?: number | null; grupo?: string | null; casasDecimais: number; classificacoes?: string | null; dependencias?: number[]; dependenciasNaoEncontradas?: string[] }
interface DraftVariable extends VariableOption { exibirGrafico: boolean }
interface DraftSection { key: string; codSecao?: number; nome: string; coluna: 1 | 2; variaveis: DraftVariable[] }
interface ApiSectionVariable { codVariavel: number; nome: string; sigla: string; exibirGrafico: boolean }
interface ApiSection { codSecao: number; nome: string; ordem: number; x: number; y: number; variaveis: ApiSectionVariable[] }
interface ModelItem { codModelo: number; nome: string; totalSecoes: number }
interface ModelDetail { codModelo: number; nome: string; secoes: ApiSection[] }
type DragPayload = { type: 'bank-variable'; variableId: number } | { type: 'group'; groupKey: string } | { type: 'section'; sectionKey: string } | { type: 'section-variable'; sectionKey: string; variableId: number }

const api = useApi()
const { toast, alert, confirm } = useStudioSwal()
const { downloadText } = useHtmlPreview()
const models = ref<ModelItem[]>([])
const variables = ref<VariableOption[]>([])
const modelId = ref<number | null>(null)
const modelName = ref('')
const sections = ref<DraftSection[]>([])
const savedSnapshot = ref('')
const modelSearch = ref('')
const variableSearch = ref('')
const groupFilter = ref('all')
const selectedIds = ref<number[]>([])
const expandedGroups = ref<string[]>([])
const details = ref<VariableOption | null>(null)
const previewText = ref('')
const loading = ref(false)
const saving = ref(false)
const previewing = ref(false)
const newModelName = ref('')
const dragPayload = ref<DragPayload | null>(null)
const sectionNameModalOpen = ref(false)
const sectionNameMode = ref<'create' | 'rename'>('create')
const sectionNameValue = ref('')
const sectionNameColumn = ref<1 | 2>(1)
const sectionNameTarget = ref<DraftSection | null>(null)
const measureBankOpen = ref(true)
const measureBankSide = ref<'left' | 'right'>('right')
const measureBankRef = ref<HTMLElement | null>(null)
const measureBankPosition = reactive<{ x: number | null; y: number | null }>({ x: null, y: null })
const measureBankMoveOffset = { x: 0, y: 0 }
let sectionSequence = 0

const variableById = computed(() => new Map(variables.value.map(variable => [variable.codVariavel, variable])))
const groupOptions = computed(() => {
  const map = new Map<string, { key: string; codGrupo: number | null; nome: string; variables: VariableOption[] }>()
  for (const variable of variables.value) {
    const key = variable.codGrupo == null ? 'ungrouped' : String(variable.codGrupo)
    if (!map.has(key)) map.set(key, { key, codGrupo: variable.codGrupo ?? null, nome: variable.grupo || 'Sem grupo', variables: [] })
    map.get(key)!.variables.push(variable)
  }
  return [...map.values()].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'))
})
const visibleGroups = computed(() => {
  const term = variableSearch.value.trim().toLocaleLowerCase()
  return groupOptions.value.filter(group => groupFilter.value === 'all' || group.key === groupFilter.value).map(group => ({ ...group, variables: group.variables.filter(variable => !term || `${variable.nome} ${variable.sigla} ${variable.abreviacao || ''}`.toLocaleLowerCase().includes(term)) })).filter(group => group.variables.length)
})
const selectedSet = computed(() => new Set(selectedIds.value))
const usedIds = computed(() => new Set(sections.value.flatMap(section => section.variaveis.map(variable => variable.codVariavel))))
const measureBankStyle = computed(() => measureBankPosition.x == null || measureBankPosition.y == null ? undefined : ({ left: `${measureBankPosition.x}px`, top: `${measureBankPosition.y}px`, right: 'auto', bottom: 'auto' }))
const dirty = computed(() => modelId.value != null && serializeDraft() !== savedSnapshot.value)
const validationErrors = computed(() => {
  const errors: string[] = []
  const names = sections.value.map(section => section.nome.trim().toLocaleLowerCase())
  if (!modelName.value.trim()) errors.push('Informe o nome do modelo.')
  if (names.some(name => !name)) errors.push('Todas as seções precisam de nome.')
  if (new Set(names).size !== names.length) errors.push('Existem seções com nomes repetidos.')
  const ids = sections.value.flatMap(section => section.variaveis.map(variable => variable.codVariavel))
  if (new Set(ids).size !== ids.length) errors.push('Uma medida aparece mais de uma vez no modelo.')
  for (const id of ids) {
    const variable = variableById.value.get(id)
    if (variable?.dependenciasNaoEncontradas?.length) errors.push(`${variable.nome}: dependências não encontradas (${variable.dependenciasNaoEncontradas.join(', ')}).`)
  }
  return [...new Set(errors)]
})

function createKey() { sectionSequence += 1; return `draft-${Date.now()}-${sectionSequence}` }
function serializeDraft() { return JSON.stringify({ nome: modelName.value.trim(), sections: sections.value.map(section => ({ codSecao: section.codSecao || null, nome: section.nome.trim(), coluna: section.coluna, variaveis: section.variaveis.map(variable => ({ codVariavel: variable.codVariavel, exibirGrafico: variable.exibirGrafico })) })) }) }
function columnSections(column: 1 | 2) { return sections.value.filter(section => section.coluna === column) }
function compositionPayload() { return { nome: modelName.value.trim(), secoes: ([1, 2] as const).flatMap(coluna => columnSections(coluna).map((section, index) => ({ codSecao: section.codSecao || null, nome: section.nome.trim(), coluna, ordem: index + 1, variaveis: section.variaveis.map((variable, variableIndex) => ({ codVariavel: variable.codVariavel, exibirGrafico: variable.exibirGrafico, ordem: variableIndex + 1 })) }))) } }
function draftVariable(id: number, showChart = false): DraftVariable | null { const source = variableById.value.get(id); return source ? { ...source, exibirGrafico: showChart } : null }
function normalizeApiModel(model: ModelDetail) {
  modelId.value = model.codModelo; modelName.value = model.nome
  sections.value = [...model.secoes].sort((a, b) => (a.x >= 6 ? 2 : 1) - (b.x >= 6 ? 2 : 1) || a.y - b.y || a.ordem - b.ordem).map(section => ({ key: `section-${section.codSecao}`, codSecao: section.codSecao, nome: section.nome, coluna: section.x >= 6 ? 2 : 1, variaveis: section.variaveis.map(item => draftVariable(item.codVariavel, item.exibirGrafico)).filter(Boolean) as DraftVariable[] }))
  previewText.value = ''; selectedIds.value = []; savedSnapshot.value = serializeDraft()
}
async function loadModels() { const query = new URLSearchParams({ page: '1', pageSize: '100', nome: modelSearch.value.trim() }); const response = await api.get<{ data: ModelItem[] }>(`/api/web/modelos?${query}`); models.value = response.data || [] }
async function loadVariables() { const response = await api.get<{ data: VariableOption[] }>('/api/web/modelos/variaveis'); variables.value = response.data || [] }
async function loadModel(id: number, skipDirtyCheck = false) {
  if (!skipDirtyCheck && dirty.value) { const result = await confirm('Descartar alterações?', 'As alterações não salvas do modelo atual serão perdidas.'); if (!result.isConfirmed) return }
  loading.value = true
  try { const response = await api.get<{ data: ModelDetail }>(`/api/web/modelos/${id}`); normalizeApiModel(response.data) } catch (error) { await toast(message(error), 'error') } finally { loading.value = false }
}
async function createModel() {
  const name = newModelName.value.trim(); if (!name) return; saving.value = true
  try { const response = await api.post<{ data: { codModelo: number } }>('/api/web/modelos', { nome: name }); newModelName.value = ''; await loadModels(); await loadModel(response.data.codModelo, true); await toast('Modelo criado.', 'success') } catch (error) { await toast(message(error), 'error') } finally { saving.value = false }
}
async function deleteModel(model: ModelItem | { codModelo: number; nome: string }) {
  const result = await confirm('Excluir modelo', `Todas as seções de "${model.nome}" serão excluídas.`); if (!result.isConfirmed) return
  try {
    await api.del(`/api/web/modelos/${model.codModelo}`)
    if (modelId.value === model.codModelo) { modelId.value = null; modelName.value = ''; sections.value = []; savedSnapshot.value = ''; previewText.value = '' }
    await loadModels()
    await toast('Modelo excluído.', 'success')
  } catch (error) { await toast(message(error), 'error') }
}
async function deleteCurrentModel() { if (modelId.value) await deleteModel({ codModelo: modelId.value, nome: modelName.value }) }
function toggleGroup(groupKey: string) { expandedGroups.value = expandedGroups.value.includes(groupKey) ? expandedGroups.value.filter(key => key !== groupKey) : [...expandedGroups.value, groupKey] }
function groupSelection(groupKey: string) { const group = groupOptions.value.find(item => item.key === groupKey); return selectedIds.value.filter(id => group?.variables.some(variable => variable.codVariavel === id)) }
function toggleGroupSelection(groupKey: string) {
  const group = groupOptions.value.find(item => item.key === groupKey); if (!group) return
  const available = group.variables.filter(variable => !usedIds.value.has(variable.codVariavel)).map(variable => variable.codVariavel)
  const allSelected = available.length > 0 && available.every(id => selectedSet.value.has(id))
  selectedIds.value = allSelected ? selectedIds.value.filter(id => !available.includes(id)) : [...new Set([...selectedIds.value, ...available])]
}
function openCreateSectionModal(column: 1 | 2 = 1) { sectionNameMode.value = 'create'; sectionNameColumn.value = column; sectionNameTarget.value = null; sectionNameValue.value = ''; sectionNameModalOpen.value = true }
function openRenameSectionModal(section: DraftSection) { sectionNameMode.value = 'rename'; sectionNameColumn.value = section.coluna; sectionNameTarget.value = section; sectionNameValue.value = section.nome; sectionNameModalOpen.value = true }
function saveSectionName() {
  const name = sectionNameValue.value.trim()
  if (!name) return
  if (sectionNameMode.value === 'rename' && sectionNameTarget.value) sectionNameTarget.value.nome = name
  else sections.value.push({ key: createKey(), nome: name, coluna: sectionNameColumn.value, variaveis: [] })
  sectionNameModalOpen.value = false
}
async function createSectionFromGroup(groupKey: string, column: 1 | 2) {
  const group = groupOptions.value.find(item => item.key === groupKey); if (!group) return
  const selected = groupSelection(groupKey).filter(id => !usedIds.value.has(id)); if (!selected.length) return void await toast('Selecione ao menos uma medida disponível deste grupo.', 'warning')
  const section: DraftSection = { key: createKey(), nome: group.nome, coluna: column, variaveis: [] }; sections.value.push(section)
  try { const added = addVariablesWithDependencies(section, selected); selectedIds.value = selectedIds.value.filter(id => !selected.includes(id)); if (added.length) await toast(`Dependências adicionadas: ${added.join(', ')}`, 'info') } catch (error) { sections.value = sections.value.filter(item => item.key !== section.key); await alert('Não foi possível criar a seção', message(error), 'error') }
}
function addVariablesWithDependencies(section: DraftSection, ids: number[], targetIndex?: number) {
  const addedDependencies: string[] = []; const explicit = new Set(ids); const visiting = new Set<number>(); const ordered: number[] = []
  const visit = (id: number) => {
    if (usedIds.value.has(id) || ordered.includes(id)) return
    if (visiting.has(id)) throw new Error(`Ciclo de dependências detectado em ${variableById.value.get(id)?.nome || id}.`)
    const variable = variableById.value.get(id); if (!variable) throw new Error(`Medida ${id} não encontrada.`)
    if (variable.dependenciasNaoEncontradas?.length) throw new Error(`A fórmula de ${variable.nome} possui referências inexistentes: ${variable.dependenciasNaoEncontradas.join(', ')}.`)
    visiting.add(id); for (const dependencyId of variable.dependencias || []) visit(dependencyId); visiting.delete(id); ordered.push(id); if (!explicit.has(id)) addedDependencies.push(variable.nome)
  }
  for (const id of ids) visit(id)
  const additions = ordered.map(id => draftVariable(id)).filter(Boolean) as DraftVariable[]; section.variaveis.splice(targetIndex ?? section.variaveis.length, 0, ...additions); return addedDependencies
}
function clearSection(section: DraftSection) { section.variaveis = [] }
async function removeSection(section: DraftSection) { const result = await confirm('Excluir seção', `Remover "${section.nome}" da composição?`); if (result.isConfirmed) sections.value = sections.value.filter(item => item.key !== section.key) }
function moveSection(section: DraftSection, direction: -1 | 1) { const column = columnSections(section.coluna); const index = column.findIndex(item => item.key === section.key); const target = index + direction; if (target < 0 || target >= column.length) return; const a = sections.value.findIndex(item => item.key === column[index]!.key); const b = sections.value.findIndex(item => item.key === column[target]!.key); [sections.value[a], sections.value[b]] = [sections.value[b]!, sections.value[a]!] }
function moveSectionToColumn(section: DraftSection) { section.coluna = section.coluna === 1 ? 2 : 1 }
function moveVariable(section: DraftSection, index: number, direction: -1 | 1) { const target = index + direction; if (target < 0 || target >= section.variaveis.length) return; [section.variaveis[index], section.variaveis[target]] = [section.variaveis[target]!, section.variaveis[index]!] }
function moveVariableToOtherSection(source: DraftSection, variable: DraftVariable) {
  const targets = sections.value.filter(section => section.key !== source.key)
  if (!targets.length) return void toast('Crie outra seção antes de mover a medida.', 'warning')
  const choices = targets.map((section, index) => `${index + 1}. ${section.nome} (coluna ${section.coluna})`).join('\n')
  const selected = Number(window.prompt(`Mover "${variable.nome}" para qual seção?\n\n${choices}`)) - 1
  const target = targets[selected]
  if (!target) return
  source.variaveis = source.variaveis.filter(item => item.codVariavel !== variable.codVariavel)
  target.variaveis.push(variable)
}
function removeVariable(section: DraftSection, id: number) { section.variaveis = section.variaveis.filter(variable => variable.codVariavel !== id) }

function startDrag(payload: DragPayload, event?: DragEvent) { dragPayload.value = payload; event?.dataTransfer?.setData('text/plain', JSON.stringify(payload)); if (event?.dataTransfer) event.dataTransfer.effectAllowed = payload.type === 'bank-variable' || payload.type === 'group' ? 'copy' : 'move' }
function endDrag() { dragPayload.value = null }
function dockMeasureBank(side: 'left' | 'right') { measureBankSide.value = side; measureBankPosition.x = null; measureBankPosition.y = null }
function startMeasureBankMove(event: PointerEvent) {
  if (event.button !== 0 || !measureBankRef.value) return
  const rect = measureBankRef.value.getBoundingClientRect()
  measureBankMoveOffset.x = event.clientX - rect.left
  measureBankMoveOffset.y = event.clientY - rect.top
  measureBankPosition.x = rect.left
  measureBankPosition.y = rect.top
  window.addEventListener('pointermove', moveMeasureBank)
  window.addEventListener('pointerup', stopMeasureBankMove, { once: true })
  document.body.style.userSelect = 'none'
  event.preventDefault()
}
function moveMeasureBank(event: PointerEvent) {
  const panel = measureBankRef.value
  if (!panel) return
  const margin = 8
  const maxX = Math.max(margin, window.innerWidth - panel.offsetWidth - margin)
  const maxY = Math.max(margin, window.innerHeight - panel.offsetHeight - margin)
  measureBankPosition.x = Math.min(maxX, Math.max(margin, event.clientX - measureBankMoveOffset.x))
  measureBankPosition.y = Math.min(maxY, Math.max(margin, event.clientY - measureBankMoveOffset.y))
}
function stopMeasureBankMove() {
  window.removeEventListener('pointermove', moveMeasureBank)
  document.body.style.userSelect = ''
}
function dropSectionOnColumn(column: 1 | 2) { const payload = dragPayload.value; if (!payload) return; if (payload.type === 'group') void createSectionFromGroup(payload.groupKey, column); if (payload.type === 'section') { const section = sections.value.find(item => item.key === payload.sectionKey); if (section) section.coluna = column } endDrag() }
function dropSectionBefore(target: DraftSection) { const payload = dragPayload.value; if (payload?.type !== 'section') return; const moving = sections.value.find(item => item.key === payload.sectionKey); if (!moving || moving.key === target.key) return; sections.value = sections.value.filter(item => item.key !== moving.key); moving.coluna = target.coluna; sections.value.splice(sections.value.findIndex(item => item.key === target.key), 0, moving); endDrag() }
async function dropVariable(section: DraftSection, targetIndex?: number) {
  const payload = dragPayload.value; if (!payload) return
  try {
    if (payload.type === 'bank-variable') { if (usedIds.value.has(payload.variableId)) return void await toast('Essa medida já está no modelo.', 'warning'); const added = addVariablesWithDependencies(section, [payload.variableId], targetIndex); if (added.length) await toast(`Dependências adicionadas: ${added.join(', ')}`, 'info') }
    if (payload.type === 'section-variable') { const source = sections.value.find(item => item.key === payload.sectionKey); if (!source) return; const fromIndex = source.variaveis.findIndex(variable => variable.codVariavel === payload.variableId); if (fromIndex < 0) return; const [moving] = source.variaveis.splice(fromIndex, 1); const adjusted = source.key === section.key && targetIndex != null && fromIndex < targetIndex ? targetIndex - 1 : targetIndex; section.variaveis.splice(adjusted ?? section.variaveis.length, 0, moving!) }
  } catch (error) { await alert('Não foi possível adicionar a medida', message(error), 'error') } finally { endDrag() }
}
async function saveComposition() {
  if (!modelId.value || validationErrors.value.length) return void await alert('Revise a composição', validationErrors.value.join('\n') || 'Selecione um modelo.', 'warning')
  saving.value = true
  try { const response = await api.put<{ data: ModelDetail }>(`/api/web/modelos/${modelId.value}/composicao`, compositionPayload()); normalizeApiModel(response.data); await loadModels(); await toast('Composição salva.', 'success') } catch (error) { await alert('Erro ao salvar', message(error), 'error') } finally { saving.value = false }
}
function discardChanges() { if (modelId.value) void loadModel(modelId.value, true) }
async function generatePreview() {
  if (!modelId.value || validationErrors.value.length) return void await alert('Revise a composição', validationErrors.value.join('\n') || 'Selecione um modelo.', 'warning')
  previewing.value = true
  try { const response = await api.post<{ data: { conteudo: string } }>(`/api/web/modelos/${modelId.value}/preview`, compositionPayload()); previewText.value = response.data.conteudo } catch (error) { await alert('Erro no preview', message(error), 'error') } finally { previewing.value = false }
}
async function downloadTxt() { if (!modelId.value) return; if (dirty.value) return void await alert('Salve antes de baixar', 'O download representa a última composição salva.', 'warning'); try { const blob = await api.getBlob(`/api/web/modelos/${modelId.value}/gerar?formato=texto&download=true`); downloadText(await blob.text(), `modelo_${modelId.value}_modo_texto.txt`) } catch (error) { await alert('Erro ao gerar TXT', message(error), 'error') } }
function message(error: unknown) { return error instanceof Error ? error.message : 'Erro inesperado.' }
function beforeUnload(event: BeforeUnloadEvent) { if (dirty.value) { event.preventDefault(); event.returnValue = '' } }
onBeforeRouteLeave(async () => { if (!dirty.value) return true; const result = await confirm('Sair sem salvar?', 'As alterações realizadas neste modelo serão perdidas.'); return result.isConfirmed })
onMounted(async () => { window.addEventListener('beforeunload', beforeUnload); loading.value = true; try { await Promise.all([loadVariables(), loadModels()]); expandedGroups.value = groupOptions.value.slice(0, 2).map(group => group.key); if (models.value[0]) await loadModel(models.value[0].codModelo, true) } catch (error) { await alert('Erro ao carregar o montador', message(error), 'error') } finally { loading.value = false } })
onBeforeUnmount(() => { window.removeEventListener('beforeunload', beforeUnload); stopMeasureBankMove() })
</script>

<template>
  <div>
    <StudioDsPageHeader title="Montador Modo Texto" subtitle="Selecione medidas do banco e organize o laudo em duas colunas" icon="bi-layout-split" />
    <StudioDsPageShell class="!max-w-[1600px]">
      <div class="mb-4 flex flex-wrap items-center justify-between gap-3 rounded-ds border border-ds-border bg-ds-surface p-3">
        <div class="flex items-center gap-2 text-sm">
          <span class="h-2.5 w-2.5 rounded-full" :class="dirty ? 'bg-amber-400' : 'bg-ds-success'" />
          <strong class="text-ds-text">{{ dirty ? 'Alterações não salvas' : 'Composição salva' }}</strong>
          <span v-if="validationErrors.length" class="text-ds-danger">· {{ validationErrors.length }} pendência(s)</span>
        </div>
        <div class="flex flex-wrap gap-2">
          <StudioDsButton variant="secondary" icon="bi-rulers" @click="measureBankOpen = !measureBankOpen">
            Banco de medidas<span v-if="selectedIds.length"> ({{ selectedIds.length }})</span>
          </StudioDsButton>
          <StudioDsButton variant="ghost" icon="bi-arrow-counterclockwise" :disabled="!dirty" @click="discardChanges">Descartar</StudioDsButton>
          <StudioDsButton variant="secondary" icon="bi-eye" :loading="previewing" :disabled="!modelId" @click="generatePreview">Atualizar preview</StudioDsButton>
          <StudioDsButton icon="bi-check2" :loading="saving" :disabled="!dirty || !modelId" @click="saveComposition">Salvar alterações</StudioDsButton>
          <StudioDsButton variant="secondary" icon="bi-download" :disabled="!modelId" @click="downloadTxt">Baixar TXT</StudioDsButton>
        </div>
      </div>

      <div class="grid gap-5 xl:grid-cols-[240px_minmax(0,1fr)]">
        <StudioDsCard title="1. Modelo">
          <div class="space-y-3">
            <input v-model="modelSearch" class="studio-builder-input" placeholder="Buscar modelo" @keyup.enter="loadModels">
            <div class="flex gap-2">
              <input v-model="newModelName" class="studio-builder-input min-w-0 flex-1" placeholder="Novo modelo" @keyup.enter="createModel">
              <StudioDsButton icon="bi-plus-lg" :loading="saving" aria-label="Criar modelo" @click="createModel" />
            </div>
            <div class="max-h-64 space-y-2 overflow-auto pr-1">
              <div v-for="model in models" :key="model.codModelo" class="flex items-start gap-2 rounded-ds-sm border px-3 py-2 text-sm transition-colors" :class="modelId === model.codModelo ? 'border-ds-primary-accent bg-ds-primary/10 text-ds-primary-accent' : 'border-ds-border text-ds-text hover:bg-ds-surface-elevated'">
                <button type="button" class="min-w-0 flex-1 text-left" @click="loadModel(model.codModelo)">
                  <strong class="block truncate">{{ model.nome }}</strong>
                  <span class="text-xs text-ds-muted">{{ model.totalSecoes }} seção(ões)</span>
                </button>
                <button type="button" class="studio-icon-button text-ds-danger" title="Excluir modelo" @click.stop="deleteModel(model)"><i class="bi bi-trash" /></button>
              </div>
            </div>
            <div v-if="modelId" class="space-y-2 border-t border-ds-border pt-3">
              <label class="block text-xs font-semibold text-ds-muted" for="studio-model-name">Nome do modelo</label>
              <input id="studio-model-name" v-model="modelName" class="studio-builder-input">
              <StudioDsButton variant="danger" icon="bi-trash" class="w-full" @click="deleteCurrentModel">Excluir modelo</StudioDsButton>
            </div>
          </div>
        </StudioDsCard>

        <StudioDsCard title="2. Composição em duas colunas">
          <div v-if="loading" class="py-16 text-center text-sm text-ds-muted">Carregando composição...</div>
          <div v-else-if="!modelId" class="py-16 text-center text-sm text-ds-muted">Crie ou selecione um modelo.</div>
          <div v-else class="grid gap-4 lg:grid-cols-2">
            <div v-for="column in ([1, 2] as const)" :key="column" class="min-h-[32rem] rounded-ds border border-dashed border-ds-border bg-ds-page/40 p-3" @dragover.prevent @drop="dropSectionOnColumn(column)">
              <div class="mb-3 flex items-center justify-between gap-2"><div><strong class="text-sm text-ds-text">Coluna {{ column }}</strong><span class="ml-2 text-xs text-ds-muted">{{ columnSections(column).length }} seção(ões)</span></div><StudioDsButton variant="ghost" icon="bi-plus-lg" @click="openCreateSectionModal(column)">Seção</StudioDsButton></div>
              <div class="space-y-3">
                <article v-for="(section, sectionIndex) in columnSections(column)" :key="section.key" draggable="true" class="rounded-ds-sm border border-ds-border bg-ds-surface shadow-sm" @dragstart="startDrag({ type: 'section', sectionKey: section.key }, $event)" @dragend="endDrag" @dragover.prevent @drop.stop="dropSectionBefore(section)">
                  <header class="flex cursor-grab items-center gap-2 border-b border-ds-border px-3 py-2">
                    <i class="bi bi-grip-vertical text-ds-muted" /><strong class="min-w-0 flex-1 truncate text-sm text-ds-text">{{ section.nome }}</strong>
                    <button type="button" class="studio-icon-button" title="Subir" :disabled="sectionIndex === 0" @click.stop="moveSection(section, -1)"><i class="bi bi-arrow-up" /></button>
                    <button type="button" class="studio-icon-button" title="Descer" :disabled="sectionIndex === columnSections(column).length - 1" @click.stop="moveSection(section, 1)"><i class="bi bi-arrow-down" /></button>
                    <button type="button" class="studio-icon-button" :title="`Mover para coluna ${column === 1 ? 2 : 1}`" @click.stop="moveSectionToColumn(section)"><i class="bi bi-arrow-left-right" /></button>
                    <button type="button" class="studio-icon-button" title="Renomear" @click.stop="openRenameSectionModal(section)"><i class="bi bi-pencil" /></button>
                    <button type="button" class="studio-icon-button text-ds-danger" title="Excluir" @click.stop="removeSection(section)"><i class="bi bi-trash" /></button>
                  </header>
                  <div class="min-h-16 space-y-1 p-2" @dragover.prevent @drop.stop="dropVariable(section)">
                    <p v-if="!section.variaveis.length" class="py-4 text-center text-xs text-ds-muted">Arraste medidas para esta seção.</p>
                    <div v-for="(variable, variableIndex) in section.variaveis" :key="variable.codVariavel" draggable="true" class="flex cursor-grab items-center gap-2 rounded border border-transparent px-2 py-1.5 text-xs hover:border-ds-border hover:bg-ds-surface-elevated" @dragstart.stop="startDrag({ type: 'section-variable', sectionKey: section.key, variableId: variable.codVariavel }, $event)" @dragend="endDrag" @dragover.prevent @drop.stop="dropVariable(section, variableIndex)">
                      <i class="bi bi-grip-vertical text-ds-muted" /><span class="min-w-0 flex-1"><strong class="block truncate text-ds-text">{{ variable.nome }}</strong><span class="text-ds-muted">{{ variable.sigla }}{{ variable.unidade ? ` · ${variable.unidade}` : '' }}</span></span>
                      <span v-if="variable.formula" class="text-ds-primary-accent" title="Fórmula">ƒ</span><i v-if="variable.normalidade" class="bi bi-activity text-ds-success" title="Normalidade" />
                      <label class="flex items-center gap-1 text-ds-muted" title="Exibir gráfico"><input v-model="variable.exibirGrafico" type="checkbox" @click.stop><i class="bi bi-bar-chart" /></label>
                      <button type="button" class="studio-icon-button" title="Subir" :disabled="variableIndex === 0" @click.stop="moveVariable(section, variableIndex, -1)"><i class="bi bi-arrow-up" /></button>
                      <button type="button" class="studio-icon-button" title="Descer" :disabled="variableIndex === section.variaveis.length - 1" @click.stop="moveVariable(section, variableIndex, 1)"><i class="bi bi-arrow-down" /></button>
                      <button type="button" class="studio-icon-button" title="Mover para outra seção" @click.stop="moveVariableToOtherSection(section, variable)"><i class="bi bi-box-arrow-right" /></button>
                      <button type="button" class="studio-icon-button" title="Detalhes" @click.stop="details = variable"><i class="bi bi-info-circle" /></button><button type="button" class="studio-icon-button text-ds-danger" title="Remover" @click.stop="removeVariable(section, variable.codVariavel)"><i class="bi bi-x-lg" /></button>
                    </div>
                  </div>
                  <footer v-if="section.variaveis.length" class="flex justify-end border-t border-ds-border px-2 py-1"><button type="button" class="text-xs text-ds-muted hover:text-ds-danger" @click="clearSection(section)">Esvaziar seção</button></footer>
                </article>
                <div v-if="!columnSections(column).length" class="flex min-h-36 items-center justify-center rounded-ds-sm border border-dashed border-ds-border text-center text-xs text-ds-muted">Arraste um grupo ou uma seção para esta coluna.</div>
              </div>
            </div>
          </div>
        </StudioDsCard>
      </div>

      <StudioDsCard v-if="validationErrors.length" title="Pendências da composição" class="mt-5 border border-ds-danger/50"><ul class="list-disc space-y-1 pl-5 text-sm text-ds-danger"><li v-for="error in validationErrors" :key="error">{{ error }}</li></ul></StudioDsCard>
      <StudioDsCard title="Preview TXT do rascunho" class="mt-5"><StudioModoTextoPreview :text="previewText" /></StudioDsCard>

      <aside
        v-if="measureBankOpen"
        ref="measureBankRef"
        class="fixed top-24 z-40 flex h-[min(46rem,calc(100vh-7.25rem))] w-[min(32rem,calc(100vw-2rem))] flex-col overflow-hidden rounded-ds border border-ds-border bg-ds-surface shadow-2xl"
        :class="measureBankPosition.x == null ? (measureBankSide === 'right' ? 'right-4 xl:right-6' : 'left-4 xl:left-6') : ''"
        :style="measureBankStyle"
        aria-label="Banco de medidas"
      >
        <header class="flex cursor-move select-none items-start gap-3 border-b border-ds-border bg-ds-surface-elevated px-4 py-3" title="Arraste para mover o painel" @pointerdown="startMeasureBankMove">
          <i class="bi bi-grip-vertical mt-0.5 text-ds-muted" aria-hidden="true" />
          <div class="min-w-0 flex-1">
            <h2 class="font-semibold text-ds-text">Banco de medidas</h2>
            <p class="mt-0.5 text-xs text-ds-muted">Arraste uma medida para uma seção ou um grupo selecionado para uma coluna.</p>
          </div>
          <button type="button" class="studio-icon-button" :title="measureBankSide === 'right' ? 'Mover painel para a esquerda' : 'Mover painel para a direita'" @pointerdown.stop @click="dockMeasureBank(measureBankSide === 'right' ? 'left' : 'right')">
            <i class="bi bi-arrow-left-right" />
          </button>
          <button type="button" class="studio-icon-button" title="Fechar banco de medidas" aria-label="Fechar banco de medidas" @pointerdown.stop @click="measureBankOpen = false"><i class="bi bi-x-lg" /></button>
        </header>

        <div class="grid gap-3 border-b border-ds-border p-4 sm:grid-cols-[minmax(0,1fr)_13rem]">
          <input v-model="variableSearch" class="studio-builder-input" placeholder="Buscar nome, sigla ou abreviação">
          <select v-model="groupFilter" class="studio-builder-input"><option value="all">Todos os grupos</option><option v-for="group in groupOptions" :key="group.key" :value="group.key">{{ group.nome }}</option></select>
        </div>

        <div class="min-h-0 flex-1 space-y-2 overflow-y-auto p-3">
          <section v-for="group in visibleGroups" :key="group.key" class="rounded-ds-sm border border-ds-border bg-ds-surface-elevated">
            <div draggable="true" class="flex cursor-grab items-center gap-2 px-3 py-2.5" @dragstart="startDrag({ type: 'group', groupKey: group.key }, $event)" @dragend="endDrag">
              <button type="button" class="text-ds-muted" :aria-label="`Expandir ${group.nome}`" @click="toggleGroup(group.key)"><i class="bi" :class="expandedGroups.includes(group.key) ? 'bi-chevron-down' : 'bi-chevron-right'" /></button>
              <input type="checkbox" :checked="group.variables.filter(v => !usedIds.has(v.codVariavel)).length > 0 && group.variables.filter(v => !usedIds.has(v.codVariavel)).every(v => selectedSet.has(v.codVariavel))" :aria-label="`Selecionar medidas de ${group.nome}`" @click.stop @change="toggleGroupSelection(group.key)">
              <strong class="min-w-0 flex-1 whitespace-normal break-words text-sm leading-5 text-ds-text">{{ group.nome }}</strong>
              <span class="shrink-0 text-xs text-ds-muted">{{ groupSelection(group.key).length }}/{{ group.variables.length }}</span>
              <button type="button" class="studio-icon-button text-ds-primary-accent" title="Criar seção na coluna esquerda" @click.stop="createSectionFromGroup(group.key, 1)"><i class="bi bi-plus-square" /></button>
            </div>
            <div v-if="expandedGroups.includes(group.key)" class="space-y-1 border-t border-ds-border p-2">
              <div v-for="variable in group.variables" :key="variable.codVariavel" draggable="true" class="flex cursor-grab items-start gap-3 rounded-ds-sm border border-transparent px-3 py-2.5 text-sm" :class="usedIds.has(variable.codVariavel) ? 'opacity-45' : 'hover:border-ds-border hover:bg-ds-surface'" @dragstart="startDrag({ type: 'bank-variable', variableId: variable.codVariavel }, $event)" @dragend="endDrag">
                <input v-model="selectedIds" class="mt-1" type="checkbox" :value="variable.codVariavel" :disabled="usedIds.has(variable.codVariavel)" @click.stop>
                <span class="min-w-0 flex-1">
                  <strong class="block whitespace-normal break-words leading-5 text-ds-text">{{ variable.nome }}</strong>
                  <small class="mt-0.5 block text-ds-muted">{{ variable.sigla }}{{ variable.unidade ? ` · ${variable.unidade}` : '' }}</small>
                </span>
                <span v-if="variable.formula" title="Possui fórmula" class="mt-0.5 text-ds-primary-accent">ƒ</span>
                <button type="button" class="studio-icon-button" title="Ver detalhes" @click.stop="details = variable"><i class="bi bi-info-circle" /></button>
              </div>
            </div>
          </section>
          <p v-if="!visibleGroups.length" class="py-10 text-center text-sm text-ds-muted">Nenhuma medida encontrada.</p>
        </div>

        <footer class="flex items-center justify-between gap-3 border-t border-ds-border bg-ds-surface-elevated px-4 py-2 text-xs text-ds-muted">
          <span>{{ variables.length }} medida(s)</span>
          <span>{{ selectedIds.length }} selecionada(s)</span>
        </footer>
      </aside>

      <DsModal v-model="sectionNameModalOpen" :title="sectionNameMode === 'rename' ? 'Renomear seção' : 'Nova seção'" size="sm">
        <form class="space-y-4" @submit.prevent="saveSectionName">
          <DsInput v-model="sectionNameValue" label="Nome da seção" placeholder="Informe o nome da seção" required autocomplete="off" />
        </form>
        <template #footer>
          <DsButton variant="secondary" @click="sectionNameModalOpen = false">Cancelar</DsButton>
          <DsButton :disabled="!sectionNameValue.trim()" @click="saveSectionName">Salvar</DsButton>
        </template>
      </DsModal>

      <div v-if="details" class="fixed inset-0 z-50 flex justify-end bg-black/45" role="dialog" aria-modal="true" @click.self="details = null">
        <aside class="h-full w-full max-w-lg overflow-auto bg-ds-surface p-6 shadow-2xl">
          <div class="mb-5 flex items-start justify-between gap-3"><div><h2 class="text-lg font-semibold text-ds-text">{{ details.nome }}</h2><p class="text-sm text-ds-muted">{{ details.sigla }} · {{ details.grupo || 'Sem grupo' }}</p></div><button type="button" class="studio-icon-button" aria-label="Fechar" @click="details = null"><i class="bi bi-x-lg" /></button></div>
          <dl class="space-y-4 text-sm"><div><dt class="font-semibold text-ds-muted">Unidade</dt><dd class="mt-1 text-ds-text">{{ details.unidade || 'Sem unidade' }}</dd></div><div><dt class="font-semibold text-ds-muted">Casas decimais</dt><dd class="mt-1 text-ds-text">{{ details.casasDecimais }}</dd></div><div><dt class="font-semibold text-ds-muted">Normalidades</dt><dd class="mt-1 whitespace-pre-wrap text-ds-text">{{ details.normalidade || 'Sem normalidade' }}</dd></div><div><dt class="font-semibold text-ds-muted">Classificações</dt><dd class="mt-1 text-ds-text">{{ details.classificacoes || 'Sem classificações' }}</dd></div><div><dt class="font-semibold text-ds-muted">Fórmula / função</dt><dd class="mt-1 break-words rounded bg-ds-surface-elevated p-3 font-mono text-xs text-ds-text">{{ details.formula || 'Sem fórmula' }}</dd></div><div><dt class="font-semibold text-ds-muted">Dependências</dt><dd class="mt-1 text-ds-text">{{ details.dependencias?.map(id => variableById.get(id)?.nome || id).join(', ') || 'Nenhuma' }}</dd></div></dl>
        </aside>
      </div>
    </StudioDsPageShell>
  </div>
</template>

<style scoped>
.studio-builder-input { width: 100%; height: 2.5rem; border-radius: var(--studio-radius-sm); border: 1px solid var(--color-ds-field-border); background: var(--color-ds-surface-elevated); padding: 0 0.75rem; font-size: 0.875rem; color: var(--color-ds-text); }
.studio-builder-input:focus { outline: 2px solid color-mix(in srgb, var(--color-ds-primary-accent) 35%, transparent); border-color: var(--color-ds-primary-accent); }
.studio-icon-button { display: inline-flex; width: 1.75rem; height: 1.75rem; flex-shrink: 0; align-items: center; justify-content: center; border-radius: 0.375rem; color: var(--color-ds-muted); }
.studio-icon-button:hover:not(:disabled) { background: var(--color-ds-surface-elevated); color: var(--color-ds-primary-accent); }
.studio-icon-button:disabled { opacity: 0.3; cursor: not-allowed; }
</style>
