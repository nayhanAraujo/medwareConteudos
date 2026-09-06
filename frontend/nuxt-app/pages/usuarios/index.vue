<template>
  <div>
    <DsPageHeader title="Usuários" icon="people">
      <template #actions>
        <DsButton v-if="canCreate" icon="person-plus" @click="openCreate">Novo usuário</DsButton>
      </template>
    </DsPageHeader>
    <DsPageShell>
      <DsAlert v-if="errorMsg" variant="error" class="mb-4">{{ errorMsg }}</DsAlert>
      <DsTable>
        <template #head>
          <tr><th>Nome</th><th>Login</th><th>Perfil</th><th>Status</th><th v-if="canEdit || canDelete">Ações</th></tr>
        </template>
        <tr v-for="u in users" :key="u.codUsuario">
          <td>{{ u.nome }}</td><td>{{ u.identificacao }}</td><td>{{ u.perfil }}</td>
          <td><DsBadge :variant="u.status === -1 ? 'success' : 'default'">{{ u.status === -1 ? 'Ativo' : 'Inativo' }}</DsBadge></td>
          <td v-if="canEdit || canDelete" class="flex gap-2">
            <DsButton v-if="canEdit" size="sm" variant="secondary" icon="pencil" @click="openEdit(u)">Editar</DsButton>
            <DsButton v-if="canDelete" size="sm" variant="danger" icon="trash" @click="remove(u)">Excluir</DsButton>
          </td>
        </tr>
      </DsTable>
    </DsPageShell>

    <DsModal v-model="modalOpen" :title="editingId ? 'Editar usuário' : 'Novo usuário'">
      <form class="grid gap-4" @submit.prevent="save">
        <DsInput v-model="form.nome" label="Nome" required />
        <DsInput v-model="form.identificacao" label="Login" required />
        <DsSelect v-model="form.perfil" label="Perfil" required><option value="admin">Administrador</option><option value="usuario">Usuário</option></DsSelect>
        <DsSelect v-model="form.status" label="Status" required><option :value="-1">Ativo</option><option :value="0">Inativo</option></DsSelect>
        <DsInput v-model="form.senha" label="Senha" type="password" :required="!editingId" :hint="editingId ? 'Deixe em branco para manter a senha atual.' : 'Mínimo de 6 caracteres.'" />
        <DsInput v-model="form.confirmarSenha" label="Confirmar senha" type="password" :required="!editingId" />
        <div class="flex justify-end gap-2"><DsButton variant="secondary" @click="modalOpen = false">Cancelar</DsButton><DsButton type="submit" :loading="saving">Salvar</DsButton></div>
      </form>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })
interface UserItem { codUsuario: number; nome: string; identificacao: string; perfil: string; status: number }
const api = useApi(); const auth = useAuthStore(); const swal = useSwal()
const users = ref<UserItem[]>([]); const modalOpen = ref(false); const editingId = ref<number | null>(null); const saving = ref(false); const errorMsg = ref('')
const canCreate = computed(() => auth.can('usuarios', 'criar'))
const canEdit = computed(() => auth.can('usuarios', 'editar'))
const canDelete = computed(() => auth.can('usuarios', 'excluir'))
const emptyForm = () => ({ nome: '', identificacao: '', perfil: 'usuario', status: -1, senha: '', confirmarSenha: '' })
const form = reactive(emptyForm())
async function load() {
  errorMsg.value = ''
  try {
    const res = await api.get<{ data: UserItem[] }>('/api/web/users')
    users.value = res.data || []
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : 'Erro ao carregar usuários.'
  }
}
function resetForm() { Object.assign(form, emptyForm()) }
function openCreate() { if (!canCreate.value) return; editingId.value = null; resetForm(); modalOpen.value = true }
function openEdit(u: UserItem) { if (!canEdit.value) return; editingId.value = u.codUsuario; Object.assign(form, { nome: u.nome, identificacao: u.identificacao, perfil: u.perfil, status: u.status, senha: '', confirmarSenha: '' }); modalOpen.value = true }
async function save() {
  if ((editingId.value && !canEdit.value) || (!editingId.value && !canCreate.value)) return
  saving.value = true
  try {
    const payload = { ...form, status: Number(form.status) }
    if (editingId.value) await api.put(`/api/web/users/${editingId.value}`, payload)
    else await api.post('/api/web/users', payload)
    modalOpen.value = false; await load(); await swal.toast('Usuário salvo com sucesso.')
  } catch (e) { await swal.error('Erro ao salvar usuário', e instanceof Error ? e.message : String(e)) } finally { saving.value = false }
}
async function remove(u: UserItem) {
  if (!canDelete.value) return
  const result = await swal.confirm('Excluir usuário?', `O usuário ${u.nome} será removido.`); if (!result?.isConfirmed) return
  try { await api.del(`/api/web/users/${u.codUsuario}`); await load(); await swal.toast('Usuário excluído.') } catch (e) { await swal.error('Erro ao excluir', e instanceof Error ? e.message : String(e)) }
}
onMounted(load)
</script>
