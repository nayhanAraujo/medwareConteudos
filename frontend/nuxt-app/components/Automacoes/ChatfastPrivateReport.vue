<script setup lang="ts">
import type { ChatfastSummary } from '~/utils/chatfastReport'
type Table = { title: string; columns: string[]; rows: (string | number | null)[][] }
type Case = { client: string; technicians: string; team: string; status: string; description: string; dates: string; occurrences: number | null; severity?: string }
type PrivateDetails = { tables?: Table[]; cases?: Case[]; recommendations?: string[]; positiveExamples?: string[]; sources?: { file: string; sha256: string }[]; tracker?: { count: number; updatedAt: string; note: string } }
const props = defineProps<{ document: Record<string, unknown> }>()
const search = ref('')
const tab = ref('indicadores')
const summary = computed(() => props.document.summary as ChatfastSummary | undefined)
const details = computed(() => (props.document.details ?? {}) as PrivateDetails)
const tables = computed(() => Array.isArray(details.value.tables) ? details.value.tables : [])
const cases = computed(() => (Array.isArray(details.value.cases) ? details.value.cases : []).filter(c => matches([c.client, c.technicians, c.team, c.status])))
const recommendations = computed(() => Array.isArray(details.value.recommendations) ? details.value.recommendations : [])
const positives = computed(() => Array.isArray(details.value.positiveExamples) ? details.value.positiveExamples : [])
function matches(values: unknown[]) { return !search.value.trim() || values.some(v => String(v ?? '').toLocaleLowerCase('pt-BR').includes(search.value.trim().toLocaleLowerCase('pt-BR'))) }
function filteredRows(table: Table) { return Array.isArray(table.rows) ? table.rows.filter(row => Array.isArray(row) && matches(row)) : [] }
watch(() => props.document, () => { search.value = ''; tab.value = 'indicadores' })
</script>
<template>
  <div class="private-report">
    <p class="privacy">Restrito a administradores ativos · não compartilhar esta visualização como relatório público.</p>
    <div class="tabs" role="group" aria-label="Seções do relatório privado"><button v-for="item in [{code:'indicadores',label:'Indicadores e método'},{code:'tecnicos',label:'Técnicos e equipes'},{code:'casos',label:'Seleções críticas'},{code:'acoes',label:'Recomendações e exemplos'}]" :key="item.code" :aria-pressed="tab === item.code" @click="tab = item.code">{{ item.label }}</button></div>
    <AutomacoesChatfastSummary v-if="tab === 'indicadores' && summary" :summary="summary" />
    <label v-if="['tecnicos','casos'].includes(tab)" class="search">Pesquisar nesta seção <input v-model="search" type="search" placeholder="Técnico, equipe, cliente ou situação" /></label>
    <p v-if="search && ['tecnicos','casos'].includes(tab)">A pesquisa filtra apenas esta seção privada; os indicadores globais não mudam.</p>
    <template v-if="tab === 'tecnicos'"><section v-for="(table,i) in tables" :key="i"><h3>{{ table.title }}</h3><div class="table-scroll" tabindex="0" :aria-label="table.title"><table><thead><tr><th v-for="(column,j) in table.columns" :key="j">{{ column }}</th></tr></thead><tbody><tr v-for="(row,j) in filteredRows(table)" :key="j"><td v-for="(cell,k) in row" :key="k">{{ cell ?? '—' }}</td></tr></tbody></table></div><p v-if="!filteredRows(table).length">Sem linhas para esta pesquisa.</p></section><p v-if="!tables.length">Sem indicadores individuais neste snapshot.</p></template>
<template v-if="tab === 'casos'"><div v-if="details.tracker" class="tracker"><h3>Acompanhamento formal de casos</h3><p>{{ details.tracker.count }} registros · {{ details.tracker.updatedAt }}</p><p>{{ details.tracker.note }}</p></div><h3>Atendimentos selecionados nos resumos</h3><p>A lista pode incluir críticos e esperas longas. Não é o cadastro formal de casos; a mesma ocorrência pode aparecer em vários dias.</p><details v-for="(item,i) in cases" :key="i"><summary>{{ item.client }} · {{ item.severity || 'Seleção do resumo' }} · {{ item.status }}</summary><dl><dt>Técnicos / equipe</dt><dd>{{ item.technicians }} · {{ item.team }}</dd><dt>Datas / ocorrências</dt><dd>{{ item.dates }} · {{ item.occurrences ?? 'não informado' }}</dd></dl><p>{{ item.description }}</p></details><p v-if="!cases.length">Nenhuma seleção com os filtros atuais.</p></template>
    <template v-if="tab === 'acoes'"><h3>Recomendações do snapshot</h3><ol><li v-for="(item,i) in recommendations" :key="i">{{ item }}</li></ol><p v-if="!recommendations.length">Sem recomendações estruturadas neste snapshot.</p><h3>Bons exemplos</h3><ul><li v-for="(item,i) in positives" :key="i">{{ item }}</li></ul><p v-if="!positives.length">Consulte a tabela de bons exemplos na seção Técnicos e equipes.</p></template>
    <details class="raw"><summary>Documento original e proveniência · privado</summary><pre>{{ JSON.stringify(document, null, 2) }}</pre></details>
  </div>
</template>
<style scoped>
.private-report{min-width:0}.privacy{background:#f0f4ff;border-radius:12px;padding:14px;font-size:13px;color:#29426d}.tabs{display:flex;flex-wrap:wrap;gap:8px;margin:20px 0}.tabs button{padding:9px 14px;border:1px solid #cbd6e2;border-radius:20px;background:transparent;color:inherit}.tabs button[aria-pressed=true]{background:#193d55;color:white}.search{display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin:20px 0}.search input{padding:9px;border:1px solid #cbd6e2;border-radius:8px;min-width:0;max-width:100%;width:340px}h3{font-size:16px;font-weight:650;margin:24px 0 12px}p,li,dd,dt{font-size:13px;line-height:1.7}ul,ol{padding-left:22px}.table-scroll{max-width:100%;overflow:auto;border:1px solid #dce4ed;border-radius:12px}table{width:100%;border-collapse:collapse;font-size:12px}th,td{padding:12px;border-bottom:1px solid #dce4ed;text-align:left;min-width:110px}th{background:#eef3f8;color:#263f55}td:first-child{min-width:180px}section{margin:24px 0}details:not(.raw){padding:16px;border:1px solid #dce4ed;border-radius:12px;margin:12px 0}summary{cursor:pointer;font-size:13px;font-weight:600}dd{margin:0 0 8px}dt{font-weight:600}.tracker{padding:16px;background:#f5f7fa;border-radius:12px}.tracker h3{margin-top:0}.raw{margin-top:28px}pre{white-space:pre-wrap;overflow-wrap:anywhere;max-height:400px;overflow:auto;font-size:11px;margin-top:16px}
</style>
