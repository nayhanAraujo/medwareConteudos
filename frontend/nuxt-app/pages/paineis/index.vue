<template>
  <div>
    <DsPageHeader :title="headerTitle" :subtitle="headerSubtitle" icon="bar-chart-line">
      <template #actions>
        <DsButton v-if="canEditPaineis" variant="secondary" size="sm" icon="gear" to="/paineis/cadastros">Cadastros</DsButton>
      </template>
    </DsPageHeader>

    <DsPageShell>
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
          <DsButton v-if="canCreatePaineis" variant="success" icon="plus-lg" to="/paineis/nova">Novo painel</DsButton>
        </div>
      </template>

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

      <template v-else>
        <input
          ref="imageUploadInput"
          class="hidden"
          type="file"
          accept="image/jpeg,image/png,image/gif"
          multiple
          @change="uploadSelectedImages"
        >

        <div class="mb-5 flex flex-wrap items-center justify-between gap-3">
          <DsButton variant="secondary" size="sm" icon="arrow-left" @click="voltarNav">Voltar</DsButton>
          <DsButton v-if="canCreatePaineis" variant="success" size="sm" icon="plus-lg" :to="novaLink">Novo painel</DsButton>
        </div>

        <div class="space-y-4">
          <aside>
            <section class="p-1">
              <h2 class="mb-2 font-manrope text-base font-semibold text-ds-text">Explorar</h2>
              <div class="grid grid-cols-1 items-end gap-2 md:grid-cols-2 xl:grid-cols-[minmax(220px,1.4fr)_minmax(160px,0.8fr)_minmax(180px,0.9fr)_auto]">
                <DsInput v-model="filtros.search" label="Buscar" placeholder="Nome ou descrição" @enter="load(1)" />
                <DsSelect v-model="filtros.ativo" label="Status">
                  <option value="">Todos</option>
                  <option value="1">Ativo</option>
                  <option value="0">Inativo</option>
                </DsSelect>
                <DsSelect v-model="filtros.clienteId" label="Cliente">
                  <option value="">Todos</option>
                  <option v-for="c in clientes" :key="c.codCliente" :value="c.codCliente">{{ c.nome }}</option>
                </DsSelect>
                <div class="flex flex-wrap gap-2 md:col-span-2 xl:col-span-1">
                  <DsButton class="min-w-[120px] flex-1" variant="secondary" size="md" icon="eraser" @click="limparFiltros">Limpar</DsButton>
                  <DsButton class="min-w-[120px] flex-1" size="md" icon="search" @click="load(1)">Filtrar</DsButton>
                </div>
              </div>
            </section>
          </aside>

          <main class="min-w-0 rounded-2xl border border-white/70 bg-white/55 p-4 shadow-ds-card sm:p-5">
            <div class="mb-5 flex flex-wrap items-end justify-between gap-3">
              <div>
                <h2 class="font-manrope text-2xl font-semibold text-ds-text">Meus Painéis</h2>
                <p class="mt-1 text-sm text-gray-500">{{ totalItems }} painel(éis)</p>
              </div>
              <DsButton v-if="canCreatePaineis" variant="success" size="sm" icon="plus-lg" :to="novaLink">Novo painel</DsButton>
            </div>

            <div v-if="loading" class="py-12 text-center text-gray-500">Carregando painéis...</div>
            <DsAlert v-else-if="errorMsg" variant="error">{{ errorMsg }}</DsAlert>
            <DsAlert v-else-if="!items.length" variant="info">Nenhum painel encontrado.</DsAlert>

            <div v-else class="grid grid-cols-1 gap-6 xl:grid-cols-2">
              <article
                v-for="item in items"
                :key="item.codpainel"
                class="flex min-h-[440px] flex-col overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm transition hover:shadow-ds"
              >
                <header class="flex min-h-12 items-center justify-between gap-3 px-4 py-2">
                  <div class="flex min-w-0 items-center gap-2">
                    <span class="shrink-0 text-xs text-gray-500">#{{ item.codpainel }}</span>
                    <h3 class="truncate font-manrope text-sm font-semibold text-ds-text" :title="item.nome">{{ item.nome }}</h3>
                  </div>
                  <span
                    class="shrink-0 rounded-full px-2 py-0.5 text-xs font-medium"
                    :class="item.tipo_painel === 'POWERBI' ? 'bg-emerald-50 text-emerald-700' : 'bg-orange-50 text-orange-700'"
                  >
                    {{ item.tipo_painel === 'POWERBI' ? 'Power BI' : 'API' }}
                  </span>
                </header>

                <div class="flex flex-wrap items-center gap-x-4 gap-y-1 border-t border-gray-100 px-4 py-2 text-xs text-gray-600">
                  <span class="font-medium text-ds-text">{{ item.ultima_versao || '-' }}</span>
                  <span class="truncate" :title="item.nome_modulo || 'Sem módulo'">{{ item.nome_modulo || 'Sem módulo' }}</span>
                </div>

                <button
                  type="button"
                  class="relative flex min-h-[300px] flex-1 items-center justify-center overflow-hidden border-y border-gray-100 bg-slate-100 text-left cursor-zoom-in"
                  :aria-label="`Ver imagens da última versão de ${item.nome}`"
                  @click="galleryPanel = item"
                >
                    <img
                      v-if="thumbnailSrc(item)"
                      class="absolute inset-0 h-full w-full object-contain"
                      :src="thumbnailSrc(item)"
                      :alt="`Pré-visualização do painel ${item.nome}`"
                    >
                    <div v-else class="p-6 text-center text-sm text-gray-500">
                      <i class="bi bi-images mb-2 block text-3xl" />
                      {{ thumbnailErrors[item.codpainel] ? 'Prévia indisponível. Clique para abrir a galeria.' : cardCanPreview(item) ? 'Carregando pré-visualização...' : 'Última versão sem imagens' }}
                    </div>
                </button>

                <footer class="flex min-h-[64px] flex-wrap items-center gap-2 px-4 py-3">
                  <DsButton variant="ghost" size="sm" icon="eye-fill" @click="galleryPanel = item">Ver</DsButton>
                  <DsButton variant="secondary" size="sm" icon="info-circle" :to="`/paineis/${item.codpainel}`">Detalhes e versões</DsButton>
                  <DsButton v-if="canEditPaineis" variant="secondary" size="sm" icon="pencil-square" :to="`/paineis/${item.codpainel}/editar`">Editar</DsButton>
                  <DsButton
                    v-if="item.tem_arquivo_pbix"
                    variant="secondary"
                    size="sm"
                    icon="download"
                    @click="baixar(item)"
                  >
                    PBIX
                  </DsButton>
                  <DsButton
                    v-if="canEditPaineis"
                    variant="secondary"
                    size="sm"
                    icon="image"
                    @click="selectImages(item)"
                  >
                    Imagem
                  </DsButton>
                  <button
                    type="button"
                    class="inline-flex h-9 w-14 items-center rounded-full border border-gray-200 p-1 transition xl:ml-auto"
                    :class="item.ativo ? 'bg-[#34456f]' : 'bg-gray-200'"
                    :aria-checked="!!item.ativo"
                    :title="item.ativo ? 'Desativar' : 'Ativar'"
                    role="switch"
                    :disabled="!canActivatePaineis"
                    @click="toggleStatus(item)"
                  >
                    <span
                      class="h-7 w-7 rounded-full bg-white shadow-sm transition"
                      :class="item.ativo ? 'translate-x-5' : 'translate-x-0'"
                    />
                  </button>
                  <DsButton
                    v-if="canDeletePaineis"
                    variant="danger"
                    size="sm"
                    icon="trash-fill"
                    @click="excluir(item)"
                  >
                    Excluir
                  </DsButton>
                </footer>
              </article>
            </div>

            <div v-if="totalPages > 1" class="mt-6 flex flex-wrap justify-center gap-2">
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
          </main>
        </div>
      </template>
    </DsPageShell>
    <PainelGallery :painel="galleryPanel" @close="galleryPanel = null" />
  </div>
</template>

<script setup lang="ts">
import PainelGallery from '~/components/paineis/PainelGallery.vue'
import type { PainelItem } from '~/composables/usePaineisApi'

definePageMeta({ layout: 'default' })

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const api = usePaineisApi()
const versoesApi = usePaineisVersoesApi()
const swal = useSwal()

const canCreatePaineis = computed(() => auth.can('paineis', 'criar'))
const canEditPaineis = computed(() => auth.can('paineis', 'editar'))
const canDeletePaineis = computed(() => auth.can('paineis', 'excluir'))
const canActivatePaineis = computed(() => auth.can('paineis', 'ativar'))

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
const galleryPanel = ref<PainelItem | null>(null)
const thumbnailErrors = reactive<Record<number, boolean>>({})
const thumbnailObjectUrls = reactive<Record<number, string>>({})
const imageUploadInput = ref<HTMLInputElement | null>(null)
const imageUploadTarget = ref<PainelItem | null>(null)

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

function cardCanPreview(item: PainelItem) {
  return !!panelThumbnailPath(item)
}

function panelThumbnailPath(item: PainelItem) {
  return item.thumbnailUrl || item.thumbnail_url || null
}

function panelLatestVersionId(item: PainelItem) {
  return item.thumbnailVersionId || item.thumbnail_version_id || null
}

function thumbnailSrc(item: PainelItem) {
  return thumbnailObjectUrls[item.codpainel] || ''
}

function clearUnusedThumbnailUrls() {
  for (const [key, value] of Object.entries(thumbnailObjectUrls)) {
      URL.revokeObjectURL(value)
      delete thumbnailObjectUrls[Number(key)]
  }
}

async function loadThumbnails() {
  if (!import.meta.client) return
  clearUnusedThumbnailUrls()
  await Promise.all(items.value.map(async (item) => {
    const path = panelThumbnailPath(item)
    if (!path || thumbnailObjectUrls[item.codpainel]) return
    thumbnailErrors[item.codpainel] = false
    try {
      const blob = await api.getThumbnailBlob(path)
      thumbnailObjectUrls[item.codpainel] = URL.createObjectURL(blob)
    } catch {
      thumbnailErrors[item.codpainel] = true
      delete thumbnailObjectUrls[item.codpainel]
    }
  }))
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
    await loadThumbnails()
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

async function selectImages(item: PainelItem) {
  if (!canEditPaineis.value) return
  const versaoId = panelLatestVersionId(item)
  if (!versaoId) {
    await swal.toast('Crie uma versão para este painel antes de anexar imagens.', 'error')
    return
  }
  imageUploadTarget.value = item
  if (imageUploadInput.value) {
    imageUploadInput.value.value = ''
    imageUploadInput.value.click()
  }
}

async function uploadSelectedImages(event: Event) {
  if (!canEditPaineis.value) return
  const input = event.target as HTMLInputElement
  const files = Array.from(input.files || [])
  const item = imageUploadTarget.value
  const versaoId = item ? panelLatestVersionId(item) : null
  if (!item || !versaoId || !files.length) return

  try {
    const form = new FormData()
    files.forEach((file) => form.append('imagens', file))
    const res = await versoesApi.addImages(item.codpainel, Number(versaoId), form)
    await swal.toast(`${res.data?.total || files.length} imagem(ns) anexada(s).`)
    if (thumbnailObjectUrls[item.codpainel]) {
      URL.revokeObjectURL(thumbnailObjectUrls[item.codpainel])
      delete thumbnailObjectUrls[item.codpainel]
    }
    await load(page.value)
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao anexar imagens.', 'error')
  } finally {
    imageUploadTarget.value = null
    input.value = ''
  }
}

async function toggleStatus(item: PainelItem) {
  if (!canActivatePaineis.value) return
  try {
    await api.setStatus(item.codpainel, item.ativo ? 0 : 1)
    await swal.toast('Status atualizado.')
    await load(page.value)
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao alterar status', 'error')
  }
}

async function excluir(item: PainelItem) {
  if (!canDeletePaineis.value) return
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
      await loadStats()
      if (!clientes.value.length) await loadLookups()
      await load(1)
    }
  },
  { immediate: true }
)

onBeforeUnmount(() => {
  for (const url of Object.values(thumbnailObjectUrls)) URL.revokeObjectURL(url)
})
</script>
