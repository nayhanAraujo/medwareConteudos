<script setup lang="ts">
withDefaults(defineProps<{
  title?: string
  padding?: boolean
}>(), {
  padding: true
})
</script>

<template>
  <div class="studio-card" :class="{ '!p-0': !padding }">
    <h3 v-if="title" class="mb-4 text-xs font-semibold text-ds-text">{{ title }}</h3>
    <slot />
  </div>
</template>
