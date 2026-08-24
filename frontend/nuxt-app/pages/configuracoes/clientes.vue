<template>
  <div>
    <DsPageHeader
      title="Clientes"
      subtitle="Gerenciamento de clientes do sistema (tabela CLIENTES)"
      icon="building"
    >
      <template #actions>
        <DsButton variant="secondary" size="sm" icon="arrow-left" to="/biblioteca">Voltar</DsButton>
        <DsButton v-if="auth.isAdmin" variant="success" size="sm" icon="plus-lg" @click="openEditor()">Novo cliente</DsButton>
      </template>
    </DsPageHeader>

    <DsPageShell>
      <DsAlert v-if="!auth.isAdmin" variant="info" class="mb-4">
        A consulta está disponível para todos os usuários autenticados. Somente administradores podem alterar os cadastros.
      </DsAlert>
      <DsAlert v-if="error" variant="error" class="mb-4">{{ error }}</DsAlert>

      <div v-if="loading" class="py-10 text-center text-gray-500">Carregando...</div>
      <DsAlert v-else-if="!rows.length && !error" variant="info">Nenhum cliente cadastrado.</DsAlert>
      <DsTable v-else-if="rows.length">
        <template #head>
          <tr>
            <th>Código</th>
            <th>Nome</th>
            <th>Painéis vinculados</th>
            <th v-if="auth.isAdmin" />
          </tr>
        </template>
        <tr v-for="row in rows" :key="row.id">
          <td>{{ row.id }}</td>
          <td>
            <span class="font-semibold">{{ row.nome }}</span>
            <span v-if="row.protegido" class="ml-2 text-xs text-gray-500">padrão</span>
          </td>
          <td>{{ row.quantidadeVinculos }}</td>
          <td v-if="auth.isAdmin">
            <div class="flex justify-end gap-2">
              <DsButton variant="secondary" size="sm" icon="pencil-square" @click="openEditor(row)">Editar</DsButton>
              <DsButton
                variant="danger"
                size="sm"
                icon="trash-fill"
                :disabled="row.protegido || row.quantidadeVinculos > 0"
                @click="remove(row)"
              >
                Excluir
              </DsButton>
            </div>
          </td>
        </tr>
      </DsTable>
    </DsPageShell>

    <DsModal v-model="editorOpen" :title="editing ? 'Editar cliente' : 'Novo cliente'">
      <DsInput v-model="name" label="Nome" placeholder="Nome do cliente" required @enter="save" />
      <template #footer>
        <DsButton variant="secondary" @click="editorOpen = false">Cancelar</DsButton>
        <DsButton :loading="saving" :disabled="!name.trim()" @click="save">Salvar</DsButton>
      </template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

type ClienteRow = { id: number; nome: string; quantidadeVinculos: number; protegido: boolean }

const api = useApi()
const auth = useAuthStore()
const swal = useSwal()
const endpoint = '/api/web/paineis/cadastros/clientes'

const rows = ref<ClienteRow[]>([])
const loading = ref(false)
const saving = ref(false)
const error = ref('')
const editorOpen = ref(false)
const editing = ref<ClienteRow | null>(null)
const name = ref('')

function message(reason: unknown) {
  return reason instanceof Error ? reason.message : 'Não foi possível concluir a operação.'
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const response = await api.get<{ data: ClienteRow[] }>(endpoint)
    rows.value = response.data || []
  } catch (reason) {
    error.value = message(reason)
    rows.value = []
  } finally {
    loading.value = false
  }
}

function openEditor(row?: ClienteRow) {
  editing.value = row || null
  name.value = row?.nome || ''
  editorOpen.value = true
}

async function save() {
  if (!name.value.trim() || saving.value) return
  saving.value = true
  try {
    const body = { nome: name.value.trim() }
    if (editing.value) await api.put(`${endpoint}/${editing.value.id}`, body)
    else await api.post(endpoint, body)
    editorOpen.value = false
    await load()
    await swal.toast('Cliente salvo com sucesso.')
  } catch (reason) {
    await swal.toast(message(reason), 'error')
  } finally {
    saving.value = false
  }
}

async function remove(row: ClienteRow) {
  if (row.protegido || row.quantidadeVinculos > 0) return
  const confirmation = await swal.confirm('Excluir cliente', `Deseja excluir "${row.nome}"?`)
  if (!confirmation?.isConfirmed) return
  try {
    await api.del(`${endpoint}/${row.id}`)
    await load()
    await swal.toast('Cliente excluído com sucesso.')
  } catch (reason) {
    await swal.toast(message(reason), 'error')
  }
}

onMounted(load)
</script>
