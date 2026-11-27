<script setup lang="ts">
const origin = defineModel<'new' | 'open' | 'json' | 'image'>({ required: true })
const props = defineProps<{ disabled?: boolean; canCancel?: boolean }>()
defineEmits<{ cancel: [] }>()
const heading = ref<HTMLElement | null>(null)
onMounted(() => { if (props.canCancel) heading.value?.focus() })

const options = [
  { id: 'new', title: 'Novo modelo', description: 'Comece vazio e monte falando.', icon: 'bi-file-earmark-plus' },
  { id: 'open', title: 'Abrir modelo', description: 'Use um modelo já cadastrado.', icon: 'bi-folder2-open' },
  { id: 'json', title: 'Importar arquivo', description: 'Use um modelo em formato JSON.', icon: 'bi-file-earmark-arrow-up' },
  { id: 'image', title: 'Usar imagem', description: 'Comece a partir de uma imagem.', icon: 'bi-image' }
] as const
</script>

<template>
  <StudioDsCard>
    <h1 ref="heading" tabindex="-1" class="mb-4 text-2xl font-semibold">Como deseja começar?</h1>
    <div class="mb-5 grid gap-3 sm:grid-cols-2 xl:grid-cols-4" role="group" aria-label="Origem do modelo">
      <button v-for="option in options" :key="option.id" type="button" :disabled="disabled"
        :aria-pressed="origin === option.id" class="voice-origin rounded-xl border p-4 text-left"
        :class="origin === option.id ? 'border-ds-primary-accent bg-ds-surface-elevated' : 'border-ds-divider'"
        @click="origin = option.id">
        <i :class="option.icon" class="bi mb-2 block text-xl text-ds-primary-accent" aria-hidden="true" />
        <span class="block font-semibold">{{ option.title }}</span>
        <span class="mt-1 block text-sm text-ds-text-secondary">{{ option.description }}</span>
      </button>
    </div>
    <slot />
    <slot name="resume" />
    <button v-if="canCancel" type="button" class="voice-origin mt-4 rounded-lg border border-ds-divider px-3 py-2 text-sm"
      :disabled="disabled" @click="$emit('cancel')">Voltar ao modelo atual</button>
  </StudioDsCard>
</template>

<style scoped>
.voice-origin:focus-visible { outline: 2px solid var(--color-ds-primary-accent); outline-offset: 3px; }
.voice-origin:disabled { opacity: .55; }
</style>
