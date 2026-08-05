<script setup lang="ts">
import type { ConversionRecord } from '~/types/conversion'

definePageMeta({ layout: 'studio' })

const store = useConversionStore()
const { convertImage, validateHtml } = useConversionApi()
const { downloadHtml, copyToClipboard } = useHtmlPreview()
const { toast, alert } = useStudioSwal()

const selectedFile = ref<File | null>(null)
const imagePreview = ref<string | null>(null)
const generatedHtml = ref('')
const validation = ref<ConversionRecord['validation'] | null>(null)
const loading = ref(false)
const executeScripts = ref(false)

onMounted(() => {
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

const onFileSelect = (file: File) => {
  selectedFile.value = file
  imagePreview.value = URL.createObjectURL(file)
  generatedHtml.value = ''
  validation.value = null
}

const handleConvert = async () => {
  if (!selectedFile.value) {
    toast('Selecione uma imagem primeiro', 'warning')
    return
  }

  loading.value = true
  try {
    const result = await convertImage(selectedFile.value)
    generatedHtml.value = result.html
    validation.value = result.validation

    const record: ConversionRecord = {
      id: crypto.randomUUID(),
      html: result.html,
      sourceFileName: result.sourceFileName,
      convertedAt: result.convertedAt,
      provider: result.provider,
      imagePreviewUrl: imagePreview.value ?? undefined,
      validation: result.validation
    }
    store.setCurrent(record)

    if (result.validation.isValid) {
      toast('Conversão concluída com sucesso', 'success')
    } else {
      await alert('Conversão concluída com avisos', 'Revise os erros de validação.', 'warning')
    }
  } catch {
    toast('Erro ao converter imagem. Verifique se a API está rodando.', 'error')
  } finally {
    loading.value = false
  }
}

const handleValidate = async () => {
  if (!generatedHtml.value) return
  try {
    validation.value = await validateHtml(generatedHtml.value)
    toast(validation.value.isValid ? 'HTML válido' : 'HTML com erros', validation.value.isValid ? 'success' : 'warning')
  } catch {
    toast('Erro ao validar HTML', 'error')
  }
}

const handleCopy = async () => {
  if (!generatedHtml.value) return
  await copyToClipboard(generatedHtml.value)
  toast('HTML copiado', 'success')
}

const handleDownload = () => {
  if (!generatedHtml.value) return
  const name = selectedFile.value?.name.replace(/\.[^.]+$/, '') ?? 'laudo'
  downloadHtml(generatedHtml.value, `${name}-laudosux.html`)
  toast('Download iniciado', 'success')
}
</script>

<template>
  <div>
    <StudioDsPageHeader
      title="Converter Imagem"
      subtitle="Gere HTML LaudosUX a partir de uma imagem de referência"
      icon="bi-magic"
    />
    <StudioDsPageShell>
      <div class="mb-6">
        <StudioImageUploadZone @select="onFileSelect" />
      </div>

      <div class="mb-4 flex flex-wrap gap-3">
        <StudioDsButton icon="bi-play-fill" :loading="loading" @click="handleConvert">
          Converter
        </StudioDsButton>
        <StudioDsButton variant="secondary" icon="bi-check2-circle" :disabled="!generatedHtml" @click="handleValidate">
          Validar
        </StudioDsButton>
        <StudioDsButton variant="secondary" icon="bi-clipboard" :disabled="!generatedHtml" @click="handleCopy">
          Copiar
        </StudioDsButton>
        <StudioDsButton variant="secondary" icon="bi-download" :disabled="!generatedHtml" @click="handleDownload">
          Baixar
        </StudioDsButton>
        <label class="flex items-center gap-2 text-sm text-ds-muted">
          <input v-model="executeScripts" type="checkbox" class="rounded">
          Executar scripts no preview
        </label>
      </div>

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

        <StudioDsCard title="Preview HTML">
          <StudioHtmlPreviewFrame :html="generatedHtml" :execute-scripts="executeScripts" />
        </StudioDsCard>
      </div>

      <div class="mt-6 grid gap-6 lg:grid-cols-2">
        <StudioDsCard title="Validação">
          <StudioValidationPanel :validation="validation" />
        </StudioDsCard>

        <StudioDsCard title="Código HTML gerado">
          <textarea
            v-model="generatedHtml"
            class="h-64 w-full resize-y rounded-ds-sm border border-ds-field-border bg-ds-surface-elevated p-3 font-mono text-xs text-ds-text outline-none focus:border-ds-primary-accent focus:ring-1 focus:ring-ds-primary-accent"
            placeholder="O HTML gerado aparecerá aqui..."
          />
        </StudioDsCard>
      </div>
    </StudioDsPageShell>
  </div>
</template>
