<script setup lang="ts">
const props = withDefaults(defineProps<{
  elapsedMs: number
  percent: number
  format?: 'html' | 'modoTexto' | 'jsonStudio'
  kind?: 'conversion' | 'voice-build' | 'voice-export'
}>(), {
  kind: 'conversion'
})

const elapsedLabel = computed(() => {
  const total = Math.max(0, Math.floor(props.elapsedMs / 1000))
  const minutes = Math.floor(total / 60)
  const seconds = total % 60
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
})

const stage = computed(() => {
  const seconds = props.elapsedMs / 1000
  if (props.kind === 'voice-build') {
    if (seconds < 8) return 'Enviando a fala ao agente...'
    if (seconds < 25) return 'Interpretando seções e medidas...'
    if (seconds < 50) return 'Montando o modelo JSON Studio...'
    return 'Ainda gerando o modelo. Isso pode levar cerca de um minuto...'
  }
  if (props.kind === 'voice-export') {
    if (seconds < 10) return 'Exportando TXT modo texto...'
    if (seconds < 30) return 'Validando o laudo gerado...'
    return 'Ainda exportando. Isso pode levar alguns minutos...'
  }
  if (seconds < 8) return 'Enviando imagem...'
  if (props.format === 'modoTexto' || props.format === 'jsonStudio') {
    if (seconds < 25) return 'Analisando campos e seções...'
    if (seconds < 60) return props.format === 'jsonStudio' ? 'Preparando modelo JSON Studio...' : 'Gerando DSL modo texto...'
    return 'Ainda processando. Isso pode levar alguns minutos...'
  }
  if (seconds < 25) return 'Analisando o layout do laudo...'
  if (seconds < 60) return 'Gerando HTML com o Composer...'
  return 'Ainda processando. Isso pode levar alguns minutos...'
})

const barWidth = computed(() => `${Math.min(100, Math.max(4, props.percent))}%`)
</script>

<template>
  <div
    class="rounded-ds-sm border border-ds-primary-accent/40 bg-ds-surface-elevated px-4 py-3"
    role="status"
    aria-live="polite"
    aria-busy="true"
  >
    <div class="mb-2 flex items-center justify-between gap-3">
      <p class="flex items-center gap-2 text-sm text-ds-text">
        <i class="bi bi-hourglass-split text-ds-primary-accent" />
        <span>{{ stage }}</span>
      </p>
      <p class="font-mono text-sm font-semibold tabular-nums text-ds-text" aria-label="Tempo decorrido">
        {{ elapsedLabel }}
      </p>
    </div>
    <div
      class="h-2 overflow-hidden rounded-full border border-ds-border bg-ds-media"
      role="progressbar"
      :aria-valuenow="Math.round(percent)"
      aria-valuemin="0"
      aria-valuemax="100"
      :aria-valuetext="`Tempo decorrido ${elapsedLabel}`"
    >
      <div class="studio-progress-fill h-full rounded-full" :style="{ width: barWidth }" />
    </div>
    <p class="mt-2 text-xs text-ds-muted">
      {{ kind.startsWith('voice')
        ? 'Tempo decorrido · o agente pode levar cerca de um minuto'
        : 'Tempo decorrido · a conversão pode levar alguns minutos' }}
    </p>
  </div>
</template>
