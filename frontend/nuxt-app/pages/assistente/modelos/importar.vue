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
        Selecione um modelo já cadastrado em Scripts/Pacotes. Laudos Flex importa a DLL em Base64; Laudos UX importa JSON (tipo 3). O MRD padrão do script será copiado automaticamente.
      </DsAlert>
      <form class="space-y-6" @submit.prevent="submit">
        <DsCard>
          <DsSectionTitle title="1. Script de origem (/scripts/pacotes)" />
          <div class="mt-4 grid gap-4 md:grid-cols-2">
            <DsSelect v-model="form.sistema" label="Sistema" required @update:model-value="onSistemaChange">
              <option value="Laudos Flex">Laudos Flex</option>
              <option value="Laudos UX">Laudos UX</option>
            </DsSelect>
            <DsInput
              v-model="scriptSearch"
              label="Filtrar por nome"
              hint="Atualiza a lista do campo Script abaixo conforme você digita."
              placeholder="Ex.: ecocardiograma, consulta..."
              @update:model-value="loadScripts"
            />
            <DsSelect v-model="codScriptSelecionado" label="Script" required class="md:col-span-2" hint="Modelos cadastrados em /scripts/pacotes do sistema escolhido." @update:model-value="onScriptSelected">
              <option value="0">Selecione um script</option>
              <option v-for="script in scripts" :key="script.codScriptLaudo" :value="String(script.codScriptLaudo)">
                {{ script.nome }} — {{ script.sistema }}{{ script.nomePacote ? ` (${script.nomePacote})` : '' }}
              </option>
            </DsSelect>
            <DsInput v-model="form.tituloScript" label="Título no Assistente (opcional)" placeholder="Usa o nome do script se vazio" />
            <DsInput v-model="form.tituloMrd" label="Título do MRD no Assistente (opcional)" placeholder="Usa o MRD padrão se vazio" />
            <div v-if="selectedScript" class="md:col-span-2 text-sm text-gray-600">
              Tipo detectado: <strong>{{ tipoLabel(inferredTipo) }}</strong>
              · DLL: {{ selectedScript.temArquivoDll ? 'sim' : 'não' }}
              · JSON: {{ selectedScript.temArquivoJson ? 'sim' : 'não' }}
              · MRD: {{ selectedScript.temArquivoMrd ? 'sim' : 'não' }}
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
          <DsButton type="submit" variant="success" :loading="saving" :disabled="!form.codScriptLaudoOrigem">Importar modelo</DsButton>
        </div>
      </form>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { ScriptListItem } from '~/composables/useScriptsApi'

definePageMeta({ layout: 'default', middleware: 'admin' })

const api = useAssistenteApi()
const scriptsApi = useScriptsApi()
const swal = useSwal()

const saving = ref(false)
const validationError = ref('')
const especialidades = ref<{ id: number; nome: string }[]>([])
const scripts = ref<ScriptListItem[]>([])
const scriptSearch = ref('')

const form = reactive({
  codScriptLaudoOrigem: 0,
  sistema: 'Laudos Flex' as 'Laudos Flex' | 'Laudos UX',
  tituloScript: '',
  tituloMrd: '',
  especialidades: [] as number[],
  procedimentos: [] as number[]
})

const codScriptSelecionado = computed({
  get: () => String(form.codScriptLaudoOrigem || 0),
  set: (value: string | number) => {
    form.codScriptLaudoOrigem = Number(value) || 0
  }
})

function normalizeScript(raw: Record<string, unknown>): ScriptListItem {
  return {
    codScriptLaudo: Number(raw.codScriptLaudo ?? raw.CodScriptLaudo ?? 0),
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
  const id = form.codScriptLaudoOrigem
  if (!id) return null
  return scripts.value.find(s => s.codScriptLaudo === id) ?? null
})

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

async function fetchScripts(search = scriptSearch.value.trim()) {
  try {
    const res = await scriptsApi.listScripts({
      sistema: form.sistema,
      nome: search || undefined,
      page: 1,
      pageSize: 100,
      ativo: 1
    })
    scripts.value = (res.data || []).map(item => normalizeScript(item as Record<string, unknown>))
  } catch (reason) {
    validationError.value = reason instanceof Error ? reason.message : 'Não foi possível carregar os scripts.'
  }
}

function loadScripts() {
  clearTimeout(scriptTimer)
  scriptTimer = setTimeout(() => fetchScripts(), 300)
}

function onSistemaChange() {
  form.codScriptLaudoOrigem = 0
  form.tituloScript = ''
  form.tituloMrd = ''
  scriptSearch.value = ''
  fetchScripts('')
}

function onScriptSelected() {
  const script = selectedScript.value
  if (!script) return
  if (!form.tituloScript) form.tituloScript = script.nome
}

async function validate() {
  if (!form.codScriptLaudoOrigem) return 'Selecione um script de origem.'
  if (!form.especialidades.length) return 'Selecione ao menos uma especialidade.'
  const script = selectedScript.value
  if (!script) return 'Script de origem inválido.'
  if (form.sistema === 'Laudos UX' && !script.temArquivoJson) return 'O script selecionado não possui JSON.'
  if (form.sistema === 'Laudos Flex' && !script.temArquivoDll) return 'O script selecionado não possui DLL.'
  if (!script.temArquivoMrd) return 'O script selecionado não possui MRD padrão.'
  return ''
}

async function submit() {
  validationError.value = await validate()
  if (validationError.value) return
  saving.value = true
  try {
    const result = await api.importModel({
      codScriptLaudoOrigem: form.codScriptLaudoOrigem,
      tituloScript: form.tituloScript.trim() || undefined,
      tituloMrd: form.tituloMrd.trim() || undefined,
      especialidades: form.especialidades,
      procedimentos: form.procedimentos
    })
    await swal.toast(`Modelo importado. Script ${result.codScriptLaudo} e MRD ${result.codPagFotos} cadastrados.`)
    await navigateTo('/assistente/scripts')
  } catch (reason) {
    validationError.value = reason instanceof Error ? reason.message : 'Não foi possível importar o modelo.'
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  try {
    especialidades.value = await api.options('especialidades')
    await fetchScripts()
  } catch (reason) {
    validationError.value = reason instanceof Error ? reason.message : 'Não foi possível carregar os vínculos.'
  }
})

onUnmounted(() => clearTimeout(scriptTimer))
</script>
