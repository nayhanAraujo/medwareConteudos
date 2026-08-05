<script setup lang="ts">
definePageMeta({ layout: 'studio' })

const store = useConversionStore()
const { validateHtml } = useConversionApi()
const { downloadHtml, copyToClipboard } = useHtmlPreview()
const { toast } = useStudioSwal()

const html = ref(store.current?.html ?? '')
const validation = ref(store.current?.validation ?? null)
const executeScripts = ref(false)
const loading = ref(false)

onMounted(() => {
  if (store.current?.html && !html.value) {
    html.value = store.current.html
    validation.value = store.current.validation
  }
})

watch(html, () => {
  if (store.current) {
    store.updateHtml(store.current.id, html.value)
  }
})

const handleValidate = async () => {
  if (!html.value) return
  loading.value = true
  try {
    validation.value = await validateHtml(html.value)
    toast(validation.value.isValid ? 'HTML válido' : 'HTML com erros', validation.value.isValid ? 'success' : 'warning')
  } catch {
    toast('Erro ao validar', 'error')
  } finally {
    loading.value = false
  }
}

const handleCopy = async () => {
  await copyToClipboard(html.value)
  toast('Copiado', 'success')
}

const handleDownload = () => {
  downloadHtml(html.value, 'laudo-editado.html')
  toast('Download iniciado', 'success')
}
</script>

<template>
  <div>
    <StudioDsPageHeader
      title="Editor HTML"
      subtitle="Edite e valide o script LaudosUX gerado"
      icon="bi-code-slash"
    />
    <StudioDsPageShell>
      <div class="mb-4 flex flex-wrap gap-3">
        <StudioDsButton icon="bi-check2-circle" :loading="loading" :disabled="!html" @click="handleValidate">
          Validar
        </StudioDsButton>
        <StudioDsButton variant="secondary" icon="bi-clipboard" :disabled="!html" @click="handleCopy">
          Copiar
        </StudioDsButton>
        <StudioDsButton variant="secondary" icon="bi-download" :disabled="!html" @click="handleDownload">
          Baixar
        </StudioDsButton>
        <label class="flex items-center gap-2 text-sm text-ds-muted">
          <input v-model="executeScripts" type="checkbox" class="rounded">
          Executar scripts no preview
        </label>
      </div>

      <div class="grid gap-6 lg:grid-cols-2">
        <StudioDsCard title="Editor">
          <textarea
            v-model="html"
            class="h-[500px] w-full resize-none rounded-ds-sm border border-ds-field-border bg-ds-surface-elevated p-3 font-mono text-xs text-ds-text outline-none focus:border-ds-primary-accent focus:ring-1 focus:ring-ds-primary-accent"
            placeholder="Cole ou edite o HTML LaudosUX aqui..."
          />
        </StudioDsCard>

        <div class="space-y-6">
          <StudioDsCard title="Preview">
            <StudioHtmlPreviewFrame :html="html" :execute-scripts="executeScripts" />
          </StudioDsCard>
          <StudioDsCard title="Validação">
            <StudioValidationPanel :validation="validation" />
          </StudioDsCard>
        </div>
      </div>
    </StudioDsPageShell>
  </div>
</template>
