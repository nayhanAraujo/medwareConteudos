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
        <DsButton variant="secondary" size="sm" icon="journal-medical" to="/variaveis/referencias-normalidades">
          Referências x Normalidades
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
      </DsAlert>

      <DsTable v-else>
        <template #head>
          <tr>
            <th class="w-16 text-center">Cód.</th>
            <th>Grupo</th>
            <th>Nome Clínico</th>
            <th>Variável (Código)</th>
            <th class="text-center">Sigla</th>
            <th class="text-center">Abreviação</th>
            <th>Alternativas</th>
            <th>Scripts Vinculados</th>
            <th>Anexos</th>
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
          <td class="text-center">{{ item.abreviacao || '-' }}</td>
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
            <template v-if="item.anexos?.length">
              <ul class="list-none p-0 m-0 text-sm space-y-1">
                <li v-for="anexo in item.anexos.slice(0, 2)" :key="anexo.codAnexo">
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
                    title="Abrir anexo"
                  >
                    <i class="bi bi-box-arrow-up-right" />
                  </a>
                </li>
                <li v-if="item.anexos.length > 2">
                  <DsButton variant="ghost" size="sm" class="p-0 h-auto" @click="openListModal('anexos', item)">
                    ... e mais {{ item.anexos.length - 2 }}
                  </DsButton>
                </li>
              </ul>
            </template>
            <span v-else class="text-gray-400 italic text-sm">-</span>
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
  </div>
</template>

<script setup lang="ts">
import type { VariavelAnexoDto, VariavelListItem } from '~/composables/useVariaveisApi'

definePageMeta({ layout: 'default' })

const PAGE_SIZE = 10
const variaveisApi = useVariaveisApi()
const { open: listModalOpen, show: showListModal, hide: hideListModal } = useDsModal()

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

onMounted(async () => {
  await loadGrupos()
  await load(1)
})
</script>
