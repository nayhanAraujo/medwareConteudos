<template>
  <fieldset>
    <legend class="mb-1.5 text-sm font-medium text-ds-text">{{ label }}</legend>
    <div class="max-h-52 space-y-1 overflow-y-auto rounded-2xl border border-gray-200 bg-gray-50 p-3">
      <label v-for="option in options" :key="option.id" class="flex cursor-pointer items-center gap-2 rounded-lg border border-transparent bg-white px-2 py-1.5 text-ds-text shadow-sm transition hover:border-gray-200 hover:bg-gray-100">
        <input v-model="selected" type="checkbox" :value="option.id" :disabled="disabled" class="rounded border-gray-300 text-ds-primary focus:ring-ds-primary">
        <span class="text-sm">{{ option.nome }}</span>
      </label>
      <p v-if="!options.length" class="text-sm text-gray-500">Nenhuma opção disponível.</p>
    </div>
    <p v-if="hint" class="mt-1 text-xs text-gray-500">{{ hint }}</p>
  </fieldset>
</template>

<script setup lang="ts">
import type { AssistenteOption } from '~/composables/useAssistenteApi'
const props = defineProps<{ modelValue: number[]; label: string; options: AssistenteOption[]; hint?: string; disabled?: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: number[]] }>()
const selected = computed({ get: () => props.modelValue, set: value => emit('update:modelValue', value) })
</script>
