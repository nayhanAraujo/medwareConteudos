<template>
  <div>
    <DsPageHeader title="Dashboard Principal" subtitle="Visão geral do sistema MDW - SGC" icon="speedometer2" />
    <DsPageShell>
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
        <div v-for="stat in cards" :key="stat.label" class="rounded-2xl border border-gray-200 bg-white/80 p-6">
          <div class="flex items-center justify-between"><p class="text-sm text-gray-600">{{ stat.label }}</p><i :class="`bi bi-${stat.icon} text-gray-400`" /></div>
          <p class="mt-2 text-3xl font-semibold font-manrope text-ds-text">{{ loading ? '—' : stat.value.toLocaleString('pt-BR') }}</p>
        </div>
      </div>
      <DsAlert v-if="error" variant="danger" class="mt-6">{{ error }}</DsAlert>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })
interface DashboardStats { variaveis: number; referencias: number; formulas: number; scripts: number; relatorios: number; paineis: number; modelosMensagens: number; impressos: number; usuarios: number }
const api = useApi(); const loading = ref(true); const error = ref('')
const stats = ref<DashboardStats>({ variaveis: 0, referencias: 0, formulas: 0, scripts: 0, relatorios: 0, paineis: 0, modelosMensagens: 0, impressos: 0, usuarios: 0 })
const cards = computed(() => [
  { label: 'Variáveis', value: stats.value.variaveis, icon: 'braces' }, { label: 'Referências', value: stats.value.referencias, icon: 'journal-medical' },
  { label: 'Fórmulas', value: stats.value.formulas, icon: 'calculator' }, { label: 'Scripts', value: stats.value.scripts, icon: 'code-square' },
  { label: 'Relatórios', value: stats.value.relatorios, icon: 'file-earmark-text' }, { label: 'Painéis', value: stats.value.paineis, icon: 'bar-chart' },
  { label: 'Modelos de mensagens', value: stats.value.modelosMensagens, icon: 'chat-square-text' }, { label: 'Impressos', value: stats.value.impressos, icon: 'printer' },
  { label: 'Usuários', value: stats.value.usuarios, icon: 'people' }
])
onMounted(async () => { try { const res = await api.get<{ data: DashboardStats }>('/api/web/dashboard/stats'); stats.value = res.data } catch (e) { error.value = e instanceof Error ? e.message : String(e) } finally { loading.value = false } })
</script>
