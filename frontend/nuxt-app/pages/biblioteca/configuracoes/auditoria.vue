<template>
  <div>
    <DsPageHeader title="Central de Auditoria" subtitle="Ações e alterações registradas no sistema." icon="clock-history" />
    <DsPageShell panel-class="!p-4 md:!p-6">
      <template v-if="auth.isAdmin">
        <form class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-3 border-b border-gray-200 pb-5" @submit.prevent="applyFilters">
          <DsInput id="audit-from" v-model="filters.from" label="Desde (horário local)" type="datetime-local" step="0.001" required />
          <DsInput id="audit-to" v-model="filters.to" label="Até (limite exclusivo)" type="datetime-local" step="0.001" required />
          <DsSelect id="audit-module" v-model="filters.module" label="Módulo"><option value="">Todos</option><option v-for="item in options('modules', filters.module)" :key="item" :value="item">{{ label(item) }}</option></DsSelect>
          <DsSelect id="audit-action" v-model="filters.action" label="Ação"><option value="">Todas</option><option v-for="item in options('actions', filters.action)" :key="item" :value="item">{{ label(item) }}</option></DsSelect>
          <DsSelect id="audit-result" v-model="filters.result" label="Resultado"><option value="">Todos</option><option v-for="item in options('results', filters.result)" :key="item" :value="item">{{ label(item) }}</option></DsSelect>
          <DsInput id="audit-user" v-model="filters.actorId" label="Código do usuário" placeholder="Ex.: 12" />
          <DsSelect id="audit-type" v-model="filters.entityType" label="Tipo do registro"><option value="">Todos</option><option v-for="item in entityOptions" :key="item" :value="item">{{ auditEntityLabels[item] || item }}</option></DsSelect>
          <DsInput id="audit-entity" v-model="filters.entityId" label="Código do registro" placeholder="Ex.: 37" :maxlength="120" />
          <div class="sm:col-span-2"><DsInput id="audit-correlation" v-model="filters.correlationId" label="Código de correlação" placeholder="Código da requisição ou atendimento" :maxlength="128" /></div>
          <div class="flex flex-wrap items-end justify-end gap-2 sm:col-span-2">
            <DsButton variant="secondary" icon="arrow-counterclockwise" :disabled="store.loading" @click="clearFilters">Limpar</DsButton>
            <DsButton type="submit" icon="funnel" :loading="store.loading">Filtrar</DsButton>
          </div>
        </form>
        <DsAlert v-if="formError || store.error" variant="error" class="mt-4">{{ formError || store.error }}</DsAlert>
        <DsAlert v-if="store.catalogError" variant="warning" class="mt-4">Não foi possível carregar as opções de filtro. <button type="button" class="underline bg-transparent border-0" @click="store.loadCatalog(api)">Tentar novamente</button></DsAlert>
        <div class="flex flex-wrap justify-between gap-3 items-center py-4 text-xs text-gray-500">
          <span>Horários exibidos no seu fuso: {{ timeZone }}.</span>
          <DsButton size="sm" variant="ghost" icon="arrow-clockwise" :disabled="store.loading" @click="refresh">Atualizar consulta</DsButton>
        </div>
        <p v-if="store.loading" class="py-12 text-center text-gray-500" role="status" aria-live="polite">Carregando eventos...</p>
        <template v-else-if="!store.error && !formError">
          <div v-if="store.rows.length" class="hidden md:block overflow-x-auto">
            <table class="w-full text-sm text-left">
              <caption class="sr-only">Eventos de auditoria, do mais recente para o mais antigo</caption>
              <thead class="border-b border-gray-200 text-gray-500"><tr><th class="py-3 pr-4">Data e autor</th><th class="pr-4">Módulo / ação</th><th class="pr-4">Registro / resumo</th><th class="pr-4">Resultado</th><th><span class="sr-only">Detalhes</span></th></tr></thead>
              <tbody><tr v-for="row in store.rows" :key="row.id" class="border-b border-gray-100 hover:bg-gray-50">
                <td class="py-4 pr-4 whitespace-nowrap align-top"><time :datetime="row.occurredAtUtc">{{ formatAuditDate(row.occurredAtUtc) }}</time><span class="block font-medium mt-1">{{ row.actorName || label(row.actorKind) }}<span v-if="row.actorId" class="text-gray-400 font-normal"> #{{ row.actorId }}</span></span></td>
                <td class="pr-4 align-top py-4 min-w-32">{{ label(row.module) }}<span class="block text-gray-500 mt-1">{{ label(row.action) }}</span></td>
                <td class="pr-4 py-4 align-top max-w-md"><p class="font-medium break-words">{{ row.entityName || auditEntityLabels[row.entityType || ''] || row.entityType || '—' }}<span v-if="row.entityId" class="font-normal text-gray-500"> #{{ row.entityId }}</span></p><p class="text-gray-500 mt-1 break-words">{{ row.summary }}</p></td>
                <td class="pr-4 py-4 align-top"><DsBadge :variant="resultVariant(row.result)">{{ label(row.result) }}</DsBadge></td>
                <td class="py-3 align-top"><DsButton variant="ghost" size="sm" icon="eye" :aria-label="`Ver evento ${row.id}`" @click="store.openDetail(api, row.id)">Detalhes</DsButton></td>
              </tr></tbody>
            </table>
          </div>
          <div v-if="store.rows.length" class="md:hidden space-y-3">
            <article v-for="row in store.rows" :key="row.id" class="rounded-xl border border-gray-200 p-4 space-y-2">
              <div class="flex flex-wrap justify-between gap-2 text-xs"><time :datetime="row.occurredAtUtc">{{ formatAuditDate(row.occurredAtUtc) }}</time><DsBadge :variant="resultVariant(row.result)">{{ label(row.result) }}</DsBadge></div>
              <p class="font-medium text-sm">{{ label(row.module) }} · {{ label(row.action) }}</p>
              <p class="text-sm break-words">{{ row.summary }}</p>
              <p class="text-xs text-gray-500 break-words">{{ row.actorName || label(row.actorKind) }}<span v-if="row.entityId"> · Registro #{{ row.entityId }}</span></p>
              <DsButton variant="secondary" size="sm" icon="eye" :aria-label="`Ver evento ${row.id}`" @click="store.openDetail(api, row.id)">Detalhes</DsButton>
            </article>
          </div>
          <div v-if="!store.rows.length" class="py-12 text-center"><i class="bi bi-clock-history text-3xl text-gray-400" /><p class="mt-3 text-sm text-gray-500">Nenhum evento encontrado para esses filtros.</p></div>
          <div class="flex flex-wrap justify-between items-center gap-3 pt-5 text-sm">
            <p class="text-gray-500">{{ store.rows.length }} eventos nesta página · mais recentes primeiro</p>
            <div class="flex gap-2"><DsButton variant="secondary" size="sm" :disabled="!cursor" @click="goToPage()">Primeira página</DsButton><DsButton variant="secondary" size="sm" icon="chevron-right" :disabled="!store.nextCursor" @click="goToPage(store.nextCursor || undefined)">Próxima página</DsButton></div>
          </div>
        </template>
        <p class="mt-5 mb-0 text-xs text-gray-500">O histórico começa na ativação da auditoria e cobre as ações instrumentadas. Não reconstrói alterações anteriores nem permite restaurar registros.</p>
      </template>
      <DsAlert v-else variant="error">A Central de Auditoria está disponível apenas para administradores.</DsAlert>
    </DsPageShell>
    <AuditEventDetail :event-id="store.detailId" :detail="store.detail" :loading="store.detailLoading" :error="store.detailError" @close="store.closeDetail()" />
  </div>
</template>

<script setup lang="ts">
import { useAuditoriaStore } from '~/stores/auditoria'
import { auditLabels, auditEntityLabels, auditFiltersFromQuery, auditFilterQuery, formatAuditDate, type AuditCatalog } from '~/utils/auditoria'
import AuditEventDetail from '~/components/auditoria/AuditEventDetail.vue'

definePageMeta({ layout: 'default', middleware: 'admin', compactWorkspace: true })
useHead({ title: 'Central de Auditoria' })
const auth = useAuthStore(), api = useApi(), store = useAuditoriaStore()
const route = useRoute(), router = useRouter()
const filters = reactive(auditFiltersFromQuery({}))
const formError = ref(''), timeZone = ref('horário local')
const cursor = computed(() => typeof route.query.cursor === 'string' ? route.query.cursor : '')
const label = (value: string) => auditLabels[value] || value
const resultVariant = (value: string) => value === 'sucesso' ? 'success' : value === 'falha' ? 'danger' : value === 'negado' ? 'warning' : 'neutral'
const options = (key: keyof AuditCatalog, selected: string) => [...new Set([...store.catalog[key], ...(selected ? [selected] : [])])]
const entityOptions = computed(() => [...new Set([...Object.keys(auditEntityLabels), ...store.rows.flatMap(row => row.entityType ? [row.entityType] : []), ...(filters.entityType ? [filters.entityType] : [])])])

async function readRoute() {
  if (!auth.isAdmin) return
  Object.assign(filters, auditFiltersFromQuery(route.query))
  formError.value = ''
  try { await store.load(api, auditFilterQuery(filters), cursor.value) }
  catch (error) { formError.value = error instanceof Error ? error.message : 'Filtros inválidos.' }
}
async function applyFilters() {
  try {
    formError.value = ''
    const query = auditFilterQuery(filters)
    if (new URLSearchParams(query).toString() === new URLSearchParams(route.query as Record<string, string>).toString()) await readRoute()
    else await router.push({ path: route.path, query })
  } catch (error) { formError.value = error instanceof Error ? error.message : 'Filtros inválidos.' }
}
async function clearFilters() { Object.assign(filters, auditFiltersFromQuery({})); await applyFilters() }
async function refresh() {
  await readRoute()
}
async function goToPage(next?: string) {
  await router.push({ path: route.path, query: { ...route.query, ...(next ? { cursor: next } : { cursor: undefined }) } })
}
watch(() => route.fullPath, readRoute)
onMounted(async () => {
  store.clear()
  timeZone.value = Intl.DateTimeFormat().resolvedOptions().timeZone
  if (!auth.isAdmin) return
  if (!route.query.from || !route.query.to) {
    await router.replace({ path: route.path, query: { ...route.query, ...auditFilterQuery(auditFiltersFromQuery(route.query)) } })
  } else await readRoute()
  await store.loadCatalog(api)
})
onBeforeUnmount(() => store.clear())
</script>
