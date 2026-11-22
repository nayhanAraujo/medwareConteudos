<script setup lang="ts">
import { chatfastStatistics, chatfastMethods, chatfastLimitations, formatChatfastNumber as number } from '~/utils/chatfastReport'
import type { ChatfastSummary } from '~/utils/chatfastReport'
const props = defineProps<{ summary: ChatfastSummary }>()
const maxVolume = computed(() => Math.max(1, ...props.summary.dailyVolume.map(p => p.events)))
const date = (v: string) => v.split('-').reverse().join('/')
</script>
<template>
  <div class="extended-summary">
    <section class="stats" aria-label="Indicadores globais detalhados">
      <article v-for="stat in summary.statistics" :key="stat.code"><h2>{{ chatfastStatistics[stat.code]?.label || 'Indicador' }}</h2><p class="value">{{ number(stat.value) }} <small>{{ chatfastStatistics[stat.code]?.unit }}</small></p><p v-if="stat.value == null">Não informado pelo produtor</p><p v-if="stat.previousValue != null">Período anterior: {{ number(stat.previousValue) }} {{ chatfastStatistics[stat.code]?.unit }}</p></article>
    </section>
    <div class="charts">
      <section v-if="summary.dailyVolume.length" class="block"><h2>Volume diário de eventos</h2><p>Eventos do histórico, não conversas únicas.</p><div v-for="point in summary.dailyVolume" :key="point.date" class="bar-row"><span>{{ date(point.date) }}</span><div class="track"><div class="bar" :style="{ width: `${point.events / maxVolume * 100}%` }" /></div><strong>{{ number(point.events) }}</strong></div></section>
      <section v-if="summary.trend.length" class="block"><h2>Evolução do índice global</h2><p>Desvios a cada 100 conversas. Compare considerando as limitações.</p><table><thead><tr><th>Período</th><th scope="col">Índice /100</th></tr></thead><tbody><tr v-for="point in summary.trend" :key="point.start"><td>{{ date(point.start) }} – {{ date(point.end) }}</td><td><strong>{{ number(point.deviationsPer100) }}</strong></td></tr></tbody></table></section>
    </div>
    <section v-if="summary.methodCodes.length" class="block"><h2>Como os indicadores são calculados</h2><ul><li v-for="code in summary.methodCodes" :key="code">{{ chatfastMethods[code] }}</li></ul></section>
    <section v-if="summary.limitationCodes.length" class="block limitations"><h2>Cobertura e limitações</h2><ul><li v-for="code in summary.limitationCodes" :key="code">{{ chatfastLimitations[code] }}</li></ul></section>
  </div>
</template>
<style scoped>
.stats{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px}.stats article,.block{padding:24px;background:#fff;border:1px solid #dce4ed;border-radius:18px}.stats h2,.block h2{font-size:14px;font-weight:650;line-height:1.5;margin:0 0 10px}.value{font-size:34px;font-weight:700;color:#193d55;margin:8px 0}.value small{font-size:14px;font-weight:500}.stats p:not(.value),.block p{font-size:12px;color:#52677e;line-height:1.6}.charts{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px;margin-top:20px}.block{margin-top:20px}.charts .block{margin:0}.bar-row{display:flex;gap:12px;align-items:center;margin-top:20px;font-size:12px}.track{height:12px;background:#edf2f7;border-radius:20px;flex:1;overflow:hidden}.bar{height:100%;background:#3a8495;border-radius:20px}table{width:100%;border-collapse:collapse;font-size:13px}th,td{padding:12px 4px;border-bottom:1px solid #e4ebf0;text-align:left}th:last-child,td:last-child{text-align:right}ul{padding-left:20px;margin:0}li{font-size:13px;line-height:1.7;color:#52677e;margin:8px 0}.limitations{background:#fffaf0;border-color:#efdfbc}@media(max-width:750px){.stats{grid-template-columns:repeat(2,minmax(0,1fr))}.charts{grid-template-columns:1fr}.stats article,.block{padding:18px}.value{font-size:30px}}@media(max-width:420px){.stats{grid-template-columns:1fr}}
</style>
