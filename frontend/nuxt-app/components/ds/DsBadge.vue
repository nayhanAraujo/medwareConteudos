<template>
  <span
    class="inline-flex items-center rounded-full text-xs font-medium px-3 py-1"
    :class="variantClass"
  >
    <slot>{{ label }}</slot>
  </span>
</template>

<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    label?: string
    variant?: 'default' | 'primary' | 'success' | 'warning' | 'danger' | 'dark'
  }>(),
  { variant: 'default' }
)

const variantClass = computed(() => {
  const map = {
    default: 'bg-ds-surface text-ds-text',
    primary: 'bg-blue-600 text-white',
    success: 'bg-green-600 text-white',
    warning: 'bg-orange-500 text-white',
    danger: 'bg-rose-600 text-white',
    dark: 'bg-gray-800 text-white'
  }
  return map[props.variant]
})
</script>
