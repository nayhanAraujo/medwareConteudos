<template>
  <section class="rounded-2xl border border-white/70 bg-white/45 backdrop-blur-xl p-6 shadow-ds-float h-full">
    <div class="flex items-start justify-between gap-3 mb-4">
      <div>
        <h2 class="text-lg font-semibold font-manrope text-ds-text mb-1">
          Referências por Ano
        </h2>
        <p class="text-xs text-gray-500 mb-0">Quantidade cadastrada na tabela REFERENCIA</p>
      </div>
      <span class="h-10 w-10 rounded-xl bg-cyan-500/15 text-cyan-600 flex items-center justify-center ring-1 ring-white/70">
        <i class="bi bi-journal-medical" />
      </span>
    </div>
    <p v-if="!series.labels.length" class="text-sm text-gray-500 mb-0">
      Nenhuma referência com ano cadastrado.
    </p>
    <ClientOnly v-else>
      <div class="h-64">
        <canvas ref="canvas" />
      </div>
    </ClientOnly>
  </section>
</template>

<script setup lang="ts">
import type { ReferenciasPorAnoSeries } from '~/composables/useDashboardInsights'

const props = defineProps<{
  series: ReferenciasPorAnoSeries
}>()

const canvas = ref<HTMLCanvasElement | null>(null)
let chart: { destroy: () => void } | null = null

async function renderChart() {
  if (!import.meta.client || !canvas.value || !props.series.labels.length) return
  const {
    Chart,
    BarController,
    BarElement,
    CategoryScale,
    LinearScale,
    Tooltip
  } = await import('chart.js')

  Chart.register(BarController, BarElement, CategoryScale, LinearScale, Tooltip)
  chart?.destroy()
  chart = new Chart(canvas.value, {
    type: 'bar',
    data: {
      labels: props.series.labels,
      datasets: [
        {
          label: 'Nº de Referências',
          data: props.series.totals,
          backgroundColor: 'rgba(8, 145, 178, 0.82)',
          hoverBackgroundColor: 'rgba(14, 116, 144, 0.92)',
          borderRadius: 8
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { display: false }
      },
      scales: {
        x: {
          grid: { display: false },
          ticks: {
            font: { family: 'Inter', size: 11 },
            maxRotation: 45,
            minRotation: 0
          }
        },
        y: {
          beginAtZero: true,
          ticks: {
            precision: 0,
            font: { family: 'Inter', size: 11 }
          },
          grid: { color: 'rgba(148, 163, 184, 0.25)' }
        }
      }
    }
  })
}

watch(canvas, (el) => {
  if (el) void renderChart()
}, { immediate: true })
watch(() => props.series, () => {
  if (canvas.value) void renderChart()
}, { deep: true })
onBeforeUnmount(() => {
  chart?.destroy()
  chart = null
})
</script>
