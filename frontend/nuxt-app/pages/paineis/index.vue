<template>
  <div>
    <DsPageHeader :title="headerTitle" :subtitle="headerSubtitle" icon="bar-chart-line">
      <template #actions>
        <DsButton v-if="auth.isAdmin" variant="secondary" size="sm" icon="gear" to="/paineis/cadastros">Cadastros</DsButton>
      </template>
    </DsPageHeader>

    <DsPageShell>
      <!-- Tipo hub -->
      <template v-if="view === 'tipo'">
        <DsSectionTitle title="Escolha o tipo" subtitle="Filtrar listagem de painéis" />
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-6 max-w-4xl mx-auto">
          <DsHubCard
            title="Todos"
            desc="Todos os painéis cadastrados"
            icon="collection"
            theme-name="gray"
            @click="goLista('todos')"
          />
          <DsHubCard
            title="Power BI"
            :desc="`${stats.powerbi} ativo(s)`"
            icon="graph-up"
            theme-name="green"
            :badge="String(stats.powerbi)"
            @click="goPacotes"
          />
          <DsHubCard
            title="API"
            :desc="`${stats.api} ativo(s)`"
            icon="braces"
            theme-name="orange"
            :badge="String(stats.api)"
            @click="goModulos"
          />
        </div>
        <div class="flex justify-center mt-8">
          <DsButton variant="success" icon="plus-lg" to="/paineis/nova">Novo painel</DsButton>
        </div>
      </template>

      <!-- Pacotes (Power BI) -->
      <template v-else-if="view === 'pacotes'">
        <div class="flex justify-between mb-4">
          <DsButton variant="secondary" size="sm" icon="arrow-left" @click="router.push('/paineis')">Voltar</DsButton>
          <DsButton size="sm" icon="list" @click="goLista('powerbi')">Ver todos Power BI</DsButton>
        </div>
        <DsSectionTitle title="Pacotes comerciais" subtitle="Selecione um pacote para filtrar" />
        <div v-if="loadingNav" class="text-center py-8 text-gray-500">Carregando...</div>
        <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          <DsHubCard
            v-for="(p, i) in pacotes"
            :key="p.codPacote"
            :title="p.nome"
            desc="Filtrar por este pacote"
            icon="box-seam"
            theme-name="green"
            :delay-index="i"
            @click="goLista('powerbi', { pacoteId: p.codPacote })"
          />
        </div>
      </template>

      <!-- Módulos (API) -->
      <template v-else-if="view === 'modulos'">
        <div class="flex justify-between mb-4">
          <DsButton variant="secondary" size="sm" icon="arrow-left" @click="router.push('/paineis')">Voltar</DsButton>
          <DsButton size="sm" icon="list" @click="goLista('api')">Ver todos API</DsButton>
        </div>
        <DsSectionTitle title="Módulos" subtitle="Selecione um módulo para filtrar" />
        <div v-if="loadingNav" class="text-center py-8 text-gray-500">Carregando...</div>
        <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          <DsHubCard
            v-for="(m, i) in modulos"
            :key="m.codModulo"
            :title="m.nome"
            desc="Filtrar por este módulo"
            icon="folder2-open"
            theme-name="orange"
            :delay-index="i"
            @click="goLista('api', { moduloId: m.codModulo })"
          />
        </div>
      </template>

      <!-- Lista -->
      <template v-else>
        <div class="flex flex-wrap justify-between gap-2 mb-4">
          <DsButton variant="secondary" size="sm" icon="arrow-left" @click="voltarNav">Voltar</DsButton>
          <DsButton variant="success" size="sm" icon="plus-lg" :to="novaLink">Novo painel</DsButton>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-12 gap-3 mb-6 items-end">
          <div class="lg:col-span-4">
            <DsInput v-model="filtros.search" label="Buscar" placeholder="Nome ou descrição" @enter="load(1)" />
          </div>
          <div class="lg:col-span-2">
            <DsSelect v-model="filtros.ativo" label="Status">
              <option value="">Todos</option>
              <option value="1">Ativo</option>
              <option value="0">Inativo</option>
            </DsSelect>
          </div>
          <div class="lg:col-span-3">
            <DsSelect v-model="filtros.clienteId" label="Cliente">
              <option value="">Todos</option>
              <option v-for="c in clientes" :key="c.codCliente" :value="c.codCliente">{{ c.nome }}</option>
            </DsSelect>
          </div>
          <div class="lg:col-span-3 flex justify-end gap-2">
            <DsButton variant="secondary" size="sm" icon="eraser" @click="limparFiltros">Limpar</DsButton>
            <DsButton size="sm" icon="search" @click="load(1)">Filtrar</DsButton>
          </div>
        </div>

        <div v-if="loading" class="text-center py-8 text-gray-500">Carregando painéis...</div>
        <DsAlert v-else-if="errorMsg" variant="error">{{ errorMsg }}</DsAlert>
        <DsAlert v-else-if="!items.length" variant="info">Nenhum painel encontrado.</DsAlert>

        <DsTable v-else>
          <template #head>
            <tr>
              <th>#</th>
              <th>Nome</th>
              <th>Tipo</th>
              <th>Cliente</th>
              <th>Módulo</th>
              <th>Versão</th>
              <th>Status</th>
              <th />
            </tr>
          </template>
          <tr v-for="item in items" :key="item.codpainel">
            <td>{{ item.codpainel }}</td>
            <td>
              <p class="font-semibold mb-0">{{ item.nome }}</p>
              <small class="text-gray-500">{{ (item.pacotes || []).join(', ') || '—' }}</small>
            </td>
            <td>{{ item.tipo_painel }}</td>
            <td>{{ item.nome_cliente || '—' }}</td>
            <td>{{ item.nome_modulo || '—' }}</td>
            <td>{{ item.ultima_versao || '—' }}</td>
            <td>{{ item.ativo ? 'Ativo' : 'Inativo' }}</td>
            <td>
              <div class="flex flex-wrap justify-end gap-2">
                <DsButton variant="ghost" size="sm" icon="eye-fill" :to="`/paineis/${item.codpainel}`">Ver</DsButton>
                <DsButton variant="secondary" size="sm" icon="pencil-square" :to="`/paineis/${item.codpainel}/editar`">Editar</DsButton>
                <DsButton
                  v-if="item.tem_arquivo_pbix"
                  variant="secondary"
                  size="sm"
                  icon="download"
                  @click="baixar(item)"
                >
                  PBIX
                </DsButton>
                <DsButton variant="secondary" size="sm" @click="toggleStatus(item)">
                  {{ item.ativo ? 'Desativar' : 'Ativar' }}
                </DsButton>
                <DsButton
                  v-if="auth.isAdmin"
                  variant="danger"
                  size="sm"
                  icon="trash-fill"
                  @click="excluir(item)"
                >
                  Excluir
                </DsButton>
              </div>
            </td>
          </tr>
        </DsTable>

        <div v-if="totalPages > 1" class="flex justify-center gap-2 mt-6">
          <DsButton variant="secondary" size="sm" :disabled="page <= 1" @click="load(page - 1)">Anterior</DsButton>
          <DsButton
            v-for="p in pagesToShow"
            :key="p"
            :variant="p === page ? 'primary' : 'secondary'"
            size="sm"
            @click="load(p)"
          >
            {{ p }}
          </DsButton>
          <DsButton variant="secondary" size="sm" :disabled="page >= totalPages" @click="load(page + 1)">Próxima</DsButton>
        </div>
        <p v-if="!loading" class="text-sm text-gray-500 text-center mt-2">{{ totalItems }} painel(éis)</p>
      </template>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { PainelItem } from '~/composables/usePaineisApi'

definePageMeta({ layout: 'default' })

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const api = usePaineisApi()
const swal = useSwal()

const view = computed(() => {
  if (route.query.view === 'lista' || route.query.tipo) return 'lista'
  if (route.query.view === 'pacotes') return 'pacotes'
  if (route.query.view === 'modulos') return 'modulos'
  return 'tipo'
})

const headerTitle = computed(() => {
  if (view.value === 'lista') return 'Painéis'
  if (view.value === 'pacotes') return 'Power BI — Pacotes'
  if (view.value === 'modulos') return 'API — Módulos'
  return 'Painéis'
})

const headerSubtitle = computed(() => {
  if (view.value === 'lista') return 'Catálogo de painéis Power BI e API'
  return 'Navegação e filtros do módulo de painéis'
})

const loading = ref(false)
const loadingNav = ref(false)
const errorMsg = ref('')
const items = ref<PainelItem[]>([])
const page = ref(1)
const totalPages = ref(0)
const totalItems = ref(0)
const stats = reactive({ powerbi: 0, api: 0 })
const clientes = ref<{ codCliente: number; nome: string }[]>([])
const modulos = ref<{ codModulo: number; nome: string }[]>([])
const pacotes = ref<{ codPacote: number; nome: string }[]>([])

const filtros = reactive({
  search: '',
  ativo: '',
  clienteId: '',
  tipo: 'todos',
  moduloId: '' as string | number,
  pacoteId: '' as string | number
})

const novaLink = computed(() => {
  const tipo = String(filtros.tipo || 'todos').toLowerCase()
  if (tipo === 'powerbi' || tipo === 'api') return `/paineis/nova?tipo=${tipo}`
  return '/paineis/nova'
})

const pagesToShow = computed(() => {
  const max = totalPages.value
  if (max <= 7) return Array.from({ length: max }, (_, i) => i + 1)
  const start = Math.max(1, page.value - 2)
  const end = Math.min(max, start + 4)
  return Array.from({ length: end - start + 1 }, (_, i) => start + i)
})

function goLista(tipo: string, extra: Record<string, string | number> = {}) {
  router.push({
    path: '/paineis',
    query: { view: 'lista', tipo, ...Object.fromEntries(Object.entries(extra).map(([k, v]) => [k, String(v)])) }
  })
}

function goPacotes() {
  router.push({ path: '/paineis', query: { view: 'pacotes' } })
}

function goModulos() {
  router.push({ path: '/paineis', query: { view: 'modulos' } })
}

function voltarNav() {
  const tipo = String(route.query.tipo || '').toLowerCase()
  if (tipo === 'powerbi') router.push({ path: '/paineis', query: { view: 'pacotes' } })
  else if (tipo === 'api') router.push({ path: '/paineis', query: { view: 'modulos' } })
  else router.push('/paineis')
}

function syncFromRoute() {
  filtros.tipo = typeof route.query.tipo === 'string' ? route.query.tipo : 'todos'
  filtros.moduloId = typeof route.query.moduloId === 'string' ? route.query.moduloId : ''
  filtros.pacoteId = typeof route.query.pacoteId === 'string' ? route.query.pacoteId : ''
}

async function loadStats() {
  try {
    const res = await api.getStats()
    stats.powerbi = res.powerbi || 0
    stats.api = res.api || 0
  } catch {
    stats.powerbi = 0
    stats.api = 0
  }
}

async function loadLookups() {
  loadingNav.value = true
  try {
    const [c, m, p] = await Promise.all([api.listClientes(), api.listModulos(), api.listPacotes()])
    clientes.value = c.data || []
    modulos.value = m.data || []
    pacotes.value = p.data || []
  } finally {
    loadingNav.value = false
  }
}

async function load(p = page.value) {
  loading.value = true
  errorMsg.value = ''
  page.value = p
  try {
    const res = await api.listPaineis({
      page: p,
      perPage: 10,
      search: filtros.search,
      ativo: filtros.ativo,
      clienteId: filtros.clienteId,
      tipo: filtros.tipo,
      moduloId: filtros.moduloId,
      pacoteId: filtros.pacoteId
    })
    items.value = res.data || []
    totalPages.value = res.totalPages || 0
    totalItems.value = res.totalItems || 0
  } catch (err) {
    items.value = []
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar painéis.'
  } finally {
    loading.value = false
  }
}

function limparFiltros() {
  filtros.search = ''
  filtros.ativo = ''
  filtros.clienteId = ''
  load(1)
}

async function baixar(item: PainelItem) {
  try {
    await api.downloadPbix(item.codpainel, item.nome_arquivo_pbix || `${item.nome}.pbix`)
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro no download', 'error')
  }
}

async function toggleStatus(item: PainelItem) {
  try {
    await api.setStatus(item.codpainel, item.ativo ? 0 : 1)
    await swal.toast('Status atualizado.')
    await load(page.value)
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao alterar status', 'error')
  }
}

async function excluir(item: PainelItem) {
  const ok = await swal.confirm('Excluir painel', `Excluir "${item.nome}" e todas as versões?`)
  if (!ok?.isConfirmed) return
  try {
    await api.deletePainel(item.codpainel)
    await swal.toast('Painel excluído.')
    await load(1)
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao excluir', 'error')
  }
}

watch(
  () => route.query,
  async () => {
    syncFromRoute()
    if (view.value === 'tipo') {
      await loadStats()
    } else if (view.value === 'pacotes' || view.value === 'modulos') {
      await loadLookups()
    } else {
      if (!clientes.value.length) await loadLookups()
      await load(1)
    }
  },
  { immediate: true }
)
</script>
