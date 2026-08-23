<template>
  <div class="inline-flex flex-wrap rounded-full border border-white/70 bg-white/45 backdrop-blur-md p-1 shadow-ds-float">
    <button
      v-for="option in options"
      :key="option.value"
      type="button"
      class="px-3 py-1.5 text-xs font-semibold rounded-full transition-all border-0"
      :class="
        model === option.value
          ? 'bg-white text-ds-text shadow-sm'
          : 'bg-transparent text-gray-600 hover:text-ds-text'
      "
      @click="model = option.value"
    >
      {{ option.label }}
    </button>
  </div>
</template>

<script setup lang="ts">
import type { DashboardPeriod } from '~/composables/useDashboardInsights'

const model = defineModel<DashboardPeriod>({ default: '30d' })

const options: { value: DashboardPeriod; label: string }[] = [
  { value: 'hoje', label: 'Hoje' },
  { value: 'ontem', label: 'Ontem' },
  { value: '7d', label: '7 Dias' },
  { value: '30d', label: '30 Dias' }
]
</script>
