<script setup lang="ts">
const props = defineProps<{
  title: string
  storageStatus: string
  temporary?: boolean
  imported?: boolean
  phase: 'edit' | 'review'
  disabled?: boolean
  openDisabled?: boolean
  download: () => Promise<boolean>
}>()
const emit = defineEmits<{ open: [] }>()
const downloading = ref(false)
const downloaded = ref(false)
const error = ref('')
const heading = ref<HTMLElement | null>(null)
let viewVersion = 0
const current = computed(() => downloading.value || downloaded.value ? 3 : props.phase === 'review' ? 2 : 1)
const steps = ['Começar', 'Editar por voz', 'Revisar', 'Baixar']
defineExpose({
  focus: () => heading.value?.focus(),
  reset: () => { viewVersion++; downloaded.value = false; error.value = '' }
})

async function startDownload() {
  if (props.disabled || downloading.value) return
  downloading.value = true
  const version = viewVersion
  downloaded.value = false
  error.value = ''
  try {
    const started = await props.download()
    if (version === viewVersion) downloaded.value = started
  }
  catch { error.value = 'Não foi possível iniciar o download. Tente novamente.' }
  finally { downloading.value = false }
}
</script>

<template>
  <header class="voice-workspace-header rounded-xl border border-ds-divider bg-ds-surface-elevated p-4 sm:p-5">
    <div class="flex flex-wrap items-center justify-between gap-4">
      <div class="min-w-0">
        <h1 ref="heading" tabindex="-1" class="break-words text-2xl font-semibold">{{ title }}</h1>
        <div class="mt-2 flex flex-wrap gap-2 text-sm">
          <span role="status" class="rounded-full border border-ds-divider px-3 py-1" :class="{ 'text-amber-500': temporary }">{{ storageStatus }}</span>
          <span v-if="imported" class="rounded-full border border-ds-divider px-3 py-1">Modelo importado</span>
        </div>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <StudioDsButton variant="secondary" :disabled="openDisabled || downloading" @click="emit('open')">Abrir outro modelo</StudioDsButton>
        <slot name="actions" />
        <StudioDsButton icon="bi-download" :loading="downloading" :disabled="disabled || downloading" @click="startDownload">Baixar modelo</StudioDsButton>
      </div>
    </div>
    <ol class="mt-4 grid grid-cols-2 gap-2 border-t border-ds-divider pt-4 text-sm sm:grid-cols-4" aria-label="Etapas da edição por voz">
      <li v-for="(label, index) in steps" :key="label" :aria-current="index === current && !downloaded ? 'step' : undefined"
        class="flex items-center gap-2 rounded-md px-2 py-1" :class="index === current && !downloaded ? 'bg-ds-surface font-semibold text-ds-primary-accent' : 'text-ds-text-secondary'">
        <i class="bi" :class="index < current || downloaded ? 'bi-check-circle-fill' : index === current ? 'bi-record-circle' : 'bi-circle'" aria-hidden="true" />
        {{ label }}
        <span v-if="index < current || downloaded" class="sr-only">(concluída)</span>
      </li>
    </ol>
    <p v-if="downloaded" role="status" class="mt-2 text-sm">Download iniciado.</p>
    <p v-if="error" role="alert" class="mt-2 text-sm text-ds-danger">{{ error }}</p>
  </header>
</template>

<style>
.voice-workspace {
  display: grid;
  gap: 1rem;
  grid-template-columns: minmax(0, 1fr);
  grid-template-areas: 'header' 'assistant' 'model' 'review' 'history';
}
.voice-workspace-header { grid-area: header; }
.voice-workspace-model { grid-area: model; min-width: 0; }
.voice-workspace-assistant { grid-area: assistant; min-width: 0; align-self: start; }
.voice-workspace-review { grid-area: review; min-width: 0; }
.voice-workspace-history { grid-area: history; min-width: 0; }
.voice-workspace :is(button, input, textarea, select, summary):focus-visible {
  outline: 2px solid var(--color-ds-primary-accent);
  outline-offset: 2px;
}
.voice-mic-controls { background: var(--color-ds-surface-elevated); }
@media (min-width: 1101px) {
  .voice-workspace {
    grid-template-columns: minmax(0, 1fr) clamp(310px, 29vw, 380px);
    grid-template-areas: 'header header' 'model assistant' 'review assistant' 'history assistant';
    align-items: start;
  }
}
@media (min-width: 1101px), (max-width: 1023px) {
  .voice-workspace-assistant { position: sticky; top: 0; max-height: calc(100dvh - 1rem); overflow: auto; }
}
@media (max-width: 1023px) {
  .voice-workspace-assistant { max-height: 38dvh; z-index: 20; background: var(--color-ds-surface-elevated); }
  .voice-mic-controls { position: sticky; top: 0; z-index: 1; padding-bottom: .5rem; }
  .voice-hidden-column { display: none; }
}
</style>
