<template>
  <section class="rounded-2xl border border-white/70 bg-white/45 backdrop-blur-xl p-6 shadow-ds-float h-full">
    <div class="flex items-start justify-between gap-3 mb-4">
      <div>
        <h2 class="text-lg font-semibold font-manrope text-ds-text mb-1">Painel de Atividade Recente</h2>
        <p class="text-xs text-gray-500 mb-0">Últimas ações registradas no sistema</p>
      </div>
      <span class="h-10 w-10 rounded-xl bg-violet-500/15 text-violet-600 flex items-center justify-center ring-1 ring-white/70">
        <i class="bi bi-activity" />
      </span>
    </div>
    <ul class="m-0 p-0 list-none space-y-3 max-h-64 overflow-y-auto">
      <li
        v-for="(item, index) in items"
        :key="`${item.time}-${index}`"
        class="flex items-start gap-3 rounded-xl border border-white/60 bg-white/40 px-3 py-2.5"
      >
        <span class="mt-0.5 h-8 w-8 rounded-lg bg-white/70 text-indigo-600 flex items-center justify-center shrink-0">
          <i :class="`bi bi-${item.icon || 'lightning-charge'}`" />
        </span>
        <div class="min-w-0">
          <p class="mb-0 text-sm text-ds-text leading-snug">{{ item.message }}</p>
        </div>
      </li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import type { DashboardActivityItem } from '~/composables/useDashboardInsights'

defineProps<{
  items: DashboardActivityItem[]
}>()
</script>
