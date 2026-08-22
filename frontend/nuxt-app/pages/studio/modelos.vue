<script setup lang="ts">
definePageMeta({ layout: 'studio' })

interface VariableOption {
  codVariavel: number
  nome: string
  sigla: string
  formula?: string | null
  normalidade?: string | null
  unidade?: string | null
  abreviacao?: string | null
}
interface SectionVariable {
  codVariavel: number
  nome: string
  sigla: string
  exibirGrafico: boolean
  ordem: number
  unidade?: string | null
  normalidade?: string | null
}
interface Section {
  codSecao: number
  nome: string
  ordem: number
  x: number
  y: number
  largura: number
  altura: number
  variaveis: SectionVariable[]
}
interface ModelItem { codModelo: number; nome: string; totalSecoes: number }
interface ModelDetail { codModelo: number; nome: string; secoes: Section[] }

const api = useApi()
const { toast, alert } = useStudioSwal()
const { downloadText } = useHtmlPreview()

const models = ref<ModelItem[]>([])
const variables = ref<VariableOption[]>([])
const currentModel = ref<ModelDetail | null>(null)
const loading = ref(false)
const saving = ref(false)
const modelSearch = ref('')
const variableSearch = ref('')
const newModelName = ref('')
const newSectionName = ref('')
const draggingSectionId = ref<number | null>(null)
const draggingVariable = ref<{ codVariavel: number; fromSectionId?: number } | null>(null)

const filteredVariables = computed(() => {
  const term = variableSearch.value.trim().toLocaleLowerCase()
  const rows = variables.value
  if (!term) return rows.slice(0, 120)
  return rows
    .filter(v => `${v.nome} ${v.sigla} ${v.abreviacao ?? ''}`.toLocaleLowerCase().includes(term))
    .slice(0, 120)
})

async function loadModels() {
  const q = new URLSearchParams({ page: '1', pageSize: '50', nome: modelSearch.value })
  const res = await api.get<{ data: ModelItem[] }>(`/api/web/modelos?${q}`)
  models.value = res.data || []
  if (!currentModel.value && models.value[0]) await loadModel(models.value[0].codModelo)
}

async function loadVariables() {
  const res = await api.get<{ data: VariableOption[] }>('/api/web/modelos/variaveis')
  variables.value = res.data || []
}

async function loadModel(id: number) {
  loading.value = true
  try {
    const res = await api.get<{ data: ModelDetail }>(`/api/web/modelos/${id}`)
    currentModel.value = res.data
  } catch (e) {
    await toast(msg(e), 'error')
  } finally {
    loading.value = false
  }
}

async function createModel() {
  const name = newModelName.value.trim()
  if (!name) return
  saving.value = true
  try {
    const res = await api.post<{ data: { codModelo: number } }>('/api/web/modelos', { nome: name })
    newModelName.value = ''
    await loadModels()
    await loadModel(res.data.codModelo)
    await toast('Modelo criado.')
  } catch (e) {
    await toast(msg(e), 'error')
  } finally {
    saving.value = false
  }
}

async function createSection() {
  if (!currentModel.value) return
  const name = newSectionName.value.trim()
  if (!name) return
  saving.value = true
  try {
    await api.post(`/api/web/modelos/${currentModel.value.codModelo}/secoes`, { nome: name, variaveis: [] })
    newSectionName.value = ''
    await loadModel(currentModel.value.codModelo)
  } catch (e) {
    await toast(msg(e), 'error')
  } finally {
    saving.value = false
  }
}

async function renameSection(section: Section) {
  const name = window.prompt('Nome da seção', section.nome)?.trim()
  if (!currentModel.value || !name) return
  await saveSection({ ...section, nome: name })
}

async function deleteSection(section: Section) {
  if (!currentModel.value || !window.confirm(`Excluir "${section.nome}"?`)) return
  try {
    await api.del(`/api/web/modelos/${currentModel.value.codModelo}/secoes/${section.codSecao}`)
    await loadModel(currentModel.value.codModelo)
  } catch (e) {
    await toast(msg(e), 'error')
  }
}

function dragBankVariable(variable: VariableOption) {
  draggingVariable.value = { codVariavel: variable.codVariavel }
}

function dragSectionVariable(section: Section, variable: SectionVariable) {
  draggingVariable.value = { codVariavel: variable.codVariavel, fromSectionId: section.codSecao }
}

async function dropVariable(target: Section, targetIndex?: number) {
  if (!currentModel.value || !draggingVariable.value) return
  const variableId = draggingVariable.value.codVariavel
  const fromSection = currentModel.value.secoes.find(s => s.codSecao === draggingVariable.value?.fromSectionId)
  const movingInsideTarget = fromSection?.codSecao === target.codSecao

  if (movingInsideTarget) {
    const currentIndex = target.variaveis.findIndex(v => v.codVariavel === variableId)
    if (currentIndex < 0) return
    const [moved] = target.variaveis.splice(currentIndex, 1)
    const nextIndex = targetIndex == null ? target.variaveis.length : Math.min(targetIndex, target.variaveis.length)
    target.variaveis.splice(nextIndex, 0, moved!)
    await saveSection(target)
    draggingVariable.value = null
    return
  }

  if (fromSection) {
    fromSection.variaveis = fromSection.variaveis.filter(v => v.codVariavel !== variableId)
    await saveSection(fromSection, false)
  }

  if (!target.variaveis.some(v => v.codVariavel === variableId)) {
    const source = variables.value.find(v => v.codVariavel === variableId)
    if (!source) return
    const next: SectionVariable = {
      codVariavel: source.codVariavel,
      nome: source.nome,
      sigla: source.sigla,
      exibirGrafico: false,
      ordem: target.variaveis.length + 1,
      unidade: source.unidade,
      normalidade: source.normalidade
    }
    target.variaveis.splice(targetIndex ?? target.variaveis.length, 0, next)
  }

  await saveSection(target)
  draggingVariable.value = null
}

async function removeVariable(section: Section, variable: SectionVariable) {
  section.variaveis = section.variaveis.filter(v => v.codVariavel !== variable.codVariavel)
  await saveSection(section)
}

async function saveSection(section: Section, reload = true) {
  if (!currentModel.value) return
  const payload = {
    nome: section.nome,
    variaveis: section.variaveis.map((v, index) => ({ codVariavel: v.codVariavel, exibirGrafico: v.exibirGrafico, ordem: index + 1 }))
  }
  try {
    await api.put(`/api/web/modelos/${currentModel.value.codModelo}/secoes/${section.codSecao}`, payload)
    if (reload) await loadModel(currentModel.value.codModelo)
  } catch (e) {
    await toast(msg(e), 'error')
  }
}

async function dropSection(targetIndex: number) {
  if (!currentModel.value || draggingSectionId.value == null) return
  const sections = [...currentModel.value.secoes]
  const fromIndex = sections.findIndex(s => s.codSecao === draggingSectionId.value)
  if (fromIndex < 0) return
  const [moved] = sections.splice(fromIndex, 1)
  sections.splice(targetIndex, 0, moved!)
  currentModel.value.secoes = sections
  draggingSectionId.value = null
  try {
    await api.put(`/api/web/modelos/${currentModel.value.codModelo}/secoes/ordem`, { secaoIds: sections.map(s => s.codSecao) })
  } catch (e) {
    await toast(msg(e), 'error')
  }
}

async function downloadTxt() {
  if (!currentModel.value) return
  try {
    const blob = await api.getBlob(`/api/web/modelos/${currentModel.value.codModelo}/gerar?formato=texto&download=true`)
    downloadText(await blob.text(), `modelo_${currentModel.value.codModelo}_modo_texto.txt`)
  } catch (e) {
    await alert('Erro ao gerar TXT', msg(e), 'error')
  }
}

function msg(e: unknown) {
  return e instanceof Error ? e.message : 'Erro inesperado.'
}

onMounted(async () => {
  await Promise.all([loadVariables(), loadModels()])
})
</script>

<template>
  <div>
    <StudioDsPageHeader
      title="Modelos Modo Texto"
      subtitle="Monte modelos usando seções e variáveis do banco de referências"
      icon="bi-grid-3x3-gap"
    />
    <StudioDsPageShell>
      <div class="grid gap-6 xl:grid-cols-[320px_1fr]">
        <StudioDsCard title="Modelos">
          <div class="space-y-3">
            <input
              v-model="modelSearch"
              class="h-10 w-full rounded-ds-sm border border-ds-field-border bg-ds-surface-elevated px-3 text-sm text-ds-text"
              placeholder="Buscar modelo"
              @keyup.enter="loadModels"
            >
            <div class="flex gap-2">
              <input
                v-model="newModelName"
                class="h-10 min-w-0 flex-1 rounded-ds-sm border border-ds-field-border bg-ds-surface-elevated px-3 text-sm text-ds-text"
                placeholder="Novo modelo"
                @keyup.enter="createModel"
              >
              <StudioDsButton icon="bi-plus" :loading="saving" @click="createModel" />
            </div>
            <div class="max-h-72 space-y-2 overflow-auto">
              <button
                v-for="model in models"
                :key="model.codModelo"
                type="button"
                class="w-full rounded-ds-sm border px-3 py-2 text-left text-sm"
                :class="currentModel?.codModelo === model.codModelo ? 'border-ds-primary-accent text-ds-primary-accent' : 'border-ds-border text-ds-text'"
                @click="loadModel(model.codModelo)"
              >
                <strong class="block">{{ model.nome }}</strong>
                <span class="text-xs text-ds-muted">{{ model.totalSecoes }} seção(ões)</span>
              </button>
            </div>
          </div>
        </StudioDsCard>

        <div class="space-y-6">
          <StudioDsCard title="Banco de variáveis">
            <input
              v-model="variableSearch"
              class="mb-3 h-10 w-full rounded-ds-sm border border-ds-field-border bg-ds-surface-elevated px-3 text-sm text-ds-text"
              placeholder="Buscar variável para arrastar..."
            >
            <div class="grid max-h-56 gap-2 overflow-auto md:grid-cols-2 xl:grid-cols-3">
              <div
                v-for="variable in filteredVariables"
                :key="variable.codVariavel"
                draggable="true"
                class="cursor-grab rounded-ds-sm border border-ds-border bg-ds-surface-elevated px-3 py-2 text-sm text-ds-text"
                @dragstart="dragBankVariable(variable)"
              >
                <strong class="block truncate">{{ variable.nome }}</strong>
                <span class="text-xs text-ds-muted">{{ variable.sigla }} {{ variable.unidade ? `· ${variable.unidade}` : '' }}</span>
              </div>
            </div>
          </StudioDsCard>

          <StudioDsCard :title="currentModel?.nome || 'Modelo'">
            <div v-if="loading" class="py-10 text-center text-sm text-ds-muted">Carregando modelo...</div>
            <div v-else-if="!currentModel" class="py-10 text-center text-sm text-ds-muted">Selecione ou crie um modelo.</div>
            <div v-else class="space-y-4">
              <div class="flex flex-wrap gap-2">
                <input
                  v-model="newSectionName"
                  class="h-10 min-w-64 rounded-ds-sm border border-ds-field-border bg-ds-surface-elevated px-3 text-sm text-ds-text"
                  placeholder="Nova seção"
                  @keyup.enter="createSection"
                >
                <StudioDsButton icon="bi-plus-lg" @click="createSection">Adicionar seção</StudioDsButton>
                <StudioDsButton variant="secondary" icon="bi-download" @click="downloadTxt">Baixar TXT</StudioDsButton>
              </div>

              <section
                v-for="(section, sectionIndex) in currentModel.secoes"
                :key="section.codSecao"
                draggable="true"
                class="rounded-ds border border-ds-border bg-ds-surface px-4 py-3"
                @dragstart="draggingSectionId = section.codSecao"
                @dragover.prevent
                @drop="dropSection(sectionIndex)"
              >
                <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
                  <h2 class="text-base font-semibold text-ds-text">{{ sectionIndex + 1 }}. {{ section.nome }}</h2>
                  <div class="flex gap-2">
                    <StudioDsButton variant="ghost" icon="bi-pencil" @click="renameSection(section)" />
                    <StudioDsButton variant="danger" icon="bi-trash" @click="deleteSection(section)" />
                  </div>
                </div>

                <div
                  class="min-h-20 rounded-ds-sm border border-dashed border-ds-border p-3"
                  @dragover.prevent
                  @drop="dropVariable(section)"
                >
                  <div v-if="!section.variaveis.length" class="text-sm text-ds-muted">Arraste variáveis para esta seção.</div>
                  <div v-else class="space-y-2">
                    <div
                      v-for="variable in section.variaveis"
                      :key="variable.codVariavel"
                      draggable="true"
                      class="flex items-center justify-between gap-3 rounded-ds-sm border border-ds-border bg-ds-surface-elevated px-3 py-2 text-sm"
                      @dragstart.stop="dragSectionVariable(section, variable)"
                      @dragover.prevent
                      @drop.stop="dropVariable(section, section.variaveis.findIndex(v => v.codVariavel === variable.codVariavel))"
                    >
                      <span class="min-w-0">
                        <strong class="block truncate text-ds-text">{{ variable.nome }}</strong>
                        <span class="text-xs text-ds-muted">{{ variable.sigla }} {{ variable.unidade ? `· ${variable.unidade}` : '' }}</span>
                      </span>
                      <button type="button" class="text-ds-muted hover:text-ds-danger" title="Remover" @click="removeVariable(section, variable)">
                        <i class="bi bi-x-lg" />
                      </button>
                    </div>
                  </div>
                </div>
              </section>
            </div>
          </StudioDsCard>
        </div>
      </div>
    </StudioDsPageShell>
  </div>
</template>
