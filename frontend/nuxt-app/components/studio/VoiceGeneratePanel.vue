<script setup lang="ts">
import type { ConversionFormat, ValidationResponse } from '~/types/conversion'

defineProps<{
  loading?: boolean
  hasSession?: boolean
  outputFormat?: ConversionFormat | null
  validation?: ValidationResponse | null
}>()

const emit = defineEmits<{
  generate: []
  openEditor: []
}>()
</script>

<template>
  <StudioDsCard title="Gerar laudo">
    <p class="mb-4 text-sm text-ds-text-secondary">
      Exporte o modelo atual para HTML LaudosUX ou TXT Modo Texto, com validação automática.
    </p>

    <StudioDsButton
      icon="bi-file-earmark-arrow-down"
      :loading="loading"
      :disabled="!hasSession"
      @click="emit('generate')"
    >
      Gerar laudo
    </StudioDsButton>

    <div v-if="outputFormat && validation" class="mt-4">
      <StudioValidationPanel :format="outputFormat" :validation="validation" />
      <div class="mt-3">
        <StudioDsButton variant="secondary" icon="bi-pencil" @click="emit('openEditor')">
          Abrir no editor
        </StudioDsButton>
      </div>
    </div>
  </StudioDsCard>
</template>
