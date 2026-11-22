<script setup lang="ts">
type Metrics = { analyzed: number | null; waiting: number | null; deviations: number | null; critical: number | null; treated: number | null }
import type { ChatfastPublicReport as Report } from '~/utils/chatfastReport'
const props = defineProps<{ type: 'diario' | 'semanal' }>()
const config = useRuntimeConfig()
const route = useRoute()
const demonstration = computed(() => import.meta.dev && route.query.demo === '1')
const date = computed(() => typeof route.query.date === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(route.query.date) ? route.query.date : undefined)
const baseURL = import.meta.server ? config.apiServerBase : config.public.apiBase
// No useApi/auth store, bearer header, user cookie forwarding or private report endpoint here.
const { data, status, error, refresh } = await useAsyncData(`chatfast-public-${props.type}`, () =>
  $fetch<{ success: boolean; data: Report | null }>(demonstration.value ? `/api/publico/chatfast/v1/demonstracao/${props.type}` : `/api/publico/chatfast/v1/relatorios/${props.type}`, {
    baseURL: String(baseURL), query: date.value ? { date: date.value } : undefined, credentials: 'omit'
  }), { watch: [date, demonstration] })
const report = computed(() => data.value?.data)
const labels: { key: keyof Metrics; label: string; note: string }[] = [
  { key: 'analyzed', label: 'Analisados', note: 'Atendimentos avaliados pelo bot' },
  { key: 'waiting', label: 'Em acompanhamento', note: 'Aguardando o próximo passo' },
  { key: 'deviations', label: 'Desvios identificados', note: 'Ocorrências no período' },
  { key: 'critical', label: 'Críticos', note: 'Ocorrências de maior prioridade' },
  { key: 'treated', label: 'Marcados como tratados', note: 'Marcação recebida, não comprova resolução' }
]
const formatted = (v: string) => new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short', timeZone: 'America/Sao_Paulo' }).format(new Date(v))
function chooseDate(event: Event) {
  const value = (event.target as HTMLInputElement).value
  navigateTo({ path: route.path, query: { ...(value ? { date: value } : {}), ...(demonstration.value ? { demo: '1' } : {}) } })
}
useHead({ title: `ChatFast · ${props.type === 'diario' ? 'Diário' : 'Semanal'} | MDW` })
</script>

<template>
  <main class="chatfast-public">
    <div class="dashboard-shell">
      <header>
        <div><p class="eyebrow">MDW · AUTOMAÇÕES</p><h1>ChatFast</h1><p>Visão {{ type === 'diario' ? 'diária' : 'semanal' }} dos atendimentos</p></div>
        <nav aria-label="Relatórios ChatFast"><NuxtLink :to="{ path: '/automacoes/chatfast/diario', query: demonstration ? { demo: '1' } : {} }" :aria-current="type === 'diario' ? 'page' : undefined">Diário</NuxtLink><NuxtLink :to="{ path: '/automacoes/chatfast/semanal', query: demonstration ? { demo: '1' } : {} }" :aria-current="type === 'semanal' ? 'page' : undefined">Semanal</NuxtLink></nav>
      </header>
      <p v-if="demonstration" class="notice" role="status">Demonstração local · dados históricos dos arquivos fornecidos · sem gravação no banco. Não representa acompanhamento ao vivo.</p>
      <div class="toolbar"><span>Indicadores globais · sem dados pessoais</span><label v-if="!demonstration">{{ type === 'diario' ? 'Data' : 'Início da semana' }} <input type="date" :value="date" @change="chooseDate" /></label></div>
      <p v-if="status === 'pending'" role="status">Carregando indicadores…</p>
      <section v-else-if="error" class="notice" role="alert"><h2>Relatório temporariamente indisponível</h2><p>Tente novamente em alguns instantes.</p><button @click="refresh()">Tentar novamente</button></section>
      <section v-else-if="!report" class="notice"><h2>Ainda não há relatório disponível</h2><p>Aguardando uma publicação completa para o período selecionado.</p></section>
      <template v-else>
        <div class="period"><div><strong>{{ formatted(report.periodStart) }} — {{ formatted(report.periodEnd) }}</strong><p>Última geração: {{ formatted(report.generatedAt) }} · horário de Brasília</p></div><span :class="['badge', { stale: report.stale }]">{{ report.stale ? 'Atualização pendente' : 'Publicado' }}</span></div>
        <p v-if="report.stale" class="notice" role="status">Exibindo o último relatório completo. Os dados podem estar desatualizados.</p>
        <AutomacoesChatfastSummary v-if="report.summary" :summary="report.summary" />
        <section v-else class="metric-grid" aria-label="Indicadores do período"><article v-for="item in labels" :key="item.key"><h2>{{ item.label }}</h2><p class="metric">{{ report.metrics[item.key] == null ? '—' : report.metrics[item.key]?.toLocaleString('pt-BR') }}</p><p>{{ report.metrics[item.key] == null ? 'Não informado pelo produtor' : item.note }}</p></article></section>
      </template>
      <footer>Os indicadores refletem a análise do bot. A marcação “tratado” não encerra automaticamente um caso nem confirma sua resolução.</footer>
    </div>
  </main>
</template>

<style scoped>
.chatfast-public{min-height:100vh;background:#f4f7fb;color:#182b43;padding:48px 24px;font-family:Inter,system-ui,sans-serif}.dashboard-shell{max-width:1120px;margin:auto}header,.toolbar,.period{display:flex;justify-content:space-between;align-items:center;gap:24px}header{padding-bottom:28px;border-bottom:1px solid #dce4ed}h1{font-size:40px;letter-spacing:-1.5px;font-weight:750;margin:4px 0 8px}.eyebrow{font-size:11px;font-weight:700;letter-spacing:2px;color:#48788b}p{margin:6px 0;color:#52677e}nav{display:flex;gap:8px}nav a,button{border:1px solid #d8e2ed;border-radius:24px;padding:9px 18px;text-decoration:none;color:#254460;background:white}nav a[aria-current=page]{background:#163e55;color:white;border-color:#163e55}.toolbar{margin:24px 0;font-size:13px}.toolbar label{display:flex;align-items:center;gap:10px}input{padding:8px;border:1px solid #d8e2ed;border-radius:8px;background:white}.period{margin:32px 0 24px}.period p{font-size:12px}.badge{border-radius:20px;background:#e3f3ec;color:#28684c;padding:6px 12px;font-size:12px;white-space:nowrap}.badge.stale{background:#fff1da;color:#875417}.metric-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px}.metric-grid article,.notice{background:white;border:1px solid #e1e7ee;border-radius:18px;padding:24px}.metric-grid h2,.notice h2{font-size:15px;font-weight:650;margin:0}.metric-grid .metric{font-size:42px;font-weight:700;color:#193d55;line-height:1.3;margin:16px 0 8px}.metric-grid article>p:last-child{font-size:12px;line-height:1.6}.notice{margin:20px 0}.notice button{margin-top:12px;cursor:pointer}footer{margin-top:32px;font-size:12px;color:#65788b;line-height:1.7;border-top:1px solid #dce4ed;padding-top:20px}@media(max-width:700px){.chatfast-public{padding:28px 16px}.metric-grid{grid-template-columns:repeat(2,minmax(0,1fr))}header,.toolbar,.period{align-items:flex-start;flex-direction:column;gap:14px}.metric-grid article{padding:18px}.metric-grid .metric{font-size:34px}}@media(max-width:390px){.metric-grid{grid-template-columns:1fr}}
</style>
