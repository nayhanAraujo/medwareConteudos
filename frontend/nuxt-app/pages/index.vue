<template>
  <div>
    <DsPageHeader
      title="Dashboard Principal"
      subtitle="Visão geral do sistema MDW - SGC"
      icon="speedometer2"
      :show-clock="false"
    >
      <template #actions>
        <DashboardDateRange v-model="period" />
      </template>
    </DsPageHeader>
    <DsPageShell>
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
        <DashboardMetricCard
          v-for="card in cards"
          :key="card.label"
          :label="card.label"
          :value="card.value"
          :icon="card.icon"
          :accent="card.accent"
          :series="card.series"
          :variation="card.variation"
          :loading="loading"
        />
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mt-8">
        <div class="lg:col-span-2">
          <DashboardReferenciasChart :series="referenciasSeries" />
        </div>
        <DashboardActivityFeed :items="activity" />
      </div>

      <DsAlert v-if="error" variant="error" class="mt-6">{{ error }}</DsAlert>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type {
  DashboardActivityItem,
  DashboardPeriod,
  DashboardReferenciasAnoItem,
  DashboardStats
} from '~/composables/useDashboardInsights'

definePageMeta({ layout: 'default' })

const api = useApi()
const auth = useAuthStore()
const { metrics, buildMetricSeries, computeVariation, toReferenciasPorAnoSeries, buildFallbackActivity } = useDashboardInsights()

const loading = ref(true)
const error = ref('')
const period = ref<DashboardPeriod>('30d')
const stats = ref<DashboardStats>({
  variaveis: 0,
  referencias: 0,
  formulas: 0,
  scripts: 0,
  relatorios: 0,
  paineis: 0,
  modelosMensagens: 0,
  impressos: 0,
  usuarios: 0
})
const activity = ref<DashboardActivityItem[]>([])
const referenciasSeries = ref(toReferenciasPorAnoSeries([]))

const cards = computed(() =>
  metrics.map((metric) => {
    const value = stats.value[metric.key]
    const series = buildMetricSeries(metric.label, value, period.value)
    return {
      ...metric,
      value,
      series,
      variation: computeVariation(series)
    }
  })
)

onMounted(async () => {
  try {
    const res = await api.get<{ data: DashboardStats }>('/api/web/dashboard/stats')
    stats.value = res.data
  }
  catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  }
  finally {
    loading.value = false
  }

  try {
    const res = await api.get<{ data: DashboardActivityItem[] }>('/api/web/dashboard/activity')
    activity.value = Array.isArray(res.data) && res.data.length
      ? res.data
      : buildFallbackActivity(auth.user?.nome || 'clinicas')
  }
  catch {
    activity.value = buildFallbackActivity(auth.user?.nome || 'clinicas')
  }

  try {
    const res = await api.get<{ data: DashboardReferenciasAnoItem[] }>('/api/web/dashboard/referencias-por-ano')
    referenciasSeries.value = toReferenciasPorAnoSeries(Array.isArray(res.data) ? res.data : [])
  }
  catch {
    referenciasSeries.value = toReferenciasPorAnoSeries([])
  }
})
</script>
