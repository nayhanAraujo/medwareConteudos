<template>
  <div>
    <DsPageHeader title="Central de Orçamentos" subtitle="Carteira compartilhada do departamento, da análise ao encaminhamento." icon="receipt" />
    <DsPageShell>
      <div class="flex flex-wrap justify-between gap-3 mb-5"><div class="flex flex-wrap gap-2"><DsSelect id="quotes-status" v-model="status" label="Estado"><option value="">Todos</option><option v-for="(label,key) in quoteStatuses" :key="key" :value="key">{{ label }}</option></DsSelect><label class="self-end text-sm flex gap-2 items-center py-3"><input v-model="archived" type="checkbox">Arquivados</label></div><div class="flex flex-wrap gap-2 items-end"><DsButton v-if="auth.can('orcamentos','configurar_precos') || auth.can('orcamentos','configurar_clientes') || auth.can('orcamentos','configurar_regras')" to="/biblioteca/orcamentos/configuracoes" variant="secondary" icon="gear">Configurações</DsButton><DsButton v-if="auth.can('orcamentos','elaborar')" icon="plus-lg" @click="creating = !creating">Novo orçamento</DsButton></div></div>
      <form v-if="creating" class="rounded-2xl bg-slate-50 border border-gray-200 p-4 mb-5 space-y-3" @submit.prevent="create">
        <h2 class="text-lg font-semibold">Novo rascunho</h2><DsSelect id="quote-client" v-model="clientId" label="Cliente existente" required><option value="">Selecione</option><option v-for="c in clients" :key="c.id" :value="c.id">{{ c.name }} · #{{ c.id }}</option></DsSelect><DsInput id="quote-title" v-model="title" label="Título / solicitação" :maxlength="200" required />
        <p class="text-sm text-gray-500">Serão utilizadas somente condições previamente configuradas pela gestão. Dados pendentes deverão ser preenchidos antes da aprovação.</p><DsButton type="submit" :loading="busy">Criar rascunho</DsButton>
      </form>
      <DsAlert v-if="error || store.error" variant="error" class="mb-4">{{ error || store.error }}</DsAlert>
      <p v-if="store.loading" class="text-center py-10" role="status">Carregando carteira...</p>
      <div v-else-if="store.rows.length" class="overflow-x-auto"><table class="w-full text-sm text-left"><caption class="sr-only">Orçamentos do departamento</caption><thead><tr class="border-b text-gray-500"><th class="p-3">Orçamento / cliente</th><th class="p-3">Revisão / estado</th><th class="p-3">Valor final</th><th class="p-3">Atualização</th><th><span class="sr-only">Abrir</span></th></tr></thead><tbody><tr v-for="row in store.rows" :key="row.id" class="border-b border-gray-100"><td class="p-3"><NuxtLink :to="`/biblioteca/orcamentos/${row.id}`" class="font-semibold text-blue-700">#{{ row.id }} — {{ row.title || 'Sem título' }}</NuxtLink><span class="block text-gray-500 mt-1">{{ row.clientName }}</span></td><td class="p-3">r{{ row.revision }} · {{ quoteStatuses[row.status] || row.status }}</td><td class="p-3 whitespace-nowrap">{{ quoteMoney(row.total) }}</td><td class="p-3 whitespace-nowrap">{{ quoteDate(row.updatedAtUtc) }}</td><td class="p-3"><DsButton :to="`/biblioteca/orcamentos/${row.id}`" variant="ghost" size="sm" icon="arrow-right" :aria-label="`Abrir orçamento ${row.id}`" /></td></tr></tbody></table></div>
      <p v-else-if="!store.error" class="text-gray-500 text-center py-10">Nenhum orçamento encontrado neste filtro.</p>
      <div class="flex gap-2 justify-end mt-5"><DsButton variant="secondary" size="sm" :disabled="!before || store.loading" @click="page()">Primeira página</DsButton><DsButton variant="secondary" size="sm" :disabled="!store.nextBefore || store.loading" @click="page(store.nextBefore || '')">Próxima página</DsButton></div>
    </DsPageShell>
  </div>
</template>
<script setup lang="ts">
import { useOrcamentosStore } from '~/stores/orcamentos'
import { newQuoteDraft, quoteStatuses, quoteMoney, quoteDate } from '~/utils/orcamentos'
definePageMeta({ layout: 'default', compactWorkspace: true })
useHead({ title: 'Central de Orçamentos' })
const auth = useAuthStore(), api = useApi(), store = useOrcamentosStore(), route = useRoute(), router = useRouter()
const status = ref(typeof route.query.status === 'string' ? route.query.status : ''), archived = ref(route.query.archived === 'true')
const before = computed(() => typeof route.query.before === 'string' ? route.query.before : '')
const clients = ref<{id:number;name:string}[]>([]), clientId = ref(''), title = ref(''), creating = ref(false), busy = ref(false), error = ref('')
async function load() { await store.list(api, { ...(status.value ? {status:status.value} : {}), archived: String(archived.value), ...(before.value ? {before:before.value} : {}) }) }
async function page(next = '') { await router.push({ path: route.path, query: { ...(status.value ? {status:status.value} : {}), ...(archived.value ? {archived:'true'} : {}), ...(next ? {before:next} : {}) } }); await load() }
watch([status,archived], () => page())
watch(() => route.fullPath, () => { status.value = typeof route.query.status === 'string' ? route.query.status : ''; archived.value = route.query.archived === 'true'; void load() })
async function create() {
  error.value = ''; busy.value = true
  try { const draft = newQuoteDraft(); draft.title = title.value; const result = await api.post<{id:string}>('/api/web/orcamentos', { clientId:Number(clientId.value), draft }); await navigateTo(`/biblioteca/orcamentos/${result.id}`) }
  catch (e) { error.value = e instanceof Error ? e.message : 'Não foi possível criar.' }
  finally { busy.value = false }
}
onMounted(async () => { store.clear(); await load(); if(auth.can('orcamentos','elaborar')) try { clients.value = (await api.get<{data:{id:number;name:string}[]}>('/api/web/orcamentos/clientes')).data } catch(e) { error.value = e instanceof Error ? e.message : 'Não foi possível listar clientes.' } })
onBeforeUnmount(() => store.clear())
</script>
