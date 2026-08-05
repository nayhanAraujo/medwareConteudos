<script setup lang="ts">
defineProps<{
  title: string
  subtitle?: string
  icon?: string
}>()
</script>

<template>
  <div class="studio-header">
    <div class="mx-auto max-w-7xl px-4 py-6 sm:px-6">
      <div class="flex items-start gap-3">
        <div
          v-if="icon"
          class="flex h-8 w-8 items-center justify-center text-ds-text"
        >
          <i :class="['bi', icon, 'text-xl']" />
        </div>
        <div>
          <h1 class="text-ds-text">{{ title }}</h1>
          <p v-if="subtitle" class="studio-subtitle mt-1">{{ subtitle }}</p>
        </div>
      </div>
    </div>
  </div>
</template>
