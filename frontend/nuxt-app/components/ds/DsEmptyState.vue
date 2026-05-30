<template>
  <div class="text-center py-16 animate-fade-in">
    <div class="w-16 h-16 rounded-full bg-ds-surface flex items-center justify-center mx-auto mb-4">
      <i :class="[`bi bi-${icon}`, 'text-2xl text-gray-400']" />
    </div>
    <p class="font-semibold text-ds-text">{{ title }}</p>
    <p v-if="message" class="text-sm text-gray-600 mt-1">{{ message }}</p>
    <DsButton v-if="actionLabel" variant="secondary" size="sm" class="mt-4" @click="$emit('action')">
      {{ actionLabel }}
    </DsButton>
  </div>
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    title: string
    message?: string
    icon?: string
    actionLabel?: string
  }>(),
  { icon: 'search' }
)

defineEmits<{ action: [] }>()
</script>
