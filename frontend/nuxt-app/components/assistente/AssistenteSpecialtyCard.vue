<template>
  <button
    type="button"
    class="group flex min-h-28 w-full flex-col rounded-lg border bg-white p-4 text-left shadow-sm transition hover:-translate-y-0.5 hover:border-gray-400 hover:shadow-md focus:outline-none focus-visible:ring-2 focus-visible:ring-black/20"
    :class="selected ? 'border-gray-900 ring-1 ring-gray-900' : 'border-gray-200'"
    :aria-pressed="selected"
    @click="$emit('select')"
  >
    <span class="flex w-full min-w-0 items-start gap-3">
      <span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-gray-100 text-gray-700 group-hover:bg-gray-200">
        <i :class="`bi bi-${icon}`" />
      </span>
      <span class="min-w-0 flex-1 break-words text-sm font-semibold uppercase leading-5 text-ds-text">{{ group.nome }}</span>
      <i class="bi bi-chevron-right shrink-0 text-sm text-gray-400" />
    </span>
    <span class="mt-auto pt-4">
      <DsBadge variant="primary" class="!px-2.5 !py-1.5">
        {{ group.totalScripts }} {{ group.totalScripts === 1 ? 'modelo' : 'modelos' }}
      </DsBadge>
    </span>
  </button>
</template>

<script setup lang="ts">
import type { AssistenteScriptEspecialidadeGroup } from '~/composables/useAssistenteApi'

const props = defineProps<{
  group: AssistenteScriptEspecialidadeGroup
  selected?: boolean
}>()

defineEmits<{ select: [] }>()

const icon = computed(() => {
  const value = props.group.nome.toLocaleLowerCase('pt-BR')
  if (value.includes('cardio')) return 'heart-pulse-fill'
  if (value.includes('pediatr')) return 'emoji-smile-fill'
  if (value.includes('oftalmo')) return 'eye-fill'
  if (value.includes('ultra') || value.includes('imagem')) return 'soundwave'
  if (value.includes('sem especialidade')) return 'folder2-open'
  return 'clipboard2-pulse-fill'
})
</script>
