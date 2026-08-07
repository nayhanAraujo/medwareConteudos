<template>
  <div>
    <DsPageHeader
      title="Cadastros auxiliares de painéis"
      subtitle="Clientes, módulos do sistema e pacotes comerciais"
      icon="gear"
    >
      <template #actions>
        <DsButton variant="secondary" size="sm" icon="arrow-left" to="/paineis">Voltar</DsButton>
      </template>
    </DsPageHeader>

    <DsPageShell>
      <div class="flex flex-wrap justify-between gap-3 mb-5">
        <div class="flex flex-wrap gap-2">
          <DsButton
            v-for="item in sections"
            :key="item.key"
            :variant="active === item.key ? 'primary' : 'secondary'"
            size="sm"
            @click="selectSection(item.key)"
          >
            {{ item.label }}
          </DsButton>
        </div>
        <DsButton v-if="auth.isAdmin" variant="success" size="sm" icon="plus-lg" @click="openEditor()">
          Novo {{ current.singular.toLowerCase() }}
        </DsButton>
      </div>

      <DsAlert v-if="!auth.isAdmin" variant="info" class="mb-4">
        A consulta está disponível para todos os usuários autenticados. Somente administradores podem alterar estes cadastros.
      </DsAlert>
      <DsAlert v-if="error" variant="error" class="mb-4">{{ error }}</DsAlert>

      <div v-if="loading" class="py-10 text-center text-gray-500">Carregando...</div>
      <DsAlert v-else-if="!rows.length && !error" variant="info">Nenhum registro cadastrado.</DsAlert>
      <DsTable v-else-if="rows.length">
        <template #head>
          <tr>
            <th>Código</th>
            <th>Nome</th>
            <th>Painéis vinculados</th>
            <th />
          </tr>
        </template>
        <tr v-for="row in rows" :key="row.id">
          <td>{{ row.id }}</td>
          <td>
            <span class="font-semibold">{{ row.nome }}</span>
            <span v-if="row.protegido" class="ml-2 text-xs text-gray-500">padrão</span>
          </td>
          <td>{{ row.quantidadeVinculos }}</td>
          <td>
            <div v-if="auth.isAdmin" class="flex justify-end gap-2">
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

    <DsModal v-model="editorOpen" :title="editing ? `Editar ${current.singular.toLowerCase()}` : `Novo ${current.singular.toLowerCase()}`">
      <DsInput v-model="name" label="Nome" :placeholder="`Nome do ${current.singular.toLowerCase()}`" @enter="save" />
      <template #footer>
        <DsButton variant="secondary" @click="editorOpen = false">Cancelar</DsButton>
        <DsButton :loading="saving" :disabled="!name.trim()" @click="save">Salvar</DsButton>
      </template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

type SectionKey = 'clientes' | 'modulos' | 'pacotes-comerciais'
type CadastroRow = { id: number; nome: string; quantidadeVinculos: number; protegido: boolean }

const sections = [
  { key: 'clientes' as const, label: 'Clientes', singular: 'Cliente' },
  { key: 'modulos' as const, label: 'Módulos', singular: 'Módulo' },
  { key: 'pacotes-comerciais' as const, label: 'Pacotes comerciais', singular: 'Pacote comercial' }
]

const api = useApi()
const auth = useAuthStore()
const swal = useSwal()
const active = ref<SectionKey>('clientes')
const rows = ref<CadastroRow[]>([])
const loading = ref(false)
const saving = ref(false)
const error = ref('')
const editorOpen = ref(false)
const editing = ref<CadastroRow | null>(null)
const name = ref('')
const current = computed(() => sections.find(item => item.key === active.value)!)
const endpoint = computed(() => `/api/web/paineis/cadastros/${active.value}`)

function message(reason: unknown) {
  return reason instanceof Error ? reason.message : 'Não foi possível concluir a operação.'
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const response = await api.get<{ data: CadastroRow[] }>(endpoint.value)
    rows.value = response.data || []
  } catch (reason) {
    error.value = message(reason)
    rows.value = []
  } finally {
    loading.value = false
  }
}

function selectSection(section: SectionKey) {
  active.value = section
  void load()
}

function openEditor(row?: CadastroRow) {
  editing.value = row || null
  name.value = row?.nome || ''
  editorOpen.value = true
}

async function save() {
  if (!name.value.trim() || saving.value) return
  saving.value = true
  try {
    const body = { nome: name.value.trim() }
    if (editing.value) await api.put(`${endpoint.value}/${editing.value.id}`, body)
    else await api.post(endpoint.value, body)
    editorOpen.value = false
    await load()
    await swal.toast(`${current.value.singular} salvo com sucesso.`)
  } catch (reason) {
    await swal.toast(message(reason), 'error')
  } finally {
    saving.value = false
  }
}

async function remove(row: CadastroRow) {
  if (row.protegido || row.quantidadeVinculos > 0) return
  const confirmation = await swal.confirm(
    `Excluir ${current.value.singular.toLowerCase()}`,
    `Deseja excluir "${row.nome}"?`
  )
  if (!confirmation?.isConfirmed) return
  try {
    await api.del(`${endpoint.value}/${row.id}`)
    await load()
    await swal.toast(`${current.value.singular} excluído com sucesso.`)
  } catch (reason) {
    await swal.toast(message(reason), 'error')
  }
}

onMounted(load)
</script>
