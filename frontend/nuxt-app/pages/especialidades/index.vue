<template>
  <div>
    <DsPageHeader
      title="Especialidades"
      subtitle="Cadastro de especialidades do banco de referências"
      icon="heart-pulse"
    >
      <template #actions>
        <DsButton variant="secondary" size="sm" icon="arrow-left" to="/biblioteca">Biblioteca</DsButton>
        <DsButton v-if="auth.isAdmin" variant="success" size="sm" icon="plus-lg" @click="openCreate">Nova especialidade</DsButton>
      </template>
    </DsPageHeader>

    <DsPageShell>
      <div class="flex gap-3 mb-4 items-end">
        <DsSearchInput v-model="search" class="flex-1" placeholder="Pesquisar especialidade" @enter="load" />
        <DsButton variant="secondary" icon="arrow-clockwise" @click="load">Atualizar</DsButton>
      </div>

      <DsAlert v-if="error" variant="error">{{ error }}</DsAlert>
      <div v-else-if="loading" class="text-center py-8 text-gray-500">Carregando...</div>
      <DsEmptyState v-else-if="!filtered.length" title="Nenhuma especialidade encontrada" message="Cadastre uma nova especialidade ou ajuste a busca." />
      <DsTable v-else>
        <template #head>
          <tr>
            <th>Código</th>
            <th>Nome</th>
            <th />
          </tr>
        </template>
        <tr v-for="row in filtered" :key="row.codEspecialidade">
          <td>{{ row.codEspecialidade }}</td>
          <td><strong>{{ row.nome }}</strong></td>
          <td>
            <div v-if="auth.isAdmin" class="flex justify-end gap-2">
              <DsButton size="sm" variant="secondary" icon="pencil" @click="openEdit(row)">Editar</DsButton>
              <DsButton size="sm" variant="danger" icon="trash" @click="remove(row)">Excluir</DsButton>
            </div>
          </td>
        </tr>
      </DsTable>
    </DsPageShell>

    <DsModal v-model="modal" :title="editing ? 'Editar especialidade' : 'Nova especialidade'" size="md">
      <DsInput v-model="form.nome" label="Nome da especialidade" required hint="Máximo de 100 caracteres" />
      <template #footer>
        <DsButton variant="secondary" @click="modal = false">Cancelar</DsButton>
        <DsButton :disabled="saving || !form.nome?.trim()" @click="save">
          {{ saving ? 'Salvando...' : 'Salvar' }}
        </DsButton>
      </template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

type Especialidade = { codEspecialidade: number; nome: string }

const api = useApi()
const auth = useAuthStore()
const swal = useSwal()

const rows = ref<Especialidade[]>([])
const search = ref('')
const loading = ref(false)
const saving = ref(false)
const error = ref('')
const modal = ref(false)
const editing = ref<Especialidade | null>(null)
const form = reactive({ nome: '' })

const filtered = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return rows.value
  return rows.value.filter(r => r.nome.toLowerCase().includes(q) || String(r.codEspecialidade).includes(q))
})

function openCreate() {
  editing.value = null
  form.nome = ''
  modal.value = true
}

function openEdit(row: Especialidade) {
  editing.value = row
  form.nome = row.nome
  modal.value = true
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const res = await api.get<{ data?: Especialidade[] } | Especialidade[]>('/api/web/cadastros/especialidades')
    const payload = res as any
    rows.value = payload?.data || (Array.isArray(payload) ? payload : [])
  }
  catch (e) {
    error.value = e instanceof Error ? e.message : 'Erro ao carregar especialidades.'
  }
  finally {
    loading.value = false
  }
}

async function save() {
  const nome = form.nome.trim()
  if (!nome) {
    await swal.toast('Informe o nome da especialidade.', 'error')
    return
  }
  if (nome.length > 100) {
    await swal.toast('O nome deve ter no máximo 100 caracteres.', 'error')
    return
  }
  saving.value = true
  try {
    if (editing.value) {
      await api.put(`/api/web/cadastros/especialidades/${editing.value.codEspecialidade}`, { nome })
    }
    else {
      await api.post('/api/web/cadastros/especialidades', { nome })
    }
    modal.value = false
    await swal.toast('Especialidade salva.')
    await load()
  }
  catch (e) {
    await swal.toast(e instanceof Error ? e.message : 'Erro ao salvar.', 'error')
  }
  finally {
    saving.value = false
  }
}

async function remove(row: Especialidade) {
  const c = await swal.confirm('Confirmar exclusão', `Excluir "${row.nome}"?`)
  if (!c?.isConfirmed) return
  try {
    await api.del(`/api/web/cadastros/especialidades/${row.codEspecialidade}`)
    await swal.toast('Especialidade excluída.')
    await load()
  }
  catch (e) {
    await swal.toast(e instanceof Error ? e.message : 'Erro ao excluir. Pode haver vínculos com referências.', 'error')
  }
}

onMounted(load)
</script>
