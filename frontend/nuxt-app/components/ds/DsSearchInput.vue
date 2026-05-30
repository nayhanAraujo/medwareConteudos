<template>
  <div class="mb-8" :class="wrapperClass">
    <div class="relative">
      <span class="absolute inset-y-0 left-0 flex items-center pl-4 pointer-events-none text-gray-400">
        <i class="bi bi-search" />
      </span>
      <input
        :value="modelValue"
        type="text"
        class="w-full pl-11 pr-11 py-3 bg-white border border-gray-200 rounded-2xl text-sm text-ds-text placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-black/10 focus:border-gray-300 transition-all"
        :placeholder="placeholder"
        :aria-label="ariaLabel || placeholder"
        @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
        @keyup.enter="$emit('enter')"
      />
      <button
        v-if="modelValue"
        type="button"
        class="absolute inset-y-0 right-0 flex items-center pr-4 text-gray-400 hover:text-gray-700 transition-colors"
        aria-label="Limpar busca"
        @click="$emit('update:modelValue', '')"
      >
        <i class="bi bi-x-lg" />
      </button>
    </div>
    <p v-if="hint" class="text-xs text-gray-500 mt-2 text-center">
      <i class="bi bi-info-circle me-1" />{{ hint }}
    </p>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  modelValue: string
  placeholder?: string
  hint?: string
  ariaLabel?: string
  wrapperClass?: string
}>()

defineEmits<{
  'update:modelValue': [value: string]
  enter: []
}>()
</script>
