<template>
  <div>
    <DsPageHeader
      title="Banco de Referências e Variáveis"
      subtitle="Catálogo de variáveis e atalhos para referências e normalidades"
      icon="calculator"
    />
    <DsPageShell>
      <div class="flex flex-wrap justify-end gap-2 mb-4">
        <DsButton variant="secondary" size="sm" icon="journal-richtext" to="/referencias">Listar Referências</DsButton>
        <DsButton v-if="auth.isAdmin" variant="success" size="sm" icon="plus-lg" to="/variaveis/nova">
          Nova Variável
        </DsButton>
        <DsButton variant="secondary" size="sm" icon="journal-medical" to="/variaveis/referencias-normalidades">
          Normalidades x Referências
        </DsButton>
        <DsButton
          v-if="auth.isAdmin"
          variant="secondary"
          size="sm"
          icon="diagram-3"
          to="/variaveis/classificacoes"
        >
          Grupos/Classificações
        </DsButton>
        <DsButton
          v-if="auth.isAdmin"
          variant="secondary"
          size="sm"
          icon="upload"
          to="/variaveis/importar"
        >
          Importar (.cs)
        </DsButton>
        <DsButton
          v-if="auth.isAdmin"
          variant="secondary"
          size="sm"
          icon="link-45deg"
          to="/variaveis/modelos-modo-texto"
        >
          Modelos modo texto
        </DsButton>
        <DsButton
          v-if="auth.isAdmin"
          variant="secondary"
          size="sm"
          icon="check2-all"
          to="/variaveis/especialidades-lote"
        >
          Vincular em Especialidade
        </DsButton>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-12 gap-3 mb-6 items-end">
        <div class="lg:col-span-3">
          <DsSelect v-model="filtros.grupo" label="Grupo">
            <option value="">Todos os grupos</option>
            <option v-for="g in grupos" :key="g.codGrupo" :value="String(g.codGrupo)">{{ g.nome }}</option>
          </DsSelect>
        </div>
        <div class="lg:col-span-5">
          <DsSearchInput
            v-model="filtros.search"
            placeholder="Buscar por nome, tag, descrição..."
            wrapper-class="mb-0"
            @enter="applyFilters"
          />
        </div>
        <div class="lg:col-span-4 flex justify-end gap-2">
          <DsButton variant="secondary" size="sm" icon="eraser" @click="clearFilters">Limpar</DsButton>
          <DsButton size="sm" icon="search" @click="applyFilters">Filtrar</DsButton>
        </div>
      </div>

      <div v-if="loading" class="text-center py-8 text-gray-500">Carregando variáveis...</div>
      <DsAlert v-else-if="errorMsg" variant="error">{{ errorMsg }}</DsAlert>
      <DsAlert v-else-if="!items.length" variant="info">
        Nenhuma variável encontrada.
        <template v-if="hasActiveFilter"> Tente refinar sua busca ou limpar os filtros.</template>
        <template v-else-if="auth.isAdmin">
          <DsButton variant="ghost" size="sm" class="ml-2 p-0 h-auto" to="/variaveis/nova">
            Cadastre uma nova variável
          </DsButton>
        </template>
      </DsAlert>

      <template v-else>
        <VariaveisActionsLegend />

        <DsTable>
          <template #head>
            <tr>
              <th class="w-16 text-center">Cód.</th>
              <th>Grupo</th>
              <th>Nome Clínico</th>
              <th>Variável (Código)</th>
              <th class="text-center">Sigla</th>
              <th>Alternativas</th>
              <th>Scripts Vinculados</th>
              <th class="text-center w-[120px]">Ações</th>
            </tr>
          </template>
          <tr v-for="item in items" :key="item.codVariavel">
            <td class="text-center">{{ item.codVariavel }}</td>
            <td>
              <DsBadge v-if="item.nomeGrupo" variant="primary">{{ item.nomeGrupo }}</DsBadge>
              <DsBadge v-else variant="dark">Sem grupo</DsBadge>
            </td>
            <td>{{ item.nome }}</td>
            <td><code class="text-sm bg-gray-100 px-1 rounded">{{ item.variavel }}</code></td>
            <td class="text-center">{{ item.sigla }}</td>
            <td>
              <template v-if="item.alternativas?.length">
                <DsBadge variant="default" class="mr-1">{{ item.alternativas.length }}</DsBadge>
                <DsButton variant="ghost" size="sm" icon="list-ul" @click="openListModal('alternativas', item)">Ver</DsButton>
              </template>
              <span v-else class="text-gray-400 italic text-sm">-</span>
            </td>
            <td>
              <template v-if="item.scripts?.length">
                <DsBadge variant="default" class="mr-1">{{ item.scripts.length }}</DsBadge>
                <DsButton variant="ghost" size="sm" icon="list-ul" @click="openListModal('scripts', item)">Ver</DsButton>
              </template>
              <span v-else class="text-gray-400 italic text-sm">-</span>
            </td>
            <td>
              <div class="flex gap-1 justify-center">
                <DsButton variant="secondary" size="sm" icon="sliders" title="Complementos" :to="`/variaveis/${item.codVariavel}/complementos`" />
                <VariaveisRowActions :item="item" @detalhes="openDetalhesModal" @formula="openFormulaModal" @dicom="openDicomSwal" @excluir="confirmarExclusao" />
              </div>
            </td>
          </tr>
        </DsTable>
      </template>

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

      <p v-if="totalItems > 0" class="text-center text-sm text-gray-500 mt-4 mb-0">
        Página {{ page }} de {{ totalPages }} (Total de {{ totalItems }} variáveis)
      </p>
    </DsPageShell>

    <DsModal v-model="listModalOpen" :title="listModalTitle" size="lg">
      <ul v-if="listModalMode === 'alternativas'" class="list-none p-0 m-0 space-y-2">
        <li v-for="(alt, idx) in listModalItems" :key="idx">
          <code class="text-sm bg-gray-100 px-1 rounded">{{ alt }}</code>
        </li>
      </ul>
      <ul v-else-if="listModalMode === 'scripts'" class="list-none p-0 m-0 space-y-2">
        <li v-for="(nome, idx) in listModalItems" :key="idx">{{ nome }}</li>
      </ul>
      <ul v-else-if="listModalMode === 'anexos'" class="list-none p-0 m-0 space-y-2 text-sm">
        <li v-for="anexo in listModalAnexos" :key="anexo.codAnexo">
          <i class="bi bi-paperclip" />
          {{ anexo.descricao || anexo.tipoAnexo }}
          <span v-if="anexo.referencia?.titulo" class="text-gray-500">
            (Ref: {{ truncate(anexo.referencia.autores, 15) || 'N/A' }} {{ anexo.referencia.ano || '' }})
          </span>
          <a
            v-if="anexo.caminho"
            :href="anexo.caminho"
            target="_blank"
            rel="noopener noreferrer"
            class="ml-1 text-ds-primary"
          >
            <i class="bi bi-box-arrow-up-right" />
          </a>
        </li>
      </ul>
      <template #footer>
        <DsButton variant="secondary" size="sm" @click="hideListModal">Fechar</DsButton>
      </template>
    </DsModal>

    <DsModal v-model="detalhesModalOpen" :title="detalhesModalTitle" size="xl">
      <div v-if="detalhesLoading" class="text-center py-6 text-gray-500">Carregando detalhes...</div>
      <DsAlert v-else-if="detalhesError" variant="error">{{ detalhesError }}</DsAlert>
      <template v-else-if="detalhesData">
        <p class="mb-1"><strong>Nome clínico:</strong> {{ detalhesItem?.nome }}</p>
        <p class="mb-4"><strong>Variável:</strong> <code class="text-sm bg-gray-100 px-1 rounded">{{ detalhesItem?.variavel }}</code></p>

        <h6 class="font-semibold mb-2">Normalidades</h6>
        <ul v-if="detalhesData.normalidades.length" class="list-none p-0 m-0 mb-4 space-y-2 text-sm">
          <li v-for="n in detalhesData.normalidades" :key="n.codNormalidade" class="border-b border-gray-100 pb-2">
            <strong>Sexo:</strong> {{ n.sexo || 'N/A' }},
            <strong>Min:</strong> {{ n.valorMin ?? '-' }},
            <strong>Max:</strong> {{ n.valorMax ?? '-' }}
            <br />
            <span class="text-gray-500">Idade: {{ n.idadeMin ?? 'N/A' }}-{{ n.idadeMax ?? 'N/A' }}</span>
            <br v-if="n.referencia?.titulo" />
            <span v-if="n.referencia?.titulo" class="text-gray-500">
              (Ref: {{ n.referencia.titulo || 'N/A' }} ({{ n.referencia.ano || '' }}) {{ n.referencia.autores || '' }})
            </span>
            <span v-else class="text-gray-500">(Sem referência)</span>
          </li>
        </ul>
        <p v-else class="text-gray-500 italic text-sm mb-4">Nenhuma normalidade definida para esta variável.</p>

        <h6 class="font-semibold mb-2">Equações</h6>
        <ul v-if="detalhesData.equacoes.length" class="list-none p-0 m-0 space-y-2 text-sm">
          <li v-for="eq in detalhesData.equacoes" :key="eq.codEquacao" class="border-b border-gray-100 pb-2">
            <strong>{{ eq.linguagem || 'N/A' }}:</strong>
            <code class="text-sm bg-gray-100 px-1 rounded">{{ eq.equacao || '-' }}</code>
            <br v-if="eq.referencia?.titulo" />
            <span v-if="eq.referencia?.titulo" class="text-gray-500">
              (Ref: {{ eq.referencia.titulo || 'N/A' }} ({{ eq.referencia.ano || '' }}) {{ eq.referencia.autores || '' }})
            </span>
            <span v-else class="text-gray-500">(Sem referência)</span>
          </li>
        </ul>
        <p v-else class="text-gray-500 italic text-sm">Nenhuma equação associada a esta variável.</p>
      </template>
      <template #footer>
        <DsButton variant="secondary" size="sm" @click="hideDetalhesModal">Fechar</DsButton>
      </template>
    </DsModal>

    <DsModal v-model="formulaModalOpen" :title="formulaModalTitle" size="lg">
      <p class="mb-2">
        <strong>Variável:</strong>
        <code class="text-sm bg-gray-100 px-1 rounded">{{ formulaItem?.variavel }}</code>
      </p>
      <p class="mb-2"><strong>Fórmula:</strong></p>
      <pre class="bg-gray-100 rounded-xl p-3 text-sm overflow-x-auto"><code>{{ formulaItem?.formula || '-' }}</code></pre>
      <p class="mb-0"><strong>Casas Decimais:</strong> {{ formulaItem?.casasDecimais ?? 'N/A' }}</p>
      <template #footer>
        <DsButton variant="secondary" size="sm" @click="hideFormulaModal">Fechar</DsButton>
      </template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
import type {
  VariavelAnexoDto,
  VariavelDetalhesCompletosDto,
  VariavelListItem
} from '~/composables/useVariaveisApi'

definePageMeta({ layout: 'default' })

const PAGE_SIZE = 10
const auth = useAuthStore()
const swal = useSwal()
const variaveisApi = useVariaveisApi()
const { open: listModalOpen, show: showListModal, hide: hideListModal } = useDsModal()
const { open: detalhesModalOpen, show: showDetalhesModal, hide: hideDetalhesModal } = useDsModal()
const { open: formulaModalOpen, show: showFormulaModal, hide: hideFormulaModal } = useDsModal()

const filtros = reactive({ search: '', grupo: '' })
const applied = reactive({ search: '', grupo: '' })
const grupos = ref<{ codGrupo: number; nome: string }[]>([])

const loading = ref(false)
const errorMsg = ref('')
const items = ref<VariavelListItem[]>([])
const totalItems = ref(0)
const page = ref(1)

const hasActiveFilter = computed(() => !!(applied.search || applied.grupo))
const totalPages = computed(() => Math.max(1, Math.ceil(totalItems.value / PAGE_SIZE)))

const pagesToShow = computed(() => {
  const total = totalPages.value
  const current = page.value
  const delta = 2
  const pages: number[] = []
  const start = Math.max(1, current - delta)
  const end = Math.min(total, current + delta)
  for (let i = start; i <= end; i++) pages.push(i)
  return pages
})

const listModalMode = ref<'alternativas' | 'scripts' | 'anexos'>('alternativas')
const listModalTitle = ref('')
const listModalItems = ref<string[]>([])
const listModalAnexos = ref<VariavelAnexoDto[]>([])

const detalhesItem = ref<VariavelListItem | null>(null)
const detalhesData = ref<VariavelDetalhesCompletosDto | null>(null)
const detalhesLoading = ref(false)
const detalhesError = ref('')
const detalhesModalTitle = computed(() =>
  detalhesItem.value ? `Detalhes da Variável: ${detalhesItem.value.nome}` : 'Detalhes da Variável'
)

const formulaItem = ref<VariavelListItem | null>(null)
const formulaModalTitle = computed(() =>
  formulaItem.value ? `Fórmula da Variável: ${formulaItem.value.variavel}` : 'Fórmula da Variável'
)

function truncate(value: string | undefined, max: number) {
  if (!value) return ''
  return value.length > max ? `${value.slice(0, max)}…` : value
}

async function loadGrupos() {
  try {
    const res = await variaveisApi.listGrupos()
    grupos.value = res.data || []
  } catch {
    grupos.value = []
  }
}

async function load(p = 1) {
  loading.value = true
  errorMsg.value = ''
  page.value = p
  try {
    const res = await variaveisApi.listVariaveis({
      skip: (p - 1) * PAGE_SIZE,
      take: PAGE_SIZE,
      search: applied.search || undefined,
      grupo: applied.grupo ? Number(applied.grupo) : undefined
    })
    items.value = res.data ?? []
    totalItems.value = res.total ?? items.value.length
  } catch (err) {
    items.value = []
    totalItems.value = 0
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar variáveis.'
  } finally {
    loading.value = false
  }
}

function applyFilters() {
  applied.search = filtros.search.trim()
  applied.grupo = filtros.grupo
  load(1)
}

function clearFilters() {
  filtros.search = ''
  filtros.grupo = ''
  applied.search = ''
  applied.grupo = ''
  load(1)
}

function openListModal(mode: 'alternativas' | 'scripts' | 'anexos', item: VariavelListItem) {
  listModalMode.value = mode
  if (mode === 'alternativas') {
    listModalTitle.value = `Alternativas para ${item.variavel}`
    listModalItems.value = item.alternativas ?? []
    listModalAnexos.value = []
  } else if (mode === 'scripts') {
    listModalTitle.value = `Scripts vinculados a ${item.variavel}`
    listModalItems.value = (item.scripts ?? []).map((s) => s.nome || `Script #${s.codScriptLaudo}`)
    listModalAnexos.value = []
  } else {
    listModalTitle.value = `Anexos de ${item.variavel}`
    listModalItems.value = []
    listModalAnexos.value = item.anexos ?? []
  }
  showListModal()
}

async function openDetalhesModal(item: VariavelListItem) {
  detalhesItem.value = item
  detalhesData.value = null
  detalhesError.value = ''
  detalhesLoading.value = true
  showDetalhesModal()
  try {
    const res = await variaveisApi.getDetalhesCompletos(item.codVariavel)
    detalhesData.value = res.data
  } catch (err) {
    detalhesError.value = err instanceof Error ? err.message : 'Erro ao carregar detalhes.'
  } finally {
    detalhesLoading.value = false
  }
}

function openFormulaModal(item: VariavelListItem) {
  formulaItem.value = item
  showFormulaModal()
}

async function openDicomSwal(item: VariavelListItem) {
  if (!import.meta.client) return
  const { default: Swal } = await import('sweetalert2')
  Swal.fire({
    title: 'Códigos DICOM Vinculados',
    html: `
      <p class="text-start mb-1"><strong>Variável:</strong> ${item.nome || ''}</p>
      <div class="text-start mt-2">
        <h6 class="mb-1">Códigos Vinculados:</h6>
        <ul id="codigos-list-swal" class="list-unstyled ps-3 small text-start">
          <li>Carregando...</li>
        </ul>
      </div>`,
    showCloseButton: true,
    showConfirmButton: false,
    didOpen: async () => {
      const listEl = Swal.getHtmlContainer()?.querySelector('#codigos-list-swal')
      if (!listEl) return
      try {
        const res = await variaveisApi.getCodigosVinculados(item.codVariavel)
        const codigos = res.data || []
        if (!codigos.length) {
          listEl.innerHTML = '<li><i class="bi bi-info-circle me-1"></i>Nenhum código DICOM vinculado.</li>'
          return
        }
        listEl.innerHTML = codigos
          .map(
            (c) =>
              `<li><code>${c.codigo}</code> - ${c.descricaoPtBr || '<span class="text-muted fst-italic">Sem descrição</span>'}</li>`
          )
          .join('')
      } catch {
        listEl.innerHTML =
          '<li class="text-danger"><i class="bi bi-exclamation-triangle-fill me-1"></i>Erro ao carregar códigos.</li>'
      }
    }
  })
}

async function confirmarExclusao(item: VariavelListItem) {
  if (!auth.isAdmin) {
    await swal.warning('Acesso negado', 'Apenas administradores podem excluir variáveis.')
    return
  }
  try {
    const deps = (await variaveisApi.getDependencias(item.codVariavel)).data
    const details = Object.entries(deps)
      .filter(([key, value]) => key !== 'possuiVinculos' && Number(value) > 0)
      .map(([key, value]) => `${key}: ${value}`)
      .join(', ')
    const confirm = await swal.confirm(
      'Excluir variável?',
      deps.possuiVinculos
        ? `Existem vínculos (${details}). A exclusão forçada removerá todos em uma transação.`
        : `Deseja excluir "${item.nome}"?`
    )
    if (confirm.isConfirmed) {
      await variaveisApi.deleteVariavel(item.codVariavel, deps.possuiVinculos)
      await swal.toast('Variável excluída.')
      await load(page.value)
    }
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao excluir variável.', 'error')
  }
}

onMounted(async () => {
  await loadGrupos()
  await load(1)
})
</script>
