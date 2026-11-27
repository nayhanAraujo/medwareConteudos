<script setup lang="ts">
defineProps<{
  listening: boolean
  supported: boolean
  busy: boolean
  applying?: boolean
  disabled?: boolean
  pending?: boolean
}>()
const instruction = defineModel<string>({ required: true })
defineEmits<{ speak: []; stop: []; apply: [] }>()
</script>

<template>
  <div class="voice-mic-controls">
    <p class="mb-2 text-sm font-semibold">Falar → conferir → aplicar</p>
    <p role="status" class="mb-2 flex items-center gap-2 text-sm font-semibold">
      <i class="bi bi-circle-fill text-ds-primary-accent" aria-hidden="true" />
      {{ applying ? 'Aplicando alteração' : busy ? 'Processando áudio' : listening ? 'Ouvindo' : instruction.trim() ? 'Confira sua instrução' : 'Microfone pausado' }}
    </p>
    <StudioDsButton :icon="listening ? 'bi-stop-fill' : 'bi-mic-fill'" class="min-h-12 w-full"
      :disabled="!listening && (busy || disabled || pending || !supported)" @click="listening ? $emit('stop') : $emit('speak')">
      {{ listening ? 'Parar gravação' : 'Falar alteração' }}
    </StudioDsButton>
  </div>
  <StudioVoiceTips />
  <p v-if="!supported" class="my-2 text-sm">Microfone indisponível neste navegador. Escreva a instrução ou envie um áudio.</p>
  <label class="my-3 block text-sm">
    Sua instrução
    <textarea v-model="instruction" rows="3" maxlength="8000" :disabled="busy || disabled || pending"
      class="mt-2 w-full rounded-lg border border-ds-divider bg-ds-surface p-3"
      placeholder="Ex.: adicione anel aórtico na seção Valvas" />
  </label>
  <StudioDsButton variant="secondary" class="w-full" :disabled="busy || disabled || listening || (!instruction.trim() && !pending)"
    @click="$emit('apply')">{{ pending ? 'Tentar mesma alteração novamente' : 'Aplicar alteração' }}</StudioDsButton>
</template>
