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
              <legend class="mb-2 text-sm font-medium">Especialidades padrão do pacote</legend>
              <DsSearchInput v-model="search" placeholder="Pesquisar especialidade" />
              <div class="mt-2 max-h-60 overflow-y-auto rounded-lg border border-gray-200 bg-gray-50 p-3">
                <label v-for="item in filteredSpecialties" :key="item.id" class="flex items-start gap-2 py-2 text-sm">
                  <input v-model="selected" type="checkbox" :value="item.id" class="mt-1 shrink-0">
                  <span class="break-words">{{ item.nome }}</span>
                </label>
              </div>
            </fieldset>
          </div>
          <div class="mt-4 flex flex-wrap justify-end gap-2">
            <DsButton icon="check-lg" :disabled="!packageId" :loading="busy" @click="save">Salvar padrão e publicar pacote</DsButton>
          </div>
        </section>

        <section v-if="packageId" class="border-b border-gray-200 py-6">
          <div class="mb-4 flex flex-wrap items-end justify-between gap-3">
            <div>
              <h2 class="text-lg font-semibold">Regras por script · {{ packageScriptsTotal }}</h2>
              <p class="text-sm text-gray-500">A regra específica substitui as especialidades padrão somente para o script selecionado.</p>
            </div>
            <div class="flex flex-wrap gap-2">
              <DsButton size="sm" variant="secondary" icon="sliders" :disabled="!checkedScripts.length || busy" @click="openBulkEdit">Aplicar nos selecionados</DsButton>
              <DsButton size="sm" variant="secondary" icon="arrow-counterclockwise" :disabled="!checkedScripts.length || busy" @click="clearBulkRule">Voltar selecionados ao padrão</DsButton>
            </div>
          </div>
          <div class="mb-4 flex flex-wrap items-center gap-3">
            <DsSearchInput v-model="scriptSearch" placeholder="Pesquisar script do pacote" wrapper-class="!mb-0 w-full sm:w-96" />
            <DsSelect v-model="scriptStatus" class="w-full sm:w-48">
              <option value="1">Ativos</option>
              <option value="0">Inativos</option>
            </DsSelect>
            <label class="flex items-center gap-2 text-sm">
              <input type="checkbox" :checked="allVisibleChecked" :disabled="!packageScripts.length" @change="toggleVisibleScripts">
              Selecionar exibidos
            </label>
          </div>
          <DsEmptyState v-if="!packageScripts.length" title="Nenhum script encontrado neste pacote" icon="inbox" />
          <div v-else class="space-y-3">
            <article v-for="item in packageScripts" :key="item.id" class="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
              <div class="flex flex-wrap items-start justify-between gap-3">
                <div class="flex min-w-0 flex-1 gap-3">
                  <input v-model="checkedScripts" type="checkbox" :value="item.id" class="mt-1 shrink-0">
                  <div class="min-w-0">
                    <NuxtLink :to="`/scripts/${item.id}/editar`" class="break-words font-semibold">#{{ item.id }} · {{ item.nome }}</NuxtLink>
                    <div class="mt-2 flex flex-wrap gap-2 text-sm">
                      <DsBadge :variant="item.usaRegraEspecifica ? 'primary' : 'neutral'">{{ item.usaRegraEspecifica ? 'Regra específica' : 'Padrão do pacote' }}</DsBadge>
                      <DsBadge :variant="item.ativo === 1 ? 'success' : 'danger'">{{ item.ativo === 1 ? 'Ativo' : 'Inativo' }}</DsBadge>
                      <DsBadge :variant="item.aprovadoPublicacao ? 'success' : 'warning'">{{ approvalText(item) }}</DsBadge>
                      <DsBadge v-if="item.estado" :variant="stateVariant(item.estado)">{{ stateLabel(item.estado) }}</DsBadge>
                      <span class="text-gray-500">{{ item.sistema || 'Sistema não informado' }}</span>
                    </div>
                    <p v-if="!item.aprovadoPublicacao" class="mt-2 text-sm text-orange-700">Este script não será publicado até ser aprovado.</p>
                    <p class="mt-2 text-sm text-gray-600">Especialidades: {{ names(item.especialidadesEfetivas) }}</p>
                  </div>
                </div>
                <div class="flex flex-wrap gap-2">
                  <DsButton size="sm" variant="secondary" icon="pencil" :disabled="busy" @click="openScriptEdit(item)">Configurar</DsButton>
                  <DsButton v-if="item.usaRegraEspecifica" size="sm" variant="secondary" icon="arrow-counterclockwise" :disabled="busy" @click="clearScriptRule(item)">Voltar ao padrão</DsButton>
                </div>
              </div>
            </article>
          </div>
          <div class="mt-4 flex items-center justify-end gap-3">
            <DsButton variant="secondary" icon="chevron-left" aria-label="Página anterior" :disabled="scriptPage === 1 || busy" @click="changeScriptPage(-1)" />
            <span>{{ scriptPage }}</span>
            <DsButton variant="secondary" icon="chevron-right" aria-label="Próxima página" :disabled="scriptPage * 30 >= packageScriptsTotal || busy" @click="changeScriptPage(1)" />
          </div>
        </section>

        <section class="pt-6">
          <div class="mb-4 flex flex-wrap items-end justify-between gap-3">
            <div>
              <h2 class="text-lg font-semibold">Publicações · {{ total }}</h2>
              <p class="text-sm text-gray-500">Acompanhe o que já foi sincronizado, o que está pendente e o que precisa de aprovação.</p>
            </div>
          </div>
          <div class="mb-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
            <button
              v-for="card in publicationSummaryCards"
              :key="card.key"
              type="button"
              class="rounded-lg border bg-white p-4 text-left shadow-sm transition hover:-translate-y-0.5 hover:shadow-md"
              :class="publicationStatusFilter === card.key ? 'border-black ring-2 ring-black/10' : 'border-gray-200'"
              @click="togglePublicationStatus(card.key)"
            >
              <div class="flex items-center justify-between gap-3">
                <span class="text-sm font-medium text-gray-600">{{ card.label }}</span>
                <i :class="[`bi bi-${card.icon}`, card.color]" />
              </div>
              <strong class="mt-3 block text-2xl">{{ card.count }}</strong>
            </button>
          </div>
          <div class="mb-4 grid gap-3 lg:grid-cols-[minmax(16rem,24rem)_12rem_12rem_12rem]">
            <DsSearchInput v-model="publicationSearch" placeholder="Pesquisar por título ou código" wrapper-class="!mb-0" />
            <DsSelect v-model="publicationPackageFilter">
              <option value="">Todos pacotes</option>
              <option v-for="item in packages" :key="item.id" :value="String(item.id)">{{ item.nome }}</option>
            </DsSelect>
            <DsSelect v-model="publicationStatusFilter">
              <option value="">Todos status</option>
              <option v-for="item in publicationStates" :key="item.key" :value="item.key">{{ item.label }}</option>
            </DsSelect>
            <DsSelect v-model="publicationApprovalFilter">
              <option value="">Toda aprovação</option>
              <option value="1">Aprovados</option>
              <option value="0">Aguardando aprovação</option>
            </DsSelect>
          </div>
          <DsEmptyState v-if="!items.length" title="Nenhuma publicação registrada" icon="inbox" />
          <div v-else class="space-y-3">
          <article v-for="item in items" :key="item.id" class="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
            <div class="flex flex-wrap items-start justify-between gap-3">
            <div class="min-w-0 flex-1 basis-60">
              <NuxtLink :to="`/scripts/${item.id}/editar`" class="break-words font-semibold">#{{ item.id }} · {{ item.nome || 'Script excluído na origem' }}</NuxtLink>
              <div class="mt-2 flex flex-wrap gap-2 text-sm">
                <DsBadge :variant="stateVariant(item.estado)">{{ stateLabel(item.estado) }}</DsBadge>
                <DsBadge :variant="item.aprovadoPublicacao ? 'success' : 'warning'">{{ approvalText(item) }}</DsBadge>
                <DsBadge v-if="item.packageName" variant="neutral">{{ item.packageName }}</DsBadge>
                <span class="text-gray-500">{{ item.sistema || 'Sistema não informado' }}</span>
                <span v-if="item.numeroVersaoAtiva" class="text-gray-500">Versão {{ item.numeroVersaoAtiva }}</span>
              </div>
              <div class="mt-3 grid gap-2 text-sm text-gray-600 sm:grid-cols-2 lg:grid-cols-4">
                <span>Destino: <NuxtLink v-if="item.codDestino" :to="`/assistente/scripts?search=${item.codDestino}`" class="underline">Assistente #{{ item.codDestino }}</NuxtLink><span v-else>Não publicado</span></span>
                <span>Especialidade destino: {{ destinationSpecialtiesText(item) }}</span>
                <span>Publicado: {{ formatDate(item.publicadoEm) }}</span>
                <span>Aprovação: {{ item.aprovadoPor || 'Sem responsável' }}</span>
              </div>
              <p v-if="!item.aprovadoPublicacao" class="mt-3 rounded-lg border border-orange-200 bg-orange-50 px-3 py-2 text-sm text-orange-800">Aguardando aprovação. Este script não será enviado ao Assistente até a aprovação {{ item.aprovacaoOrigem === 'versao_ativa' ? 'da versão ativa' : 'do cadastro principal' }}.</p>
              <p v-if="item.erro && item.estado !== 'aguardando_aprovacao'" class="mt-3 break-words rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-800">{{ item.erro }}</p>
            </div>
            <div class="flex flex-wrap gap-2">
              <DsButton v-if="item.estado === 'suspenso'" size="sm" icon="play" :disabled="busy" @click="action(item.id, 'retomar')">Retomar publicação</DsButton>
              <DsButton v-else size="sm" variant="secondary" icon="arrow-repeat" :disabled="busy || !item.aprovadoPublicacao" @click="action(item.id, 'repetir')">Repetir</DsButton>
              <DsButton v-if="item.estado === 'falha'" size="sm" variant="secondary" icon="link-45deg" :disabled="busy || !item.aprovadoPublicacao" @click="candidates(item.id)">Conciliar existente</DsButton>
            </div>
            </div>
          </article>
          </div>
          <div class="mt-4 flex items-center justify-end gap-3">
            <DsButton variant="secondary" icon="chevron-left" aria-label="Página anterior" :disabled="page === 1 || busy" @click="changePage(-1)" />
            <span>{{ page }}</span>
            <DsButton variant="secondary" icon="chevron-right" aria-label="Próxima página" :disabled="page * 30 >= total || busy" @click="changePage(1)" />
          </div>
        </section>
      </template>
    </DsPageShell>

    <DsModal v-model="scriptModal" :title="scriptModalTitle">
      <fieldset :disabled="busy">
        <legend class="mb-2 text-sm font-medium">Especialidades específicas</legend>
        <DsSearchInput v-model="modalSearch" placeholder="Pesquisar especialidade" />
        <div class="mt-2 max-h-72 overflow-y-auto rounded-lg border border-gray-200 bg-gray-50 p-3">
          <label v-for="item in modalSpecialties" :key="item.id" class="flex items-start gap-2 py-2 text-sm">
            <input v-model="scriptSelected" type="checkbox" :value="item.id" class="mt-1 shrink-0">
            <span class="break-words">{{ item.nome }}</span>
          </label>
        </div>
      </fieldset>
      <p class="mt-3 text-sm text-gray-500">Deixe sem seleção para voltar ao padrão do pacote.</p>
      <div class="mt-4 flex justify-end gap-2">
        <DsButton variant="secondary" :disabled="busy" @click="scriptModal = false">Cancelar</DsButton>
        <DsButton icon="check-lg" :loading="busy" @click="saveScriptRule">Salvar regra</DsButton>
      </div>
    </DsModal>

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
type Publication = {
  id: number
  nome: string
  estado: string
  erro?: string
  publicadoEm?: string
  codDestino?: number
  packageId?: number
  packageName?: string
  sistema?: string
  aprovadoPublicacao: boolean
  aprovacaoOrigem: string
  aprovadoPor?: string
  dataAprovacao?: string
  numeroVersaoAtiva?: string
  especialidadesDestino: string[]
}
type PackageScript = {
  id: number
  nome: string
  sistema?: string
  linguagem?: string
  ativo: number
  estado?: string
  codDestino?: number
  aprovadoPublicacao: boolean
  aprovacaoOrigem: string
  aprovadoPor?: string
  dataAprovacao?: string
  numeroVersaoAtiva?: string
  usaRegraEspecifica: boolean
  especialidadesEspecificas: number[]
  especialidadesEfetivas: number[]
}
const packages = ref<Option[]>([])
const specialties = ref<Option[]>([])
const mappings = ref<{ pacote: number; especialidade: number }[]>([])
const items = ref<Publication[]>([])
const publicationSummary = ref<Record<string, number>>({})
const packageScripts = ref<PackageScript[]>([])
const packageId = ref('')
const selected = ref<number[]>([])
const checkedScripts = ref<number[]>([])
const search = ref('')
const scriptSearch = ref('')
const scriptStatus = ref('1')
const publicationSearch = ref('')
const publicationPackageFilter = ref('')
const publicationStatusFilter = ref('')
const publicationApprovalFilter = ref('')
const modalSearch = ref('')
const enabled = ref(false)
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const page = ref(1)
const scriptPage = ref(1)
const total = ref(0)
const packageScriptsTotal = ref(0)
const modal = ref(false)
const scriptModal = ref(false)
const editingScripts = ref<number[]>([])
const editingScriptName = ref('')
const reconcileId = ref(0)
const scriptSelected = ref<number[]>([])
const matches = ref<{ id: number; nome: string; conteudoIgual: boolean; tipoIgual: boolean; mrds: Option[] }[]>([])
const mrdSelections = ref<Record<number, string>>({})
const key = (row: Record<string, unknown>, name: string) => Object.keys(row).find(k => k.toLowerCase() === name.toLowerCase())
const read = <T,>(row: Record<string, unknown>, name: string, fallback: T): T => {
  const found = key(row, name)
  return (found ? row[found] : fallback) as T
}
const lower = <T,>(rows: Record<string, unknown>[]): T[] => rows.map(row => Object.fromEntries(Object.entries(row).map(([k, v]) => [k.toLowerCase(), v])) as T)
const asNumbers = (value: unknown): number[] => Array.isArray(value) ? value.map(Number).filter(Number.isFinite) : []
const filteredSpecialties = computed(() => specialties.value.filter(item => item.nome.toLocaleLowerCase().includes(search.value.toLocaleLowerCase())))
const modalSpecialties = computed(() => specialties.value.filter(item => item.nome.toLocaleLowerCase().includes(modalSearch.value.toLocaleLowerCase())))
const allVisibleChecked = computed(() => packageScripts.value.length > 0 && packageScripts.value.every(item => checkedScripts.value.includes(item.id)))
const scriptModalTitle = computed(() => editingScripts.value.length > 1 ? `Configurar ${editingScripts.value.length} scripts` : `Configurar ${editingScriptName.value}`)
const publicationStates = [
  { key: 'sincronizado', label: 'Sincronizados', icon: 'check-circle-fill', color: 'text-green-600' },
  { key: 'pendente', label: 'Pendentes', icon: 'clock-fill', color: 'text-orange-500' },
  { key: 'aguardando_aprovacao', label: 'Aguardando aprovação', icon: 'patch-exclamation-fill', color: 'text-orange-500' },
  { key: 'falha', label: 'Falhas', icon: 'x-circle-fill', color: 'text-rose-600' },
  { key: 'suspenso', label: 'Suspensos', icon: 'pause-circle-fill', color: 'text-gray-500' }
]
const publicationSummaryCards = computed(() => publicationStates.map(item => ({ ...item, count: publicationSummary.value[item.key] || 0 })))
watch(packageId, async () => {
  selected.value = mappings.value.filter(m => m.pacote === Number(packageId.value)).map(m => m.especialidade)
  scriptPage.value = 1
  checkedScripts.value = []
  await loadPackageScripts()
})
watch(scriptSearch, async () => {
  scriptPage.value = 1
  checkedScripts.value = []
  await loadPackageScripts()
})
watch(scriptStatus, async () => {
  scriptPage.value = 1
  checkedScripts.value = []
  await loadPackageScripts()
})
watch([publicationSearch, publicationPackageFilter, publicationStatusFilter, publicationApprovalFilter], async () => {
  page.value = 1
  await loadPublications()
})
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
    if (packageId.value)
      selected.value = mappings.value.filter(m => m.pacote === Number(packageId.value)).map(m => m.especialidade)
    await loadPublications()
    await loadPackageScripts()
  } catch (e) { error.value = e instanceof Error ? e.message : 'Falha ao carregar publicação.' }
  finally { loading.value = false; busy.value = false }
}
async function loadPublications() {
  if (!enabled.value) return
  const query = new URLSearchParams({ page: String(page.value) })
  if (publicationSearch.value.trim()) query.set('search', publicationSearch.value.trim())
  if (publicationPackageFilter.value) query.set('pacote', publicationPackageFilter.value)
  if (publicationStatusFilter.value) query.set('estado', publicationStatusFilter.value)
  if (publicationApprovalFilter.value) query.set('aprovado', publicationApprovalFilter.value)
  const result = await api.get<{ items: Record<string, unknown>[]; total: number; summary?: Record<string, number> }>(`${root}/scripts?${query}`)
  items.value = result.items.map(mapPublication)
  total.value = result.total
  publicationSummary.value = result.summary || {}
}
async function loadPackageScripts() {
  if (!packageId.value || !enabled.value) {
    packageScripts.value = []
    packageScriptsTotal.value = 0
    return
  }
  const query = new URLSearchParams({ page: String(scriptPage.value) })
  query.set('ativo', scriptStatus.value)
  if (scriptSearch.value.trim()) query.set('search', scriptSearch.value.trim())
  const result = await api.get<{ items: Record<string, unknown>[]; total: number }>(`${root}/pacotes/${packageId.value}/scripts?${query}`)
  packageScripts.value = result.items.map(row => ({
    id: Number(read(row, 'id', 0)),
    nome: String(read(row, 'nome', '')),
    sistema: read<string | undefined>(row, 'sistema', undefined),
    linguagem: read<string | undefined>(row, 'linguagem', undefined),
    ativo: Number(read(row, 'ativo', 0)),
    estado: read<string | undefined>(row, 'estado', undefined),
    codDestino: read<number | undefined>(row, 'codDestino', undefined),
    aprovadoPublicacao: Boolean(read(row, 'aprovadoPublicacao', false)),
    aprovacaoOrigem: String(read(row, 'aprovacaoOrigem', 'script')),
    aprovadoPor: read<string | undefined>(row, 'aprovadoPor', undefined),
    dataAprovacao: read<string | undefined>(row, 'dataAprovacao', undefined),
    numeroVersaoAtiva: read<string | undefined>(row, 'numeroVersaoAtiva', undefined),
    usaRegraEspecifica: Boolean(read(row, 'usaRegraEspecifica', false)),
    especialidadesEspecificas: asNumbers(read(row, 'especialidadesEspecificas', [])),
    especialidadesEfetivas: asNumbers(read(row, 'especialidadesEfetivas', []))
  }))
  packageScriptsTotal.value = result.total
  const visible = new Set(packageScripts.value.map(x => x.id))
  checkedScripts.value = checkedScripts.value.filter(id => visible.has(id))
}
async function perform(task: () => Promise<unknown>) {
  busy.value = true
  try { await task(); await load() }
  catch (e) { await swal.error('Publicação', e instanceof Error ? e.message : 'Falha na operação.') }
  finally { busy.value = false }
}
async function save() {
  if (!(await swal.confirm('Salvar associação?', 'Scripts sem regra específica serão publicados nas especialidades padrão do pacote.')).isConfirmed) return
  await perform(() => api.put(`${root}/pacotes/${packageId.value}`, { especialidades: selected.value }))
}
function names(ids: number[]) {
  if (!ids.length) return 'Nenhuma especialidade definida'
  return ids.map(id => specialties.value.find(item => item.id === id)?.nome || `#${id}`).join(', ')
}
function mapPublication(row: Record<string, unknown>): Publication {
  return {
    id: Number(read(row, 'id', 0)),
    nome: String(read(row, 'nome', '')),
    estado: String(read(row, 'estado', 'pendente')),
    erro: read<string | undefined>(row, 'erro', undefined),
    publicadoEm: read<string | undefined>(row, 'publicadoEm', undefined),
    codDestino: read<number | undefined>(row, 'codDestino', undefined),
    packageId: read<number | undefined>(row, 'packageId', undefined),
    packageName: read<string | undefined>(row, 'packageName', undefined),
    sistema: read<string | undefined>(row, 'sistema', undefined),
    aprovadoPublicacao: Boolean(read(row, 'aprovadoPublicacao', false)),
    aprovacaoOrigem: String(read(row, 'aprovacaoOrigem', 'script')),
    aprovadoPor: read<string | undefined>(row, 'aprovadoPor', undefined),
    dataAprovacao: read<string | undefined>(row, 'dataAprovacao', undefined),
    numeroVersaoAtiva: read<string | undefined>(row, 'numeroVersaoAtiva', undefined),
    especialidadesDestino: asStrings(read(row, 'especialidadesDestino', []))
  }
}
function asStrings(value: unknown): string[] {
  return Array.isArray(value) ? value.map(String).filter(Boolean) : []
}
function stateVariant(state?: string) {
  if (state === 'sincronizado') return 'success'
  if (state === 'falha') return 'danger'
  if (state === 'suspenso') return 'neutral'
  return 'warning'
}
function stateLabel(state?: string) {
  return publicationStates.find(item => item.key === state)?.label.replace(/s$/, '') || state || 'Pendente'
}
function approvalText(item: { aprovadoPublicacao: boolean; aprovacaoOrigem?: string }) {
  if (item.aprovadoPublicacao) return item.aprovacaoOrigem === 'versao_ativa' ? 'Versão aprovada' : 'Aprovado'
  return item.aprovacaoOrigem === 'versao_ativa' ? 'Versão pendente' : 'Aguardando aprovação'
}
function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString('pt-BR') : 'Ainda não publicado'
}
function destinationSpecialtiesText(item: Publication) {
  if (!item.codDestino) return 'Não publicado'
  return item.especialidadesDestino.length ? item.especialidadesDestino.join(', ') : 'Sem especialidade vinculada'
}
function togglePublicationStatus(state: string) {
  publicationStatusFilter.value = publicationStatusFilter.value === state ? '' : state
}
function toggleVisibleScripts(event: Event) {
  const checked = (event.target as HTMLInputElement).checked
  const ids = packageScripts.value.map(item => item.id)
  checkedScripts.value = checked
    ? Array.from(new Set([...checkedScripts.value, ...ids]))
    : checkedScripts.value.filter(id => !ids.includes(id))
}
function openScriptEdit(item: PackageScript) {
  editingScripts.value = [item.id]
  editingScriptName.value = `#${item.id} · ${item.nome}`
  scriptSelected.value = [...item.especialidadesEspecificas]
  modalSearch.value = ''
  scriptModal.value = true
}
function openBulkEdit() {
  editingScripts.value = [...checkedScripts.value]
  editingScriptName.value = ''
  scriptSelected.value = []
  modalSearch.value = ''
  scriptModal.value = true
}
async function saveScriptRule() {
  const ids = [...scriptSelected.value]
  if (editingScripts.value.length === 1)
    await perform(() => api.put(`${root}/pacotes/${packageId.value}/scripts/${editingScripts.value[0]}`, { especialidades: ids }))
  else
    await perform(() => api.put(`${root}/pacotes/${packageId.value}/scripts`, { scripts: editingScripts.value, especialidades: ids }))
  scriptModal.value = false
}
async function clearScriptRule(item: PackageScript) {
  if (!(await swal.confirm('Voltar ao padrão?', 'Este script deixará de ter regra específica e passará a usar as especialidades do pacote.')).isConfirmed) return
  await perform(() => api.put(`${root}/pacotes/${packageId.value}/scripts/${item.id}`, { especialidades: [] }))
}
async function clearBulkRule() {
  if (!(await swal.confirm('Voltar selecionados ao padrão?', 'Os scripts selecionados deixarão de ter regra específica.')).isConfirmed) return
  await perform(() => api.put(`${root}/pacotes/${packageId.value}/scripts`, { scripts: checkedScripts.value, especialidades: [] }))
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
async function changePage(delta: number) { page.value += delta; await loadPublications() }
async function changeScriptPage(delta: number) { scriptPage.value += delta; checkedScripts.value = []; await loadPackageScripts() }
onMounted(load)
</script>
