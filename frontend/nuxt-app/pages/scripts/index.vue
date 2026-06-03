<template>
  <div>
    <DsPageHeader title="Scripts Cadastrados" icon="card-checklist" />
    <DsAlert v-if="!hasParams" variant="warning" class="mb-4">
      Esta página requer pacote e sistema.
      <DsButton size="sm" to="/scripts/sistema" class="ml-2">Seleção de Sistema</DsButton>
    </DsAlert>
    <template v-else>
      <ScriptsScriptsBreadcrumb :sistema="sistema" :pacote="pacote" :pacote-nome="pacoteNome" />
      <DsPageShell>
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-3 mb-6 items-end">
          <div class="lg:col-span-4">
            <label class="block text-sm font-medium text-ds-text mb-1.5">Buscar</label>
            <DsSearchInput
              v-model="filtros.nome"
              placeholder="Buscar por nome..."
              wrapper-class="mb-0"
              @enter="load(1)"
            />
          </div>
          <div class="lg:col-span-2">
            <DsSelect v-model="filtros.aprovado" label="Aprovado">
              <option value="">Todos</option>
              <option value="1">Aprovado</option>
              <option value="0">Não aprovado</option>
            </DsSelect>
          </div>
          <div class="lg:col-span-2">
            <DsSelect v-model="filtros.ativo" label="Ativo">
              <option value="1">Somente ativos</option>
              <option value="0">Somente inativos</option>
              <option value="">Todos</option>
            </DsSelect>
          </div>
          <div class="lg:col-span-4 flex flex-wrap gap-2 justify-end">
            <DsButton variant="secondary" size="sm" icon="funnel-fill" @click="filterModalOpen = true">Filtros</DsButton>
            <DsButton :variant="viewMode === 'cards' ? 'primary' : 'secondary'" size="sm" icon="grid-3x3-gap-fill" @click="viewMode = 'cards'" />
            <DsButton :variant="viewMode === 'list' ? 'primary' : 'secondary'" size="sm" icon="list-ul" @click="viewMode = 'list'" />
            <DsButton v-if="auth.isAdmin" variant="success" size="sm" icon="plus-circle-fill" :to="{ path: '/scripts/novo', query: { pacote, sistema } }">
              Novo Script
            </DsButton>
          </div>
        </div>

        <div v-if="viewMode === 'list'" class="rounded-2xl border border-blue-100 bg-blue-50/50 p-4 mb-4">
          <div class="flex flex-wrap justify-between items-center gap-3">
            <div class="flex items-center gap-3">
              <label class="flex items-center gap-2 text-sm font-medium">
                <input id="selectAllScripts" v-model="selectAll" type="checkbox" class="rounded" @change="toggleSelectAll" />
                Selecionar Todos
              </label>
              <span class="text-sm text-gray-600">{{ selectedCountLabel }}</span>
            </div>
            <div class="flex flex-wrap gap-2">
              <DsButton variant="secondary" size="sm" :disabled="!hasJsonSelected" @click="exportSelected('json')">JSON</DsButton>
              <DsButton variant="secondary" size="sm" :disabled="!hasDllSelected" @click="exportSelected('dll')">DLL</DsButton>
              <DsButton variant="secondary" size="sm" :disabled="!hasMrdSelected" @click="exportSelected('mrd')">MRD</DsButton>
            </div>
          </div>
        </div>

        <div v-if="loading" class="text-center py-10 text-gray-500">Carregando scripts...</div>
        <DsAlert v-else-if="errorMsg" variant="error">{{ errorMsg }}</DsAlert>
        <DsAlert v-else-if="!items.length" variant="info">Nenhum script encontrado para os filtros selecionados.</DsAlert>

        <div v-else-if="viewMode === 'cards'" class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
          <article
            v-for="item in items"
            :key="item.codScriptLaudo"
            class="rounded-xl border border-gray-200 bg-white hover:shadow-ds transition-shadow flex flex-col overflow-hidden"
          >
            <div class="aspect-[16/9] bg-ds-surface overflow-hidden">
              <img
                v-if="item.imagensDisplay?.[0]?.caminho"
                :src="mediaUrl(item.imagensDisplay[0].caminho)"
                :alt="item.nome"
                class="w-full h-full object-cover"
              />
              <div v-else class="w-full h-full flex items-center justify-center text-gray-400">
                <i class="bi bi-image-fill text-2xl" />
              </div>
            </div>
            <div class="p-3 flex flex-col gap-1.5">
              <h3 class="font-semibold font-manrope text-sm text-ds-text line-clamp-2">{{ item.nome }}</h3>
              <div class="flex flex-wrap gap-1">
                <DsBadge v-if="item.nomePacote" variant="primary">{{ item.nomePacote }}</DsBadge>
                <DsBadge variant="default">{{ item.sistema }}</DsBadge>
                <DsBadge v-if="item.ultimaVersao" variant="success">{{ item.ultimaVersao }}</DsBadge>
              </div>
              <p class="text-[11px] text-gray-600 leading-tight">
                Aprovado: <span :class="item.aprovado ? 'text-green-600' : 'text-rose-600'">{{ item.aprovado ? 'Sim' : 'Não' }}</span>
                · Ativo: {{ item.ativo ? 'Sim' : 'Não' }}
              </p>
              <div class="flex justify-between items-center gap-1.5 pt-0.5">
                <DsButton variant="secondary" size="sm" icon="info-circle-fill" @click="detailModalId = item.codScriptLaudo">Detalhes</DsButton>
                <ScriptsScriptActionsDropdown :item="item" />
              </div>
            </div>
          </article>
        </div>

        <DsTable v-else>
          <template #head>
            <tr>
              <th class="w-10" />
              <th>Nome</th>
              <th>Sistema</th>
              <th>Pacote</th>
              <th>Aprovado</th>
              <th>Ativo</th>
              <th />
            </tr>
          </template>
          <tr v-for="item in items" :key="item.codScriptLaudo">
            <td>
              <input
                v-model="selectedScriptIds"
                type="checkbox"
                class="rounded"
                :value="item.codScriptLaudo"
                @change="syncSelectAll"
              />
            </td>
            <td>{{ item.nome }}</td>
            <td>{{ item.sistema }}</td>
            <td>{{ item.nomePacote || '—' }}</td>
            <td>{{ item.aprovado ? 'Sim' : 'Não' }}</td>
            <td>{{ item.ativo ? 'Sim' : 'Não' }}</td>
            <td>
              <div class="flex gap-2 justify-end">
                <DsButton variant="ghost" size="sm" icon="info-circle-fill" @click="detailModalId = item.codScriptLaudo" />
                <ScriptsScriptActionsDropdown :item="item" />
              </div>
            </td>
          </tr>
        </DsTable>

        <div v-if="totalPages > 1" class="flex justify-center gap-2 mt-6">
          <DsButton variant="secondary" size="sm" :disabled="page <= 1" @click="load(page - 1)">Anterior</DsButton>
          <DsButton
            v-for="p in totalPages"
            :key="p"
            :variant="p === page ? 'primary' : 'secondary'"
            size="sm"
            @click="load(p)"
          >
            {{ p }}
          </DsButton>
          <DsButton variant="secondary" size="sm" :disabled="page >= totalPages" @click="load(page + 1)">Próxima</DsButton>
        </div>
        <p class="text-sm text-gray-500 text-center mt-2">{{ totalItems }} script(s) encontrado(s)</p>
      </DsPageShell>

      <DsModal v-model="filterModalOpen" title="Filtros Avançados">
        <div class="space-y-4">
          <DsInput v-model="filtros.nome" label="Nome do Script" />
          <DsSelect v-model="filtros.aprovado" label="Aprovado">
            <option value="">Todos</option>
            <option value="1">Aprovado</option>
            <option value="0">Não aprovado</option>
          </DsSelect>
          <DsSelect v-model="filtros.ativo" label="Ativo">
            <option value="1">Somente ativos</option>
            <option value="0">Somente inativos</option>
            <option value="">Todos</option>
          </DsSelect>
        </div>
        <template #footer>
          <DsButton variant="secondary" @click="limparFiltros">Limpar</DsButton>
          <DsButton @click="filterModalOpen = false; load(1)">Aplicar</DsButton>
        </template>
      </DsModal>

      <DsModal
        :model-value="detailModalId !== null"
        :title="detailItem ? `Detalhes: ${detailItem.nome}` : 'Detalhes'"
        size="xl"
        @update:model-value="(v) => !v && (detailModalId = null)"
      >
        <template v-if="detailItem">
          <div class="grid md:grid-cols-2 gap-4 text-sm">
            <div class="space-y-2">
              <p><strong>Nome:</strong> {{ detailItem.nome }}</p>
              <p><strong>Pacote:</strong> {{ detailItem.nomePacote || '—' }}</p>
              <p><strong>Sistema:</strong> {{ detailItem.sistema || '—' }}</p>
              <p><strong>Linguagem:</strong> {{ detailItem.linguagem || '—' }}</p>
              <p v-if="detailItem.ultimaVersao"><strong>Última Versão:</strong> {{ detailItem.ultimaVersao }}</p>
              <p><strong>Descrição:</strong> {{ detailItem.descricao || '—' }}</p>
            </div>
            <div class="space-y-2">
              <p><strong>Aprovado:</strong> {{ detailItem.aprovado ? 'Sim' : 'Não' }}</p>
              <p><strong>Status:</strong> {{ detailItem.ativo ? 'Ativo' : 'Inativo' }}</p>
              <p><strong>Verificado em:</strong> {{ formatDateTime(detailItem.dataVerificacao) }}</p>
              <p><strong>Aprovado Por:</strong> {{ detailItem.aprovadoPor || '—' }}</p>
              <p><strong>Criado por:</strong> {{ detailItem.criadoPor || '—' }}</p>
            </div>
          </div>
          <hr class="my-4 border-gray-200" />
          <h6 class="font-semibold mb-2"><i class="bi bi-images me-1" />Imagens</h6>
          <DsCarousel v-if="detailSlides.length" :slides="detailSlides" class="mb-4" />
          <p v-else class="text-gray-500 text-sm mb-4">Nenhuma imagem.</p>
          <h6 class="font-semibold mb-2"><i class="bi bi-boxes me-1" />Variáveis</h6>
          <ul v-if="detailItem.variaveis?.length" class="text-sm space-y-1 mb-4">
            <li v-for="v in detailItem.variaveis" :key="v.variavel"><code>{{ v.variavel }}</code> ({{ v.nome }})</li>
          </ul>
          <p v-else class="text-gray-500 text-sm">Nenhuma variável vinculada.</p>
        </template>
        <template #footer>
          <DsButton variant="secondary" @click="detailModalId = null">Fechar</DsButton>
        </template>
      </DsModal>
    </template>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

const route = useRoute()
const auth = useAuthStore()
const scriptsApi = useScriptsApi()
const { isSistemaValido } = useScriptsNav()

const sistema = computed(() => String(route.query.sistema || ''))
const pacote = computed(() => String(route.query.pacote || ''))
const hasParams = computed(() => isSistemaValido(sistema.value) && !!pacote.value)

const loading = ref(false)
const items = ref<import('~/composables/useScriptsApi').ScriptListItem[]>([])
const page = ref(1)
const totalPages = ref(0)
const totalItems = ref(0)
const viewMode = ref<'cards' | 'list'>('cards')
const pacoteNome = ref('')
const errorMsg = ref('')
const selectAll = ref(false)
const selectedScriptIds = ref<number[]>([])
const filtros = reactive({ nome: String(route.query.nome || ''), aprovado: '', ativo: '1' })
const swal = useSwal()
const filterModalOpen = ref(false)
const detailModalId = ref<number | null>(null)

const detailItem = computed(() =>
  items.value.find((x) => x.codScriptLaudo === detailModalId.value) ?? null
)

function dedupeImagens(imgs: { caminho: string; nomeArquivo: string }[]) {
  const seen = new Set<string>()
  return imgs.filter((img) => {
    const key = (img.caminho || '').trim().toLowerCase()
    if (!key || seen.has(key)) return false
    seen.add(key)
    return true
  })
}

const detailSlides = computed(() =>
  dedupeImagens(detailItem.value?.imagensDisplay ?? []).map((img) => ({
    src: mediaUrl(img.caminho),
    alt: img.nomeArquivo,
    caption: img.nomeArquivo
  }))
)

const selectedItems = computed(() => items.value.filter((x) => selectedScriptIds.value.includes(x.codScriptLaudo)))
const selectedCountLabel = computed(() => {
  const n = selectedScriptIds.value.length
  return `${n} script${n === 1 ? '' : 's'} selecionado${n === 1 ? '' : 's'}`
})
const hasJsonSelected = computed(() => selectedItems.value.some((x) => x.sistema === 'Laudos UX' && x.temArquivoJson))
const hasDllSelected = computed(() => selectedItems.value.some((x) => x.sistema === 'Laudos Flex' && x.temArquivoDll))
const hasMrdSelected = computed(() => selectedItems.value.some((x) => x.sistema === 'Laudos Flex' && x.temArquivoMrd))

function mediaUrl(path?: string) {
  if (!path) return ''
  if (/^https?:\/\//i.test(path)) return path
  return `http://localhost:5080${path.startsWith('/') ? '' : '/'}${path}`
}

function formatDateTime(value?: string | null) {
  if (!value) return '—'
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

async function load(p = 1) {
  if (!hasParams.value) return
  loading.value = true
  errorMsg.value = ''
  page.value = p
  try {
    const baseQuery = {
      sistema: sistema.value,
      pacote: pacote.value,
      nome: filtros.nome,
      aprovado: filtros.aprovado || undefined,
      page: p,
      pageSize: 10
    }
    let res = await scriptsApi.listScripts({ ...baseQuery, ativo: filtros.ativo })
    if (!res.data.length && filtros.ativo === '1') {
      res = await scriptsApi.listScripts(baseQuery)
      if (res.data.length) filtros.ativo = ''
    }
    items.value = res.data
    selectedScriptIds.value = selectedScriptIds.value.filter((id) => items.value.some((x) => x.codScriptLaudo === id))
    syncSelectAll()
    totalPages.value = res.totalPages
    totalItems.value = res.totalItems
    if (items.value[0]?.nomePacote) pacoteNome.value = items.value[0].nomePacote!
    else {
      const pac = await scriptsApi.getPacotes()
      pacoteNome.value = pac.data.find((x) => String(x.codPacote) === pacote.value)?.nome || ''
    }
  } catch (err) {
    items.value = []
    totalPages.value = 0
    totalItems.value = 0
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar scripts.'
  } finally {
    loading.value = false
  }
}

function toggleSelectAll() {
  selectedScriptIds.value = selectAll.value ? items.value.map((x) => x.codScriptLaudo) : []
}

function syncSelectAll() {
  selectAll.value = items.value.length > 0 && selectedScriptIds.value.length === items.value.length
}

function limparFiltros() {
  filtros.nome = ''
  filtros.aprovado = ''
  filtros.ativo = '1'
  load(1)
}

async function exportSelected(tipo: 'json' | 'dll' | 'mrd') {
  const targets = selectedItems.value.filter((x) => {
    if (tipo === 'json') return x.sistema === 'Laudos UX' && x.temArquivoJson
    if (tipo === 'dll') return x.sistema === 'Laudos Flex' && x.temArquivoDll
    return x.sistema === 'Laudos Flex' && x.temArquivoMrd
  })
  if (!targets.length) {
    await swal.toast(`Nenhum script selecionado para exportação ${tipo.toUpperCase()}.`, 'warning')
    return
  }
  for (const item of targets) {
    if (tipo === 'json') await scriptsApi.download(`/api/web/scripts/${item.codScriptLaudo}/exportar-json`, `${item.nome}.json`)
    else if (tipo === 'dll') await scriptsApi.download(`/api/web/scripts/${item.codScriptLaudo}/exportar-dll`, `${item.nome}.dll`)
    else await scriptsApi.download(`/api/web/scripts/${item.codScriptLaudo}/exportar-mrd`, `${item.nome}.mrd`)
  }
  await swal.toast(`Exportação ${tipo.toUpperCase()} iniciada (${targets.length}).`, 'success')
}

onMounted(() => load(1))
watch(() => route.query, () => load(1))
watch(() => [filtros.aprovado, filtros.ativo], () => load(1))
</script>
