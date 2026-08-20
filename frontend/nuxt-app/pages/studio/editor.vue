<script setup lang="ts">
import type { ConversionFormat } from '~/types/conversion'
definePageMeta({ layout: 'studio' })

const store = useConversionStore()
const { validateContent } = useConversionApi()
const { downloadHtml, downloadText, copyToClipboard } = useHtmlPreview()
const { toast } = useStudioSwal()

const format = ref<ConversionFormat>(store.current?.format ?? 'html')
const html = ref(store.current?.html ?? '')
const text = ref(store.current?.text ?? '')
const validation = ref(store.current?.validation ?? null)
const executeScripts = ref(false)
const loading = ref(false)

const content = computed({
  get: () => (format.value === 'modoTexto' ? text.value : html.value),
  set: (value: string) => {
    if (format.value === 'modoTexto') {
      text.value = value
    } else {
      html.value = value
    }
  }
})

const hasContent = computed(() => content.value.length > 0)

onMounted(() => {
  if (store.current) {
    format.value = store.current.format ?? 'html'
    html.value = store.current.html ?? ''
    text.value = store.current.text ?? ''
    validation.value = store.current.validation
  }
})

watch([html, text, format], () => {
  if (store.current) {
    store.updateContent(store.current.id, format.value, content.value)
  }
})

const handleValidate = async () => {
  if (!hasContent.value) return
  loading.value = true
  try {
    validation.value = await validateContent(format.value, content.value)
    const label = format.value === 'modoTexto' ? 'TXT' : 'HTML'
    toast(
      validation.value.isValid ? `${label} válido` : `${label} com erros`,
      validation.value.isValid ? 'success' : 'warning'
    )
  } catch {
    toast('Erro ao validar', 'error')
  } finally {
    loading.value = false
  }
}

const handleCopy = async () => {
  await copyToClipboard(content.value)
  toast('Copiado', 'success')
}

const handleDownload = () => {
  if (format.value === 'modoTexto') {
    downloadText(text.value, 'laudo-modo-texto.txt')
  } else {
    downloadHtml(html.value, 'laudo-editado.html')
  }
  toast('Download iniciado', 'success')
}
</script>

<template>
  <div>
    <StudioDsPageHeader
      :title="format === 'modoTexto' ? 'Editor TXT' : 'Editor HTML'"
      :subtitle="format === 'modoTexto' ? 'Edite e valide o modo texto LaudosUX' : 'Edite e valide o script LaudosUX gerado'"
      icon="bi-code-slash"
    />
    <StudioDsPageShell>
      <div class="mb-4 flex flex-wrap gap-3">
        <StudioDsButton icon="bi-check2-circle" :loading="loading" :disabled="!hasContent" @click="handleValidate">
          Validar
        </StudioDsButton>
        <StudioDsButton variant="secondary" icon="bi-clipboard" :disabled="!hasContent" @click="handleCopy">
          Copiar
        </StudioDsButton>
        <StudioDsButton variant="secondary" icon="bi-download" :disabled="!hasContent" @click="handleDownload">
          Baixar
        </StudioDsButton>
        <label v-if="format === 'html'" class="flex items-center gap-2 text-sm text-ds-muted">
          <input v-model="executeScripts" type="checkbox" class="rounded">
          Executar scripts no preview
        </label>
      </div>

      <div class="grid gap-6 lg:grid-cols-2">
        <StudioDsCard title="Editor">
          <textarea
            v-model="content"
            class="h-[500px] w-full resize-none rounded-ds-sm border border-ds-field-border bg-ds-surface-elevated p-3 font-mono text-xs text-ds-text outline-none focus:border-ds-primary-accent focus:ring-1 focus:ring-ds-primary-accent"
            :placeholder="format === 'modoTexto' ? 'Cole ou edite o TXT modo texto aqui...' : 'Cole ou edite o HTML LaudosUX aqui...'"
          />
        </StudioDsCard>

        <div class="space-y-6">
          <StudioDsCard :title="format === 'modoTexto' ? 'Preview TXT (modo texto)' : 'Preview'">
            <template v-if="format === 'modoTexto'">
              <StudioModoTextoPreview :text="text" />
            </template>            <template v-else>
              <StudioHtmlPreviewFrame :html="html" :execute-scripts="executeScripts" />
            </template>
          </StudioDsCard>
          <StudioDsCard title="Validação">
            <StudioValidationPanel :validation="validation" :format="format" />
          </StudioDsCard>
        </div>
      </div>
    </StudioDsPageShell>
  </div>
</template>
