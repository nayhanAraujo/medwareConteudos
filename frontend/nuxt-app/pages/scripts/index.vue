<template>
  <div>
    <LayoutAppPageHeader title="Scripts Cadastrados" icon="card-checklist" />
    <div v-if="!hasParams" class="alert alert-warning">
      Esta página requer pacote e sistema.
      <NuxtLink to="/scripts/sistema" class="btn btn-primary btn-sm ms-2">Seleção de Sistema</NuxtLink>
    </div>
    <template v-else>
      <ScriptsScriptsBreadcrumb :sistema="sistema" :pacote="pacote" :pacote-nome="pacoteNome" />
      <div class="card shadow-sm mb-4">
        <div class="card-body">
          <div class="row mb-3 align-items-center g-2">
            <div class="col-md-4">
              <div class="input-group">
                <input v-model="filtros.nome" type="text" class="form-control" placeholder="Buscar por nome..." @keyup.enter="load(1)" />
                <button class="btn btn-outline-primary" type="button" @click="load(1)"><i class="bi bi-search" /></button>
              </div>
            </div>
            <div class="col-md-3">
              <select v-model="filtros.aprovado" class="form-select" @change="load(1)">
                <option value="">Aprovado: Todos</option>
                <option value="1">Aprovado</option>
                <option value="0">Não aprovado</option>
              </select>
            </div>
            <div class="col-md-2">
              <select v-model="filtros.ativo" class="form-select" @change="load(1)">
                <option value="1">Somente ativos</option>
                <option value="0">Somente inativos</option>
                <option value="">Ativo: Todos</option>
              </select>
            </div>
            <div class="col-md-3 text-md-end">
              <button type="button" class="btn btn-outline-primary me-2" data-bs-toggle="modal" data-bs-target="#filterModal">
                <i class="bi bi-funnel-fill" /> Filtros Avançados
              </button>
              <div class="btn-group me-2">
                <button type="button" class="btn" :class="viewMode === 'cards' ? 'btn-primary' : 'btn-outline-primary'" @click="viewMode = 'cards'">
                  <i class="bi bi-grid-3x3-gap-fill" />
                </button>
                <button type="button" class="btn" :class="viewMode === 'list' ? 'btn-primary' : 'btn-outline-secondary'" @click="viewMode = 'list'">
                  <i class="bi bi-list-ul" />
                </button>
              </div>
              <NuxtLink
                v-if="auth.isAdmin"
                class="btn btn-success"
                :to="{ path: '/scripts/novo', query: { pacote, sistema } }"
              >
                <i class="bi bi-plus-circle-fill" /> Novo Script
              </NuxtLink>
            </div>
          </div>
          <div v-if="viewMode === 'list'" class="row mb-3">
            <div class="col-12">
              <div class="card border-primary">
                <div class="card-body py-2">
                  <div class="d-flex justify-content-between align-items-center">
                    <div class="d-flex align-items-center">
                      <div class="form-check me-3">
                        <input
                          id="selectAllScripts"
                          v-model="selectAll"
                          class="form-check-input"
                          type="checkbox"
                          @change="toggleSelectAll"
                        />
                        <label class="form-check-label fw-bold" for="selectAllScripts">Selecionar Todos</label>
                      </div>
                      <span class="text-muted">{{ selectedCountLabel }}</span>
                    </div>
                    <div class="btn-group">
                      <button type="button" class="btn btn-outline-success btn-sm" :disabled="!hasJsonSelected" @click="exportSelected('json')">
                        <i class="bi bi-file-earmark-arrow-down-fill me-1" />Exportar JSON
                      </button>
                      <button type="button" class="btn btn-outline-dark btn-sm" :disabled="!hasDllSelected" @click="exportSelected('dll')">
                        <i class="bi bi-file-earmark-code-fill me-1" />Exportar DLL
                      </button>
                      <button type="button" class="btn btn-outline-primary btn-sm" :disabled="!hasMrdSelected" @click="exportSelected('mrd')">
                        <i class="bi bi-file-earmark-binary-fill me-1" />Exportar MRD
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div v-if="loading" class="text-center py-5 text-muted">Carregando scripts...</div>
          <div v-else-if="errorMsg" class="alert alert-danger">
            <i class="bi bi-exclamation-triangle-fill me-2" />
            {{ errorMsg }}
          </div>
          <div v-else-if="!items.length" class="alert alert-info">
            <i class="bi bi-info-circle-fill me-2" />
            Nenhum script encontrado para os filtros selecionados.
          </div>
          <div v-else-if="viewMode === 'cards'" class="row row-cols-1 row-cols-md-2 row-cols-lg-3 g-4">
            <div v-for="item in items" :key="item.codScriptLaudo" class="col">
              <div class="card h-100 shadow-sm script-display-card">
                <img
                  v-if="item.imagensDisplay?.[0]?.caminho"
                  :src="mediaUrl(item.imagensDisplay[0].caminho)"
                  class="card-img-top"
                  :alt="item.nome"
                />
                <div v-else class="card-img-top script-thumb-placeholder">
                  <i class="bi bi-image-fill fs-1 text-secondary" />
                </div>
                <div class="card-body d-flex flex-column">
                  <h5 class="fw-bold">{{ item.nome }}</h5>
                  <div class="mb-2">
                    <span v-if="item.nomePacote" class="badge bg-primary me-1">
                      <i :class="`${getEspecialidadeIcon(item.nomePacote)} me-1`" />
                      {{ item.nomePacote }}
                    </span>
                    <span class="badge bg-info me-1">{{ item.sistema }}</span>
                    <span v-if="item.ultimaVersao" class="badge bg-success">{{ item.ultimaVersao }}</span>
                  </div>
                  <p class="small text-muted">
                    <strong>Aprovado:</strong>
                    <span :class="item.aprovado ? 'text-success' : 'text-danger'">{{ item.aprovado ? 'Sim' : 'Não' }}</span>
                    · <strong>Ativo:</strong> {{ item.ativo ? 'Sim' : 'Não' }}
                  </p>
                  <div class="mt-auto d-flex justify-content-between align-items-center">
                    <button
                      class="btn btn-outline-info btn-sm"
                      type="button"
                      data-bs-toggle="modal"
                      :data-bs-target="`#detailsModal-${item.codScriptLaudo}`"
                    >
                      <i class="bi bi-info-circle-fill" /> Detalhes
                    </button>
                    <ScriptsScriptActionsDropdown :item="item" />
                  </div>
                </div>
              </div>
            </div>
            <div
              v-for="item in items"
              :id="`detailsModal-${item.codScriptLaudo}`"
              :key="`modal-${item.codScriptLaudo}`"
              class="modal fade"
              tabindex="-1"
              aria-hidden="true"
            >
              <div class="modal-dialog modal-xl modal-dialog-scrollable">
                <div class="modal-content">
                  <div class="modal-header">
                    <h5 class="modal-title">
                      <i class="bi bi-clipboard-data-fill me-2" />
                      Detalhes do Script: {{ item.nome }}
                    </h5>
                    <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close" />
                  </div>
                  <div class="modal-body">
                    <div class="row">
                      <div class="col-md-6">
                        <p><strong>Nome:</strong> {{ item.nome }}</p>
                        <p><strong>Pacote:</strong> {{ item.nomePacote || '-' }}</p>
                        <p><strong>Sistema:</strong> {{ item.sistema || '-' }}</p>
                        <p><strong>Linguagem:</strong> {{ item.linguagem || '-' }}</p>
                        <p v-if="item.ultimaVersao"><strong>Última Versão:</strong> {{ item.ultimaVersao }}</p>
                        <p><strong>Descrição:</strong> {{ item.descricao || '-' }}</p>
                      </div>
                      <div class="col-md-6">
                        <p><strong>Aprovado:</strong> {{ item.aprovado ? 'Sim' : 'Não' }}</p>
                        <p><strong>Status:</strong> {{ item.ativo ? 'Ativo' : 'Inativo' }}</p>
                        <p><strong>Aprovado Por:</strong> {{ item.aprovadoPor || '-' }}</p>
                        <p><strong>Criado por:</strong> {{ item.criadoPor || '-' }}</p>
                      </div>
                    </div>
                    <hr />
                    <h6 class="mt-3"><i class="bi bi-images me-1" /> Imagens da Interface</h6>
                    <div v-if="item.imagensDisplay?.length" class="row g-2">
                      <div v-for="img in item.imagensDisplay" :key="`${item.codScriptLaudo}-${img.caminho}`" class="col-md-4">
                        <img :src="mediaUrl(img.caminho)" :alt="img.nomeArquivo" class="img-fluid rounded border" />
                        <small class="text-muted d-block mt-1">{{ img.nomeArquivo }}</small>
                      </div>
                    </div>
                    <p v-else class="text-muted">Nenhuma imagem disponível.</p>
                    <hr />
                    <h6 class="mt-3"><i class="bi bi-boxes me-1" /> Variáveis Vinculadas</h6>
                    <ul v-if="item.variaveis?.length" class="list-group list-group-flush">
                      <li v-for="v in item.variaveis" :key="`${item.codScriptLaudo}-${v.variavel}`" class="list-group-item">
                        <code>{{ v.variavel }}</code> ({{ v.nome }})
                      </li>
                    </ul>
                    <p v-else class="text-muted">Nenhuma variável vinculada.</p>
                    <hr />
                    <h6 class="mt-3"><i class="bi bi-file-earmark-pdf me-1" /> PDFs de Impressão</h6>
                    <div v-if="item.pdfsDisplay?.length" class="list-group">
                      <a
                        v-for="pdf in item.pdfsDisplay"
                        :key="`${item.codScriptLaudo}-${pdf.caminho}`"
                        class="list-group-item list-group-item-action d-flex justify-content-between align-items-center"
                        :href="mediaUrl(pdf.caminho)"
                        target="_blank"
                      >
                        <span><i class="bi bi-file-earmark-pdf text-danger me-2" />{{ pdf.nomeArquivo }}</span>
                        <i class="bi bi-box-arrow-up-right" />
                      </a>
                    </div>
                    <p v-else class="text-muted">Nenhum PDF de impressão disponível.</p>
                  </div>
                  <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Fechar</button>
                  </div>
                </div>
              </div>
            </div>
          </div>
          <div v-else class="table-responsive">
            <table class="table table-hover align-middle bg-white">
              <thead>
                <tr>
                  <th class="text-center" style="width: 40px" />
                  <th>Nome</th>
                  <th>Sistema</th>
                  <th>Pacote</th>
                  <th>Aprovado</th>
                  <th>Ativo</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in items" :key="item.codScriptLaudo">
                  <td class="text-center">
                    <input
                      :id="`sel-${item.codScriptLaudo}`"
                      v-model="selectedScriptIds"
                      class="form-check-input"
                      type="checkbox"
                      :value="item.codScriptLaudo"
                      @change="syncSelectAll"
                    />
                  </td>
                  <td>{{ item.nome }}</td>
                  <td>{{ item.sistema }}</td>
                  <td>
                    <span v-if="item.nomePacote">
                      <i :class="`${getEspecialidadeIcon(item.nomePacote)} me-1`" />
                      {{ item.nomePacote }}
                    </span>
                    <span v-else>-</span>
                  </td>
                  <td>{{ item.aprovado ? 'Sim' : 'Não' }}</td>
                  <td>{{ item.ativo ? 'Sim' : 'Não' }}</td>
                  <td>
                    <div class="d-flex gap-2 justify-content-end">
                      <button
                        class="btn btn-outline-info btn-sm"
                        type="button"
                        data-bs-toggle="modal"
                        :data-bs-target="`#detailsModal-${item.codScriptLaudo}`"
                      >
                        <i class="bi bi-info-circle-fill" />
                      </button>
                      <ScriptsScriptActionsDropdown :item="item" />
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <nav v-if="totalPages > 1" class="mt-4">
            <ul class="pagination justify-content-center">
              <li class="page-item" :class="{ disabled: page <= 1 }">
                <a class="page-link" href="#" @click.prevent="load(page - 1)">Anterior</a>
              </li>
              <li v-for="p in totalPages" :key="p" class="page-item" :class="{ active: p === page }">
                <a class="page-link" href="#" @click.prevent="load(p)">{{ p }}</a>
              </li>
              <li class="page-item" :class="{ disabled: page >= totalPages }">
                <a class="page-link" href="#" @click.prevent="load(page + 1)">Próxima</a>
              </li>
            </ul>
          </nav>
          <p class="text-muted small text-center">{{ totalItems }} script(s) encontrado(s)</p>
        </div>
      </div>
      <div id="filterModal" class="modal fade" tabindex="-1" aria-hidden="true">
        <div class="modal-dialog">
          <div class="modal-content">
            <div class="modal-header">
              <h5 class="modal-title">Filtros Avançados</h5>
              <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close" />
            </div>
            <div class="modal-body">
              <div class="mb-3">
                <label for="filterNome" class="form-label">Nome do Script</label>
                <input id="filterNome" v-model="filtros.nome" type="text" class="form-control" placeholder="Digite o nome do script" />
              </div>
              <div class="mb-3">
                <label for="filterAprovado" class="form-label">Aprovado</label>
                <select id="filterAprovado" v-model="filtros.aprovado" class="form-select">
                  <option value="">Todos</option>
                  <option value="1">Aprovado</option>
                  <option value="0">Não aprovado</option>
                </select>
              </div>
              <div class="mb-3">
                <label for="filterAtivo" class="form-label">Ativo</label>
                <select id="filterAtivo" v-model="filtros.ativo" class="form-select">
                  <option value="1">Somente ativos</option>
                  <option value="0">Somente inativos</option>
                  <option value="">Todos</option>
                </select>
              </div>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-outline-secondary" @click="limparFiltros">Limpar Filtros</button>
              <button type="button" class="btn btn-primary" data-bs-dismiss="modal" @click="load(1)">
                <i class="bi bi-check-lg me-1" /> Aplicar Filtros
              </button>
            </div>
          </div>
        </div>
      </div>
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

const selectedItems = computed(() => items.value.filter((x) => selectedScriptIds.value.includes(x.codScriptLaudo)))
const selectedCountLabel = computed(() => {
  const n = selectedScriptIds.value.length
  return `${n} script${n === 1 ? '' : 's'} selecionado${n === 1 ? '' : 's'}`
})
const hasJsonSelected = computed(() => selectedItems.value.some((x) => x.sistema === 'Laudos UX' && x.temArquivoJson))
const hasDllSelected = computed(() => selectedItems.value.some((x) => x.sistema === 'Laudos Flex' && x.temArquivoDll))
const hasMrdSelected = computed(() => selectedItems.value.some((x) => x.sistema === 'Laudos Flex' && x.temArquivoMrd))

function getEspecialidadeIcon(nomePacote?: string) {
  const nome = (nomePacote || '').toLowerCase()
  if (nome.includes('angiologia') || nome.includes('vascular')) return 'bi bi-activity'
  if (nome.includes('cardiologia')) return 'bi bi-heart-fill'
  if (nome.includes('consulta')) return 'bi bi-clipboard2-pulse-fill'
  if (nome.includes('ultrassonografia')) return 'bi bi-soundwave'
  if (nome.includes('pediatria')) return 'bi bi-emoji-smile-fill'
  if (nome.includes('oftalmologia')) return 'bi bi-eye-fill'
  if (nome.includes('nutri')) return 'bi bi-egg-fill'
  return 'bi bi-box-seam-fill'
}

function mediaUrl(path?: string) {
  if (!path) return ''
  if (/^https?:\/\//i.test(path)) return path
  return `http://localhost:5080${path.startsWith('/') ? '' : '/'}${path}`
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

    let res = await scriptsApi.listScripts({
      ...baseQuery,
      ativo: filtros.ativo
    })

    // Paridade com legado: filtro "somente ativos" é padrão.
    // Quando não há ativos para o pacote/sistema, faz fallback para "todos"
    // para evitar tela vazia e facilitar a continuidade da migração.
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
  if (selectAll.value) {
    selectedScriptIds.value = items.value.map((x) => x.codScriptLaudo)
  } else {
    selectedScriptIds.value = []
  }
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
    if (tipo === 'json') {
      await scriptsApi.download(`/api/web/scripts/${item.codScriptLaudo}/exportar-json`, `${item.nome}.json`)
    } else if (tipo === 'dll') {
      await scriptsApi.download(`/api/web/scripts/${item.codScriptLaudo}/exportar-dll`, `${item.nome}.dll`)
    } else {
      await scriptsApi.download(`/api/web/scripts/${item.codScriptLaudo}/exportar-mrd`, `${item.nome}.mrd`)
    }
  }
  await swal.toast(`Exportação ${tipo.toUpperCase()} iniciada (${targets.length}).`, 'success')
}

onMounted(() => load(1))
watch(() => route.query, () => load(1))
</script>
