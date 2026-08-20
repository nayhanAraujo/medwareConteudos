<script setup lang="ts">
const props = defineProps<{
  isSupported: boolean
  isListening: boolean
  transcript: string
  error: string | null
  sending?: boolean
  hasModel?: boolean
}>()

const emit = defineEmits<{
  'update:transcript': [value: string]
  start: []
  stop: []
  build: []
  edit: []
  uploadAudio: [file: File]
}>()

const localTranscript = computed({
  get: () => props.transcript,
  set: (v: string) => emit('update:transcript', v)
})

const onAudioFile = (event: Event) => {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (file) emit('uploadAudio', file)
  input.value = ''
}
</script>

<template>
  <StudioDsCard title="Modo voz">
    <div class="space-y-4">
      <p class="text-sm text-ds-text-secondary">
        Fale o laudo em português (seções, medidas, unidades, normalidades). Ao <strong>parar</strong>, o agente Cursor analisa o transcript, monta o modelo e gera o TXT modo texto.
      </p>

      <div v-if="!isSupported" class="rounded-ds-btn border border-amber-500/40 bg-amber-500/10 p-3 text-sm text-amber-200">
        Web Speech API indisponível neste navegador. Use upload de áudio ou Chrome/Edge em HTTPS/localhost.
      </div>

      <div class="flex flex-wrap items-center gap-3">
        <StudioDsButton
          v-if="!isListening"
          icon="bi-mic-fill"
          variant="success"
          :disabled="sending"
          @click="emit('start')"
        >
          Falar
        </StudioDsButton>
        <StudioDsButton
          v-else
          icon="bi-stop-fill"
          variant="danger"
          :loading="sending"
          @click="emit('stop')"
        >
          Parar — gerar modelo e TXT
        </StudioDsButton>

        <label class="inline-flex cursor-pointer items-center gap-2 text-sm text-ds-text-secondary hover:text-ds-primary-accent">
          <i class="bi bi-file-earmark-music" />
          Enviar áudio
          <input type="file" accept="audio/*" class="hidden" @change="onAudioFile">
        </label>

        <span v-if="isListening" class="inline-flex items-center gap-2 text-sm text-ds-danger">
          <span class="h-2 w-2 animate-pulse rounded-full bg-ds-danger" />
          Ouvindo…
        </span>
      </div>

      <div>
        <label class="mb-1 block text-xs font-medium text-ds-muted">Transcript (revise se o reconhecimento errar termos médicos)</label>
        <textarea
          v-model="localTranscript"
          rows="4"
          class="w-full rounded-ds-btn border border-ds-divider bg-ds-surface px-3 py-2 text-sm text-ds-text placeholder:text-ds-muted focus:border-ds-primary-accent focus:outline-none"
          placeholder="Ex.: quero um laudo com dados do paciente altura peso superfície corporal e câmaras esquerda via de saída do VE anel aórtico em milímetros..."
        />
      </div>

      <p v-if="error" class="text-sm text-ds-danger">{{ error }}</p>

      <div class="flex flex-wrap gap-2">
        <StudioDsButton
          icon="bi-diagram-3"
          :loading="sending"
          :disabled="!localTranscript.trim() || isListening"
          @click="emit('build')"
        >
          Gerar modelo e TXT
        </StudioDsButton>

        <StudioDsButton
          v-if="hasModel"
          icon="bi-pencil-square"
          variant="secondary"
          :loading="sending"
          :disabled="!localTranscript.trim() || isListening"
          title="Comandos como: remover peso, adicionar altura em cm, criar seção valvas"
          @click="emit('edit')"
        >
          Editar modelo com voz
        </StudioDsButton>
      </div>
    </div>
  </StudioDsCard>
</template>
