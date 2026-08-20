<script setup lang="ts">
import type { VoiceTranscriptEntry } from '~/types/voice'

defineProps<{
  history: VoiceTranscriptEntry[]
}>()
</script>

<template>
  <StudioDsCard title="Histórico de falas">
    <div v-if="!history.length" class="text-sm text-ds-muted">
      Nenhuma fala enviada ainda.
    </div>
    <div v-else class="max-h-64 space-y-3 overflow-y-auto">
      <div
        v-for="(entry, index) in history"
        :key="`${entry.at}-${index}`"
        class="rounded-ds-btn border border-ds-divider bg-ds-surface p-3"
      >
        <p class="text-xs text-ds-muted">{{ new Date(entry.at).toLocaleString('pt-BR') }}</p>
        <p class="mt-1 text-sm text-ds-text">"{{ entry.transcript }}"</p>
        <p class="mt-2 text-xs text-ds-primary-accent">{{ entry.summary }}</p>
      </div>
    </div>
  </StudioDsCard>
</template>
