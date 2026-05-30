<template>
  <div>
    <DsPageHeader
      title="Referências Cadastradas"
      subtitle="Listagem, filtros e ações do banco de referências"
      icon="journal-richtext"
    />

    <DsPageShell>
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-3 mb-6 items-end">
        <div class="lg:col-span-3">
          <DsInput v-model="filtros.titulo" label="Título" placeholder="Ex.: fração de ejeção" @enter="load(1)" />
        </div>
        <div class="lg:col-span-2">
          <DsInput v-model="filtros.ano" label="Ano" placeholder="2024" @enter="load(1)" />
        </div>
        <div class="lg:col-span-3">
          <DsInput v-model="filtros.autor" label="Autor/Organização" placeholder="Ex.: Simpson" @enter="load(1)" />
        </div>
        <div class="lg:col-span-2">
          <DsInput v-model="filtros.abreviacao" label="Abreviação" placeholder="SIMP" @enter="load(1)" />
        </div>
        <div class="lg:col-span-2 flex justify-end gap-2">
          <DsButton variant="secondary" size="sm" icon="eraser" @click="limparFiltros">Limpar</DsButton>
          <DsButton size="sm" icon="search" @click="load(1)">Filtrar</DsButton>
        </div>
      </div>

      <div class="flex justify-end mb-4">
        <DsButton variant="success" size="sm" icon="journal-plus" to="/referencias/nova">Nova Referência</DsButton>
      </div>

      <div v-if="loading" class="text-center py-8 text-gray-500">Carregando referências...</div>
      <DsAlert v-else-if="errorMsg" variant="error">{{ errorMsg }}</DsAlert>
      <DsAlert v-else-if="!items.length" variant="info">Nenhuma referência encontrada com os filtros aplicados.</DsAlert>

      <DsTable v-else>
        <template #head>
          <tr>
            <th>#</th>
            <th>Título</th>
            <th>Autores (Abreviação)</th>
            <th>Ano</th>
            <th />
          </tr>
        </template>
        <tr v-for="item in items" :key="item.codReferencia">
          <td>{{ item.codReferencia }}</td>
          <td>
            <p class="font-semibold mb-0">{{ item.titulo }}</p>
            <small class="text-gray-500">{{ item.especialidade || 'Sem especialidade' }}</small>
          </td>
          <td>{{ item.autores || 'N/A' }}</td>
          <td>{{ item.ano || '-' }}</td>
          <td>
            <div class="flex flex-wrap justify-end gap-2">
              <DsButton variant="ghost" size="sm" icon="eye-fill" @click="abrirPreview(item)">Visualizar</DsButton>
              <DsButton variant="secondary" size="sm" icon="pencil-square" :to="`/referencias/${item.codReferencia}/editar`">Editar</DsButton>
              <DsButton variant="secondary" size="sm" icon="paperclip" :to="`/referencias/${item.codReferencia}/anexos`">Anexos</DsButton>
              <DsButton variant="secondary" size="sm" icon="people" :to="`/referencias/${item.codReferencia}/autores`">Autores</DsButton>
              <DsButton variant="danger" size="sm" icon="trash-fill" @click="confirmarExclusao(item)">Excluir</DsButton>
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

      <p v-if="!loading" class="text-sm text-gray-500 text-center mt-2">{{ totalItems }} referência(s) encontrada(s)</p>
    </DsPageShell>

    <DsModal v-model="previewOpen" :title="previewItem ? `Referência #${previewItem.codReferencia}` : 'Visualizar referência'" size="lg">
      <div v-if="previewItem" class="space-y-3 text-sm">
        <p><strong>Título:</strong> {{ previewItem.titulo }}</p>
        <p><strong>Ano:</strong> {{ previewItem.ano || '-' }}</p>
        <p><strong>Autores:</strong> {{ previewItem.autores || 'Nenhum autor' }}</p>
        <p><strong>Especialidade:</strong> {{ previewItem.especialidade || '-' }}</p>
        <p><strong>Descrição:</strong> {{ previewItem.descricao || '-' }}</p>
      </div>
      <template #footer>
        <DsButton variant="secondary" @click="previewOpen = false">Fechar</DsButton>
      </template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

const referenciasApi = useReferenciasApi()
const swal = useSwal()

const loading = ref(false)
const errorMsg = ref('')
const items = ref<import('~/composables/useReferenciasApi').ReferenciaItem[]>([])
const page = ref(1)
const totalPages = ref(0)
const totalItems = ref(0)
const filtros = reactive({
  titulo: '',
  ano: '',
  autor: '',
  abreviacao: ''
})

const previewOpen = ref(false)
const previewItem = ref<import('~/composables/useReferenciasApi').ReferenciaItem | null>(null)

const pagesToShow = computed(() => {
  const max = totalPages.value
  if (max <= 7) return Array.from({ length: max }, (_, i) => i + 1)
  const start = Math.max(1, page.value - 2)
  const end = Math.min(max, start + 4)
  return Array.from({ length: end - start + 1 }, (_, i) => start + i)
})

async function load(p = page.value) {
  loading.value = true
  errorMsg.value = ''
  page.value = p
  try {
    const res = await referenciasApi.listReferencias({
      page: p,
      pageSize: 10,
      titulo: filtros.titulo,
      ano: filtros.ano,
      autor: filtros.autor,
      abreviacao: filtros.abreviacao
    })
    items.value = res.data || []
    totalPages.value = res.totalPages || 0
    totalItems.value = res.totalItems || 0
  } catch (err) {
    items.value = []
    totalPages.value = 0
    totalItems.value = 0
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar referências.'
  } finally {
    loading.value = false
  }
}

function limparFiltros() {
  filtros.titulo = ''
  filtros.ano = ''
  filtros.autor = ''
  filtros.abreviacao = ''
  load(1)
}

function abrirPreview(item: import('~/composables/useReferenciasApi').ReferenciaItem) {
  previewItem.value = item
  previewOpen.value = true
}

async function confirmarExclusao(item: import('~/composables/useReferenciasApi').ReferenciaItem) {
  const confirm = await swal.confirm('Confirmar exclusão', `Deseja excluir a referência "${item.titulo}"?`)
  if (!confirm?.isConfirmed) return
  try {
    await referenciasApi.deleteReferencia(item.codReferencia)
    await swal.toast('Referência excluída com sucesso.')
    await load(1)
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao excluir referência', 'error')
  }
}

onMounted(() => load(1))
</script>
