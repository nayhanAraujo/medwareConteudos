<script setup lang="ts">
const props = defineProps<{
  text: string
}>()

const lines = computed(() => props.text.split('\n'))

function lineClass(line: string) {
  const trimmed = line.trim()
  if (trimmed.startsWith('[') && trimmed.endsWith(']')) {
    return 'font-semibold text-ds-primary-accent'
  }
  if (trimmed.startsWith('*')) {
    return 'text-ds-text italic'
  }
  if (trimmed.includes('[Lista]') || trimmed.includes('[Única]') || trimmed.includes('[Múltipla]')) {
    return 'text-ds-text'
  }
  return 'text-ds-text'
}
</script>

<template>
  <div
    class="overflow-hidden rounded-ds-sm border border-ds-border bg-ds-surface-elevated"
    role="region"
    aria-label="Preview TXT modo texto"
  >
    <div
      v-if="!text"
      class="flex min-h-[12rem] flex-col items-center justify-center gap-2 p-6 text-center text-sm text-ds-muted"
    >
      <i class="bi bi-file-text text-3xl opacity-60" aria-hidden="true" />
      <p>O TXT gerado aparecerá aqui com numeração de linhas e seções destacadas.</p>
    </div>

    <div v-else class="max-h-96 overflow-auto">
      <div
        v-for="(line, index) in lines"
        :key="index"
        class="flex gap-3 border-b border-ds-border/40 px-3 py-1 last:border-b-0 hover:bg-ds-surface/60"
      >
        <span class="w-8 shrink-0 select-none text-right text-[10px] leading-5 text-ds-muted">
          {{ index + 1 }}
        </span>
        <span class="min-w-0 flex-1 whitespace-pre-wrap break-words font-mono text-xs leading-5" :class="lineClass(line)">
          {{ line || ' ' }}
        </span>
      </div>
    </div>
  </div>
</template>
