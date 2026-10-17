<template>
  <section class="permission-module" :aria-label="module.title">
    <header class="module-header">
      <div class="module-title">
        <i :class="['bi', module.icon]" aria-hidden="true" />
        <span><strong>{{ module.title }}</strong><small>{{ enabled }} de {{ module.actions.length }} permitidas<span v-if="changedCount"> · {{ changedCount }} {{ changedCount === 1 ? 'alterada' : 'alteradas' }}</span></small></span>
        <button type="button" class="module-expand" :aria-expanded="expanded" :aria-controls="id" :aria-label="(expanded ? 'Recolher ' : 'Expandir ') + module.title" @click="emit('expand')"><i class="bi" :class="expanded ? 'bi-chevron-up' : 'bi-chevron-down'" aria-hidden="true" /></button>
      </div>
      <div class="module-tools">
        <label v-if="store.mode === 'perfil'" class="module-check">
          <input type="checkbox" :checked="enabled === module.actions.length" :indeterminate="enabled > 0 && enabled < module.actions.length"
            :disabled="!editable" :aria-label="'Permitir todas as permissões de ' + module.title" @change="toggleAll" />
          <span>Todas</span>
        </label>
        <details v-if="editable" ref="menu" class="module-menu">
          <summary :aria-label="'Ações em lote de ' + module.title" title="Ações do módulo"><i class="bi bi-three-dots" aria-hidden="true" /><span>Ações</span></summary>
          <div class="menu-options">
            <button type="button" @click="bulk('permitir')"><i class="bi bi-check2-all" aria-hidden="true" />Permitir tudo</button>
            <button type="button" @click="bulk('negar')"><i class="bi bi-slash-circle" aria-hidden="true" />Negar tudo</button>
            <button v-if="store.mode === 'usuario'" type="button" @click="bulk('herdar')"><i class="bi bi-arrow-counterclockwise" aria-hidden="true" />Restaurar herança</button>
          </div>
        </details>
        <button v-else type="button" class="disabled-menu" disabled title="Ações indisponíveis"><i class="bi bi-three-dots" aria-hidden="true" />Ações</button>
      </div>
    </header>
    <div :id="id" class="module-rows" :class="{ 'mobile-collapsed': !expanded }">
      <div v-for="action in actions" :key="key(action)" class="permission-row" :class="{ changed: store.changed(key(action)) }" :data-permission="key(action)">
        <div class="permission-name">
          <label v-if="store.mode === 'perfil'" :for="id + '-' + action">{{ store.actionLabel(action) }}</label>
          <span v-else :id="id + '-' + action + '-label'">{{ store.actionLabel(action) }}</span>
          <small v-if="store.description(key(action))">{{ store.description(key(action)) }}</small>
          <small v-if="store.changed(key(action))" class="change-label"><i class="bi bi-pencil" aria-hidden="true" /> Alteração pendente</small>
        </div>
        <div class="permission-control">
          <span class="permission-result" :class="{ allowed: store.effectiveHas(key(action)), denied: !store.effectiveHas(key(action)), exception: store.mode === 'usuario' && !!store.userOverride(key(action)) }">
            <i class="bi" :class="store.effectiveHas(key(action)) ? 'bi-check-circle' : 'bi-slash-circle'" aria-hidden="true" />{{ store.resultLabel(key(action)) }}
          </span>
          <input v-if="store.mode === 'perfil'" :id="id + '-' + action" type="checkbox" :checked="store.effectiveHas(key(action))" :disabled="!editable"
            @change="store.toggleProfilePermission(key(action), ($event.target as HTMLInputElement).checked)" />
          <select v-else :aria-labelledby="id + '-' + action + '-label'" :value="store.userOverride(key(action))" :disabled="!editable"
            @change="store.setUserOverride(key(action), ($event.target as HTMLSelectElement).value as '' | 'PERMITIR' | 'NEGAR')">
            <option value="">Herdar</option><option value="PERMITIR">Permitir</option><option value="NEGAR">Negar</option>
          </select>
        </div>
      </div>
    </div>
  </section>
</template>
<script setup lang="ts">
import type { PermissionModuleConfig } from '~/stores/permissionsCenter'
import { usePermissionsCenterStore } from '~/stores/permissionsCenter'
const props = defineProps<{ module: PermissionModuleConfig; actions: string[]; editable: boolean; expanded: boolean }>()
const emit = defineEmits<{ expand: []; bulk: [action: 'permitir' | 'negar' | 'herdar'] }>()
const store = usePermissionsCenterStore(), id = useId(), menu = ref<HTMLDetailsElement>()
const enabled = computed(() => store.enabledCount(props.module)), changedCount = computed(() => store.changedCount(props.module))
function key(action: string) { return props.module.domain + '.' + action }
function bulk(action: 'permitir' | 'negar' | 'herdar') { if (menu.value) menu.value.open = false; emit('bulk', action) }
function toggleAll(event: Event) {
  const input = event.target as HTMLInputElement, checked = input.checked
  input.checked = enabled.value === props.module.actions.length
  input.indeterminate = enabled.value > 0 && enabled.value < props.module.actions.length
  bulk(checked ? 'permitir' : 'negar')
}
</script>
<style scoped>
.permission-module { min-width: 0; border: 1px solid #dce0e5; border-radius: 8px; background: white; }
.module-header { display: flex; gap: 12px; align-items: center; justify-content: space-between; padding: 16px; border-bottom: 1px solid #e5e7eb; }
.module-title { display: flex; align-items: center; gap: 12px; min-width: 0; border: 0; padding: 0; background: none; text-align: left; color: #20252e; }
.module-title > i:first-child { font-size: 22px; color: #536174; }
.module-title strong { display: block; font-size: 16px; font-weight: 600; overflow-wrap: anywhere; }
.module-title small { display: block; font-size: 12px; color: #667085; margin-top: 3px; }
.module-tools { display: flex; align-items: center; gap: 12px; flex-shrink: 0; }
.module-check { display: inline-flex; align-items: center; gap: 6px; font-size: 12px; }
input[type=checkbox] { width: 17px; height: 17px; accent-color: #2563eb; flex-shrink: 0; cursor: pointer; }
.module-menu { position: relative; }
.module-menu summary, .disabled-menu { display: flex; align-items: center; gap: 6px; border: 1px solid #dce0e5; border-radius: 6px; padding: 6px 10px; font-size: 13px; list-style: none; cursor: pointer; background: white; }
.module-menu summary::-webkit-details-marker { display: none; }
.disabled-menu { color: #9ca3af; cursor: not-allowed; }
.menu-options { position: absolute; top: calc(100% + 4px); right: 0; min-width: 180px; background: white; border: 1px solid #dce0e5; border-radius: 6px; padding: 4px; box-shadow: 0 4px 12px #00000012; z-index: 15; }
.menu-options button { display: flex; align-items: center; gap: 8px; width: 100%; text-align: left; padding: 9px 8px; border: 0; background: white; font-size: 13px; border-radius: 4px; }
.menu-options button:hover { background: #f3f4f6; }
.permission-row { display: flex; align-items: center; gap: 16px; padding: 14px 16px; border-bottom: 1px solid #edf0f3; min-height: 70px; border-left: 3px solid transparent; }
.permission-row:last-child { border-bottom: 0; }
.permission-row.changed { background: #fffcf0; border-left-color: #c48b10; }
.permission-name { flex: 1; min-width: 0; }
.permission-name > label, .permission-name > span { display: block; font-size: 14px; font-weight: 500; color: #252b36; }
.permission-name small { display: block; font-size: 12px; color: #667085; margin-top: 3px; overflow-wrap: anywhere; }
.permission-name .change-label { color: #906407; }
.permission-control { display: flex; align-items: center; gap: 16px; }
.permission-result { font-size: 12px; display: inline-flex; align-items: center; gap: 5px; color: #576173; }
.permission-result.allowed { color: #187454; }
.permission-result.denied { color: #ab354b; }
.permission-result.exception { font-weight: 600; }
select { width: 112px; border: 1px solid #cbd0d7; border-radius: 6px; background: white; padding: 8px; font-size: 13px; }
:disabled { cursor: not-allowed; opacity: .6; }
button:focus-visible, summary:focus-visible, select:focus-visible, input:focus-visible { outline: 2px solid #2563eb; outline-offset: 3px; }
.module-expand { display: none; border: 0; background: none; padding: 6px; border-radius: 4px; }
@media (max-width: 1023px) {
  .module-expand { display: inline-flex; margin-left: auto; }
  .module-title { flex: 1; }
  .mobile-collapsed { display: none; }
}
@media (max-width: 600px) {
  .module-header { flex-wrap: wrap; gap: 10px; padding: 12px; }
  .module-title { flex-basis: 100%; }
  .module-tools { margin-left: auto; }
  .permission-row { align-items: stretch; flex-direction: column; gap: 10px; padding: 12px; }
  .permission-control { justify-content: space-between; gap: 8px; }
}
</style>
