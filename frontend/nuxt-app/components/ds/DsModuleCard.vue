<template>
  <div class="rounded-2xl border border-gray-200 bg-white p-6 h-full flex flex-col transition-all hover:shadow-lg hover:border-gray-300">
    <div class="w-14 h-14 rounded-xl flex items-center justify-center mb-4 text-white text-xl" :style="{ backgroundColor: iconColor }">
      <i :class="icon" />
    </div>
    <h5 class="font-semibold font-manrope text-ds-text mb-2">{{ title }}</h5>
    <p class="text-sm text-gray-600 mb-4 flex-1">{{ desc }}</p>
    <div class="flex flex-wrap gap-2">
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  title: string
  desc: string
  icon: string
  iconColor?: string
}>()
</script>
