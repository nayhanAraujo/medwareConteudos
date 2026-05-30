<template>
  <div
    class="flex flex-col items-center border rounded-2xl px-6 py-8 transition-all cursor-pointer animate-slide-up h-full relative"
    :class="[
      theme.gradient,
      theme.border,
      theme.dark ? 'hover:shadow-ds-card text-white' : 'hover:shadow-lg',
      delayIndex !== undefined ? delayClass(delayIndex) : ''
    ]"
    role="button"
    tabindex="0"
    :aria-label="title"
    :title="desc"
    @click="$emit('click')"
    @keydown.enter="$emit('click')"
  >
    <span
      v-if="badge"
      class="absolute top-3 right-3 rounded-full text-xs font-medium px-3 py-1"
      :class="theme.badgeBg || 'bg-ds-surface text-ds-text'"
    >
      {{ badge }}
    </span>

    <div class="w-12 h-12 rounded-full flex items-center justify-center mb-4" :class="theme.iconBg">
      <i :class="[`bi bi-${icon}`, theme.iconColor, 'text-xl']" />
    </div>

    <h3 class="font-semibold font-sans text-center" :class="theme.dark ? 'text-white' : 'text-ds-text'">
      {{ title }}
    </h3>
    <p class="text-sm mt-1 text-center" :class="theme.dark ? 'text-gray-300' : 'text-gray-600'">
      {{ desc }}
    </p>
    <slot />
  </div>
</template>

<script setup lang="ts">
import type { DsCardTheme, DsThemeName } from '~/composables/useDsTheme'

const props = defineProps<{
  title: string
  desc?: string
  icon: string
  themeName?: DsThemeName
  theme?: DsCardTheme
  badge?: string
  delayIndex?: number
}>()

defineEmits<{ click: [] }>()

const { getTheme, delayClass } = useDsTheme()
const theme = computed(() => props.theme || getTheme(props.themeName || 'blue'))
</script>
