<template>
  <div>
    <DsPageHeader
      title="Centro de Permissões"
      subtitle="Gerencie permissões por perfil e exceções individuais por usuário."
      icon="shield-lock"
    >
      <template #actions>
        <DsButton variant="secondary" size="sm" icon="arrow-left" to="/biblioteca">Voltar</DsButton>
      </template>
    </DsPageHeader>

    <DsPageShell>
      <DsAlert v-if="!canView" variant="error">
        Você não possui permissão para visualizar configurações.
      </DsAlert>

      <template v-else>
        <DsAlert v-if="store.error" variant="error" class="mb-4">{{ store.error }}</DsAlert>

        <section class="sticky top-3 z-20 mb-6 rounded-3xl border border-white/70 bg-white/85 p-3 shadow-[0_18px_50px_rgba(15,23,42,0.12)] backdrop-blur-xl">
          <div class="grid gap-3 lg:grid-cols-2">
            <button type="button" :class="tabClass('perfil')" @click="mode = 'perfil'">
              <i class="bi bi-diagram-3" />
              Visão por Perfil
            </button>
            <button type="button" :class="tabClass('usuario')" @click="mode = 'usuario'">
              <i class="bi bi-person-gear" />
              Visão por Usuário
            </button>
          </div>

          <div class="mt-4 grid gap-3 xl:grid-cols-[minmax(260px,1fr)_auto] xl:items-center">
            <div v-if="mode === 'perfil'" class="grid gap-2 sm:grid-cols-[180px_minmax(220px,1fr)] sm:items-center">
              <label class="text-sm font-semibold text-slate-700">Perfil Principal</label>
              <select
                v-model="store.selectedProfile"
                class="min-h-12 rounded-2xl border border-slate-200 bg-white px-4 text-slate-900 outline-none transition focus:border-slate-400 focus:ring-4 focus:ring-slate-100"
              >
                <option v-for="profile in store.profiles" :key="profile" :value="profile">
                  {{ profileLabel(profile) }}
                </option>
              </select>
            </div>

            <div v-else class="grid gap-2 sm:grid-cols-[180px_minmax(220px,1fr)] sm:items-center">
              <label class="text-sm font-semibold text-slate-700">Usuário</label>
              <select
                v-model.number="store.selectedUserId"
                class="min-h-12 rounded-2xl border border-slate-200 bg-white px-4 text-slate-900 outline-none transition focus:border-slate-400 focus:ring-4 focus:ring-slate-100"
              >
                <option :value="0">Selecione um usuário</option>
                <option v-for="user in store.users" :key="user.codUsuario" :value="user.codUsuario">
                  {{ userLabel(user) }}
                </option>
              </select>
            </div>

            <div class="flex flex-wrap gap-2 xl:justify-end">
              <DsButton
                v-if="mode === 'perfil'"
                :disabled="!canEdit || store.isAdminProfile"
                :loading="store.saving"
                icon="save"
                @click="saveProfile"
              >
                Salvar Perfil
              </DsButton>
              <DsButton
                v-else
                :disabled="!canEdit || !store.selectedUserId"
                :loading="store.saving"
                icon="save"
                @click="saveUser"
              >
                Salvar Usuário
              </DsButton>

              <DsButton variant="secondary" icon="eye" :disabled="bulkDisabled" @click="permitAll">
                Permitir Tudo
              </DsButton>
              <DsButton variant="secondary" icon="trash" :disabled="bulkDisabled" @click="denyAll">
                Negar Tudo
              </DsButton>
              <DsButton
                v-if="mode === 'usuario'"
                variant="ghost"
                icon="eraser"
                :disabled="!canEdit || !store.selectedUserId"
                @click="clearUserOverrides"
              >
                Limpar Exceções
              </DsButton>
            </div>
          </div>

          <div v-if="mode === 'perfil' && store.isAdminProfile" class="mt-3 rounded-2xl bg-blue-50 px-4 py-3 text-sm text-blue-800">
            O perfil administrador possui acesso total por regra fixa da aplicação. A matriz abaixo fica apenas para conferência.
          </div>

          <div v-if="mode === 'usuario' && store.userState" class="mt-3 rounded-2xl bg-slate-50 px-4 py-3 text-sm text-slate-700">
            Permissões herdadas do perfil <strong>{{ profileLabel(store.userState.perfil) }}</strong>.
            Use exceções individuais apenas quando o usuário precisar fugir da regra do perfil.
          </div>
        </section>

        <div v-if="store.loading" class="space-y-3">
          <div v-for="index in 5" :key="index" class="h-20 animate-pulse rounded-3xl bg-white/70" />
        </div>

        <DsEmptyState
          v-else-if="mode === 'usuario' && !store.selectedUserId"
          icon="person-gear"
          title="Selecione um usuário"
          description="Escolha um usuário no cabeçalho para consultar permissões herdadas e exceções individuais."
        />

        <DsEmptyState
          v-else-if="store.modules.length === 0"
          icon="shield-exclamation"
          title="Nenhum módulo de permissão encontrado"
          description="O catálogo de permissões não retornou permissões ativas. Reinicie a API para executar a rotina de seed ou verifique a conexão com o REFERENCIAS.FDB."
        />

        <section v-else class="space-y-4">
          <PermissionModuleCard
            v-for="module in store.modules"
            :key="module.domain"
            :module="module"
            :mode="mode"
            :editable="canEdit"
          />
        </section>
      </template>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import PermissionModuleCard from '~/components/permissions/PermissionModuleCard.vue'
import type { PermissionViewMode, PermissionUserItem } from '~/stores/permissionsCenter'
import { usePermissionsCenterStore } from '~/stores/permissionsCenter'

definePageMeta({ layout: 'default' })

const auth = useAuthStore()
const swal = useSwal()
const store = usePermissionsCenterStore()
const mode = ref<PermissionViewMode>('perfil')

const canView = computed(() => auth.isAdmin || auth.can('configuracoes', 'visualizar'))
const canEdit = computed(() => auth.isAdmin || auth.can('configuracoes', 'editar'))
const bulkDisabled = computed(() => {
  if (!canEdit.value) return true
  if (mode.value === 'perfil') return store.isAdminProfile
  return !store.selectedUserId
})

watch(() => store.selectedProfile, () => {
  if (canView.value) store.loadProfile()
})

watch(() => store.selectedUserId, () => {
  if (canView.value) store.loadUser()
})

watch(canView, (allowed) => {
  if (allowed && store.catalog.length === 0 && !store.loading) store.load()
}, { immediate: true })

function tabClass(tab: PermissionViewMode) {
  return [
    'inline-flex min-h-14 items-center justify-center gap-3 rounded-2xl px-4 text-base font-semibold transition',
    mode.value === tab
      ? 'bg-slate-800 text-white shadow-lg'
      : 'bg-white text-slate-700 hover:bg-slate-50'
  ]
}

function profileLabel(value?: string | null) {
  if (!value) return '-'
  if (value === 'admin') return 'Administrador'
  if (value === 'usuario') return 'Usuário padrão'
  if (value === 'comum') return 'Usuário comum'
  return value
}

function userLabel(user: PermissionUserItem) {
  const name = user.nome || user.identificacao || `Usuário ${user.codUsuario}`
  return `${name} — ${profileLabel(user.perfil)}`
}

async function saveProfile() {
  if (!canEdit.value || store.isAdminProfile) return
  try {
    await store.saveProfile()
    await swal.toast('Permissões do perfil salvas.')
  } catch (reason) {
    await swal.error('Erro ao salvar perfil', reason instanceof Error ? reason.message : String(reason))
  }
}

async function saveUser() {
  if (!canEdit.value || !store.selectedUserId) return
  try {
    await store.saveUser()
    await swal.toast('Exceções do usuário salvas.')
  } catch (reason) {
    await swal.error('Erro ao salvar usuário', reason instanceof Error ? reason.message : String(reason))
  }
}

function permitAll() {
  if (mode.value === 'perfil') store.permitAll()
  else store.userPermitAll()
}

function denyAll() {
  if (mode.value === 'perfil') store.denyAll()
  else store.userDenyAll()
}

function clearUserOverrides() {
  store.userOverrides = []
}

</script>
