<template>
  <div>
    <label v-if="label" :for="inputId" class="block text-sm font-medium text-ds-text mb-1.5">{{ label }}</label>
    <select
      :id="inputId"
      :value="modelValue"
      :disabled="disabled"
      :required="required"
      class="w-full px-4 py-3 bg-white border border-gray-200 rounded-2xl text-sm text-ds-text focus:outline-none focus:ring-2 focus:ring-black/10 focus:border-gray-300 transition-all disabled:bg-gray-50"
      :class="inputClass"
      @change="$emit('update:modelValue', ($event.target as HTMLSelectElement).value)"
    >
      <slot />
    </select>
    <p v-if="hint" class="text-xs text-gray-500 mt-1">{{ hint }}</p>
  </div>
</template>

<script setup lang="ts">
const props = defineProps<{
  modelValue?: string | number
  label?: string
  hint?: string
  disabled?: boolean
  required?: boolean
  inputClass?: string
  id?: string
}>()

defineEmits<{ 'update:modelValue': [value: string] }>()

const inputId = computed(() => props.id || `ds-select-${Math.random().toString(36).slice(2, 9)}`)
</script>
