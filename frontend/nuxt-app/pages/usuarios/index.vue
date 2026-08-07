<template>
  <div>
    <DsPageHeader title="Usuários" icon="people">
      <template #actions>
        <DsButton v-if="auth.isAdmin" icon="person-plus" @click="openCreate">Novo usuário</DsButton>
      </template>
    </DsPageHeader>
    <DsPageShell>
      <DsTable>
        <template #head>
          <tr><th>Nome</th><th>Login</th><th>Perfil</th><th>Status</th><th v-if="auth.isAdmin">Ações</th></tr>
        </template>
        <tr v-for="u in users" :key="u.codusuario">
          <td>{{ u.nome }}</td><td>{{ u.identificacao }}</td><td>{{ u.perfil }}</td>
          <td><DsBadge :variant="u.status === -1 ? 'success' : 'default'">{{ u.status === -1 ? 'Ativo' : 'Inativo' }}</DsBadge></td>
          <td v-if="auth.isAdmin" class="flex gap-2">
            <DsButton size="sm" variant="secondary" icon="pencil" @click="openEdit(u)">Editar</DsButton>
            <DsButton size="sm" variant="danger" icon="trash" @click="remove(u)">Excluir</DsButton>
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
interface UserItem { codusuario: number; nome: string; identificacao: string; perfil: string; status: number }
const api = useApi(); const auth = useAuthStore(); const swal = useSwal()
const users = ref<UserItem[]>([]); const modalOpen = ref(false); const editingId = ref<number | null>(null); const saving = ref(false)
const emptyForm = () => ({ nome: '', identificacao: '', perfil: 'usuario', status: -1, senha: '', confirmarSenha: '' })
const form = reactive(emptyForm())
async function load() { const res = await api.get<{ data: UserItem[] }>('/api/web/users'); users.value = res.data || [] }
function resetForm() { Object.assign(form, emptyForm()) }
function openCreate() { editingId.value = null; resetForm(); modalOpen.value = true }
function openEdit(u: UserItem) { editingId.value = u.codusuario; Object.assign(form, { nome: u.nome, identificacao: u.identificacao, perfil: u.perfil, status: u.status, senha: '', confirmarSenha: '' }); modalOpen.value = true }
async function save() {
  saving.value = true
  try {
    const payload = { ...form, status: Number(form.status) }
    if (editingId.value) await api.put(`/api/web/users/${editingId.value}`, payload)
    else await api.post('/api/web/users', payload)
    modalOpen.value = false; await load(); await swal.toast('Usuário salvo com sucesso.')
  } catch (e) { await swal.error('Erro ao salvar usuário', e instanceof Error ? e.message : String(e)) } finally { saving.value = false }
}
async function remove(u: UserItem) {
  const result = await swal.confirm('Excluir usuário?', `O usuário ${u.nome} será removido.`); if (!result?.isConfirmed) return
  try { await api.del(`/api/web/users/${u.codusuario}`); await load(); await swal.toast('Usuário excluído.') } catch (e) { await swal.error('Erro ao excluir', e instanceof Error ? e.message : String(e)) }
}
onMounted(load)
</script>
