<script setup lang="ts">
import type { ConversionFormat, ConversionRecord } from '~/types/conversion'
import { looksLikeHtml, resolveConversionContent } from '~/utils/conversionFormat'

definePageMeta({ layout: 'studio' })
const store = useConversionStore()
const { convertImage, validateContent, checkHealth } = useConversionApi()
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
