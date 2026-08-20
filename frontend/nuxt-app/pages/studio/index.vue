<script setup lang="ts">
definePageMeta({ layout: 'studio' })

const router = useRouter()
const store = useConversionStore()
const { toast } = useStudioSwal()

const onFileSelect = (file: File) => {
  sessionStorage.setItem('pendingImageName', file.name)

  const reader = new FileReader()
  reader.onload = () => {
    sessionStorage.setItem('pendingImageData', reader.result as string)
    router.push('/studio/converter')
  }
  reader.readAsDataURL(file)
  toast(`Imagem "${file.name}" selecionada`, 'success')
}
</script>

<template>
  <div>
    <StudioDsPageHeader
      title="Conversor Imagem → HTML"
      subtitle="Transforme imagens de laudos em scripts HTML compatíveis com LaudosUX"
      icon="bi-image"
    />
    <StudioDsPageShell>
      <div class="grid gap-6 lg:grid-cols-3">
        <StudioDsCard title="Upload de imagem">
          <StudioImageUploadZone @select="onFileSelect" />
        </StudioDsCard>

        <StudioDsCard title="Modo voz">
          <p class="mb-4 text-sm text-ds-text-secondary">
            Monte ou edite modelos falando em português. Exporte em HTML ou TXT Modo Texto.
          </p>
          <NuxtLink to="/studio/voz">
            <StudioDsButton icon="bi-mic">
              Abrir modo voz
            </StudioDsButton>
          </NuxtLink>
        </StudioDsCard>

        <StudioDsCard title="Como funciona">
          <ol class="list-inside list-decimal space-y-2 text-sm text-ds-text-secondary">
            <li>Faça upload de uma imagem do layout do laudo</li>
            <li>O sistema analisa e gera HTML seguindo o manual LaudosUX</li>
            <li>Revise o resultado no preview e editor</li>
            <li>Baixe o script HTML pronto para uso</li>
          </ol>
          <div class="mt-4">
            <NuxtLink to="/studio/converter">
              <StudioDsButton icon="bi-arrow-right">
                Ir para conversão
              </StudioDsButton>
            </NuxtLink>
          </div>
        </StudioDsCard>
      </div>

      <StudioDsCard v-if="store.history.length" title="Histórico da sessão" class="mt-6">
        <div class="divide-y divide-ds-divider">
          <div
            v-for="item in store.history"
            :key="item.id"
            class="flex items-center justify-between py-3"
          >
            <div>
              <p class="font-medium text-ds-text">{{ item.sourceFileName }}</p>
              <p class="text-xs text-ds-muted">{{ new Date(item.convertedAt).toLocaleString('pt-BR') }}</p>
            </div>
            <NuxtLink :to="`/studio/resultado/${item.id}`">
              <StudioDsButton variant="ghost" icon="bi-eye">Ver</StudioDsButton>
            </NuxtLink>
          </div>
        </div>
      </StudioDsCard>
    </StudioDsPageShell>
  </div>
</template>
