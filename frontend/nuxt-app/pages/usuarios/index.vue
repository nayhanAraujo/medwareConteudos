<template>
  <div>
    <DsPageHeader title="Usuários" icon="people" :show-user-info="false">
      <template #actions>
        <DsButton v-if="canCreate" icon="person-plus" @click="openCreate">Novo usuário</DsButton>
      </template>
    </DsPageHeader>
    <DsPageShell>
      <div class="users-toolbar">
        <DsSearchInput v-model="search" placeholder="Pesquisar por nome..." aria-label="Pesquisar usuários por nome" wrapper-class="mb-0" class="users-search" />
        <DsSelect v-model="statusFilter" label="Status" class="users-status-filter">
          <option value="">Todos</option>
          <option value="-1">Ativos</option>
          <option value="0">Inativos</option>
        </DsSelect>
        <span v-if="!loading && !errorMsg" class="users-count" role="status">{{ filteredUsers.length }} de {{ users.length }} usuários</span>
      </div>
      <DsAlert v-if="errorMsg" variant="error" class="mb-4">
        {{ errorMsg }}
        <DsButton variant="secondary" size="sm" icon="arrow-clockwise" class="ml-3" @click="load">Tentar novamente</DsButton>
      </DsAlert>
      <div v-else-if="loading" class="users-loading" role="status"><i class="bi bi-arrow-repeat" aria-hidden="true" /> Carregando usuários...</div>
      <DsEmptyState v-else-if="!users.length" icon="people" title="Nenhum usuário cadastrado" />
      <DsEmptyState v-else-if="!filteredUsers.length" title="Nenhum usuário encontrado" action-label="Limpar filtros" @action="clearFilters" />
      <DsTable v-else class="users-table">
        <template #head>
          <tr><th>Nome</th><th>Login</th><th>E-mail</th><th>Perfil</th><th>Status</th><th v-if="canEdit || canDelete || canActivate" class="users-actions-heading">Ações</th></tr>
        </template>
        <tr v-for="u in filteredUsers" :key="u.codUsuario">
          <td class="users-name">{{ u.nome }}</td><td>{{ u.identificacao }}</td><td class="users-email">{{ u.email || '-' }}</td><td>{{ profileLabel(u.perfil) }}</td>
          <td><DsBadge :variant="u.status === -1 ? 'success' : 'default'">{{ u.status === -1 ? 'Ativo' : 'Inativo' }}</DsBadge></td>
          <td v-if="canEdit || canDelete || canActivate" class="users-actions">
            <DsDropdown v-if="canEdit || canDelete || canToggleStatus(u)" align="right" trigger-class="users-actions-trigger" :title="`Ações de ${u.nome}`">
              <template #trigger><i class="bi bi-three-dots" aria-hidden="true" /><span>Ações</span><span class="sr-only"> de {{ u.nome }}</span><i class="bi bi-chevron-down" aria-hidden="true" /></template>
              <template #default="{ close }">
                <DsDropdownItem v-if="canEdit" @click="close(); openEdit(u)"><i class="bi bi-pencil" aria-hidden="true" />Editar</DsDropdownItem>
                <DsDropdownItem v-if="canToggleStatus(u)" :disabled="statusUpdating !== null" @click="close(); toggleStatus(u)"><i class="bi" :class="u.status === -1 ? 'bi-person-dash' : 'bi-person-check'" aria-hidden="true" />{{ u.status === -1 ? 'Inativar' : 'Ativar' }}</DsDropdownItem>
                <DsDropdownDivider v-if="(canEdit || canToggleStatus(u)) && canDelete" />
                <DsDropdownItem v-if="canDelete" danger @click="close(); remove(u)"><i class="bi bi-trash" aria-hidden="true" />Excluir</DsDropdownItem>
              </template>
            </DsDropdown>
            <span v-else>-</span>
          </td>
        </tr>
      </DsTable>
    </DsPageShell>

    <DsModal v-model="modalOpen" :title="editingId ? 'Editar usuário' : 'Novo usuário'">
      <form class="grid gap-4" @submit.prevent="save">
        <DsInput v-model="form.nome" label="Nome" required />
        <DsInput v-model="form.identificacao" label="Login" required />
        <DsInput v-model="form.email" label="E-mail para recuperação" type="email" :disabled="!auth.isAdmin" hint="Opcional. O administrador pode cadastrar ou alterar este endereço." />
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
definePageMeta({ layout: 'default', compactWorkspace: true })
interface UserItem { codUsuario: number; nome: string; identificacao: string; perfil: string; status: number; email?: string | null }
const api = useApi(); const auth = useAuthStore(); const swal = useSwal()
const users = ref<UserItem[]>([]); const modalOpen = ref(false); const editingId = ref<number | null>(null); const saving = ref(false); const errorMsg = ref('')
const search = ref(''), loading = ref(true)
const statusFilter = ref('-1'), statusUpdating = ref<number | null>(null)
const normalizeName = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim()
const filteredUsers = computed(() => {
  const query = normalizeName(search.value)
  return users.value.filter(user => normalizeName(user.nome || '').includes(query) && (statusFilter.value === '' || user.status === Number(statusFilter.value)))
})
function clearFilters() { search.value = ''; statusFilter.value = '' }
function profileLabel(value: string) { return ({ admin: 'Administrador', usuario: 'Usuário', comum: 'Usuário comum' } as Record<string, string>)[value] || value }
const canCreate = computed(() => auth.can('usuarios', 'criar'))
const canEdit = computed(() => auth.can('usuarios', 'editar'))
const canDelete = computed(() => auth.can('usuarios', 'excluir'))
const canActivate = computed(() => auth.can('usuarios', 'ativar'))
function canToggleStatus(user: UserItem) { return canActivate.value && (auth.isAdmin || user.perfil.trim().toLowerCase() !== 'admin') }
const emptyForm = () => ({ nome: '', identificacao: '', email: '', perfil: 'usuario', status: -1, senha: '', confirmarSenha: '' })
const form = reactive(emptyForm())
async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    const res = await api.get<{ data: UserItem[] }>('/api/web/users')
    users.value = res.data || []
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : 'Erro ao carregar usuários.'
  } finally { loading.value = false }
}
function resetForm() { Object.assign(form, emptyForm()) }
function openCreate() { if (!canCreate.value) return; editingId.value = null; resetForm(); modalOpen.value = true }
function openEdit(u: UserItem) { if (!canEdit.value) return; editingId.value = u.codUsuario; Object.assign(form, { nome: u.nome, identificacao: u.identificacao, email: u.email || '', perfil: u.perfil, status: u.status, senha: '', confirmarSenha: '' }); modalOpen.value = true }
async function save() {
  if ((editingId.value && !canEdit.value) || (!editingId.value && !canCreate.value)) return
  saving.value = true
  try {
    const { email, ...fields } = form
    const payload = { ...fields, ...(auth.isAdmin ? { email } : {}), status: Number(form.status) }
    if (editingId.value) await api.put(`/api/web/users/${editingId.value}`, payload)
    else await api.post('/api/web/users', payload)
    modalOpen.value = false; await load(); await swal.toast('Usuário salvo com sucesso.')
  } catch (e) { await swal.error('Erro ao salvar usuário', e instanceof Error ? e.message : String(e)) } finally { saving.value = false }
}
async function toggleStatus(user: UserItem) {
  if (!canToggleStatus(user) || statusUpdating.value !== null) return
  const status = user.status === -1 ? 0 : -1
  const action = status === -1 ? 'Ativar' : 'Inativar'
  statusUpdating.value = user.codUsuario
  try {
    const result = await swal.confirm(`${action} usuário?`, `${action} o acesso de ${user.nome}?`)
    if (!result?.isConfirmed || !canToggleStatus(user)) return
    await api.put(`/api/web/users/${user.codUsuario}/status`, { status })
    user.status = status
    await swal.toast(status === -1 ? 'Usuário ativado.' : 'Usuário inativado.')
  } catch (e) { await swal.error('Erro ao alterar status', e instanceof Error ? e.message : String(e)) }
  finally { statusUpdating.value = null }
}
async function remove(u: UserItem) {
  if (!canDelete.value) return
  const result = await swal.confirm('Excluir usuário?', `O usuário ${u.nome} será removido.`); if (!result?.isConfirmed) return
  try { await api.del(`/api/web/users/${u.codUsuario}`); await load(); await swal.toast('Usuário excluído.') } catch (e) { await swal.error('Erro ao excluir', e instanceof Error ? e.message : String(e)) }
}
onMounted(load)
</script>

<style scoped>
.users-toolbar { display: flex; align-items: flex-end; justify-content: space-between; flex-wrap: wrap; gap: 12px; margin-bottom: 20px; }
.users-search { flex: 1; max-width: 480px; min-width: 180px; margin-bottom: 0 !important; }
.users-search :deep(input) { border-radius: 8px; }
.users-status-filter { min-width: 150px; }
.users-status-filter :deep(select) { border-radius: 8px; }
.users-count { font-size: 13px; color: #667085; white-space: nowrap; padding-bottom: 12px; }
.users-loading { padding: 40px 16px; text-align: center; font-size: 14px; color: #667085; }
.users-table { border-radius: 8px; }
.users-name { font-weight: 500; min-width: 180px; }
.users-email { min-width: 180px; overflow-wrap: anywhere; }
.users-table :deep(td) { vertical-align: middle; }
.users-table :deep(.users-actions-heading), .users-actions { width: 116px; text-align: right; white-space: nowrap; }
:deep(.users-actions-trigger) { border: 1px solid #d1d5db !important; border-radius: 6px; padding: 7px 10px !important; background: white !important; font-size: 13px; color: #374151; }
:deep(.users-actions-trigger:hover) { background: #f3f4f6; }
:deep(.users-actions-trigger:focus-visible) { outline: 2px solid #2563eb; outline-offset: 2px; }
@media (max-width: 600px) { .users-search { max-width: none; flex-basis: 100%; } }
</style>
