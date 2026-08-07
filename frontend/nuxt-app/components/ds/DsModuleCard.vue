<template>
  <component
    :is="to ? NuxtLink : 'div'"
    v-bind="to ? { to } : {}"
    class="rounded-2xl border border-gray-200 bg-white p-6 h-full flex flex-col transition-all hover:shadow-lg hover:border-gray-300"
    :class="to ? 'cursor-pointer no-underline text-inherit focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-black/20' : ''"
  >
    <div class="w-14 h-14 rounded-xl flex items-center justify-center mb-4 text-white text-xl" :style="{ backgroundColor: iconColor || '#111827' }">
      <i :class="normalizedIcon" aria-hidden="true" />
    </div>
    <h5 class="font-semibold font-manrope text-ds-text mb-2">{{ title }}</h5>
    <p class="text-sm text-gray-600 mb-4 flex-1">{{ desc ?? description }}</p>
    <div class="flex flex-wrap gap-2">
      <slot />
    </div>
  </component>
</template>

<script setup lang="ts">
import { NuxtLink } from '#components'

const props = defineProps<{
  title: string
  desc?: string
  description?: string
  icon: string
  iconColor?: string
  to?: string | Record<string, unknown>
}>()

const normalizedIcon = computed(() => props.icon.startsWith('bi ') ? props.icon : `bi bi-${props.icon}`)
</script>
