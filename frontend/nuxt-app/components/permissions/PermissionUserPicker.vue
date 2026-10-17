<template>
  <div class="user-picker" @focusout="leave">
    <label :for="id">Usuário</label>
    <div class="picker-input">
      <i class="bi bi-search" aria-hidden="true" />
      <input :id="id" ref="input" role="combobox" autocomplete="off" :disabled="disabled" :value="open ? query : selectedLabel"
        placeholder="Buscar por nome ou identificação" :aria-expanded="open" :aria-controls="id + '-list'" aria-autocomplete="list"
        :aria-activedescendant="open && matches[highlight] ? id + '-' + matches[highlight]!.codUsuario : undefined"
        @focus="show" @input="search" @keydown.down.prevent="move(1)" @keydown.up.prevent="move(-1)"
        @keydown.enter.prevent="chooseHighlighted" @keydown.esc.stop="open = false" />
    </div>
    <ul v-if="open" :id="id + '-list'" role="listbox" aria-label="Usuários" class="picker-options">
      <li v-for="(user, index) in matches" :id="id + '-' + user.codUsuario" :key="user.codUsuario" role="option" :aria-selected="modelValue === user.codUsuario"
        :class="{ highlighted: highlight === index }" @mousedown.prevent="choose(user)" @mousemove="highlight = index">
        <strong>{{ user.nome || user.identificacao }}</strong><small>{{ user.identificacao }} · {{ profileLabel(user.perfil) }}</small>
      </li>
      <li v-if="!matches.length" class="no-users" role="presentation">Nenhum usuário encontrado</li>
    </ul>
  </div>
</template>
<script setup lang="ts">
import type { PermissionUserItem } from '~/stores/permissionsCenter'
const props = defineProps<{ users: PermissionUserItem[]; modelValue: number; disabled?: boolean }>()
const emit = defineEmits<{ select: [id: number] }>()
const id = useId(), input = ref<HTMLInputElement>(), open = ref(false), query = ref(''), highlight = ref(0)
const norm = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim()
const matches = computed(() => props.users.filter(user => norm(user.nome + ' ' + user.identificacao).includes(norm(query.value))))
const selectedLabel = computed(() => {
  const user = props.users.find(item => item.codUsuario === props.modelValue)
  return user ? `${user.nome || user.identificacao} · ${profileLabel(user.perfil)}` : ''
})
function profileLabel(value: string) { return value === 'admin' ? 'Administrador' : value === 'comum' ? 'Usuário comum' : value === 'usuario' ? 'Usuário padrão' : value }
function show() { open.value = true; query.value = ''; highlight.value = 0 }
function search(event: Event) { query.value = (event.target as HTMLInputElement).value; open.value = true; highlight.value = 0 }
function move(delta: number) {
  if (!open.value) { show(); return }
  highlight.value = Math.max(0, Math.min(matches.value.length - 1, highlight.value + delta))
  nextTick(() => document.getElementById(id + '-' + matches.value[highlight.value]?.codUsuario)?.scrollIntoView({ block: 'nearest' }))
}
function choose(user: PermissionUserItem) { open.value = false; emit('select', user.codUsuario) }
function chooseHighlighted() { const user = matches.value[highlight.value]; if (open.value && user) choose(user) }
function leave(event: FocusEvent) { if (!(event.currentTarget as HTMLElement).contains(event.relatedTarget as Node)) open.value = false }
</script>
<style scoped>
.user-picker { position: relative; min-width: 0; }
label { display: block; font-size: 12px; font-weight: 600; margin-bottom: 4px; color: #4b5563; }
.picker-input { display: flex; gap: 8px; align-items: center; border: 1px solid #cbd0d7; border-radius: 6px; background: white; padding: 0 10px; }
.picker-input:focus-within { outline: 2px solid #2563eb; outline-offset: 1px; }
.picker-input i { color: #6b7280; }
input { width: 100%; min-width: 0; height: 38px; border: 0; outline: none; font-size: 14px; background: transparent; }
.picker-options { position: absolute; z-index: 40; top: 100%; left: 0; right: 0; margin: 4px 0 0; padding: 4px; list-style: none; max-height: 280px; overflow: auto; background: white; border: 1px solid #cbd0d7; border-radius: 6px; box-shadow: 0 4px 12px #00000012; }
.picker-options li { padding: 8px; border-radius: 4px; cursor: pointer; }
.picker-options strong, .picker-options small { display: block; overflow-wrap: anywhere; }
.picker-options strong { font-size: 14px; font-weight: 500; }
.picker-options small { font-size: 12px; color: #6b7280; }
.picker-options .highlighted { background: #eff6ff; }
.no-users { font-size: 13px; color: #6b7280; }
</style>
