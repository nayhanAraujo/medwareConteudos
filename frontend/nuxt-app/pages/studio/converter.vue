<script setup lang="ts">
import type { AnalyzedMeasure, ConversionFormat, ConversionRecord, ReviewedMeasure, VariableNormalidade } from '~/types/conversion'
import { looksLikeHtml, resolveConversionContent } from '~/utils/conversionFormat'

definePageMeta({ layout: 'studio' })
const store = useConversionStore()
const { convertImage, validateContent, checkHealth, analyzeImage, generateModoTextoFromAnalysis } = useConversionApi()
const { downloadHtml, downloadText, copyToClipboard } = useHtmlPreview()
const { toast, alert, chooseConversionFormat } = useStudioSwal()

const selectedFile = ref<File | null>(null)
const imagePreview = ref<string | null>(null)
const outputFormat = ref<ConversionFormat>('html')
const generatedHtml = ref('')
const generatedText = ref('')
const validation = ref<ConversionRecord['validation'] | null>(null)
const loading = ref(false)
const elapsedMs = ref(0)
const executeScripts = ref(false)
const analysis = ref<AnalyzedMeasure[]>([])
const analysisSourceFileName = ref('')
const analyzing = ref(false)
const generatingReviewedText = ref(false)
type VariableOption = {
  codvariavel: number
  nome?: string
  sigla?: string
  variavel?: string
  unidade_medida?: string
  nomes_clinicos?: string[]
}
const variableOptions = ref<VariableOption[]>([])
const reviewState = ref<Record<string, {
  codVariavel: number | null
  decision: 'keep' | 'ignore' | 'pending'
  codReferencia: number | null
  normalityMode: 'simple' | 'comment' | 'classificacao' | 'texto'
}>>({})
const variableDetails = ref<Record<number, VariableNormalidade[]>>({})

const studioCodCliente = ref('')
const studioCodPadrao = ref<number | null>(null)
const studioClientes = ref<{ codCliente: number; nome: string }[]>([])
const studioPadroes = ref<Array<{ codPadrao: number; nome: string; codigo: string; padraoVigente: number }>>([])
const padraoPainelCache = ref<import('~/composables/useVariaveisApi').PadroesClientePainel | null>(null)
const usingPadraoCliente = computed(() => studioCodPadrao.value != null && studioCodPadrao.value > 0)

const hasOutput = computed(() =>
  outputFormat.value === 'modoTexto' ? generatedText.value.length > 0 : generatedHtml.value.length > 0
)

const EXPECTED_SECONDS = 70
const progressPercent = computed(() => {
  if (!loading.value) return 0
  const seconds = elapsedMs.value / 1000
  return Math.min(92, 6 + (1 - Math.exp(-seconds / EXPECTED_SECONDS)) * 86)
})

let progressTimer: ReturnType<typeof setInterval> | null = null
let progressStartedAt = 0

const startProgress = () => {
  progressStartedAt = Date.now()
  elapsedMs.value = 0
  progressTimer = setInterval(() => {
    elapsedMs.value = Date.now() - progressStartedAt
  }, 200)
}

const stopProgress = () => {
  if (progressTimer) {
    clearInterval(progressTimer)
    progressTimer = null
  }
}

onUnmounted(stopProgress)

type ApiStatus = 'checking' | 'online' | 'offline'
const apiStatus = ref<ApiStatus>('checking')
const apiProvider = ref('')
const apiSupportsModoTexto = ref(true)

const isApiOnline = computed(() => apiStatus.value === 'online')

const verifyApi = async (notify = false) => {
  apiStatus.value = 'checking'
  try {
    const health = await checkHealth()
    const online = health.status === 'healthy'
    apiStatus.value = online ? 'online' : 'offline'
    apiProvider.value = health.provider ?? ''
    apiSupportsModoTexto.value = Array.isArray(health.formats) && health.formats.includes('modoTexto')
    if (notify) {
      toast(
        online ? 'API de conversão online.' : 'A API de conversão não está saudável.',
        online ? 'success' : 'error'
      )
    }
  } catch {
    apiStatus.value = 'offline'
    apiProvider.value = ''
    apiSupportsModoTexto.value = false
    if (notify) toast('A API de conversão não está rodando.', 'error')
  }
}

onMounted(() => {
  void loadStudioClientes()
  void verifyApi()

  const pendingData = sessionStorage.getItem('pendingImageData')
  const pendingName = sessionStorage.getItem('pendingImageName')
  if (pendingData && pendingName) {
    imagePreview.value = pendingData
    fetch(pendingData)
      .then(r => r.blob())
      .then(blob => {
        selectedFile.value = new File([blob], pendingName, { type: blob.type })
      })
    sessionStorage.removeItem('pendingImageData')
    sessionStorage.removeItem('pendingImageName')
  }
})

const resetOutput = () => {
  generatedHtml.value = ''
  generatedText.value = ''
  validation.value = null
  analysis.value = []
  analysisSourceFileName.value = ''
  reviewState.value = {}
}

const onFileSelect = (file: File) => {
  selectedFile.value = file
  imagePreview.value = URL.createObjectURL(file)
  resetOutput()
}

const formatLabel = computed(() =>
  outputFormat.value === 'modoTexto' ? 'TXT Modo Texto' : 'HTML LaudosUX'
)

const handleConvert = async () => {
  if (!isApiOnline.value) {
    toast('A API de conversão não está rodando.', 'error')
    return
  }
  if (!selectedFile.value) {
    toast('Selecione uma imagem primeiro', 'warning')
    return
  }

  const format = await chooseConversionFormat()
  if (!format) return

  outputFormat.value = format
  resetOutput()

  if (format === 'modoTexto') {
    await handleAnalyzeModoTexto()
    return
  }

  loading.value = true
  startProgress()
  try {
    const result = await convertImage(selectedFile.value, format)
    const resolved = resolveConversionContent(format, result)

    outputFormat.value = resolved.format
    generatedHtml.value = resolved.html
    generatedText.value = resolved.text

    const contentToValidate = resolved.format === 'modoTexto' ? resolved.text : resolved.html
    if (contentToValidate) {
      validation.value = await validateContent(resolved.format, contentToValidate)
    } else {
      validation.value = null
    }

    if (format === 'modoTexto' && !resolved.text) {
      await alert(
        'TXT não gerado',
        resolved.apiMismatch && (result.html ?? '').includes('containerHtml')
          ? 'A API em execução está desatualizada e ignorou o formato TXT (retornou HTML). Pare o backend e reinicie com: dotnet run em backend/MdwConteudos.Api'
          : 'A conversão terminou, mas o agente não retornou conteúdo TXT. Tente novamente ou use Provider Mock para testar.',
        'error'
      )
      return
    }

    if (resolved.apiMismatch && format === 'modoTexto' && looksLikeHtml(resolved.text)) {
      await alert(
        'Conteúdo possivelmente incorreto',
        'O agente pode ter gerado HTML em vez de TXT. Tente converter novamente ou use o provider Mock para testar.',
        'warning'
      )
    }

    const record: ConversionRecord = {
      id: crypto.randomUUID(),
      format: resolved.format,
      html: resolved.html,
      text: resolved.text,
      sourceFileName: result.sourceFileName,
      convertedAt: result.convertedAt,
      provider: result.provider,
      imagePreviewUrl: imagePreview.value ?? undefined,
      validation: validation.value ?? { isValid: false, errors: [], warnings: [] }
    }
    store.setCurrent(record)

    if (validation.value?.isValid) {
      toast('Conversão concluída com sucesso', 'success')
    } else {
      await alert('Conversão concluída com avisos', 'Revise os erros de validação.', 'warning')
    }
  } catch (e) {
    toast(conversionErrorMessage(e, 'Erro ao converter imagem. Verifique se a API está rodando.'), 'error')
  } finally {
    stopProgress()
    loading.value = false
  }
}

const hasPendingReview = computed(() =>
  analysis.value.some(m => (reviewState.value[m.id]?.decision ?? 'pending') === 'pending')
)

const loadVariableOptions = async () => {
  if (variableOptions.value.length) return
  const config = useRuntimeConfig()
  const apiBase = config.public.apiBase as string
  const res = await $fetch<{ success: boolean; data: typeof variableOptions.value }>(`${apiBase}/api/v1/variaveis`)
  variableOptions.value = res.data || []
}

const handleAnalyzeModoTexto = async () => {
  if (!selectedFile.value) return
  analyzing.value = true
  loading.value = true
  startProgress()
  try {
    await loadVariableOptions()
    const result = await analyzeImage(selectedFile.value)
    analysisSourceFileName.value = result.sourceFileName
    analysis.value = result.measures || []
    reviewState.value = Object.fromEntries(analysis.value.map(m => [
      m.id,
      {
        codVariavel: m.selectedCandidate?.codVariavel ?? null,
        decision: m.selectedCandidate ? 'keep' : 'pending',
        codReferencia: null,
        normalityMode: 'simple' as const
      }
    ]))
    await Promise.all(
      analysis.value
        .map(m => m.selectedCandidate?.codVariavel)
        .filter((id): id is number => !!id)
        .map(id => loadVariableDetails(id))
    )
    for (const measure of analysis.value) {
      const state = reviewState.value[measure.id]
      if (state?.codVariavel) applyReferenceDefaults(measure.id, state.codVariavel)
    }

    if (!analysis.value.length) {
      await alert('Nenhuma medida encontrada', 'O agente não retornou medidas estruturadas para revisar.', 'warning')
      return
    }

    toast('Análise concluída. Revise as correlações antes de gerar o TXT.', 'success')
  } catch (e) {
    toast(conversionErrorMessage(e, 'Erro ao analisar imagem.'), 'error')
  } finally {
    stopProgress()
    loading.value = false
    analyzing.value = false
  }
}

const loadVariableDetails = async (codVariavel: number) => {
  if (usingPadraoCliente.value && padraoPainelCache.value) {
    variableDetails.value[codVariavel] = mapPadraoVariableDetails(codVariavel)
    return
  }
  if (variableDetails.value[codVariavel]) return
  const config = useRuntimeConfig()
  const apiBase = config.public.apiBase as string
  const res = await $fetch<{ success: boolean; data?: { normalidades?: VariableNormalidade[] } }>(
    `${apiBase}/api/v1/variaveis/${codVariavel}`
  )
  variableDetails.value[codVariavel] = res.data?.normalidades ?? []
}

const mapPadraoVariableDetails = (codVariavel: number): VariableNormalidade[] => {
  const painel = padraoPainelCache.value
  if (!painel) return []
  const faixas = painel.faixasPorVariavel?.[String(codVariavel)] ?? []
  const comentario = painel.variaveis?.find(v => v.codVariavel === codVariavel)?.comentarioTexto ?? null
  return faixas.map(f => ({
    sexo: f.sexo,
    valor_min: f.valorMin,
    valor_max: f.valorMax,
    classificacao: f.classificacao,
    comentario_texto: comentario,
    referencia: null
  }))
}

const loadStudioClientes = async () => {
  if (studioClientes.value.length) return
  try {
    const res = await usePaineisApi().listClientes()
    studioClientes.value = res.data || []
  } catch {
    studioClientes.value = []
  }
}

const onStudioClienteChange = async () => {
  studioCodPadrao.value = null
  studioPadroes.value = []
  padraoPainelCache.value = null
  variableDetails.value = {}
  if (!studioCodCliente.value) return
  try {
    const res = await useVariaveisApi().getPadroesCliente({ codCliente: Number(studioCodCliente.value) })
    studioPadroes.value = (res.data?.padroes || []).filter(p => p.ativo === 1)
    const vigente = studioPadroes.value.find(p => p.padraoVigente === 1)
    if (vigente) await onStudioPadraoChange(vigente.codPadrao)
  } catch {
    studioPadroes.value = []
  }
}

const onStudioPadraoChange = async (codPadrao: number | null) => {
  studioCodPadrao.value = codPadrao
  padraoPainelCache.value = null
  variableDetails.value = {}
  if (!codPadrao || !studioCodCliente.value) return
  try {
    const res = await useVariaveisApi().getPadroesCliente({
      codCliente: Number(studioCodCliente.value),
      codPadrao
    })
    padraoPainelCache.value = res.data
    const ids = analysis.value
      .map(m => reviewState.value[m.id]?.codVariavel)
      .filter((id): id is number => !!id)
    await Promise.all(ids.map(id => loadVariableDetails(id)))
    for (const measure of analysis.value) {
      const state = reviewState.value[measure.id]
      if (state?.codVariavel) applyReferenceDefaults(measure.id, state.codVariavel)
    }
  } catch {
    padraoPainelCache.value = null
  }
}

const referencesFor = (codVariavel: number | null) => {
  if (!codVariavel) return []
  const rows = variableDetails.value[codVariavel] ?? []
  const map = new Map<number, { codigo: number; titulo: string; ano: string; count: number }>()
  for (const row of rows) {
    const codigo = row.referencia?.codigo
    if (!codigo) continue
    const current = map.get(codigo) ?? {
      codigo,
      titulo: row.referencia?.titulo ?? `Referência ${codigo}`,
      ano: row.referencia?.ano != null ? String(row.referencia.ano) : '',
      count: 0
    }
    current.count += 1
    map.set(codigo, current)
  }
  return [...map.values()]
}

const isMultiRange = (codVariavel: number | null, codReferencia: number | null) => {
  if (!codVariavel) return false
  const rows = (variableDetails.value[codVariavel] ?? []).filter(r =>
    usingPadraoCliente.value || !codReferencia || r.referencia?.codigo === codReferencia
  )
  const bySex = new Map<string, number>()
  for (const row of rows) {
    const sexo = (row.sexo ?? '-').toUpperCase()
    bySex.set(sexo, (bySex.get(sexo) ?? 0) + 1)
  }
  return rows.length > 2 || [...bySex.values()].some(n => n > 1)
}

const applyReferenceDefaults = (measureId: string, codVariavel: number) => {
  const refs = referencesFor(codVariavel)
  const current = reviewState.value[measureId]
  if (!current) return
  const codReferencia = current.codReferencia && refs.some(r => r.codigo === current.codReferencia)
    ? current.codReferencia
    : (refs[0]?.codigo ?? null)
  const multi = isMultiRange(codVariavel, codReferencia)
  reviewState.value[measureId] = {
    ...current,
    codReferencia,
    normalityMode: multi ? 'classificacao' : 'simple'
  }
}

const selectCandidate = async (measureId: string, codVariavel: number | null) => {
  const current = reviewState.value[measureId] ?? {
    codVariavel: null,
    decision: 'pending' as const,
    codReferencia: null,
    normalityMode: 'simple' as const
  }
  reviewState.value[measureId] = {
    ...current,
    codVariavel,
    decision: codVariavel ? 'keep' : 'pending',
    codReferencia: null,
    normalityMode: 'simple'
  }
  if (codVariavel) {
    await loadVariableDetails(codVariavel)
    applyReferenceDefaults(measureId, codVariavel)
  }
}

const onVariableComboboxChange = (measureId: string, codVariavel: number | null) => {
  void selectCandidate(measureId, codVariavel)
}

const onReferenceChange = (measureId: string, event: Event) => {
  const value = event.target instanceof HTMLSelectElement ? event.target.value : ''
  const current = reviewState.value[measureId]
  if (!current) return
  const codReferencia = value ? Number(value) : null
  reviewState.value[measureId] = {
    ...current,
    codReferencia,
    normalityMode: isMultiRange(current.codVariavel, codReferencia) ? 'classificacao' : 'simple'
  }
}

const hasComment = (codVariavel: number | null, codReferencia: number | null) => {
  if (!codVariavel) return false
  if (usingPadraoCliente.value) {
    return (variableDetails.value[codVariavel] ?? []).some(row => !!row.comentario_texto?.trim())
  }
  return (variableDetails.value[codVariavel] ?? []).some(row => {
    const sameRef = !codReferencia || row.referencia?.codigo === codReferencia
    return sameRef && !!row.comentario_texto?.trim()
  })
}

const setNormalityMode = (measureId: string, mode: 'simple' | 'classificacao' | 'texto') => {
  const current = reviewState.value[measureId]
  if (!current) return
  reviewState.value[measureId] = { ...current, normalityMode: mode }
}

const setMeasureDecision = (measureId: string, decision: 'keep' | 'ignore') => {
  const current = reviewState.value[measureId] ?? {
    codVariavel: null,
    decision: 'pending' as const,
    codReferencia: null,
    normalityMode: 'simple' as const
  }
  reviewState.value[measureId] = { ...current, decision, codVariavel: decision === 'ignore' ? null : current.codVariavel }
}

const handleGenerateReviewedModoTexto = async () => {
  if (hasPendingReview.value) {
    toast('Revise todas as medidas sem correlação antes de gerar o TXT.', 'warning')
    return
  }

  generatingReviewedText.value = true
  try {
    const measures: ReviewedMeasure[] = analysis.value.map(m => {
      const state = reviewState.value[m.id] ?? {
        codVariavel: null,
        decision: 'keep' as const,
        codReferencia: null,
        normalityMode: 'simple' as const
      }
      return {
        id: m.id,
        label: m.label,
        section: m.section,
        unit: m.unit,
        originalText: m.originalText,
        codVariavel: state.codVariavel,
        codReferencia: state.codReferencia,
        normalityMode: state.normalityMode,
        decision: state.decision === 'ignore' ? 'ignore' : 'keep'
      }
    })
    const result = await generateModoTextoFromAnalysis(
      analysisSourceFileName.value,
      measures,
      studioCodPadrao.value
    )
    const resolved = resolveConversionContent('modoTexto', result)
    outputFormat.value = 'modoTexto'
    generatedText.value = resolved.text
    generatedHtml.value = ''
    validation.value = result.validation

    store.setCurrent({
      id: crypto.randomUUID(),
      format: 'modoTexto',
      html: '',
      text: generatedText.value,
      sourceFileName: result.sourceFileName,
      convertedAt: result.convertedAt,
      provider: result.provider,
      imagePreviewUrl: imagePreview.value ?? undefined,
      validation: validation.value ?? { isValid: false, errors: [], warnings: [] }
    })

    toast(validation.value?.isValid ? 'TXT gerado com sucesso' : 'TXT gerado com avisos', validation.value?.isValid ? 'success' : 'warning')
  } catch (e) {
    toast(conversionErrorMessage(e, 'Erro ao gerar TXT revisado.'), 'error')
  } finally {
    generatingReviewedText.value = false
  }
}

const currentContent = computed(() =>
  outputFormat.value === 'modoTexto' ? generatedText.value : generatedHtml.value
)

const handleValidate = async () => {
  if (!hasOutput.value) return
  if (!isApiOnline.value) {
    toast('A API de conversão não está rodando.', 'error')
    return
  }
  try {
    validation.value = await validateContent(outputFormat.value, currentContent.value)
    const label = outputFormat.value === 'modoTexto' ? 'TXT' : 'HTML'
    toast(
      validation.value.isValid ? `${label} válido` : `${label} com erros`,
      validation.value.isValid ? 'success' : 'warning'
    )
  } catch (e) {
    toast(conversionErrorMessage(e, 'Erro ao validar conteúdo'), 'error')
  }
}

function conversionErrorMessage(error: unknown, fallback: string) {
  if (error && typeof error === 'object') {
    const data = 'data' in error ? (error as { data?: { message?: string } }).data : undefined
    if (data?.message) return data.message
    if ('message' in error && typeof error.message === 'string' && error.message) {
      return error.message
    }
  }
  return fallback
}

const handleCopy = async () => {
  if (!hasOutput.value) return
  await copyToClipboard(currentContent.value)
  toast(outputFormat.value === 'modoTexto' ? 'TXT copiado' : 'HTML copiado', 'success')
}

const handleDownload = () => {
  if (!hasOutput.value) return
  const name = selectedFile.value?.name.replace(/\.[^.]+$/, '') ?? 'laudo'
  if (outputFormat.value === 'modoTexto') {
    downloadText(generatedText.value, `${name}-modo-texto.txt`)
  } else {
    downloadHtml(generatedHtml.value, `${name}-laudosux.html`)
  }
  toast('Download iniciado', 'success')
}
</script>

<template>
  <div>
    <StudioDsPageHeader
      title="Converter Imagem"
      subtitle="Gere HTML LaudosUX ou TXT modo texto a partir de uma imagem de referência"
      icon="bi-magic"
    />
    <StudioDsPageShell>
      <div
        class="mb-4 flex flex-wrap items-center justify-between gap-3 rounded-ds-sm border px-4 py-3"
        :class="{
          'border-ds-border bg-ds-surface-elevated text-ds-muted': apiStatus === 'checking',
          'border-ds-success/40 bg-ds-surface-elevated text-ds-text': apiStatus === 'online',
          'border-ds-danger/50 bg-ds-surface-elevated text-ds-text': apiStatus === 'offline'
        }"
        role="status"
      >
        <p class="flex items-center gap-2 text-sm">
          <i
            :class="{
              'bi bi-arrow-repeat animate-spin text-ds-muted': apiStatus === 'checking',
              'bi bi-check-circle-fill text-ds-success': apiStatus === 'online',
              'bi bi-x-circle-fill text-ds-danger': apiStatus === 'offline'
            }"
          />
          <span v-if="apiStatus === 'checking'">Verificando se a API está rodando...</span>
          <span v-else-if="apiStatus === 'online'">
            API de conversão online{{ apiProvider ? ` · ${apiProvider}` : '' }}
          </span>
          <span v-else>API de conversão offline. Inicie o backend em localhost:5080.</span>
        </p>
        <StudioDsButton
          v-if="apiStatus !== 'checking'"
          variant="ghost"
          icon="bi-arrow-clockwise"
          @click="() => verifyApi(true)"
        >
          Verificar novamente
        </StudioDsButton>
      </div>

      <div
        v-if="apiStatus === 'online' && !apiSupportsModoTexto"
        class="mb-4 rounded-ds-sm border border-ds-danger/50 bg-ds-surface-elevated px-4 py-3 text-sm text-ds-text"
        role="alert"
      >
        <p class="flex items-center gap-2">
          <i class="bi bi-exclamation-triangle-fill text-ds-danger" />
          A API em execução parece desatualizada (sem suporte a TXT modo texto). Reinicie o backend com <code class="rounded bg-ds-surface px-1">dotnet run</code>.
        </p>
      </div>

      <div class="mb-6">
        <StudioImageUploadZone @select="onFileSelect" />
      </div>

      <div class="mb-4 flex flex-wrap items-center gap-3">
        <StudioDsButton icon="bi-play-fill" :loading="loading" :disabled="!isApiOnline || loading" @click="handleConvert">
          {{ loading ? 'Convertendo...' : 'Converter' }}
        </StudioDsButton>
        <span
          v-if="hasOutput || loading"
          class="inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-medium"
          :class="outputFormat === 'modoTexto'
            ? 'border-ds-primary-accent/40 bg-ds-primary-accent/10 text-ds-primary-accent'
            : 'border-ds-success/40 bg-ds-success/10 text-ds-success'"
        >
          <i :class="outputFormat === 'modoTexto' ? 'bi bi-file-text' : 'bi bi-code-slash'" />
          {{ loading ? `Gerando ${formatLabel}...` : formatLabel }}
        </span>
        <StudioDsButton variant="secondary" icon="bi-check2-circle" :disabled="!hasOutput" @click="handleValidate">          Validar
        </StudioDsButton>
        <StudioDsButton variant="secondary" icon="bi-clipboard" :disabled="!hasOutput" @click="handleCopy">
          Copiar
        </StudioDsButton>
        <StudioDsButton variant="secondary" icon="bi-download" :disabled="!hasOutput" @click="handleDownload">
          Baixar
        </StudioDsButton>
        <label v-if="outputFormat === 'html'" class="flex items-center gap-2 text-sm text-ds-muted">
          <input v-model="executeScripts" type="checkbox" class="rounded" :disabled="loading">
          Executar scripts no preview
        </label>
      </div>

      <StudioConversionProgress
        v-if="loading"
        class="mb-6"
        :elapsed-ms="elapsedMs"
        :percent="progressPercent"
        :format="outputFormat"
      />

      <StudioDsCard v-if="analysis.length" class="mb-6" title="Revisão das correlações">
        <div class="mb-4 rounded-ds-sm border border-ds-border bg-ds-surface p-3">
          <p class="text-xs font-medium text-ds-muted mb-2">Padrão de normalidade do cliente (opcional)</p>
          <div class="flex flex-wrap gap-3">
            <select
              v-model="studioCodCliente"
              class="h-10 min-w-48 rounded-ds-sm border border-ds-field-border bg-ds-surface-elevated px-2 text-sm text-ds-text"
              @change="onStudioClienteChange"
            >
              <option value="">Catálogo global</option>
              <option v-for="c in studioClientes" :key="c.codCliente" :value="String(c.codCliente)">
                {{ c.nome }}
              </option>
            </select>
            <select
              v-if="studioCodCliente"
              class="h-10 min-w-56 rounded-ds-sm border border-ds-field-border bg-ds-surface-elevated px-2 text-sm text-ds-text"
              :value="studioCodPadrao ?? ''"
              @change="onStudioPadraoChange(($event.target as HTMLSelectElement).value ? Number(($event.target as HTMLSelectElement).value) : null)"
            >
              <option value="">Selecione o padrão...</option>
              <option v-for="p in studioPadroes" :key="p.codPadrao" :value="p.codPadrao">
                {{ p.nome }}{{ p.padraoVigente === 1 ? ' (vigente)' : '' }}
              </option>
            </select>
          </div>
          <p v-if="usingPadraoCliente" class="mt-2 mb-0 text-xs text-ds-primary-accent">
            Usando faixas do padrão do cliente — referências do catálogo global são ignoradas.
          </p>
        </div>
        <div class="mb-4 flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <p class="text-sm text-ds-muted">
              Confira as medidas encontradas na imagem. Itens sem correlação precisam ser associados, ignorados ou mantidos como campo novo antes de gerar o TXT.
            </p>
            <p class="mt-1 text-xs text-ds-muted">
              Abra o seletor de cada medida e use a busca no topo da lista para filtrar sugestões e o banco.
              Pendentes: {{ analysis.filter(m => (reviewState[m.id]?.decision ?? 'pending') === 'pending').length }}
            </p>
          </div>
          <div class="flex flex-wrap gap-2">
            <StudioDsButton
              icon="bi-file-text"
              :loading="generatingReviewedText"
              :disabled="hasPendingReview || generatingReviewedText"
              @click="handleGenerateReviewedModoTexto"
            >
              Gerar TXT revisado
            </StudioDsButton>
          </div>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[960px] text-left text-sm">
            <thead class="border-b border-ds-border text-xs uppercase text-ds-muted">
              <tr>
                <th class="py-2 pr-3">Medida</th>
                <th class="py-2 pr-3">Seção</th>
                <th class="py-2 pr-3">Status</th>
                <th class="py-2 pr-3">Variável</th>
                <th class="py-2 pr-3">Ações</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="measure in analysis" :key="measure.id" class="border-b border-ds-border/70">
                <td class="py-3 pr-3">
                  <strong class="block text-ds-text">{{ measure.label }}</strong>
                  <span class="text-xs text-ds-muted">{{ measure.originalText || measure.variableName || 'Sem trecho original' }}</span>
                </td>
                <td class="py-3 pr-3 text-ds-text">{{ measure.section || 'GERAL' }}</td>
                <td class="py-3 pr-3">
                  <span
                    class="inline-flex rounded-full border px-2 py-1 text-xs"
                    :class="reviewState[measure.id]?.decision === 'pending'
                      ? 'border-ds-danger/50 text-ds-danger'
                      : reviewState[measure.id]?.codVariavel
                        ? 'border-ds-success/50 text-ds-success'
                        : 'border-ds-primary-accent/50 text-ds-primary-accent'"
                  >
                    {{
                      reviewState[measure.id]?.decision === 'pending'
                        ? 'Pendente'
                        : reviewState[measure.id]?.codVariavel
                          ? 'Correlacionada'
                          : reviewState[measure.id]?.decision === 'ignore'
                            ? 'Ignorada'
                            : 'Campo novo'
                    }}
                  </span>
                </td>
                <td class="py-3 pr-3">
                  <StudioVariableCombobox
                    :model-value="reviewState[measure.id]?.codVariavel ?? null"
                    :candidates="measure.candidates"
                    :bank-options="variableOptions"
                    :aria-label="`Selecionar variável para ${measure.label}`"
                    @update:model-value="onVariableComboboxChange(measure.id, $event)"
                  />
                  <div v-if="reviewState[measure.id]?.codVariavel" class="mt-2 space-y-2">
                    <select
                      v-if="!usingPadraoCliente"
                      class="h-9 w-full rounded-ds-sm border border-ds-field-border bg-ds-surface px-2 text-xs text-ds-text"
                      :value="reviewState[measure.id]?.codReferencia ?? ''"
                      @change="onReferenceChange(measure.id, $event)"
                    >
                      <option value="">Sem referência específica</option>
                      <option
                        v-for="ref in referencesFor(reviewState[measure.id]?.codVariavel ?? null)"
                        :key="ref.codigo"
                        :value="ref.codigo"
                      >
                        {{ ref.titulo }}{{ ref.ano ? ` (${ref.ano})` : '' }} — {{ ref.count }} faixa(s)
                      </option>
                    </select>
                    <div class="flex flex-wrap gap-3 text-xs text-ds-text-secondary">
                      <label class="inline-flex items-center gap-1">
                        <input
                          type="radio"
                          :name="`normality-mode-${measure.id}`"
                          :checked="reviewState[measure.id]?.normalityMode === 'simple'"
                          @change="setNormalityMode(measure.id, 'simple')"
                        >
                        Faixa simples
                      </label>
                      <label class="inline-flex items-center gap-1">
                        <input
                          type="radio"
                          :name="`normality-mode-${measure.id}`"
                          :checked="reviewState[measure.id]?.normalityMode === 'classificacao' || reviewState[measure.id]?.normalityMode === 'comment'"
                          @change="setNormalityMode(measure.id, 'classificacao')"
                        >
                        Por classificação
                      </label>
                      <label
                        class="inline-flex items-center gap-1"
                        :class="!hasComment(reviewState[measure.id]?.codVariavel ?? null, reviewState[measure.id]?.codReferencia ?? null) ? 'opacity-50' : ''"
                      >
                        <input
                          type="radio"
                          :name="`normality-mode-${measure.id}`"
                          :disabled="!hasComment(reviewState[measure.id]?.codVariavel ?? null, reviewState[measure.id]?.codReferencia ?? null)"
                          :checked="reviewState[measure.id]?.normalityMode === 'texto'"
                          @change="setNormalityMode(measure.id, 'texto')"
                        >
                        Somente comentário
                      </label>
                    </div>
                  </div>
                </td>
                <td class="py-3 pr-3">
                  <div class="flex flex-wrap gap-2">
                    <StudioDsButton size="sm" variant="secondary" icon="bi-plus-circle" @click="setMeasureDecision(measure.id, 'keep')">
                      Campo novo
                    </StudioDsButton>
                    <StudioDsButton size="sm" variant="ghost" icon="bi-x-circle" @click="setMeasureDecision(measure.id, 'ignore')">
                      Ignorar
                    </StudioDsButton>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </StudioDsCard>

      <div class="grid gap-6 lg:grid-cols-2">
        <StudioDsCard title="Imagem original">
          <img
            v-if="imagePreview"
            :src="imagePreview"
            alt="Preview da imagem"
            class="max-h-96 w-full rounded-ds-sm bg-ds-media object-contain"
          >
          <p v-else class="text-sm text-ds-muted">Nenhuma imagem selecionada</p>
        </StudioDsCard>

        <StudioDsCard :title="outputFormat === 'modoTexto' ? 'Preview TXT (modo texto)' : 'Preview HTML'">
          <template v-if="outputFormat === 'modoTexto'">
            <StudioModoTextoPreview :text="generatedText" />
          </template>
          <template v-else>
            <StudioHtmlPreviewFrame :html="generatedHtml" :execute-scripts="executeScripts" />
          </template>
        </StudioDsCard>      </div>

      <div class="mt-6 grid gap-6 lg:grid-cols-2">
        <StudioDsCard title="Validação">
          <StudioValidationPanel :validation="validation" :format="outputFormat" />
        </StudioDsCard>

        <StudioDsCard :title="outputFormat === 'modoTexto' ? 'Código TXT gerado' : 'Código HTML gerado'">
          <textarea
            v-if="outputFormat === 'modoTexto'"
            v-model="generatedText"
            class="h-64 w-full resize-y rounded-ds-sm border border-ds-field-border bg-ds-surface-elevated p-3 font-mono text-xs text-ds-text outline-none focus:border-ds-primary-accent focus:ring-1 focus:ring-ds-primary-accent"
            placeholder="O TXT gerado aparecerá aqui..."
          />
          <textarea
            v-else
            v-model="generatedHtml"
            class="h-64 w-full resize-y rounded-ds-sm border border-ds-field-border bg-ds-surface-elevated p-3 font-mono text-xs text-ds-text outline-none focus:border-ds-primary-accent focus:ring-1 focus:ring-ds-primary-accent"
            placeholder="O HTML gerado aparecerá aqui..."
          />
        </StudioDsCard>
      </div>
    </StudioDsPageShell>
  </div>
</template>
