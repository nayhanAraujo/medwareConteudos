<template>
  <article
    class="rounded-2xl border bg-white/45 backdrop-blur-xl p-6 transition-transform duration-200 hover:-translate-y-0.5"
    :class="[accent.border, accent.glow]"
  >
    <div class="flex items-start justify-between gap-3">
      <p class="text-sm text-gray-600 mb-0">{{ label }}</p>
      <span
        class="h-10 w-10 rounded-xl flex items-center justify-center shadow-inner ring-1 ring-white/70"
        :class="accent.iconBg"
      >
        <i :class="`bi bi-${icon} text-lg ${accent.iconColor}`" />
      </span>
    </div>
    <div class="mt-3 flex items-end gap-2">
      <p class="mb-0 text-3xl font-semibold font-manrope text-ds-text leading-none">
        {{ loading ? '—' : value.toLocaleString('pt-BR') }}
      </p>
      <span
        v-if="!loading"
        class="text-xs font-semibold mb-0.5"
        :class="variation >= 0 ? 'text-emerald-600' : 'text-rose-600'"
      >
        {{ variation >= 0 ? '+' : '' }}{{ variation }}%
      </span>
    </div>
    <div class="mt-4">
      <DashboardSparkline :values="series" :color="accent.spark" />
    </div>
  </article>
</template>

<script setup lang="ts">
import type { DashboardAccent } from '~/composables/useDashboardInsights'

defineProps<{
  label: string
  value: number
  icon: string
  accent: DashboardAccent
  series: number[]
  variation: number
  loading?: boolean
}>()
</script>
