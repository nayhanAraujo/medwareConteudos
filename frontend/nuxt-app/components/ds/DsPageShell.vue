<template>
  <div class="font-sans bg-ds-page rounded-2xl px-4 py-6 md:px-6 md:py-8 -mx-1">
    <div
      class="bg-white/50 backdrop-blur-md rounded-3xl p-6 md:p-10 lg:p-12 shadow-ds border border-white/60"
      :class="panelClass"
    >
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  panelClass?: string
}>()
</script>
