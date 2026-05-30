<template>
  <div>
    <DsPageHeader title="Anexos da Referência" subtitle="Gerencie arquivos, links e observações" icon="paperclip" />
    <DsPageShell>
      <div class="flex flex-wrap justify-end gap-2 mb-4">
        <DsButton variant="secondary" icon="arrow-left" to="/referencias">Voltar</DsButton>
        <DsButton variant="secondary" icon="pencil-square" :to="`/referencias/${id}/editar`">Editar referência</DsButton>
      </div>

      <DsAlert v-if="errorMsg" variant="error" class="mb-4">{{ errorMsg }}</DsAlert>

      <div class="rounded-2xl border border-gray-200 bg-white p-4 mb-6">
        <h3 class="font-semibold text-ds-text mb-1">Referência #{{ referencia?.codReferencia }}</h3>
        <p class="text-sm text-gray-600 mb-0">{{ referencia?.titulo }} ({{ referencia?.ano || 's/ano' }})</p>
      </div>

      <DsSectionTitle title="Anexos cadastrados" />
      <DsAlert v-if="!anexos.length" variant="info" class="mb-4">Nenhum anexo cadastrado para esta referência.</DsAlert>
      <DsTable v-else class="mb-6">
        <template #head>
          <tr>
            <th>#</th>
            <th>Descrição</th>
            <th>Nome</th>
            <th>Tipo</th>
            <th>Link</th>
            <th />
          </tr>
        </template>
        <tr v-for="a in anexos" :key="a.codAnexo">
          <td>{{ a.codAnexo }}</td>
          <td>{{ a.descricao || '-' }}</td>
          <td>{{ a.nome || '-' }}</td>
          <td>{{ a.tipoAnexo || '-' }}</td>
          <td>
            <a v-if="a.link" :href="a.link" target="_blank" rel="noopener" class="text-blue-600 hover:underline">Abrir</a>
            <span v-else>-</span>
          </td>
          <td>
            <div class="flex justify-end gap-2">
              <DsButton variant="secondary" size="sm" icon="pencil-square" @click="abrirEdicao(a)">Editar</DsButton>
              <DsButton variant="danger" size="sm" icon="trash-fill" @click="excluir(a.codAnexo)">Excluir</DsButton>
            </div>
          </td>
        </tr>
      </DsTable>

      <DsSectionTitle title="Adicionar novo anexo" />
      <form class="grid grid-cols-1 md:grid-cols-2 gap-4" @submit.prevent="adicionar">
        <DsInput v-model="novo.descricao" label="Descrição *" required />
        <DsInput v-model="novo.nome" label="Nome *" required />
        <DsInput v-model="novo.link" label="Link" placeholder="https://..." />
        <div>
          <label class="block text-sm font-medium text-ds-text mb-1.5">Arquivo (PDF)</label>
          <input type="file" accept=".pdf" class="w-full px-4 py-3 bg-white border border-gray-200 rounded-2xl text-sm" @change="onNovoArquivo" />
        </div>
        <div class="md:col-span-2 flex justify-end">
          <DsButton type="submit" icon="plus-circle" :loading="saving" :disabled="saving">Adicionar anexo</DsButton>
        </div>
      </form>
    </DsPageShell>

    <DsModal v-model="editarOpen" title="Editar Anexo">
      <form class="space-y-4" @submit.prevent="salvarEdicao">
        <DsInput v-model="edicao.descricao" label="Descrição *" required />
        <DsInput v-model="edicao.nome" label="Nome *" required />
        <DsInput v-model="edicao.link" label="Link" placeholder="https://..." />
        <div>
          <label class="block text-sm font-medium text-ds-text mb-1.5">Substituir arquivo (PDF)</label>
          <input type="file" accept=".pdf" class="w-full px-4 py-3 bg-white border border-gray-200 rounded-2xl text-sm" @change="onEdicaoArquivo" />
        </div>
      </form>
      <template #footer>
        <DsButton variant="secondary" @click="editarOpen = false">Cancelar</DsButton>
        <DsButton icon="check-circle" :loading="saving" :disabled="saving" @click="salvarEdicao">Salvar</DsButton>
      </template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default', middleware: ['admin'] })

const route = useRoute()
const id = computed(() => Number(route.params.id))
const referenciasApi = useReferenciasApi()
const swal = useSwal()

const referencia = ref<import('~/composables/useReferenciasApi').ReferenciaDetail | null>(null)
const anexos = ref<import('~/composables/useReferenciasApi').ReferenciaAnexo[]>([])
const errorMsg = ref('')
const saving = ref(false)

const novo = reactive({
  descricao: '',
  nome: '',
  link: ''
})
const novoArquivo = ref<File | null>(null)

const editarOpen = ref(false)
const edicao = reactive({
  codAnexo: 0,
  descricao: '',
  nome: '',
  link: ''
})
const edicaoArquivo = ref<File | null>(null)

async function loadAll() {
  try {
    errorMsg.value = ''
    const [refRes, anexosRes] = await Promise.all([
      referenciasApi.getReferencia(id.value),
      referenciasApi.listAnexos(id.value)
    ])
    referencia.value = refRes.data
    anexos.value = anexosRes.data || []
  } catch (err) {
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar anexos.'
  }
}

function onNovoArquivo(event: Event) {
  const input = event.target as HTMLInputElement
  novoArquivo.value = input.files?.[0] || null
}

function onEdicaoArquivo(event: Event) {
  const input = event.target as HTMLInputElement
  edicaoArquivo.value = input.files?.[0] || null
}

async function adicionar() {
  saving.value = true
  try {
    const fd = new FormData()
    fd.append('descricao', novo.descricao)
    fd.append('nome', novo.nome)
    fd.append('link', novo.link)
    if (novoArquivo.value) fd.append('arquivo', novoArquivo.value)
    await referenciasApi.createAnexo(id.value, fd)
    novo.descricao = ''
    novo.nome = ''
    novo.link = ''
    novoArquivo.value = null
    await swal.toast('Anexo adicionado com sucesso.')
    await loadAll()
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao adicionar anexo', 'error')
  } finally {
    saving.value = false
  }
}

function abrirEdicao(anexo: import('~/composables/useReferenciasApi').ReferenciaAnexo) {
  edicao.codAnexo = anexo.codAnexo
  edicao.descricao = anexo.descricao || ''
  edicao.nome = anexo.nome || ''
  edicao.link = anexo.link || ''
  edicaoArquivo.value = null
  editarOpen.value = true
}

async function salvarEdicao() {
  if (!edicao.codAnexo) return
  saving.value = true
  try {
    const fd = new FormData()
    fd.append('descricao', edicao.descricao)
    fd.append('nome', edicao.nome)
    fd.append('link', edicao.link)
    if (edicaoArquivo.value) fd.append('arquivo', edicaoArquivo.value)
    await referenciasApi.updateAnexo(edicao.codAnexo, fd)
    editarOpen.value = false
    await swal.toast('Anexo atualizado com sucesso.')
    await loadAll()
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao atualizar anexo', 'error')
  } finally {
    saving.value = false
  }
}

async function excluir(codAnexo: number) {
  const confirm = await swal.confirm('Confirmar exclusão', 'Deseja excluir este anexo?')
  if (!confirm?.isConfirmed) return
  try {
    await referenciasApi.deleteAnexo(codAnexo)
    await swal.toast('Anexo excluído com sucesso.')
    await loadAll()
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao excluir anexo', 'error')
  }
}

onMounted(loadAll)
</script>
