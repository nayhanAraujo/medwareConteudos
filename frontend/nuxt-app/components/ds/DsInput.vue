<template>
  <div>
    <label v-if="label" :for="inputId" class="block text-sm font-medium text-ds-text mb-1.5">{{ label }}</label>
    <input
      :id="inputId"
      :value="modelValue"
      :type="type"
      :placeholder="placeholder"
      :disabled="disabled"
      :required="required"
      :autocomplete="autocomplete"
      class="w-full px-4 py-3 bg-white border border-gray-200 rounded-2xl text-sm text-ds-text placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-black/10 focus:border-gray-300 transition-all disabled:bg-gray-50 disabled:text-gray-500"
      :class="inputClass"
      @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
      @keyup.enter="$emit('enter')"
    />
    <p v-if="hint" class="text-xs text-gray-500 mt-1">{{ hint }}</p>
  </div>
</template>

<script setup lang="ts">
const props = defineProps<{
  modelValue?: string | number
  label?: string
  type?: string
  placeholder?: string
  hint?: string
  disabled?: boolean
  required?: boolean
  autocomplete?: string
  inputClass?: string
  id?: string
}>()

defineEmits<{
  'update:modelValue': [value: string]
  enter: []
}>()

const inputId = computed(() => props.id || `ds-input-${Math.random().toString(36).slice(2, 9)}`)
</script>
