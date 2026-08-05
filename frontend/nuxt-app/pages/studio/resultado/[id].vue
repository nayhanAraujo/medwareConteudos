<script setup lang="ts">
definePageMeta({ layout: 'studio' })

const route = useRoute()
const store = useConversionStore()
const { downloadHtml, copyToClipboard } = useHtmlPreview()
const { toast } = useStudioSwal()

const id = computed(() => route.params.id as string)
const record = computed(() => store.getById(id.value))
const executeScripts = ref(false)

const handleCopy = async () => {
  if (!record.value) return
  await copyToClipboard(record.value.html)
  toast('HTML copiado', 'success')
}

const handleDownload = () => {
  if (!record.value) return
  const name = record.value.sourceFileName.replace(/\.[^.]+$/, '')
  downloadHtml(record.value.html, `${name}-laudosux.html`)
  toast('Download iniciado', 'success')
}
</script>

<template>
  <div>
    <StudioDsPageHeader
      title="Resultado da Conversão"
      :subtitle="record?.sourceFileName ?? 'Conversão não encontrada'"
      icon="bi-file-earmark-check"
    />
    <StudioDsPageShell>
      <div v-if="record" class="space-y-6">
        <div class="flex flex-wrap gap-3">
          <StudioDsButton icon="bi-clipboard" @click="handleCopy">Copiar HTML</StudioDsButton>
          <StudioDsButton variant="secondary" icon="bi-download" @click="handleDownload">Baixar</StudioDsButton>
          <NuxtLink to="/studio/editor">
            <StudioDsButton variant="ghost" icon="bi-pencil">Editar</StudioDsButton>
          </NuxtLink>
          <label class="flex items-center gap-2 text-sm text-ds-muted">
            <input v-model="executeScripts" type="checkbox" class="rounded">
            Executar scripts no preview
          </label>
        </div>

        <div class="grid gap-4 text-sm text-ds-muted sm:grid-cols-3">
          <div>
            <span class="font-medium text-ds-text">Provider:</span> {{ record.provider }}
          </div>
          <div>
            <span class="font-medium text-ds-text">Data:</span>
            {{ new Date(record.convertedAt).toLocaleString('pt-BR') }}
          </div>
          <div>
            <span class="font-medium text-ds-text">Status:</span>
            <span :class="record.validation.isValid ? 'text-ds-success' : 'text-ds-danger'">
              {{ record.validation.isValid ? 'Válido' : 'Com erros' }}
            </span>
          </div>
        </div>

        <div class="grid gap-6 lg:grid-cols-2">
          <StudioDsCard v-if="record.imagePreviewUrl" title="Imagem original">
            <img :src="record.imagePreviewUrl" alt="Imagem original" class="max-h-80 w-full rounded-ds-sm bg-ds-media object-contain">
          </StudioDsCard>
          <StudioDsCard title="Preview HTML" :class="{ 'lg:col-span-2': !record.imagePreviewUrl }">
            <StudioHtmlPreviewFrame :html="record.html" :execute-scripts="executeScripts" />
          </StudioDsCard>
        </div>

        <StudioDsCard title="Validação">
          <StudioValidationPanel :validation="record.validation" />
        </StudioDsCard>

        <StudioDsCard title="Código HTML">
          <pre class="max-h-96 overflow-auto rounded-ds-sm bg-ds-surface-elevated p-4 font-mono text-xs text-ds-text">{{ record.html }}</pre>
        </StudioDsCard>
      </div>

      <StudioDsCard v-else>
        <p class="text-ds-muted">Conversão não encontrada no histórico da sessão.</p>
        <NuxtLink to="/studio" class="mt-4 inline-block">
          <StudioDsButton icon="bi-house">Voltar ao início</StudioDsButton>
        </NuxtLink>
      </StudioDsCard>
    </StudioDsPageShell>
  </div>
</template>
