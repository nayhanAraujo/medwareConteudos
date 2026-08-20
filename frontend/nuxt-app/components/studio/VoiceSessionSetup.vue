<script setup lang="ts">
import type { VoiceSessionMode } from '~/types/voice'

const emit = defineEmits<{
  start: [mode: VoiceSessionMode, image?: File]
}>()

const mode = ref<VoiceSessionMode>('fromScratch')
const selectedImage = ref<File | null>(null)
const imagePreview = ref<string | null>(null)
const loading = ref(false)

const onImageSelect = (file: File) => {
  selectedImage.value = file
  imagePreview.value = URL.createObjectURL(file)
  mode.value = 'fromImage'
}

const onStart = () => {
  if (mode.value === 'fromImage' && !selectedImage.value) return
  loading.value = true
  emit('start', mode.value, selectedImage.value ?? undefined)
}

defineExpose({ setLoading: (v: boolean) => { loading.value = v } })
</script>

<template>
  <StudioDsCard title="Iniciar sessão">
    <div class="space-y-4">
      <p class="text-sm text-ds-text-secondary">
        Descreva o laudo em voz natural. O modelo é montado incrementalmente em JSON Studio.
      </p>

      <div class="grid gap-3 sm:grid-cols-2">
        <button
          type="button"
          class="rounded-ds-btn border p-4 text-left transition-colors"
          :class="mode === 'fromScratch' ? 'border-ds-primary-accent bg-ds-surface-elevated' : 'border-ds-divider hover:border-ds-muted'"
          @click="mode = 'fromScratch'"
        >
          <i class="bi bi-mic mb-2 block text-xl text-ds-primary-accent" />
          <p class="font-semibold text-ds-text">Do zero</p>
          <p class="mt-1 text-xs text-ds-muted">Comece vazio e construa por voz</p>
        </button>

        <button
          type="button"
          class="rounded-ds-btn border p-4 text-left transition-colors"
          :class="mode === 'fromImage' ? 'border-ds-primary-accent bg-ds-surface-elevated' : 'border-ds-divider hover:border-ds-muted'"
          @click="mode = 'fromImage'"
        >
          <i class="bi bi-image mb-2 block text-xl text-ds-primary-accent" />
          <p class="font-semibold text-ds-text">Com imagem</p>
          <p class="mt-1 text-xs text-ds-muted">Bootstrap a partir de layout escaneado</p>
        </button>
      </div>

      <div v-if="mode === 'fromImage'">
        <StudioImageUploadZone @select="onImageSelect" />
        <img
          v-if="imagePreview"
          :src="imagePreview"
          alt="Preview"
          class="mt-3 max-h-40 rounded-ds-btn border border-ds-divider object-contain"
        >
      </div>

      <StudioDsButton
        icon="bi-play-fill"
        :loading="loading"
        :disabled="mode === 'fromImage' && !selectedImage"
        @click="onStart"
      >
        Iniciar sessão
      </StudioDsButton>
    </div>
  </StudioDsCard>
</template>
