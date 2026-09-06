<template>
  <div>
    <DsPageHeader title="Publicação no Assistente" icon="arrow-repeat" />
    <DsPageShell>
      <div class="mb-5 flex flex-wrap justify-between gap-3">
        <DsButton variant="secondary" icon="arrow-left" to="/scripts/sistema">Scripts</DsButton>
        <DsButton variant="secondary" icon="arrow-clockwise" :loading="busy" @click="load">Atualizar</DsButton>
      </div>
      <DsAlert v-if="error" variant="danger">{{ error }}</DsAlert>
      <p v-if="loading" role="status">Carregando...</p>
      <DsAlert v-else-if="!enabled" variant="warning">Publicação desabilitada. A configuração das migrações e da origem está pendente.</DsAlert>
      <template v-else>
        <section class="border-b border-gray-200 py-6">
          <h2 class="mb-4 text-lg font-semibold">Pacotes e especialidades</h2>
          <div class="grid gap-4 md:grid-cols-2">
            <DsSelect v-model="packageId" label="Pacote">
              <option value="">Selecione</option>
              <option v-for="item in packages" :key="item.id" :value="String(item.id)">{{ item.nome }}</option>
            </DsSelect>
            <fieldset :disabled="!packageId || busy" class="min-w-0">
              <legend class="mb-2 text-sm font-medium">Especialidades do Assistente</legend>
              <DsSearchInput v-model="search" placeholder="Pesquisar especialidade" />
              <div class="mt-2 max-h-60 overflow-y-auto rounded-lg border border-gray-200 bg-gray-50 p-3">
                <label v-for="item in filteredSpecialties" :key="item.id" class="flex items-start gap-2 py-2 text-sm">
                  <input v-model="selected" type="checkbox" :value="item.id" class="mt-1 shrink-0">
                  <span class="break-words">{{ item.nome }}</span>
                </label>
              </div>
            </fieldset>
          </div>
          <div class="mt-4 flex justify-end"><DsButton icon="check-lg" :disabled="!packageId" :loading="busy" @click="save">Salvar associação e publicar pacote</DsButton></div>
        </section>
        <section class="pt-6">
          <h2 class="mb-4 text-lg font-semibold">Publicações · {{ total }}</h2>
          <DsEmptyState v-if="!items.length" title="Nenhuma publicação registrada" icon="inbox" />
          <div v-for="item in items" :key="item.id" class="flex flex-wrap items-start justify-between gap-3 border-b border-gray-200 py-4">
            <div class="min-w-0 flex-1 basis-60">
              <NuxtLink :to="`/scripts/${item.id}/editar`" class="break-words font-semibold">#{{ item.id }} · {{ item.nome || 'Script excluído na origem' }}</NuxtLink>
              <div class="mt-2 flex flex-wrap gap-2 text-sm">
                <DsBadge :variant="item.estado === 'sincronizado' ? 'success' : item.estado === 'falha' ? 'danger' : 'warning'">{{ item.estado }}</DsBadge>
                <span v-if="item.publicado_em" class="text-gray-500">{{ new Date(item.publicado_em).toLocaleString('pt-BR') }}</span>
                <NuxtLink v-if="item.coddestino" :to="`/assistente/scripts?search=${item.coddestino}`" class="underline">Assistente #{{ item.coddestino }}</NuxtLink>
              </div>
              <p v-if="item.erro" class="mt-2 break-words text-sm text-red-700">{{ item.erro }}</p>
            </div>
            <div class="flex flex-wrap gap-2">
              <DsButton v-if="item.estado === 'suspenso'" size="sm" icon="play" :disabled="busy" @click="action(item.id, 'retomar')">Retomar publicação</DsButton>
              <DsButton v-else size="sm" variant="secondary" icon="arrow-repeat" :disabled="busy" @click="action(item.id, 'repetir')">Repetir</DsButton>
              <DsButton v-if="item.estado === 'falha'" size="sm" variant="secondary" icon="link-45deg" :disabled="busy" @click="candidates(item.id)">Conciliar existente</DsButton>
            </div>
          </div>
          <div class="mt-4 flex items-center justify-end gap-3">
            <DsButton variant="secondary" icon="chevron-left" aria-label="Página anterior" :disabled="page === 1 || busy" @click="changePage(-1)" />
            <span>{{ page }}</span>
            <DsButton variant="secondary" icon="chevron-right" aria-label="Próxima página" :disabled="page * 30 >= total || busy" @click="changePage(1)" />
          </div>
        </section>
      </template>
    </DsPageShell>
    <DsModal v-model="modal" title="Conciliar modelo existente">
      <DsEmptyState v-if="!matches.length" title="Nenhum candidato disponível" icon="search" />
      <div v-for="item in matches" :key="item.id" class="border-b py-3">
        <p class="break-words font-semibold">#{{ item.id }} · {{ item.nome }}</p>
        <p class="my-2 text-sm">Conteúdo {{ item.conteudoIgual ? 'igual' : 'diferente' }} · Tipo {{ item.tipoIgual ? 'igual' : 'diferente' }}</p>
        <DsSelect v-if="item.mrds?.length" v-model="mrdSelections[item.id]" label="MRD da publicação" class="mb-3">
          <option value="">Criar novo MRD</option>
          <option v-for="mrd in item.mrds" :key="mrd.id" :value="String(mrd.id)">Atualizar #{{ mrd.id }} · {{ mrd.nome }}</option>
        </DsSelect>
        <DsButton size="sm" :loading="busy" @click="reconcile(item.id)">Associar este modelo</DsButton>
      </div>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })
const api = useApi()
const swal = useSwal()
const root = '/api/web/assistente/publicacao'
type Option = { id: number; nome: string }
type Publication = { id: number; nome: string; estado: string; erro?: string; publicado_em?: string; coddestino?: number }
const packages = ref<Option[]>([])
const specialties = ref<Option[]>([])
const mappings = ref<{ pacote: number; especialidade: number }[]>([])
const items = ref<Publication[]>([])
const packageId = ref('')
const selected = ref<number[]>([])
const search = ref('')
const enabled = ref(false)
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const page = ref(1)
const total = ref(0)
const modal = ref(false)
const reconcileId = ref(0)
const matches = ref<{ id: number; nome: string; conteudoIgual: boolean; tipoIgual: boolean; mrds: Option[] }[]>([])
const mrdSelections = ref<Record<number, string>>({})
const lower = <T,>(rows: Record<string, unknown>[]): T[] => rows.map(row => Object.fromEntries(Object.entries(row).map(([k, v]) => [k.toLowerCase(), v])) as T)
const filteredSpecialties = computed(() => specialties.value.filter(item => item.nome.toLocaleLowerCase().includes(search.value.toLocaleLowerCase())))
watch(packageId, () => { selected.value = mappings.value.filter(m => m.pacote === Number(packageId.value)).map(m => m.especialidade) })
async function load() {
  busy.value = true
  error.value = ''
  try {
    const data = await api.get<{ enabled: boolean; packages: Record<string, unknown>[]; specialties: Record<string, unknown>[]; mappings: Record<string, unknown>[] }>(root)
    enabled.value = data.enabled
    if (!data.enabled) return
    packages.value = lower<Option>(data.packages)
    specialties.value = lower<Option>(data.specialties)
    mappings.value = lower(data.mappings)
    const result = await api.get<{ items: Record<string, unknown>[]; total: number }>(`${root}/scripts?page=${page.value}`)
    items.value = lower<Publication>(result.items)
    total.value = result.total
  } catch (e) { error.value = e instanceof Error ? e.message : 'Falha ao carregar publicação.' }
  finally { loading.value = false; busy.value = false }
}
async function perform(task: () => Promise<unknown>) {
  busy.value = true
  try { await task(); await load() }
  catch (e) { await swal.error('Publicação', e instanceof Error ? e.message : 'Falha na operação.') }
  finally { busy.value = false }
}
async function save() {
  if (!(await swal.confirm('Salvar associação?', 'Os scripts deste pacote serão enfileirados para publicação nas especialidades selecionadas.')).isConfirmed) return
  await perform(() => api.put(`${root}/pacotes/${packageId.value}`, { especialidades: selected.value }))
}
async function action(id: number, action: string) {
  if (action === 'retomar' && !(await swal.confirm('Retomar publicação?', 'O modelo excluído localmente poderá ser criado novamente.')).isConfirmed) return
  await perform(() => api.post(`${root}/scripts/${id}/${action}`))
}
async function candidates(id: number) {
  reconcileId.value = id
  mrdSelections.value = {}
  await perform(async () => { matches.value = await api.get(`${root}/scripts/${id}/candidatos`); modal.value = true })
}
async function reconcile(destination: number) {
  if (!(await swal.confirm('Associar e atualizar este modelo?', 'Título, tipo, conteúdo e o MRD selecionado passarão a ser controlados pelo Conteúdos. Vínculos locais e inativação serão preservados.')).isConfirmed) return
  await perform(async () => { await api.post(`${root}/scripts/${reconcileId.value}/conciliar`, { destino: destination, mrdDestino: Number(mrdSelections.value[destination]) || null }); modal.value = false })
}
async function changePage(delta: number) { page.value += delta; await load() }
onMounted(load)
</script>
