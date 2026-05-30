<template>
  <div>
    <label v-if="label" :for="inputId" class="block text-sm font-medium text-ds-text mb-1.5">{{ label }}</label>
    <textarea
      :id="inputId"
      :value="modelValue"
      :placeholder="placeholder"
      :disabled="disabled"
      :required="required"
      :rows="rows"
      class="w-full px-4 py-3 bg-white border border-gray-200 rounded-2xl text-sm text-ds-text placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-black/10 focus:border-gray-300 transition-all disabled:bg-gray-50 resize-y"
      :class="inputClass"
      @input="$emit('update:modelValue', ($event.target as HTMLTextAreaElement).value)"
    />
    <p v-if="hint" class="text-xs text-gray-500 mt-1">{{ hint }}</p>
  </div>
</template>

<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    modelValue?: string
    label?: string
    placeholder?: string
    hint?: string
    disabled?: boolean
    required?: boolean
    rows?: number
    inputClass?: string
    id?: string
  }>(),
  { rows: 4 }
)

defineEmits<{ 'update:modelValue': [value: string] }>()

const inputId = computed(() => props.id || `ds-textarea-${Math.random().toString(36).slice(2, 9)}`)
</script>
