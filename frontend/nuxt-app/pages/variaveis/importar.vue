<template>
  <div>
    <DsPageHeader title="Importar variáveis" subtitle="Importe variáveis, fórmulas, normalidades e nomes alternativos de arquivos JSON ou C#" icon="cloud-arrow-up">
      <template #actions><DsButton variant="secondary" to="/variaveis">Voltar</DsButton></template>
    </DsPageHeader>
    <DsPageShell>
      <div class="mb-6 grid grid-cols-3 gap-2">
        <div v-for="item in steps" :key="item.id" class="rounded-xl border px-3 py-3 text-center text-sm" :class="step === item.id ? 'border-black bg-black text-white' : step > item.id ? 'border-green-300 bg-green-50 text-green-800' : 'border-gray-200 text-gray-500'">
          <strong>{{ item.id }}</strong><span class="ml-2">{{ item.label }}</span>
        </div>
      </div>

      <DsAlert v-if="error" variant="error" class="mb-4">{{ error }}</DsAlert>
      <section v-if="step === 1" class="mx-auto max-w-2xl rounded-2xl border border-gray-200 bg-white p-6">
        <h2 class="mb-2 text-lg font-semibold">Selecione o arquivo</h2>
        <p class="mb-5 text-sm text-gray-600">Formatos aceitos: JSON legado, JSON Studio, JSON de normalidades Medware ou código-fonte C#. Limite de 10 MB.</p>
        <DsFileInput label="Arquivo *" accept=".json,.cs" required @change="selectFile" />
        <div class="mt-5 flex justify-end"><DsButton :loading="loading" :disabled="!file" @click="preview">Analisar arquivo</DsButton></div>
      </section>

      <template v-else-if="previewData">
        <div class="mb-4 grid gap-3 sm:grid-cols-4">
          <div class="rounded-xl border bg-white p-3"><small class="text-gray-500">Formato</small><strong class="block uppercase">{{ formatLabel }}</strong></div>
          <div class="rounded-xl border bg-white p-3"><small class="text-gray-500">Variáveis</small><strong class="block">{{ previewData.variaveis.length }}</strong></div>
          <div class="rounded-xl border bg-white p-3"><small class="text-gray-500">Fórmulas</small><strong class="block">{{ previewData.formulas.length }}</strong></div>
          <div class="rounded-xl border bg-white p-3"><small class="text-gray-500">Normalidades</small><strong class="block">{{ previewData.normalidades.length }}</strong></div>
        </div>
        <DsAlert v-for="warning in previewData.avisos" :key="warning" variant="warning" class="mb-2">{{ warning }}</DsAlert>

        <section v-if="step === 2" class="rounded-2xl border border-gray-200 bg-white p-5">
          <div v-if="needsReferenceSelection" class="mb-5 rounded-xl border border-amber-200 bg-amber-50 p-4">
            <DsSelect v-model="codReferencia" :label="referenceLabel" @update:model-value="reanalyzeWithReference">
              <option value="">Selecione a referência bibliográfica</option>
              <option v-for="reference in references" :key="reference.codReferencia" :value="reference.codReferencia">{{ reference.titulo }}{{ reference.ano ? ` (${reference.ano})` : '' }}</option>
            </DsSelect>
            <p class="mt-2 text-xs text-amber-800">A referência escolhida será usada somente nas faixas cuja fonte não foi localizada automaticamente no banco.</p>
          </div>

          <div class="mb-4 flex flex-wrap items-end justify-between gap-3">
            <DsSearchInput v-model="search" class="min-w-64 flex-1" placeholder="Pesquisar por código, nome, unidade ou sugestão" />
            <div class="flex gap-2"><DsButton size="sm" variant="secondary" @click="applyDefaults">Aplicar padrão</DsButton><DsButton size="sm" variant="secondary" @click="ignoreAll">Ignorar todas</DsButton></div>
          </div>
          <DsTabs v-model="tab" :tabs="tabs" />

          <div v-if="tab === 'variaveis'" class="overflow-x-auto">
            <table class="w-full min-w-[1100px] text-sm">
              <thead><tr class="border-b text-left"><th class="p-2">Ação</th><th class="p-2">Código</th><th class="p-2">Nome / unidade</th><th class="p-2">Dependências</th><th class="p-2">Variável principal</th><th class="p-2">Situação</th></tr></thead>
              <tbody>
                <tr v-for="item in filteredVariables" :key="item.codigo" class="border-b align-top">
                  <td class="p-2">
                    <select v-model="decisions[item.codigo].acao" class="w-44 rounded-lg border border-gray-300 bg-white px-2 py-2" @change="changeAction(item)">
                      <option value="criar" :disabled="item.existeNoBanco || !item.valido">Criar variável</option>
                      <option value="atualizar" :disabled="previewData.formato !== 'json-normalidades-medware' || !item.existeNoBanco || !item.valido">Atualizar normalidades</option>
                      <option value="alternativa" :disabled="!item.sugestoes?.length">Cadastrar alternativa</option>
                      <option value="ignorar">Ignorar</option>
                    </select>
                  </td>
                  <td class="p-2 font-mono">{{ item.codigo }}<small v-if="item.alternativasArquivo?.length" class="mt-1 block font-sans text-gray-500">Aliases do arquivo: {{ item.alternativasArquivo.join(', ') }}</small></td>
                  <td class="p-2">{{ item.nome }}<small class="block text-gray-500">{{ item.unidade }}</small></td>
                  <td class="p-2">{{ item.totalFormulas }} fórmula(s)<br>{{ item.totalNormalidades }} normalidade(s)</td>
                  <td class="p-2">
                    <select v-if="decisions[item.codigo].acao === 'alternativa'" v-model.number="decisions[item.codigo].codVariavelPrincipal" class="w-full min-w-64 rounded-lg border border-gray-300 bg-white px-2 py-2">
                      <option :value="null">Selecione a variável principal</option>
                      <option v-for="suggestion in item.sugestoes || []" :key="suggestion.codVariavel" :value="suggestion.codVariavel">{{ suggestion.codigo }} — {{ suggestion.nome || suggestion.sigla }} ({{ suggestion.pontuacao }}%)</option>
                    </select>
                    <template v-else-if="item.sugestoes?.length">
                      <small class="block text-gray-600">Mais próxima: <strong>{{ item.sugestoes[0].codigo }}</strong> · {{ item.sugestoes[0].pontuacao }}%</small>
                      <small class="text-gray-500">{{ item.sugestoes[0].motivo }}</small>
                    </template>
                    <span v-else class="text-gray-400">Nenhuma semelhante</span>
                  </td>
                  <td class="p-2">
                    <DsBadge :variant="item.existeNoBanco ? 'neutral' : item.valido ? 'success' : 'danger'">{{ item.existeNoBanco ? 'Já existe' : item.valido ? 'Pronta' : 'Revisar' }}</DsBadge>
                    <small v-if="item.erros.length" class="mt-1 block max-w-80 text-red-600">{{ item.erros.join(' ') }}</small>
                    <small v-if="decisionError(item)" class="mt-1 block text-red-600">{{ decisionError(item) }}</small>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div v-else-if="tab === 'formulas'" class="space-y-2">
            <div v-for="item in filteredFormulas" :key="`${item.variavel}-${item.expressao}`" class="rounded-xl border p-3"><strong>{{ item.variavel }}</strong><code class="mt-1 block break-all text-xs">{{ item.expressao }}</code><small v-if="item.erros.length" class="text-red-600">{{ item.erros.join(' ') }}</small></div>
            <DsEmptyState v-if="!filteredFormulas.length" title="Nenhuma fórmula encontrada" />
          </div>

          <div v-else class="space-y-2">
            <div v-for="(item,index) in filteredNormals" :key="`${item.variavel}-${index}`" class="rounded-xl border p-3">
              <div class="flex justify-between gap-3"><strong>{{ item.variavel }}</strong><DsBadge :variant="item.valido ? 'success' : 'danger'">{{ item.valido ? 'Referência validada' : 'Bloqueada' }}</DsBadge></div>
              <p class="text-sm">{{ item.sexo }} · {{ item.classificacao || 'sem classificação' }} · {{ item.valorMin }} a {{ item.valorMax }}<template v-if="item.idadeMin != null || item.idadeMax != null"> · idades {{ item.idadeMin }}–{{ item.idadeMax }}</template></p>
              <small>Referência: {{ item.codReferencia || item.referencia || 'não informada' }}{{ item.anoReferencia ? ` (${item.anoReferencia})` : '' }}{{ item.pagina ? ` · pág. ${item.pagina}` : '' }}</small>
              <small v-if="item.erros.length" class="block text-red-600">{{ item.erros.join(' ') }}</small>
            </div>
            <DsEmptyState v-if="!filteredNormals.length" title="Nenhuma normalidade encontrada" />
          </div>

          <div class="mt-5 flex items-center justify-between gap-4"><span class="text-sm text-gray-600">{{ actionableDecisions.length }} ação(ões): {{ createCount }} nova(s), {{ updateCount }} atualização(ões), {{ alternativeCount }} alternativa(s)</span><div class="flex gap-2"><DsButton variant="secondary" @click="reset">Trocar arquivo</DsButton><DsButton :disabled="!canContinue" @click="step = 3">Continuar</DsButton></div></div>
        </section>

        <section v-else class="mx-auto max-w-2xl rounded-2xl border border-gray-200 bg-white p-6">
          <h2 class="text-lg font-semibold">Confirmar importação</h2><p class="mt-2 text-gray-600">O arquivo será analisado novamente e toda a operação será executada em uma única transação.</p>
          <dl class="my-5 grid grid-cols-2 gap-3 text-sm"><div><dt class="text-gray-500">Arquivo</dt><dd class="font-semibold">{{ previewData.arquivo }}</dd></div><div><dt class="text-gray-500">Novas variáveis</dt><dd class="font-semibold">{{ createCount }}</dd></div><div><dt class="text-gray-500">Variáveis atualizadas</dt><dd class="font-semibold">{{ updateCount }}</dd></div><div><dt class="text-gray-500">Novos vínculos alternativos</dt><dd class="font-semibold">{{ alternativeCount }}</dd></div><div><dt class="text-gray-500">Fórmulas relacionadas</dt><dd class="font-semibold">{{ selectedFormulas }}</dd></div><div><dt class="text-gray-500">Normalidades relacionadas</dt><dd class="font-semibold">{{ selectedNormals }}</dd></div></dl>
          <DsAlert variant="warning" class="mb-5">No JSON de normalidades Medware, atualizar ou cadastrar como alternativa substitui as faixas da mesma variável e referência. Se qualquer ação falhar, nenhuma alteração será gravada.</DsAlert>
          <div class="flex justify-end gap-2"><DsButton variant="secondary" @click="step = 2">Voltar à revisão</DsButton><DsButton variant="success" :loading="loading" @click="confirm">Confirmar importação</DsButton></div>
        </section>
      </template>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { ImportacaoVariavelDecisao, ImportacaoVariavelItem, ImportacaoVariaveisPreview } from '~/composables/useVariaveisApi'
import type { ReferenciaItem } from '~/composables/useReferenciasApi'

definePageMeta({ layout: 'default', middleware: ['admin'] })
const api = useVariaveisApi()
const referenciasApi = useReferenciasApi()
const swal = useSwal()
const router = useRouter()
const steps = [{ id: 1, label: 'Arquivo' }, { id: 2, label: 'Revisão' }, { id: 3, label: 'Confirmação' }]
const tabs = [{ id: 'variaveis', label: 'Variáveis', icon: 'list-check' }, { id: 'formulas', label: 'Fórmulas', icon: 'calculator' }, { id: 'normalidades', label: 'Normalidades', icon: 'activity' }]
const step = ref(1)
const tab = ref('variaveis')
const file = ref<File | null>(null)
const previewData = ref<ImportacaoVariaveisPreview | null>(null)
const decisions = reactive<Record<string, ImportacaoVariavelDecisao>>({})
const search = ref('')
const loading = ref(false)
const error = ref('')
const codReferencia = ref<number | string>('')
const references = ref<ReferenciaItem[]>([])

const formatLabel = computed(() => ({ json: 'JSON legado', 'json-studio': 'JSON Studio', 'json-normalidades-medware': 'Normalidades Medware', cs: 'C#' }[previewData.value?.formato || 'json']))
const unresolvedNormals = computed(() => previewData.value?.normalidades.filter(item => !item.referenciaValida).length || 0)
const needsReferenceSelection = computed(() => previewData.value?.formato === 'json-studio' && !!previewData.value.normalidades.length || previewData.value?.formato === 'json-normalidades-medware' && unresolvedNormals.value > 0)
const referenceLabel = computed(() => previewData.value?.formato === 'json-studio' ? 'Referência das normalidades do JSON Studio *' : 'Referência para fontes não localizadas')
const filteredVariables = computed(() => { const q = search.value.trim().toLowerCase(); return previewData.value?.variaveis.filter(item => !q || `${item.codigo} ${item.nome} ${item.unidade} ${(item.sugestoes || []).map(s => `${s.codigo} ${s.nome || ''}`).join(' ')}`.toLowerCase().includes(q)) || [] })
const filteredFormulas = computed(() => { const q = search.value.trim().toLowerCase(); return previewData.value?.formulas.filter(item => !q || `${item.variavel} ${item.expressao}`.toLowerCase().includes(q)) || [] })
const filteredNormals = computed(() => { const q = search.value.trim().toLowerCase(); return previewData.value?.normalidades.filter(item => !q || `${item.variavel} ${item.referencia || ''} ${item.codReferencia || ''} ${item.classificacao || ''}`.toLowerCase().includes(q)) || [] })
const allDecisions = computed(() => Object.values(decisions))
const actionableDecisions = computed(() => allDecisions.value.filter(item => item.acao !== 'ignorar'))
const createCount = computed(() => allDecisions.value.filter(item => item.acao === 'criar').length)
const updateCount = computed(() => allDecisions.value.filter(item => item.acao === 'atualizar').length)
const alternativeCount = computed(() => allDecisions.value.filter(item => item.acao === 'alternativa').length)
const actionableItems = computed(() => previewData.value?.variaveis.filter(item => decisions[item.codigo]?.acao !== 'ignorar') || [])
const selectedFormulas = computed(() => previewData.value?.formulas.filter(formula => actionableItems.value.some(item => related(formula.variavel, item.codigo, item.sigla))).length || 0)
const selectedNormals = computed(() => previewData.value?.normalidades.filter(normal => actionableItems.value.some(item => related(normal.variavel, item.codigo, item.sigla))).length || 0)
const canContinue = computed(() => actionableDecisions.value.length > 0 && !(previewData.value?.variaveis.some(item => decisionError(item))))

function selectFile(files: FileList | null) { file.value = files?.[0] || null; previewData.value = null; error.value = '' }
function reset() { step.value = 1; previewData.value = null; clearDecisions(); search.value = ''; error.value = ''; codReferencia.value = '' }
function clearDecisions() { for (const key of Object.keys(decisions)) delete decisions[key] }
function related(value: string, code: string, sigla: string) { return value.toLowerCase() === code.toLowerCase() || value.toLowerCase() === sigla.toLowerCase() || value.toLowerCase() === `vr_${sigla}`.toLowerCase() }
function defaultDecision(item: ImportacaoVariavelItem): ImportacaoVariavelDecisao {
  if (previewData.value?.formato === 'json-normalidades-medware' && item.existeNoBanco && item.valido) return { codigo: item.codigo, acao: 'atualizar', codVariavelPrincipal: item.codVariavelExistente }
  if (!item.existeNoBanco && item.valido) return { codigo: item.codigo, acao: 'criar' }
  return { codigo: item.codigo, acao: 'ignorar' }
}
function applyDefaults() { if (!previewData.value) return; clearDecisions(); for (const item of previewData.value.variaveis) decisions[item.codigo] = defaultDecision(item) }
function ignoreAll() { if (!previewData.value) return; for (const item of previewData.value.variaveis) decisions[item.codigo] = { codigo: item.codigo, acao: 'ignorar' } }
function changeAction(item: ImportacaoVariavelItem) { const decision = decisions[item.codigo]; decision.codVariavelPrincipal = decision.acao === 'alternativa' ? item.sugestoes?.[0]?.codVariavel || null : decision.acao === 'atualizar' ? item.codVariavelExistente : null }
function decisionError(item: ImportacaoVariavelItem) {
  const decision = decisions[item.codigo]
  if (!decision || decision.acao === 'ignorar') return ''
  if ((decision.acao === 'criar' || decision.acao === 'atualizar') && !item.valido) return 'Esta ação exige a correção dos problemas do item.'
  if (decision.acao === 'alternativa' && !decision.codVariavelPrincipal) return 'Selecione a variável principal.'
  if (decision.acao === 'alternativa' && previewData.value?.formato === 'json-normalidades-medware' && !item.valido) return 'As normalidades precisam estar válidas para vincular este item.'
  return ''
}
async function preview() {
  if (!file.value) return
  loading.value = true; error.value = ''
  try { previewData.value = (await api.previewImportacao(file.value, Number(codReferencia.value) || undefined)).data; applyDefaults(); step.value = 2 }
  catch (caught) { error.value = caught instanceof Error ? caught.message : 'Não foi possível analisar o arquivo.' }
  finally { loading.value = false }
}
async function reanalyzeWithReference() { if (file.value && codReferencia.value) await preview() }
async function confirm() {
  if (!file.value || !canContinue.value) return
  loading.value = true; error.value = ''
  try {
    const result = (await api.confirmarImportacao(file.value, allDecisions.value, Number(codReferencia.value) || undefined)).data
    await swal.toast(`${result.inseridas} variável(is) criada(s), ${result.variaveisAtualizadas} atualizada(s), ${result.alternativasInseridas} alternativa(s) e ${result.normalidadesInseridas} normalidade(s) importadas.`)
    await router.push('/variaveis')
  } catch (caught) { error.value = caught instanceof Error ? caught.message : 'Não foi possível importar o arquivo.'; step.value = 2 }
  finally { loading.value = false }
}
onMounted(async () => { try { references.value = (await referenciasApi.listReferencias({ page: 1, pageSize: 200 })).data } catch { /* A referência também será validada no servidor. */ } })
</script>
