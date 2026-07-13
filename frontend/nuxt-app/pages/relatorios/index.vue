<template>
  <div>
    <DsPageHeader
      :title="headerTitle"
      :subtitle="headerSubtitle"
      icon="file-earmark-bar-graph"
    />

    <DsPageShell>
      <!-- Sistemas -->
      <template v-if="view === 'sistemas'">
        <div class="flex justify-end mb-4 gap-2">
          <DsButton variant="secondary" size="sm" icon="collection" @click="verTodosModulos">
            Ver todos os módulos
          </DsButton>
        </div>
        <div v-if="loadingNav" class="text-center py-8 text-gray-500">Carregando sistemas...</div>
        <DsAlert v-else-if="errorMsg" variant="error">{{ errorMsg }}</DsAlert>
        <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          <DsHubCard
            v-for="(s, i) in sistemas"
            :key="s.codsistema"
            :title="s.nome"
            :desc="s.descricao || `${s.qtd_modulos} módulo(s)`"
            :icon="iconForSistema(s.nome)"
            :theme-name="themeForSistema(s.nome)"
            :badge="`${s.qtd_modulos} módulos`"
            :delay-index="i"
            @click="abrirSistema(s)"
          />
        </div>
      </template>

      <!-- Módulos -->
      <template v-else-if="view === 'modulos'">
        <div class="flex flex-wrap justify-between gap-2 mb-4">
          <DsButton variant="secondary" size="sm" icon="arrow-left" @click="voltarSistemas">Voltar</DsButton>
          <div class="flex gap-2">
            <DsButton
              v-if="auth.isAdmin"
              variant="success"
              size="sm"
              icon="plus-lg"
              @click="openNovoModulo = true"
            >
              Novo módulo
            </DsButton>
            <DsButton variant="secondary" size="sm" icon="list" @click="abrirLista()">
              Ver todos os relatórios
            </DsButton>
          </div>
        </div>
        <DsSectionTitle
          :title="sistemaAtual?.nome || 'Módulos'"
          :subtitle="`${modulos.length} módulo(s)`"
        />
        <div v-if="loadingNav" class="text-center py-8 text-gray-500">Carregando módulos...</div>
        <DsAlert v-else-if="errorMsg" variant="error">{{ errorMsg }}</DsAlert>
        <DsEmptyState
          v-else-if="!modulos.length"
          title="Nenhum módulo"
          message="Não há módulos ativos para este sistema."
        />
        <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          <DsHubCard
            v-for="(m, i) in modulos"
            :key="m.codmodulo"
            :title="m.nome"
            :desc="m.descricao || `${m.total_relatorios || 0} relatório(s)`"
            :icon="iconForModulo(m.nome)"
            :theme-name="themeForModulo(m.nome)"
            :badge="`${m.total_relatorios || 0} relatórios`"
            :delay-index="i"
            @click="abrirLista(m.nome)"
          />
        </div>
      </template>

      <!-- Lista -->
      <template v-else>
        <div class="flex flex-wrap justify-between gap-2 mb-4">
          <DsButton variant="secondary" size="sm" icon="arrow-left" @click="voltarNav">Voltar</DsButton>
          <div class="flex flex-wrap gap-2">
            <DsButton
              v-if="auth.isAdmin"
              variant="success"
              size="sm"
              icon="plus-lg"
              :to="novaHref"
            >
              Novo relatório
            </DsButton>
            <DsButton
              v-if="auth.isAdmin"
              variant="secondary"
              size="sm"
              icon="upload"
              @click="openImport = true"
            >
              Importar lote
            </DsButton>
            <DsButton
              variant="secondary"
              size="sm"
              icon="download"
              :disabled="!selectedIds.length"
              @click="exportSelected"
            >
              Exportar ({{ selectedIds.length }})
            </DsButton>
            <DsButton
              v-if="auth.isAdmin"
              variant="danger"
              size="sm"
              icon="trash"
              :disabled="!selectedIds.length"
              @click="deleteSelected"
            >
              Excluir seleção
            </DsButton>
          </div>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-12 gap-3 mb-6 items-end">
          <div class="lg:col-span-3">
            <DsInput v-model="filtros.search" label="Buscar" placeholder="Nome ou módulo" @enter="load(1)" />
          </div>
          <div class="lg:col-span-2">
            <DsSelect v-model="filtros.formato" label="Formato">
              <option value="">Todos</option>
              <option value="XML">XML</option>
              <option value="JSON">JSON</option>
            </DsSelect>
          </div>
          <div class="lg:col-span-2">
            <DsSelect v-model="filtros.status" label="Status">
              <option value="">Todos</option>
              <option value="1">Ativo</option>
              <option value="0">Inativo</option>
            </DsSelect>
          </div>
          <div class="lg:col-span-2">
            <DsSelect v-model="filtros.multiselecao" label="Multiseleção">
              <option value="">Todos</option>
              <option value="1">Com</option>
              <option value="0">Sem</option>
            </DsSelect>
          </div>
          <div class="lg:col-span-3 flex justify-end gap-2">
            <DsButton variant="secondary" size="sm" icon="eraser" @click="limparFiltros">Limpar</DsButton>
            <DsButton size="sm" icon="search" @click="load(1)">Filtrar</DsButton>
          </div>
        </div>

        <!-- Legenda (status + ações), alinhada ao legado Conteúdos -->
        <div class="relatorio-legend mb-4">
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
            <div>
              <p class="relatorio-legend__title">
                <i class="bi bi-info-circle" />
                Legenda de status
              </p>
              <div class="flex flex-wrap gap-2">
                <span class="relatorio-legend__badge relatorio-legend__badge--success">
                  <i class="bi bi-check-circle" /> Ativo
                </span>
                <span class="relatorio-legend__badge relatorio-legend__badge--danger">
                  <i class="bi bi-x-circle" /> Inativo
                </span>
                <span class="relatorio-legend__badge relatorio-legend__badge--primary">
                  <i class="bi bi-file-earmark-code" /> XML
                </span>
                <span class="relatorio-legend__badge relatorio-legend__badge--ok">
                  <i class="bi bi-file-earmark-text" /> JSON
                </span>
                <span class="relatorio-legend__badge relatorio-legend__badge--info">
                  <i class="bi bi-check2-all" /> Multiseleção
                </span>
                <span class="relatorio-legend__badge relatorio-legend__badge--muted">
                  <i class="bi bi-check2" /> Sem multiseleção
                </span>
                <span class="relatorio-legend__badge relatorio-legend__badge--primary">
                  <i class="bi bi-shield-check" /> Com validação
                </span>
                <span class="relatorio-legend__badge relatorio-legend__badge--outline">
                  <i class="bi bi-shield" /> Sem validação
                </span>
              </div>
            </div>
            <div>
              <p class="relatorio-legend__title">
                <i class="bi bi-gear" />
                Ações disponíveis
              </p>
              <div class="flex flex-wrap gap-1.5">
                <span class="relatorio-legend__action relatorio-legend__action--primary" title="Visualizar conteúdo">
                  <i class="bi bi-eye" /> Visualizar
                </span>
                <span class="relatorio-legend__action relatorio-legend__action--info" title="Editar relatório">
                  <i class="bi bi-pencil" /> Editar
                </span>
                <span class="relatorio-legend__action relatorio-legend__action--success" title="Baixar em ANSI">
                  <i class="bi bi-download" /> Download
                </span>
                <span class="relatorio-legend__action relatorio-legend__action--warning" title="Alterar status">
                  <i class="bi bi-toggle-on" /> Status
                </span>
                <span class="relatorio-legend__action relatorio-legend__action--danger" title="Excluir">
                  <i class="bi bi-trash" /> Excluir
                </span>
              </div>
            </div>
          </div>
        </div>

        <div v-if="loading" class="text-center py-8 text-gray-500">Carregando relatórios...</div>
        <DsAlert v-else-if="errorMsg" variant="error">{{ errorMsg }}</DsAlert>
        <DsAlert v-else-if="!items.length" variant="info">Nenhum relatório encontrado.</DsAlert>

        <DsTable v-else>
          <template #head>
            <tr>
              <th>
                <input type="checkbox" :checked="allSelected" @change="toggleAll">
              </th>
              <th>#</th>
              <th v-if="!filtros.modulo">Módulo</th>
              <th>Nome</th>
              <th>Formato</th>
              <th>Data</th>
              <th>Status</th>
              <th>Validação</th>
              <th>Multi</th>
              <th class="text-end">Ações</th>
            </tr>
          </template>
          <tr v-for="item in items" :key="item.codrelatorio">
            <td>
              <input
                type="checkbox"
                :checked="selectedIds.includes(item.codrelatorio)"
                @change="toggleSelect(item.codrelatorio)"
              >
            </td>
            <td>{{ item.codrelatorio }}</td>
            <td v-if="!filtros.modulo">{{ item.modulo }}</td>
            <td>
              <p class="font-semibold mb-0">{{ item.nome }}</p>
              <small class="text-gray-500">ID {{ item.codrelatorio }}</small>
            </td>
            <td>
              <span
                class="relatorio-status-badge"
                :class="item.formato === 'JSON' ? 'relatorio-status-badge--ok' : 'relatorio-status-badge--primary'"
              >
                <i :class="item.formato === 'JSON' ? 'bi bi-file-earmark-text' : 'bi bi-file-earmark-code'" />
                {{ item.formato }}
              </span>
            </td>
            <td>{{ item.dthrcriacao || '-' }}</td>
            <td>
              <span
                class="relatorio-status-badge"
                :class="item.ativo ? 'relatorio-status-badge--success' : 'relatorio-status-badge--danger'"
              >
                <i :class="item.ativo ? 'bi bi-check-circle' : 'bi bi-x-circle'" />
                {{ item.ativo ? 'Ativo' : 'Inativo' }}
              </span>
            </td>
            <td>
              <span
                class="relatorio-status-badge"
                :class="item.tem_validacao ? 'relatorio-status-badge--primary' : 'relatorio-status-badge--outline'"
              >
                <i :class="item.tem_validacao ? 'bi bi-shield-check' : 'bi bi-shield'" />
                {{ item.tem_validacao ? 'Com' : 'Sem' }}
              </span>
            </td>
            <td>
              <span
                class="relatorio-status-badge"
                :class="item.tem_multiselecao ? 'relatorio-status-badge--info' : 'relatorio-status-badge--muted'"
              >
                <i :class="item.tem_multiselecao ? 'bi bi-check2-all' : 'bi bi-check2'" />
                {{ item.tem_multiselecao ? 'Sim' : 'Não' }}
              </span>
            </td>
            <td>
              <div class="relatorio-actions" role="group" aria-label="Ações do relatório">
                <button
                  type="button"
                  class="relatorio-action-btn relatorio-action-btn--primary"
                  title="Visualizar"
                  aria-label="Visualizar"
                  @click="abrirPreview(item)"
                >
                  <i class="bi bi-eye" />
                </button>
                <NuxtLink
                  v-if="auth.isAdmin"
                  :to="`/relatorios/${item.codrelatorio}/editar`"
                  class="relatorio-action-btn relatorio-action-btn--info"
                  title="Editar"
                  aria-label="Editar"
                >
                  <i class="bi bi-pencil" />
                </NuxtLink>
                <button
                  type="button"
                  class="relatorio-action-btn relatorio-action-btn--success"
                  title="Download"
                  aria-label="Download"
                  @click="baixar(item)"
                >
                  <i class="bi bi-download" />
                </button>
                <button
                  v-if="auth.isAdmin"
                  type="button"
                  class="relatorio-action-btn relatorio-action-btn--warning"
                  :title="item.ativo ? 'Desativar' : 'Ativar'"
                  :aria-label="item.ativo ? 'Desativar' : 'Ativar'"
                  @click="toggleStatus(item)"
                >
                  <i :class="item.ativo ? 'bi bi-toggle-on' : 'bi bi-toggle-off'" />
                </button>
                <button
                  v-if="auth.isAdmin"
                  type="button"
                  class="relatorio-action-btn relatorio-action-btn--danger"
                  title="Excluir"
                  aria-label="Excluir"
                  @click="excluir(item)"
                >
                  <i class="bi bi-trash" />
                </button>
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
        <p v-if="!loading" class="text-sm text-gray-500 text-center mt-2">
          {{ totalItems }} relatório(s)
        </p>
      </template>
    </DsPageShell>

    <DsModal v-model="previewOpen" :title="previewItem ? previewItem.nome : 'Visualizar'" size="xl">
      <div v-if="previewLoading" class="text-gray-500">Carregando...</div>
      <div v-else-if="previewDetail" class="space-y-3 text-sm">
        <p><strong>Módulo:</strong> {{ previewDetail.modulo }}</p>
        <p><strong>Formato:</strong> {{ previewDetail.formato }}</p>
        <p><strong>Status:</strong> {{ previewDetail.ativo ? 'Ativo' : 'Inativo' }}</p>
        <pre class="max-h-96 overflow-auto rounded-xl bg-gray-50 p-3 text-xs border">{{ previewDetail.conteudo }}</pre>
      </div>
      <template #footer>
        <DsButton variant="secondary" @click="previewOpen = false">Fechar</DsButton>
        <DsButton v-if="previewDetail" icon="download" @click="baixar(previewDetail)">Download</DsButton>
      </template>
    </DsModal>

    <DsModal v-model="openImport" title="Importar relatórios em lote" size="md">
      <div class="space-y-3">
        <DsSelect v-model="importForm.modulo" label="Módulo *">
          <option value="" disabled>Selecione</option>
          <option v-for="m in modulosSimples" :key="m.nome" :value="m.nome">{{ m.nome }}</option>
        </DsSelect>
        <DsSelect v-model="importForm.ativo" label="Status">
          <option value="1">Ativo</option>
          <option value="0">Inativo</option>
        </DsSelect>
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">Arquivos XML/JSON *</label>
          <input
            type="file"
            multiple
            accept=".xml,.json,application/json,text/xml"
            class="block w-full text-sm"
            @change="onImportFiles"
          >
          <ul v-if="importFiles.length" class="mt-2 text-sm text-gray-700 space-y-1 max-h-40 overflow-auto">
            <li v-for="(f, i) in importFiles" :key="`${f.name}-${i}`" class="flex items-center gap-2">
              <i class="bi bi-file-earmark-code text-gray-500" />
              <span class="truncate">{{ f.name }}</span>
              <span class="text-xs text-gray-400">({{ formatBytes(f.size) }})</span>
            </li>
          </ul>
          <p v-else class="text-xs text-gray-500 mt-1">Nenhum arquivo selecionado.</p>
        </div>
      </div>
      <template #footer>
        <DsButton variant="secondary" @click="closeImport">Cancelar</DsButton>
        <DsButton
          :loading="importing"
          :disabled="importing || !importForm.modulo || !importFiles.length"
          @click="runImport"
        >
          Importar{{ importFiles.length ? ` (${importFiles.length})` : '' }}
        </DsButton>
      </template>
    </DsModal>

    <DsModal v-model="openNovoModulo" title="Novo módulo" size="md">
      <div class="space-y-3">
        <DsInput v-model="moduloForm.nome" label="Nome *" />
        <DsInput v-model="moduloForm.descricao" label="Descrição" />
        <div>
          <p class="text-sm font-medium mb-2">Sistemas *</p>
          <label v-for="s in sistemas" :key="s.codsistema" class="flex items-center gap-2 text-sm mb-1">
            <input v-model="moduloForm.sistemas" type="checkbox" :value="s.codsistema">
            {{ s.nome }}
          </label>
        </div>
      </div>
      <template #footer>
        <DsButton variant="secondary" @click="openNovoModulo = false">Cancelar</DsButton>
        <DsButton :loading="savingModulo" @click="criarModulo">Salvar</DsButton>
      </template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
import type {
  RelatorioDetail,
  RelatorioItem,
  RelatorioModulo,
  RelatorioModuloSimples,
  RelatorioSistema
} from '~/composables/useRelatoriosApi'
import type { DsThemeName } from '~/composables/useDsTheme'

definePageMeta({ layout: 'default' })

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const api = useRelatoriosApi()
const swal = useSwal()

function norm(nome?: string | null) {
  return (nome || '')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .trim()
}

function matchAny(nome: string, terms: string[]) {
  const n = norm(nome)
  return terms.some((t) => n.includes(t))
}

function iconForSistema(nome: string) {
  if (matchAny(nome, ['clinicas basic', 'clinicas premium', 'clinica basic', 'clinica premium'])) {
    return matchAny(nome, ['premium']) ? 'buildings' : 'building'
  }
  if (matchAny(nome, ['medware clinicas', 'clinicas'])) {
    return matchAny(nome, ['premium']) ? 'buildings' : 'building'
  }
  if (matchAny(nome, ['laudos ux', 'ux'])) return 'pc-display-horizontal'
  if (matchAny(nome, ['laudos flex', 'flex'])) return 'laptop'
  if (matchAny(nome, ['laudo'])) return 'file-earmark-medical'
  if (matchAny(nome, ['agenda', 'agendamento'])) return 'calendar3'
  if (matchAny(nome, ['financeiro', 'financ'])) return 'cash-coin'
  if (matchAny(nome, ['faturamento', 'cobranca'])) return 'receipt'
  if (matchAny(nome, ['estoque', 'inventario'])) return 'boxes'
  if (matchAny(nome, ['medico', 'profissional'])) return 'person-badge'
  if (matchAny(nome, ['clinico', 'prontuario', 'pep'])) return 'clipboard2-pulse'
  if (matchAny(nome, ['laboratorio', 'lab'])) return 'eyedropper'
  if (matchAny(nome, ['imagem', 'radiolog', 'dicom'])) return 'image'
  if (matchAny(nome, ['api', 'integracao'])) return 'plugin'
  if (matchAny(nome, ['web', 'portal'])) return 'globe2'
  return 'cpu'
}

function themeForSistema(nome: string): DsThemeName {
  if (matchAny(nome, ['clinicas premium', 'clinica premium'])) return 'purple'
  if (matchAny(nome, ['clinicas basic', 'clinica basic', 'medware clinicas', 'clinicas'])) return 'blue'
  if (matchAny(nome, ['laudos ux', 'ux'])) return 'blue'
  if (matchAny(nome, ['laudos flex', 'flex'])) return 'purple'
  if (matchAny(nome, ['financeiro', 'faturamento', 'caixa'])) return 'green'
  if (matchAny(nome, ['estoque'])) return 'orange'
  if (matchAny(nome, ['medico', 'clinico', 'laudo'])) return 'rose'
  if (matchAny(nome, ['api', 'integracao'])) return 'slate'
  return 'gray'
}

function iconForModulo(nome: string) {
  if (matchAny(nome, ['agenda', 'agendamento', 'consulta'])) return 'calendar-check'
  if (matchAny(nome, ['caixa', 'receber', 'pagar'])) return 'wallet2'
  if (matchAny(nome, ['bibliotec', 'acervo'])) return 'journal-bookmark'
  if (matchAny(nome, ['faturamento', 'cobranca', 'guia'])) return 'receipt-cutoff'
  if (matchAny(nome, ['medico', 'honorario', 'profissional'])) return 'person-vcard'
  if (matchAny(nome, ['financeiro', 'contas', 'fluxo'])) return 'graph-up-arrow'
  if (matchAny(nome, ['estoque', 'inventario', 'produto'])) return 'box-seam'
  if (matchAny(nome, ['nota fiscal', 'nfe', 'nfse', 'fiscal'])) return 'file-earmark-ruled'
  if (matchAny(nome, ['paciente', 'cadastro'])) return 'people'
  if (matchAny(nome, ['laudo', 'resultado'])) return 'file-earmark-medical'
  if (matchAny(nome, ['exame', 'procedimento'])) return 'heart-pulse'
  if (matchAny(nome, ['rh', 'folha', 'colaborador'])) return 'person-workspace'
  if (matchAny(nome, ['compra', 'fornecedor'])) return 'cart3'
  if (matchAny(nome, ['contrato', 'comercial'])) return 'handshake'
  if (matchAny(nome, ['qualidade', 'auditoria'])) return 'shield-check'
  if (matchAny(nome, ['marketing', 'crm'])) return 'megaphone'
  return 'folder2-open'
}

function themeForModulo(nome: string): DsThemeName {
  if (matchAny(nome, ['agenda', 'consulta'])) return 'blue'
  if (matchAny(nome, ['caixa', 'financeiro', 'faturamento', 'nota fiscal', 'fiscal'])) return 'green'
  if (matchAny(nome, ['estoque', 'compra', 'produto'])) return 'orange'
  if (matchAny(nome, ['medico', 'laudo', 'exame', 'paciente'])) return 'rose'
  if (matchAny(nome, ['bibliotec'])) return 'purple'
  if (matchAny(nome, ['qualidade', 'auditoria', 'contrato'])) return 'slate'
  return 'blue'
}

const view = computed(() => {
  if (route.query.view === 'lista' || route.query.modulo) return 'lista'
  if (route.query.codsistema || route.query.view === 'modulos') return 'modulos'
  return 'sistemas'
})

const headerTitle = computed(() => {
  if (view.value === 'lista') return filtros.modulo ? `Relatórios — ${filtros.modulo}` : 'Relatórios'
  if (view.value === 'modulos') return 'Módulos de Relatórios'
  return 'Sistemas'
})

const headerSubtitle = computed(() => {
  if (view.value === 'lista') return 'Listagem, filtros e ações do catálogo'
  if (view.value === 'modulos') return 'Escolha um módulo para listar os relatórios'
  return 'Escolha o sistema para navegar nos módulos'
})

const loading = ref(false)
const loadingNav = ref(false)
const errorMsg = ref('')
const items = ref<RelatorioItem[]>([])
const page = ref(1)
const totalPages = ref(0)
const totalItems = ref(0)
const selectedIds = ref<number[]>([])
const sistemas = ref<RelatorioSistema[]>([])
const modulos = ref<RelatorioModulo[]>([])
const modulosSimples = ref<RelatorioModuloSimples[]>([])
const sistemaAtual = computed(() =>
  sistemas.value.find((s) => s.codsistema === Number(route.query.codsistema))
)

const filtros = reactive({
  search: '',
  modulo: '',
  formato: '',
  status: '',
  multiselecao: ''
})

const novaHref = computed(() => {
  const modulo = filtros.modulo?.trim()
  return modulo ? `/relatorios/nova?modulo=${encodeURIComponent(modulo)}` : '/relatorios/nova'
})

const pagesToShow = computed(() => {
  const max = totalPages.value
  if (max <= 7) return Array.from({ length: max }, (_, i) => i + 1)
  const start = Math.max(1, page.value - 2)
  const end = Math.min(max, start + 4)
  return Array.from({ length: end - start + 1 }, (_, i) => start + i)
})

const allSelected = computed(
  () => items.value.length > 0 && items.value.every((i) => selectedIds.value.includes(i.codrelatorio))
)

const previewOpen = ref(false)
const previewLoading = ref(false)
const previewItem = ref<RelatorioItem | null>(null)
const previewDetail = ref<RelatorioDetail | null>(null)

const openImport = ref(false)
const importing = ref(false)
const importFiles = ref<File[]>([])
const importForm = reactive({ modulo: '', ativo: '1' })

function formatBytes(size: number) {
  if (size < 1024) return `${size} B`
  if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`
  return `${(size / (1024 * 1024)).toFixed(1)} MB`
}

function closeImport() {
  openImport.value = false
  importFiles.value = []
}

watch(openImport, async (open) => {
  if (!open) return
  if (!modulosSimples.value.length) {
    try {
      await loadModulosSimples()
    } catch {
      /* ignore */
    }
  }
  if (!importForm.modulo && filtros.modulo) {
    importForm.modulo = filtros.modulo
  }
})

const openNovoModulo = ref(false)
const savingModulo = ref(false)
const moduloForm = reactive({ nome: '', descricao: '', sistemas: [] as number[] })

async function loadSistemas() {
  loadingNav.value = true
  errorMsg.value = ''
  try {
    const res = await api.listSistemas()
    sistemas.value = res.sistemas || []
  } catch (err) {
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar sistemas.'
  } finally {
    loadingNav.value = false
  }
}

async function loadModulos(codsistema?: number) {
  loadingNav.value = true
  errorMsg.value = ''
  try {
    const res = await api.listModulos(codsistema)
    modulos.value = res.modulos || []
  } catch (err) {
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar módulos.'
  } finally {
    loadingNav.value = false
  }
}

async function loadModulosSimples() {
  const res = await api.listModulosSimples()
  modulosSimples.value = res.modulos || []
}

async function load(p = page.value) {
  loading.value = true
  errorMsg.value = ''
  page.value = p
  selectedIds.value = []
  try {
    const res = await api.listRelatorios({
      page: p,
      perPage: 10,
      search: filtros.search,
      modulo: filtros.modulo,
      formato: filtros.formato,
      status: filtros.status,
      multiselecao: filtros.multiselecao
    })
    items.value = res.data || []
    totalPages.value = res.totalPages || 0
    totalItems.value = res.totalItems || 0
  } catch (err) {
    items.value = []
    totalPages.value = 0
    totalItems.value = 0
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar relatórios.'
  } finally {
    loading.value = false
  }
}

function syncFiltrosFromRoute() {
  filtros.modulo = typeof route.query.modulo === 'string' ? route.query.modulo : ''
}

function abrirSistema(s: RelatorioSistema) {
  router.push({ path: '/relatorios', query: { view: 'modulos', codsistema: String(s.codsistema) } })
}

function verTodosModulos() {
  router.push({ path: '/relatorios', query: { view: 'modulos' } })
}

function abrirLista(modulo?: string) {
  const query: Record<string, string> = { view: 'lista' }
  if (modulo) query.modulo = modulo
  if (route.query.codsistema) query.codsistema = String(route.query.codsistema)
  router.push({ path: '/relatorios', query })
}

function voltarSistemas() {
  router.push('/relatorios')
}

function voltarNav() {
  if (route.query.codsistema) {
    router.push({ path: '/relatorios', query: { view: 'modulos', codsistema: String(route.query.codsistema) } })
  } else if (filtros.modulo) {
    router.push({ path: '/relatorios', query: { view: 'modulos' } })
  } else {
    router.push('/relatorios')
  }
}

function limparFiltros() {
  filtros.search = ''
  filtros.formato = ''
  filtros.status = ''
  filtros.multiselecao = ''
  load(1)
}

function toggleSelect(id: number) {
  if (selectedIds.value.includes(id)) {
    selectedIds.value = selectedIds.value.filter((x) => x !== id)
  } else {
    selectedIds.value = [...selectedIds.value, id]
  }
}

function toggleAll(ev: Event) {
  const checked = (ev.target as HTMLInputElement).checked
  selectedIds.value = checked ? items.value.map((i) => i.codrelatorio) : []
}

async function abrirPreview(item: RelatorioItem) {
  previewItem.value = item
  previewOpen.value = true
  previewLoading.value = true
  previewDetail.value = null
  try {
    const res = await api.getRelatorio(item.codrelatorio)
    previewDetail.value = res.data
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao visualizar', 'error')
    previewOpen.value = false
  } finally {
    previewLoading.value = false
  }
}

async function baixar(item: { codrelatorio: number; nome: string; formato: string }) {
  try {
    await api.downloadRelatorio(item.codrelatorio, `${item.nome}.${item.formato.toLowerCase()}`)
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro no download', 'error')
  }
}

async function toggleStatus(item: RelatorioItem) {
  try {
    await api.setStatus(item.codrelatorio, item.ativo ? 0 : 1)
    await swal.toast('Status atualizado.')
    await load(page.value)
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao alterar status', 'error')
  }
}

async function excluir(item: RelatorioItem) {
  const ok = await swal.confirm('Excluir relatório', `Excluir "${item.nome}"?`)
  if (!ok?.isConfirmed) return
  try {
    await api.deleteRelatorio(item.codrelatorio)
    await swal.toast('Relatório excluído.')
    await load(1)
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao excluir', 'error')
  }
}

async function exportSelected() {
  try {
    await api.exportMultiplos(selectedIds.value)
    await swal.toast('Exportação iniciada.')
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao exportar', 'error')
  }
}

async function deleteSelected() {
  const ok = await swal.confirm('Excluir selecionados', `Excluir ${selectedIds.value.length} relatório(s)?`)
  if (!ok?.isConfirmed) return
  try {
    for (const id of selectedIds.value) {
      await api.deleteRelatorio(id)
    }
    await swal.toast('Relatórios excluídos.')
    await load(1)
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao excluir', 'error')
  }
}

function onImportFiles(ev: Event) {
  const input = ev.target as HTMLInputElement
  importFiles.value = Array.from(input.files || [])
}

async function runImport() {
  if (!importForm.modulo || !importFiles.value.length) {
    await swal.toast('Informe módulo e arquivos.', 'error')
    return
  }
  importing.value = true
  try {
    const res = await api.importLote(importForm.modulo, Number(importForm.ativo), importFiles.value)
    const created = res.created ?? 0
    const errors = Array.isArray(res.errors) ? res.errors as { arquivo?: string; error?: string }[] : []
    if (created > 0) {
      const detail = errors.length
        ? ` (${errors.length} com erro: ${errors[0]?.error || 'desconhecido'})`
        : ''
      await swal.toast((res.message || `${created} relatório(s) importado(s).`) + detail)
      filtros.modulo = importForm.modulo
      closeImport()
      await router.replace({
        path: '/relatorios',
        query: {
          view: 'lista',
          modulo: importForm.modulo,
          ...(route.query.codsistema ? { codsistema: String(route.query.codsistema) } : {})
        }
      })
      await load(1)
    } else {
      const detalhes = errors
        .slice(0, 5)
        .map((e) => `${e.arquivo || 'Arquivo'}: ${e.error || 'erro desconhecido'}`)
        .join(' | ')
      await swal.error(
        'Importação falhou',
        detalhes || res.message || 'Nenhum arquivo foi importado.'
      )
    }
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro na importação', 'error')
  } finally {
    importing.value = false
  }
}

async function criarModulo() {
  if (!moduloForm.nome.trim() || !moduloForm.sistemas.length) {
    await swal.toast('Nome e ao menos um sistema são obrigatórios.', 'error')
    return
  }
  savingModulo.value = true
  try {
    await api.createModulo({
      nomeModulo: moduloForm.nome.trim(),
      descricao: moduloForm.descricao.trim(),
      sistemas: moduloForm.sistemas
    })
    await swal.toast('Módulo criado.')
    openNovoModulo.value = false
    moduloForm.nome = ''
    moduloForm.descricao = ''
    moduloForm.sistemas = []
    await loadModulos(route.query.codsistema ? Number(route.query.codsistema) : undefined)
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao criar módulo', 'error')
  } finally {
    savingModulo.value = false
  }
}

watch(
  () => route.query,
  async () => {
    syncFiltrosFromRoute()
    if (view.value === 'sistemas') {
      await loadSistemas()
    } else if (view.value === 'modulos') {
      if (!sistemas.value.length) await loadSistemas()
      await loadModulos(route.query.codsistema ? Number(route.query.codsistema) : undefined)
    } else {
      await loadModulosSimples()
      await load(1)
    }
  },
  { immediate: true }
)
</script>

<style scoped>
.relatorio-legend {
  background: linear-gradient(135deg, #f8f9fa 0%, #eef1f4 100%);
  border: 1px solid #e5e7eb;
  border-radius: 1rem;
  padding: 0.85rem 1rem;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
}

.relatorio-legend__title {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.8125rem;
  font-weight: 600;
  color: #6b7280;
  margin-bottom: 0.5rem;
}

.relatorio-legend__badge,
.relatorio-status-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
  font-size: 0.75rem;
  font-weight: 500;
  padding: 0.3rem 0.65rem;
  border-radius: 0.5rem;
  white-space: nowrap;
}

.relatorio-legend__badge--success,
.relatorio-status-badge--success {
  background: #16a34a;
  color: #fff;
}

.relatorio-legend__badge--danger,
.relatorio-status-badge--danger {
  background: #e11d48;
  color: #fff;
}

.relatorio-legend__badge--primary,
.relatorio-status-badge--primary {
  background: #2563eb;
  color: #fff;
}

.relatorio-legend__badge--ok,
.relatorio-status-badge--ok {
  background: #16a34a;
  color: #fff;
}

.relatorio-legend__badge--info,
.relatorio-status-badge--info {
  background: #0ea5e9;
  color: #fff;
}

.relatorio-legend__badge--muted,
.relatorio-status-badge--muted {
  background: #6b7280;
  color: #fff;
}

.relatorio-legend__badge--outline,
.relatorio-status-badge--outline {
  background: #fff;
  color: #374151;
  border: 1px solid #d1d5db;
}

.relatorio-legend__action {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  font-size: 0.75rem;
  padding: 0.2rem 0.5rem;
  border-radius: 0.375rem;
  border: 1px solid;
  opacity: 0.85;
  cursor: default;
  background: #fff;
}

.relatorio-legend__action--primary {
  color: #2563eb;
  border-color: #93c5fd;
}

.relatorio-legend__action--info {
  color: #0891b2;
  border-color: #67e8f9;
}

.relatorio-legend__action--success {
  color: #15803d;
  border-color: #86efac;
}

.relatorio-legend__action--warning {
  color: #b45309;
  border-color: #fcd34d;
}

.relatorio-legend__action--danger {
  color: #be123c;
  border-color: #fda4af;
}

.relatorio-actions {
  display: inline-flex;
  flex-wrap: nowrap;
  gap: 0.25rem;
  justify-content: flex-end;
}

.relatorio-action-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 2rem;
  padding: 0;
  border-radius: 0.4rem;
  border: 1px solid;
  background: #fff;
  line-height: 1;
  text-decoration: none;
  transition: background-color 0.15s ease, color 0.15s ease;
}

.relatorio-action-btn--primary {
  color: #2563eb;
  border-color: #93c5fd;
}
.relatorio-action-btn--primary:hover {
  background: #eff6ff;
}

.relatorio-action-btn--info {
  color: #0891b2;
  border-color: #67e8f9;
}
.relatorio-action-btn--info:hover {
  background: #ecfeff;
}

.relatorio-action-btn--success {
  color: #15803d;
  border-color: #86efac;
}
.relatorio-action-btn--success:hover {
  background: #f0fdf4;
}

.relatorio-action-btn--warning {
  color: #b45309;
  border-color: #fcd34d;
}
.relatorio-action-btn--warning:hover {
  background: #fffbeb;
}

.relatorio-action-btn--danger {
  color: #be123c;
  border-color: #fda4af;
}
.relatorio-action-btn--danger:hover {
  background: #fff1f2;
}
</style>
