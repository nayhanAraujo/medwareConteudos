<template>
  <div>
    <DsPageHeader title="Central de Avaliações" icon="chat-square-heart" />
    <DsPageShell panel-class="!p-4 md:!p-6">
      <DsAlert v-if="!canView" variant="error">Sem permissão para consultar avaliações.</DsAlert>
      <template v-else>
        <form class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 xl:grid-cols-6 gap-3 border-b border-gray-200 pb-5" @submit.prevent="load(1)">
          <DsInput v-model="filters.script" label="Código do script" type="number" min="1" />
          <DsInput v-model="filters.versao" label="Versão (código)" type="number" min="0" placeholder="0 = principal" />
          <DsSelect v-model="filters.sistema" label="Sistema"><option value="">Todos</option><option>Laudos UX</option><option>Laudos Flex</option></DsSelect>
          <DsInput v-model="filters.pacote" label="Código do pacote" type="number" min="1" />
          <DsInput v-model="filters.origem" label="Origem" />
          <DsInput v-model="filters.clinica" label="Clínica" />
          <DsInput v-model="filters.inicio" label="Atualização desde" type="date" />
          <DsInput v-model="filters.fim" label="Até" type="date" />
          <DsSelect v-model="filters.nota" label="Nota geral"><option value="">Todas</option><option v-for="n in 5" :key="n" :value="n">{{ n }}</option></DsSelect>
          <DsSelect v-model="filters.recomendacao" label="Recomendação"><option value="">Todas</option><option value="sim">Sim</option><option value="talvez">Talvez</option><option value="nao">Não</option></DsSelect>
          <DsSelect v-model="filters.situacao" label="Acompanhamento"><option value="">Todos</option><option value="nova">Nova</option><option value="em_analise">Em análise</option><option value="concluida">Concluída</option></DsSelect>
          <DsSelect v-model="filters.visivel" label="Publicação"><option value="">Todas</option><option value="true">Publicada</option><option value="false">Oculta</option></DsSelect>
          <div class="sm:col-span-2 md:col-span-4 xl:col-span-6 flex flex-wrap justify-end gap-2">
            <DsButton type="button" variant="secondary" icon="arrow-counterclockwise" :disabled="loading" @click="clearFilters">Limpar</DsButton>
            <DsButton type="submit" icon="funnel" :loading="loading">Filtrar</DsButton>
          </div>
        </form>
        <DsAlert v-if="error" variant="error" class="mt-4">{{ error }}</DsAlert>
        <div v-if="summary && !error" class="flex flex-wrap items-center gap-x-8 gap-y-3 py-5 border-b border-gray-200 text-sm" aria-label="Indicadores dos filtros">
          <p><span class="text-gray-500">Avaliações</span> <strong class="ml-2 text-lg">{{ summary.quantidade }}</strong></p>
          <p><span class="text-gray-500">Média geral</span> <strong class="ml-2 text-lg">{{ average(summary.mediaGeral) }}</strong></p>
          <div class="flex flex-wrap gap-4" aria-label="Distribuição das notas">
            <span v-for="n in [5,4,3,2,1]" :key="n"><i class="bi bi-star-fill text-amber-500" /> {{ n }}: <strong>{{ summary.distribuicaoNotas[n] || 0 }}</strong></span>
          </div>
        </div>
        <p v-if="loading" class="py-10 text-center text-gray-500" role="status">Carregando avaliações...</p>
        <template v-else-if="!error">
          <div v-if="rows.length" class="relative overflow-x-auto">
            <table class="w-full text-sm text-left">
              <thead class="text-gray-500 border-b border-gray-200"><tr><th class="py-3 pr-4">Script / versão</th><th class="pr-4">Avaliador / clínica</th><th class="pr-4">Nota</th><th class="pr-4">Recomendação</th><th class="pr-4">Atualização</th><th class="pr-4">Acompanhamento</th><th class="pr-4">Publicação</th><th><span class="sr-only">Detalhes</span></th></tr></thead>
              <tbody><tr v-for="row in rows" :key="row.id" class="border-b border-gray-100 hover:bg-gray-50">
                <td class="py-3 pr-4 min-w-48"><span class="font-medium">{{ row.nomeScript }}</span><span class="block text-xs text-gray-500">#{{ row.codScriptLaudo }} · {{ row.numeroVersao || 'Principal' }}</span></td>
                <td class="pr-4 min-w-40">{{ row.nomeAvaliador }}<span class="block text-xs text-gray-500">{{ row.nomeClinica }}</span></td>
                <td class="pr-4 whitespace-nowrap"><i class="bi bi-star-fill text-amber-500" /> {{ row.notaGeral }}/5</td>
                <td class="pr-4">{{ labels[row.recomendacao] }}</td>
                <td class="pr-4 whitespace-nowrap">{{ date(row.atualizadoEm) }}</td>
                <td class="pr-4 whitespace-nowrap">{{ labels[row.situacao] }}</td>
                <td class="pr-4"><DsBadge :variant="row.visivel ? 'success' : 'default'">{{ row.visivel ? 'Publicada' : 'Oculta' }}</DsBadge></td>
                <td><DsButton variant="ghost" size="sm" icon="eye" :title="`Detalhes da avaliação de ${row.nomeAvaliador}`" aria-label="Ver avaliação" @click="openDetail(row.id)" /></td>
              </tr></tbody>
            </table>
          </div>
          <p v-else class="py-12 text-center text-gray-500">Nenhuma avaliação encontrada.</p>
          <div class="flex flex-wrap justify-between items-center gap-3 pt-4 text-sm">
            <span>{{ totalItems }} avaliações · Página {{ page }} de {{ Math.max(1,totalPages) }}</span>
            <div class="flex gap-2"><DsButton variant="secondary" size="sm" icon="chevron-left" :disabled="page <= 1" @click="load(page - 1)">Anterior</DsButton><DsButton variant="secondary" size="sm" icon="chevron-right" :disabled="page >= totalPages" @click="load(page + 1)">Próxima</DsButton></div>
          </div>
        </template>
      </template>
    </DsPageShell>
    <DsModal :model-value="detailId !== null" title="Detalhes da avaliação" size="2xl" @update:model-value="closeDetail">
      <p v-if="detailLoading" class="py-8 text-center" role="status">Carregando...</p>
      <DsAlert v-if="detailError" variant="error">{{ detailError }}</DsAlert>
      <template v-if="detail && !detailLoading">
        <div class="border-b border-gray-200 pb-4 space-y-2 text-sm">
          <h2 class="font-semibold text-lg break-words">{{ detail.data.nomeScript }} · {{ detail.data.numeroVersao || 'Principal' }}</h2>
          <p>{{ detail.data.nomeAvaliador }} · {{ detail.data.nomeClinica }}</p>
          <p class="text-gray-500 break-all">Origem: {{ detail.data.origem }} · Cliente: {{ detail.data.clienteId }} · Usuário: {{ detail.data.usuarioId }}</p>
          <p>{{ date(detail.data.criadoEm) }} · Atualizada em {{ date(detail.data.atualizadoEm) }}</p>
          <p><i class="bi bi-star-fill text-amber-500" /> <strong>{{ detail.data.notaGeral }}/5</strong> · Recomendação: {{ labels[detail.data.recomendacao] }}</p>
          <h3 v-if="detail.data.titulo" class="font-semibold break-words">{{ detail.data.titulo }}</h3>
          <p class="whitespace-pre-wrap break-words">{{ detail.data.comentario || 'Sem comentário.' }}</p>
        </div>
        <dl class="grid grid-cols-1 sm:grid-cols-2 gap-3 py-4 text-sm border-b border-gray-200">
          <div v-for="criterion in criteria" :key="criterion.key" class="flex justify-between gap-3"><dt>{{ criterion.label }}</dt><dd class="font-medium">{{ detail.data[criterion.key] ?? 'Não informado' }}</dd></div>
        </dl>
        <div v-if="canFollow" class="py-4 border-b border-gray-200 space-y-3">
          <div class="flex flex-wrap items-end gap-2"><DsSelect v-model="state" label="Acompanhamento"><option value="nova">Nova</option><option value="em_analise">Em análise</option><option value="concluida">Concluída</option></DsSelect><DsButton size="sm" icon="check2" :disabled="busy || state === detail.data.situacao" @click="change('state')">Atualizar</DsButton></div>
          <DsTextarea v-model="note" label="Anotação interna" :rows="2" :maxlength="2000" />
          <DsButton size="sm" variant="secondary" icon="journal-plus" :disabled="busy || !note.trim()" @click="change('note')">Adicionar anotação</DsButton>
        </div>
        <div v-if="canModerate" class="py-4 border-b border-gray-200 space-y-3">
          <p class="text-sm font-medium">{{ detail.data.visivel ? 'Publicada' : 'Oculta' }}</p>
          <DsTextarea v-model="reason" label="Justificativa da alteração de publicação" :rows="2" :maxlength="2000" />
          <DsButton size="sm" :variant="detail.data.visivel ? 'danger' : 'success'" :icon="detail.data.visivel ? 'eye-slash' : 'eye'" :disabled="busy || !reason.trim()" @click="change('visibility')">{{ detail.data.visivel ? 'Ocultar avaliação' : 'Restaurar publicação' }}</DsButton>
        </div>
        <section class="pt-4"><h3 class="font-semibold mb-3">Histórico</h3><ol class="space-y-4 text-sm">
          <li v-for="event in detail.historico" :key="event.id" class="border-l-2 border-gray-200 pl-3">
            <p class="font-medium">{{ labels[event.tipo] || event.tipo }}</p><p class="text-xs text-gray-500 break-all">{{ date(event.dataEvento) }} · {{ event.autor }}</p>
            <p v-if="event.observacao" class="mt-1 whitespace-pre-wrap break-words">{{ event.observacao }}</p>
            <p v-for="line in changes(event)" :key="line" class="mt-1 break-words text-gray-600">{{ line }}</p>
          </li>
        </ol></section>
      </template>
      <template #footer><DsButton variant="secondary" :disabled="busy" @click="closeDetail">Fechar</DsButton></template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
import type { Avaliacao, AvaliacaoEvento, AvaliacaoResumo } from '~/composables/useAvaliacoesApi'
definePageMeta({ layout: 'avaliacoes' })
const auth = useAuthStore()
const api = useAvaliacoesApi()
const canView = computed(() => auth.can('avaliacoes', 'visualizar'))
const canFollow = computed(() => auth.can('avaliacoes', 'acompanhar'))
const canModerate = computed(() => auth.can('avaliacoes', 'moderar'))
const empty = () => ({ script: '', versao: '', sistema: '', pacote: '', origem: '', clinica: '', inicio: '', fim: '', nota: '', recomendacao: '', situacao: '', visivel: '' })
const filters = reactive(empty())
const rows = ref<Avaliacao[]>([])
const summary = ref<AvaliacaoResumo | null>(null)
const page = ref(1), totalPages = ref(0), totalItems = ref(0)
const loading = ref(false), error = ref('')
const detailId = ref<number | null>(null), detailLoading = ref(false), detailError = ref(''), busy = ref(false)
const detail = ref<{ data: Avaliacao; historico: AvaliacaoEvento[] } | null>(null)
const state = ref('nova'), note = ref(''), reason = ref('')
const labels: Record<string,string> = { sim: 'Sim', talvez: 'Talvez', nao: 'Não', nova: 'Nova', em_analise: 'Em análise', concluida: 'Concluída', criacao: 'Recebida', revisao: 'Revisão do avaliador', triagem: 'Acompanhamento', anotacao: 'Anotação interna', ocultacao: 'Ocultada', restauracao: 'Publicação restaurada' }
const criteria = [ { key: 'facilidadeUso', label: 'Facilidade de uso' }, { key: 'qualidadeConteudo', label: 'Qualidade do conteúdo' }, { key: 'completude', label: 'Completude das informações' }, { key: 'aparencia', label: 'Aparência do laudo' }, { key: 'utilidadeClinica', label: 'Utilidade clínica' } ] as const
let request = 0
function query() {
  const params = new URLSearchParams()
  for (const [key,value] of Object.entries(filters)) if (String(value) !== '') params.set(key, String(value))
  return params
}
async function load(target = 1) {
  if (!canView.value) return
  const current = ++request
  loading.value = true; error.value = ''
  const params = query(); params.set('page', String(target))
  try {
    const [list, stats] = await Promise.all([api.list(params.toString()), api.summary(params.toString())])
    if (current !== request) return
    rows.value = list.data; page.value = list.page; totalPages.value = list.totalPages; totalItems.value = list.totalItems; summary.value = stats.data
  } catch (e: unknown) {
    if (current === request) error.value = e instanceof Error ? e.message : 'Erro ao carregar avaliações.'
  } finally { if (current === request) loading.value = false }
}
function clearFilters() { Object.assign(filters, empty()); load(1) }
async function openDetail(id: number) {
  detailId.value = id; detail.value = null; detailLoading.value = true; detailError.value = ''; note.value = ''; reason.value = ''
  try { const result = await api.detail(id); if (detailId.value === id) { detail.value = result; state.value = result.data.situacao } }
  catch (e: unknown) { if (detailId.value === id) detailError.value = e instanceof Error ? e.message : 'Erro ao carregar avaliação.' }
  finally { if (detailId.value === id) detailLoading.value = false }
}
function closeDetail() { if (!busy.value) { detailId.value = null; detail.value = null } }
async function change(action: 'state' | 'note' | 'visibility') {
  if (!detail.value || busy.value || (action === 'visibility' ? !canModerate.value : !canFollow.value)) return
  const id = detail.value.data.id
  busy.value = true; detailError.value = ''
  try {
    if (action === 'state') await api.follow(id, state.value)
    else if (action === 'note') await api.note(id, note.value)
    else await api.visibility(id, !detail.value.data.visivel, reason.value)
    const result = await api.detail(id); detail.value = result; state.value = result.data.situacao; note.value = ''; reason.value = ''
    await load(page.value)
  } catch (e: unknown) { detailError.value = e instanceof Error ? e.message : 'Erro ao atualizar avaliação.' }
  finally { busy.value = false }
}
function average(value: number | null) { return value == null ? '—' : `${value.toFixed(2).replace('.', ',')}/5` }
function date(value: string) { return new Date(value.endsWith('Z') ? value : `${value}Z`).toLocaleString('pt-BR') }
function changes(event: AvaliacaoEvento) {
  if (!event.antes || !event.depois) return []
  try {
    const before = JSON.parse(event.antes), after = JSON.parse(event.depois)
    const names: Record<string,string> = { NotaGeral: 'Nota geral', Recomendacao: 'Recomendação', FacilidadeUso: 'Facilidade de uso', QualidadeConteudo: 'Qualidade do conteúdo', Completude: 'Completude', Aparencia: 'Aparência', UtilidadeClinica: 'Utilidade clínica', Titulo: 'Título', Comentario: 'Comentário', Situacao: 'Acompanhamento', Visivel: 'Publicação', NomeAvaliador: 'Avaliador', NomeClinica: 'Clínica' }
    const display = (value: unknown) => value == null ? 'Não informado' : typeof value === 'boolean' ? value ? 'Publicada' : 'Oculta' : labels[String(value)] || String(value)
    return Object.entries(names).filter(([key]) => before[key] !== after[key]).map(([key,label]) => `${label}: ${display(before[key])} → ${display(after[key])}`)
  } catch { return [] }
}
onMounted(() => load())
</script>
