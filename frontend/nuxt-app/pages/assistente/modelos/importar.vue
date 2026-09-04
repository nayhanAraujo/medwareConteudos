<template>
  <div>
    <DsPageHeader title="Importar modelo de laudo" subtitle="Selecione um script de /scripts/pacotes e vincule especialidades e procedimentos" icon="cloud-arrow-up">
      <template #actions>
        <DsButton variant="secondary" to="/assistente">Cancelar</DsButton>
      </template>
    </DsPageHeader>
    <DsPageShell>
      <AssistenteNav />
      <DsAlert variant="info" class="mb-5">
        Selecione um modelo já cadastrado em Scripts/Pacotes. Laudos Flex importa a DLL em Base64; Laudos UX importa JSON (tipo 3). Quando houver MRD padrão, ele será copiado automaticamente.
      </DsAlert>
      <form class="space-y-6" @submit.prevent="submit">
        <DsCard>
          <DsSectionTitle title="1. Scripts de origem (/scripts/pacotes)" />
          <div class="mt-4 grid gap-4 md:grid-cols-2">
            <DsSelect v-model="form.sistema" label="Sistema" required @update:model-value="onSistemaChange">
              <option value="Laudos Flex">Laudos Flex</option>
              <option value="Laudos UX">Laudos UX</option>
            </DsSelect>
            <DsSelect v-model="pacoteFiltro" label="Pacote" @update:model-value="onPacoteChange">
              <option value="">Todos os pacotes</option>
              <option v-for="pacote in pacotes" :key="pacote.codPacote" :value="String(pacote.codPacote)">
                {{ pacote.nome }}
              </option>
            </DsSelect>
            <DsInput
              v-model="scriptSearch"
              label="Filtrar por nome"
              hint="Atualiza a lista do campo Script abaixo conforme você digita."
              placeholder="Ex.: ecocardiograma, consulta..."
              @update:model-value="loadScripts"
            />
            <label class="flex items-center gap-2 self-end rounded-2xl border border-gray-200 bg-white px-4 py-3 text-sm text-ds-text">
              <input
                v-model="importarPacoteCompleto"
                type="checkbox"
                class="rounded"
                :disabled="!pacoteFiltro"
                @change="onImportarPacoteCompletoChange"
              />
              Importar todos os modelos do pacote
            </label>
            <div class="md:col-span-2">
              <div class="mb-1.5 flex items-center justify-between gap-3">
                <label class="text-sm font-medium text-ds-text">Scripts</label>
                <span class="text-xs text-gray-500">{{ selectedScriptIds.length }} selecionado{{ selectedScriptIds.length === 1 ? '' : 's' }}</span>
              </div>
              <div class="max-h-72 overflow-auto rounded-2xl border border-gray-200 bg-white">
                <label
                  v-for="script in scripts"
                  :key="script.codScriptLaudo"
                  class="flex cursor-pointer items-start gap-3 border-b border-gray-100 px-4 py-3 text-sm last:border-b-0 hover:bg-gray-50"
                >
                  <input
                    :checked="selectedScriptIds.includes(script.codScriptLaudo)"
                    type="checkbox"
                    class="mt-1 rounded"
                    @change="toggleScript(script)"
                  />
                  <span class="min-w-0 flex-1">
                    <span class="block font-medium text-ds-text">{{ script.nome }}</span>
                    <span class="mt-0.5 block text-xs text-gray-500">
                      {{ script.sistema }}{{ script.nomePacote ? ` (${script.nomePacote})` : '' }}
                      · DLL: {{ script.temArquivoDll ? 'sim' : 'não' }}
                      · JSON: {{ script.temArquivoJson ? 'sim' : 'não' }}
                      · MRD: {{ script.temArquivoMrd ? 'sim' : 'não' }}
                    </span>
                  </span>
                </label>
                <div v-if="!scripts.length" class="px-4 py-6 text-center text-sm text-gray-500">
                  Nenhum script encontrado.
                </div>
              </div>
              <p class="mt-1 text-xs text-gray-500">Modelos cadastrados em /scripts/pacotes do sistema escolhido.</p>
            </div>
            <DsInput v-if="selectedScriptIds.length <= 1" v-model="form.tituloScript" label="Título no Assistente (opcional)" placeholder="Usa o nome do script se vazio" />
            <DsInput v-if="selectedScriptIds.length <= 1" v-model="form.tituloMrd" label="Título do MRD no Assistente (opcional)" placeholder="Usa o MRD padrão se vazio" />
            <div v-if="selectedScriptIds.length === 1 && selectedScript" class="md:col-span-2 text-sm text-gray-600">
              Tipo detectado: <strong>{{ tipoLabel(inferredTipo) }}</strong>
              · DLL: {{ selectedScript.temArquivoDll ? 'sim' : 'não' }}
              · JSON: {{ selectedScript.temArquivoJson ? 'sim' : 'não' }}
              · MRD: {{ selectedScript.temArquivoMrd ? 'sim' : 'não' }}
            </div>
            <div v-else-if="selectedScriptIds.length > 1" class="md:col-span-2 text-sm text-gray-600">
              {{ importarPacoteCompleto ? 'Todos os modelos do pacote selecionado' : 'Os modelos selecionados' }} serão importados com os títulos originais e com MRD apenas quando existir no script de origem.
            </div>
          </div>
        </DsCard>

        <DsCard>
          <DsSectionTitle title="2. Vínculos" />
          <div class="mt-4 grid gap-4 md:grid-cols-2">
            <AssistenteMultiSelect v-model="form.especialidades" label="Especialidades (obrigatório)" :options="especialidades" />
            <AssistenteSearchMultiSelect v-model="form.procedimentos" label="Procedimentos (opcional)" />
          </div>
        </DsCard>

        <DsAlert v-if="validationError" variant="error">{{ validationError }}</DsAlert>
        <div class="flex justify-end gap-2">
          <DsButton variant="secondary" to="/assistente">Cancelar</DsButton>
          <DsButton type="submit" variant="success" :loading="saving" :disabled="!selectedScriptIds.length">
            Importar {{ selectedScriptIds.length > 1 ? 'modelos' : 'modelo' }}
          </DsButton>
        </div>
      </form>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { PacoteDto, ScriptListItem } from '~/composables/useScriptsApi'

definePageMeta({ layout: 'default', middleware: 'admin' })

const api = useAssistenteApi()
const scriptsApi = useScriptsApi()
const swal = useSwal()

const saving = ref(false)
const validationError = ref('')
const especialidades = ref<{ id: number; nome: string }[]>([])
const pacotes = ref<PacoteDto[]>([])
const scripts = ref<ScriptListItem[]>([])
const knownScripts = ref<Record<number, ScriptListItem>>({})
const scriptSearch = ref('')
const pacoteFiltro = ref('')
const importarPacoteCompleto = ref(false)
const selectedScriptIds = ref<number[]>([])

const form = reactive({
  sistema: 'Laudos Flex' as 'Laudos Flex' | 'Laudos UX',
  tituloScript: '',
  tituloMrd: '',
  especialidades: [] as number[],
  procedimentos: [] as number[]
})

function normalizeScript(raw: Record<string, unknown>): ScriptListItem {
  return {
    codScriptLaudo: Number(raw.codScriptLaudo ?? raw.CodScriptLaudo ?? 0),
    codPacote: Number(raw.codPacote ?? raw.CodPacote ?? 0) || undefined,
    nome: String(raw.nome ?? raw.Nome ?? ''),
    descricao: raw.descricao as string | undefined,
    linguagem: raw.linguagem as string | undefined,
    sistema: String(raw.sistema ?? raw.Sistema ?? ''),
    aprovado: Number(raw.aprovado ?? 0),
    ativo: Number(raw.ativo ?? 0),
    temArquivoJson: Boolean(raw.temArquivoJson ?? raw.TemArquivoJson),
    temArquivoDll: Boolean(raw.temArquivoDll ?? raw.TemArquivoDll),
    temArquivoMrd: Boolean(raw.temArquivoMrd ?? raw.TemArquivoMrd),
    nomePacote: raw.nomePacote as string | undefined,
    mrdList: (raw.mrdList as ScriptListItem['mrdList']) ?? [],
    imagensDisplay: (raw.imagensDisplay as ScriptListItem['imagensDisplay']) ?? []
  }
}

const selectedScript = computed(() => {
  const id = selectedScriptIds.value[0]
  if (!id) return null
  return knownScripts.value[id] ?? null
})

const selectedScripts = computed(() =>
  selectedScriptIds.value
    .map(id => knownScripts.value[id])
    .filter((script): script is ScriptListItem => Boolean(script))
)

const inferredTipo = computed(() => {
  if (form.sistema === 'Laudos UX') return 3
  if (selectedScript.value?.linguagem?.toLowerCase().includes('c#')) return 2
  return 2
})

function tipoLabel(value: number) {
  const map: Record<number, string> = { 1: 'VB (legado)', 2: 'C#', 3: 'JSON' }
  return map[value] ?? String(value)
}

let scriptTimer: ReturnType<typeof setTimeout> | undefined

function escapeHtml(value: string) {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;')
}

function scriptName(id: number) {
  return knownScripts.value[id]?.nome || `Script #${id}`
}

async function showBatchImportFailures(result: Awaited<ReturnType<typeof api.importModels>>) {
  if (!import.meta.client) return
  const { default: Swal } = await import('sweetalert2')
  const failures = result.itens.filter(item => !item.importado)
  const items = failures.map(item => `
    <li style="padding:10px 0;border-bottom:1px solid #e5e7eb;">
      <strong style="display:block;color:#111827;">${escapeHtml(scriptName(item.codScriptLaudoOrigem))}</strong>
      <span style="display:block;margin-top:4px;color:#4b5563;font-size:0.875rem;">${escapeHtml(item.mensagem || 'Falha não informada pela API.')}</span>
    </li>
  `).join('')

  await Swal.fire({
    icon: 'warning',
    title: 'Importação parcial',
    html: `
      <p style="margin:0 0 12px;color:#4b5563;">
        ${result.totalImportado} modelo(s) importado(s), ${result.totalFalhas} falha(s).
      </p>
      <ul style="margin:0;padding:0;list-style:none;text-align:left;max-height:280px;overflow:auto;">
        ${items}
      </ul>
    `,
    width: '42rem',
    confirmButtonText: 'Entendi'
  })
}

function rememberScripts(items: ScriptListItem[]) {
  knownScripts.value = {
    ...knownScripts.value,
    ...Object.fromEntries(items.map(script => [script.codScriptLaudo, script]))
  }
}

async function fetchScripts(search = scriptSearch.value.trim()) {
  try {
    const res = await scriptsApi.listScripts({
      sistema: form.sistema,
      pacote: pacoteFiltro.value || undefined,
      nome: search || undefined,
      page: 1,
      pageSize: 100,
      ativo: 1
    })
    scripts.value = (res.data || []).map(item => normalizeScript(item as Record<string, unknown>))
    rememberScripts(scripts.value)
    if (importarPacoteCompleto.value && pacoteFiltro.value) selectVisibleScripts()
  } catch (reason) {
    validationError.value = reason instanceof Error ? reason.message : 'Não foi possível carregar os scripts.'
  }
}

async function fetchPackageScripts() {
  if (!pacoteFiltro.value) return []
  const all: ScriptListItem[] = []
  let page = 1
  let totalPages = 1
  do {
    const res = await scriptsApi.listScripts({
      sistema: form.sistema,
      pacote: pacoteFiltro.value,
      page,
      pageSize: 200,
      ativo: 1
    })
    all.push(...(res.data || []).map(item => normalizeScript(item as Record<string, unknown>)))
    totalPages = Math.max(1, Number(res.totalPages || 1))
    page += 1
  } while (page <= totalPages)
  rememberScripts(all)
  return all
}

function selectVisibleScripts() {
  selectedScriptIds.value = scripts.value.map(script => script.codScriptLaudo)
}

function loadScripts() {
  clearTimeout(scriptTimer)
  scriptTimer = setTimeout(() => fetchScripts(), 300)
}

function onSistemaChange() {
  selectedScriptIds.value = []
  importarPacoteCompleto.value = false
  pacoteFiltro.value = ''
  form.tituloScript = ''
  form.tituloMrd = ''
  scriptSearch.value = ''
  fetchScripts('')
}

function onPacoteChange() {
  selectedScriptIds.value = []
  importarPacoteCompleto.value = false
  fetchScripts()
}

async function onImportarPacoteCompletoChange() {
  selectedScriptIds.value = []
  form.tituloScript = ''
  form.tituloMrd = ''
  if (!importarPacoteCompleto.value || !pacoteFiltro.value) return
  const packageScripts = await fetchPackageScripts()
  scripts.value = packageScripts
  selectedScriptIds.value = packageScripts.map(script => script.codScriptLaudo)
}

function toggleScript(script: ScriptListItem) {
  const id = script.codScriptLaudo
  selectedScriptIds.value = selectedScriptIds.value.includes(id)
    ? selectedScriptIds.value.filter(item => item !== id)
    : [...selectedScriptIds.value, id]
  if (selectedScriptIds.value.length === 1 && !form.tituloScript) form.tituloScript = selectedScript.value?.nome || ''
  if (selectedScriptIds.value.length > 1) {
    form.tituloScript = ''
    form.tituloMrd = ''
  }
}

async function validate() {
  if (importarPacoteCompleto.value && !pacoteFiltro.value) return 'Selecione um pacote para importar todos os modelos.'
  if (!selectedScriptIds.value.length) return 'Selecione ao menos um script de origem.'
  if (!form.especialidades.length) return 'Selecione ao menos uma especialidade.'
  if (selectedScripts.value.length !== selectedScriptIds.value.length) return 'Um ou mais scripts selecionados são inválidos.'
  const missingJson = selectedScripts.value.filter(script => form.sistema === 'Laudos UX' && !script.temArquivoJson)
  if (missingJson.length) return `Script sem JSON: ${missingJson[0].nome}.`
  const missingDll = selectedScripts.value.filter(script => form.sistema === 'Laudos Flex' && !script.temArquivoDll)
  if (missingDll.length) return `Script sem DLL: ${missingDll[0].nome}.`
  const missingMrd = selectedScripts.value.filter(script => form.sistema !== 'Laudos UX' && !script.temArquivoMrd)
  if (missingMrd.length) return `Script sem MRD padrão: ${missingMrd[0].nome}.`
  return ''
}

async function submit() {
  validationError.value = await validate()
  if (validationError.value) return
  saving.value = true
  try {
    if (importarPacoteCompleto.value && pacoteFiltro.value) {
      const packageScripts = await fetchPackageScripts()
      scripts.value = packageScripts
      selectedScriptIds.value = packageScripts.map(script => script.codScriptLaudo)
      validationError.value = await validate()
      if (validationError.value) return
    }
    if (selectedScriptIds.value.length === 1) {
      const result = await api.importModel({
        codScriptLaudoOrigem: selectedScriptIds.value[0],
        sistema: form.sistema,
        tipoScript: inferredTipo.value as 1 | 2 | 3,
        tituloScript: form.tituloScript.trim() || undefined,
        tituloMrd: form.tituloMrd.trim() || undefined,
        especialidades: form.especialidades,
        procedimentos: form.procedimentos
      })
      await swal.toast(result.codPagFotos
        ? `Modelo importado. Script ${result.codScriptLaudo} e MRD ${result.codPagFotos} cadastrados.`
        : `Modelo importado. Script ${result.codScriptLaudo} cadastrado sem MRD.`
      )
    } else {
      const result = await api.importModels({
        codigosScriptLaudoOrigem: selectedScriptIds.value,
        sistema: form.sistema,
        especialidades: form.especialidades,
        procedimentos: form.procedimentos
      })
      if (result.totalFalhas) {
        await showBatchImportFailures(result)
        return
      }
      await swal.toast(`${result.totalImportado} modelo(s) importado(s).`)
    }
    await navigateTo('/assistente/scripts')
  } catch (reason) {
    validationError.value = reason instanceof Error ? reason.message : 'Não foi possível importar o modelo.'
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  try {
    const [especialidadesResult, pacotesResult] = await Promise.all([
      api.options('especialidades'),
      scriptsApi.getPacotes()
    ])
    especialidades.value = especialidadesResult
    pacotes.value = pacotesResult.data || []
    await fetchScripts()
  } catch (reason) {
    validationError.value = reason instanceof Error ? reason.message : 'Não foi possível carregar os vínculos.'
  }
})

onUnmounted(() => clearTimeout(scriptTimer))
</script>
