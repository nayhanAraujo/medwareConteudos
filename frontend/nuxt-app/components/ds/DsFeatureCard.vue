<template>
  <div class="rounded-2xl border bg-white/80 p-6 h-full flex flex-col" :class="borderClass">
    <div class="flex items-center gap-3 mb-4">
      <div class="w-10 h-10 rounded-full flex items-center justify-center" :class="iconBgClass">
        <i :class="[`bi bi-${icon}`, iconColorClass]" />
      </div>
      <h3 class="font-semibold font-manrope text-ds-text m-0">{{ title }}</h3>
    </div>
    <p v-if="desc" class="text-sm text-gray-600 mb-4">{{ desc }}</p>
    <ul v-if="features?.length" class="space-y-2 mb-4 flex-1">
      <li v-for="f in features" :key="f" class="flex items-start gap-2 text-sm text-gray-700">
        <i class="bi bi-check-circle-fill text-green-600 mt-0.5" />
        {{ f }}
      </li>
    </ul>
    <slot />
  </div>
</template>

<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    title: string
    desc?: string
    icon: string
    theme?: 'blue' | 'green' | 'orange' | 'purple'
    features?: string[]
  }>(),
  { theme: 'blue' }
)

const themeMap = {
  blue: { border: 'border-blue-100', iconBg: 'bg-blue-100', iconColor: 'text-blue-600' },
  green: { border: 'border-green-100', iconBg: 'bg-green-100', iconColor: 'text-green-600' },
  orange: { border: 'border-orange-100', iconBg: 'bg-orange-100', iconColor: 'text-orange-600' },
  purple: { border: 'border-purple-100', iconBg: 'bg-purple-100', iconColor: 'text-purple-600' }
}

const borderClass = computed(() => themeMap[props.theme].border)
const iconBgClass = computed(() => themeMap[props.theme].iconBg)
const iconColorClass = computed(() => themeMap[props.theme].iconColor)
</script>
