<template>
  <div>
    <LayoutAppPageHeader
      title="Dashboard Principal"
      subtitle="Visão geral do sistema MDW - SGC"
      icon="speedometer2"
    />
    <div class="row g-3 mb-4">
      <div class="col-md-4">
        <div class="card shadow-sm h-100">
          <div class="card-body">
            <h6 class="text-muted">API .NET</h6>
            <p class="mb-0 fw-semibold">{{ healthStatus }}</p>
          </div>
        </div>
      </div>
      <div class="col-md-4">
        <div class="card shadow-sm h-100">
          <div class="card-body">
            <h6 class="text-muted">Usuário</h6>
            <p class="mb-0 fw-semibold">{{ auth.user?.nome }} ({{ auth.user?.role }})</p>
          </div>
        </div>
      </div>
      <div class="col-md-4">
        <div class="card shadow-sm h-100">
          <div class="card-body">
            <h6 class="text-muted">Sessão</h6>
            <p class="mb-0 fw-semibold">{{ auth.isAuthenticated ? 'Autenticado' : 'Não autenticado' }}</p>
          </div>
        </div>
      </div>
    </div>
    <p class="text-muted small mt-2">
      Gráficos e estatísticas completas serão portados em iteração futura (endpoints .NET).
    </p>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

const auth = useAuthStore()
const api = useApi()
const healthStatus = ref('Verificando...')

onMounted(async () => {
  try {
    const h = await api.get<{ success: boolean; status: string; database: string }>('/api/v1/health')
    healthStatus.value = h.success ? `OK — ${h.database}` : 'Problema'
  } catch {
    healthStatus.value = 'Indisponível (inicie a API .NET na porta 5080)'
  }
})
</script>
