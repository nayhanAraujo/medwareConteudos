<script setup lang="ts">
import type { ConversionFormat, ValidationResponse } from '~/types/conversion'

defineProps<{
  loading?: boolean
  hasSession?: boolean
  outputFormat?: ConversionFormat | null
  validation?: ValidationResponse | null
  imported?: boolean
}>()

const emit = defineEmits<{
  generate: []
  openEditor: []
}>()
</script>

<template>
  <StudioDsCard title="Gerar laudo">
    <p class="mb-4 text-sm text-ds-text-secondary">
      {{ imported ? 'Baixe o JSON Studio completo, preservando os componentes e configurações do arquivo importado.' : 'Exporte o modelo atual para HTML LaudosUX, TXT Modo Texto ou JSON Studio.' }}
    </p>

    <StudioDsButton
      icon="bi-file-earmark-arrow-down"
      :loading="loading"
      :disabled="!hasSession"
      @click="emit('generate')"
    >
      {{ imported ? 'Gerar JSON completo' : 'Gerar laudo' }}
    </StudioDsButton>

    <div v-if="outputFormat && validation" class="mt-4">
      <StudioValidationPanel :format="outputFormat" :validation="validation" />
      <div v-if="!imported" class="mt-3">
        <StudioDsButton variant="secondary" icon="bi-pencil" @click="emit('openEditor')">
          Abrir no editor
        </StudioDsButton>
      </div>
    </div>
  </StudioDsCard>
</template>
