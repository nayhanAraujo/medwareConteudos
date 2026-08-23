<template>
  <div class="font-sans rounded-2xl px-4 py-6 md:px-6 md:py-8 -mx-1">
    <div
      class="bg-white/40 backdrop-blur-xl rounded-3xl p-6 md:p-10 lg:p-12 shadow-ds-studio border border-white/70 ring-1 ring-white/50"
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
