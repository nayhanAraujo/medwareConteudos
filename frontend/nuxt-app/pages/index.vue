<template>
  <div>
    <DsPageHeader title="Dashboard Principal" subtitle="Visão geral do sistema MDW - SGC" icon="speedometer2" />
    <DsPageShell>
      <div class="grid grid-cols-1 md:grid-cols-3 gap-6 mb-6">
        <div v-for="stat in stats" :key="stat.label" class="rounded-2xl border border-gray-200 bg-white/80 p-6">
          <p class="text-sm text-gray-600 mb-1">{{ stat.label }}</p>
          <p class="text-lg font-semibold font-manrope text-ds-text">{{ stat.value }}</p>
        </div>
      </div>
      <p class="text-sm text-gray-500">
        Gráficos e estatísticas completas serão portados em iteração futura (endpoints .NET).
      </p>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

const auth = useAuthStore()
const api = useApi()
const healthStatus = ref('Verificando...')

const stats = computed(() => [
  { label: 'API .NET', value: healthStatus.value },
  { label: 'Usuário', value: `${auth.user?.nome || '—'} (${auth.user?.role || '—'})` },
  { label: 'Sessão', value: auth.isAuthenticated ? 'Autenticado' : 'Não autenticado' }
])

onMounted(async () => {
  try {
    const h = await api.get<{ success: boolean; status: string; database: string }>('/api/v1/health')
    healthStatus.value = h.success ? `OK — ${h.database}` : 'Problema'
  } catch {
    healthStatus.value = 'Indisponível (inicie a API .NET na porta 5080)'
  }
})
</script>
