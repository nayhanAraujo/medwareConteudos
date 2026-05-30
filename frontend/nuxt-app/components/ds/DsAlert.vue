<template>
  <div class="rounded-2xl px-4 py-3 text-sm border" :class="variantClass" role="alert">
    <div class="flex items-start gap-2">
      <i v-if="icon" :class="[`bi bi-${icon}`, 'mt-0.5']" />
      <div class="flex-1">
        <p v-if="title" class="font-semibold mb-1">{{ title }}</p>
        <slot />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    variant?: 'info' | 'success' | 'warning' | 'error'
    title?: string
    icon?: string
  }>(),
  { variant: 'info' }
)

const variantClass = computed(() => {
  const map = {
    info: 'bg-blue-50 border-blue-100 text-blue-900',
    success: 'bg-green-50 border-green-100 text-green-900',
    warning: 'bg-orange-50 border-orange-100 text-orange-900',
    error: 'bg-rose-50 border-rose-100 text-rose-900'
  }
  return map[props.variant]
})

const icon = computed(() => {
  if (props.icon) return props.icon
  const map = { info: 'info-circle', success: 'check-circle', warning: 'exclamation-triangle', error: 'x-circle' }
  return map[props.variant]
})
</script>
