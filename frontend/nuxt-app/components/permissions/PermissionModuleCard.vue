<template>
  <article class="overflow-hidden rounded-3xl border border-white/70 bg-white/90 shadow-[0_18px_50px_rgba(15,23,42,0.10)] backdrop-blur">
    <button
      type="button"
      class="flex w-full items-center gap-4 px-5 py-4 text-left transition hover:bg-slate-50/80"
      @click="store.toggleExpanded(module.domain)"
    >
      <span class="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl border border-slate-200 bg-white text-xl text-slate-700 shadow-sm">
        <i :class="`bi ${module.icon}`" />
      </span>

      <span class="min-w-0 flex-1">
        <span class="block truncate text-lg font-semibold text-slate-950">Módulo: {{ module.title }}</span>
        <span class="block text-sm text-slate-500">{{ enabledCount }} de {{ module.actions.length }} permissões ativas</span>
      </span>

      <span class="hidden items-center gap-3 text-sm text-slate-700 sm:flex">
        <span>Permissões Ativas/Inativas</span>
        <label class="relative inline-flex cursor-pointer items-center" @click.stop>
          <input
            class="peer sr-only"
            type="checkbox"
            :checked="allEnabled"
            :disabled="!editable || moduleSwitchDisabled"
            @change="toggleModule(($event.target as HTMLInputElement).checked)"
          />
          <span class="h-8 w-14 rounded-full bg-slate-300 transition peer-checked:bg-black peer-disabled:cursor-not-allowed peer-disabled:opacity-50" />
          <span class="absolute left-1 top-1 h-6 w-6 rounded-full bg-white shadow transition peer-checked:translate-x-6" />
        </label>
      </span>

      <i :class="['bi text-xl text-slate-700 transition', expanded ? 'bi-chevron-up' : 'bi-chevron-down']" />
    </button>

    <div v-if="expanded" class="border-t border-slate-200 bg-white px-5 py-2">
      <div
        v-for="action in module.actions"
        :key="permissionKey(action)"
        class="flex flex-col gap-3 border-b border-slate-100 py-4 last:border-b-0 sm:flex-row sm:items-center"
      >
        <div class="min-w-0 flex-1">
          <p class="mb-1 font-medium text-slate-900">{{ store.actionLabel(action) }}</p>
          <p class="text-sm text-slate-500">{{ permissionDescription(action) }}</p>
        </div>

        <div class="flex items-center justify-between gap-3 sm:justify-end">
          <span
            :class="[
              'inline-flex items-center gap-1 rounded-full px-3 py-1 text-xs font-medium',
              badgeClass(action)
            ]"
          >
            <i v-if="isException(action)" class="bi bi-info-circle" />
            {{ badgeLabel(action) }}
          </span>

          <button
            v-if="mode === 'usuario' && isException(action)"
            type="button"
            class="rounded-full border border-slate-200 px-3 py-1 text-xs text-slate-600 transition hover:bg-slate-50 disabled:opacity-50"
            :disabled="!editable"
            @click="store.setUserOverride(permissionKey(action), '')"
          >
            Limpar
          </button>

          <label class="relative inline-flex cursor-pointer items-center">
            <input
              class="peer sr-only"
              type="checkbox"
              :checked="actionEnabled(action)"
              :disabled="!editable || actionDisabled"
              @change="toggleAction(action, ($event.target as HTMLInputElement).checked)"
            />
            <span class="h-8 w-14 rounded-full bg-slate-300 transition peer-checked:bg-black peer-disabled:cursor-not-allowed peer-disabled:opacity-50" />
            <span class="absolute left-1 top-1 h-6 w-6 rounded-full bg-white shadow transition peer-checked:translate-x-6" />
          </label>
        </div>
      </div>
    </div>
  </article>
</template>

<script setup lang="ts">
import type { PermissionModuleConfig, PermissionViewMode } from '~/stores/permissionsCenter'
import { usePermissionsCenterStore } from '~/stores/permissionsCenter'

const props = defineProps<{
  module: PermissionModuleConfig
  mode: PermissionViewMode
  editable: boolean
}>()

const store = usePermissionsCenterStore()

const expanded = computed(() => store.expanded.includes(props.module.domain))
const actionDisabled = computed(() => props.mode === 'perfil' && store.isAdminProfile)
const moduleSwitchDisabled = computed(() => actionDisabled.value || (props.mode === 'usuario' && !store.selectedUserId))
const enabledCount = computed(() => props.module.actions.filter((action) => actionEnabled(action)).length)
const allEnabled = computed(() => props.module.actions.length > 0 && enabledCount.value === props.module.actions.length)

function permissionKey(action: string) {
  return `${props.module.domain}.${action}`.toLowerCase()
}

function actionEnabled(action: string) {
  const key = permissionKey(action)
  return props.mode === 'perfil' ? store.profileHas(key) : store.userEffectiveHas(key)
}

function isException(action: string) {
  return props.mode === 'usuario' && !!store.userOverride(permissionKey(action))
}

function badgeLabel(action: string) {
  if (props.mode === 'usuario') return isException(action) ? 'Exceção Criada' : 'Herdado'
  return store.isAdminProfile ? 'Admin fixo' : 'Perfil'
}

function badgeClass(action: string) {
  if (props.mode === 'usuario') {
    const override = store.userOverride(permissionKey(action))
    if (override === 'PERMITIR') return 'bg-emerald-100 text-emerald-800'
    if (override === 'NEGAR') return 'bg-rose-100 text-rose-800'
    return 'bg-slate-100 text-slate-700'
  }

  return actionEnabled(action) ? 'bg-slate-900 text-white' : 'bg-slate-100 text-slate-600'
}

function permissionDescription(action: string) {
  const verb = store.actionLabel(action).toLowerCase()
  return `${store.actionLabel(action)} ${props.module.title.toLowerCase()}`
    .replace(/^Ativar\/Inativar/i, 'Ativar ou inativar')
    .replace(verb, store.actionLabel(action))
}

function toggleAction(action: string, checked: boolean) {
  const key = permissionKey(action)
  if (props.mode === 'perfil') store.toggleProfilePermission(key, checked)
  else store.toggleUserPermission(key, checked)
}

function toggleModule(checked: boolean) {
  for (const action of props.module.actions) {
    toggleAction(action, checked)
  }
}
</script>
