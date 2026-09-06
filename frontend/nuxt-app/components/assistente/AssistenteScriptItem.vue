<template>
  <article class="rounded-lg border border-gray-200 bg-white p-4 shadow-sm transition hover:border-gray-300 hover:shadow-md">
    <div class="flex items-start gap-3">
      <input
        type="checkbox"
        class="mt-1 shrink-0 rounded border-gray-300 text-ds-primary focus:ring-ds-primary"
        :checked="selected"
        :aria-label="`Selecionar script ${scriptId}`"
        @change="$emit('toggle-selection')"
      >
      <div class="min-w-0 flex-1">
        <div class="text-xs font-medium uppercase text-gray-500">Script #{{ scriptId }}</div>
        <h3 class="mt-1 break-words text-sm font-semibold leading-5 text-ds-text">{{ title }}</h3>
        <div class="mt-3 flex flex-wrap items-center gap-2">
          <DsBadge :variant="typeVariant">{{ typeLabel }}</DsBadge>
          <DsBadge :variant="active ? 'success' : 'danger'">{{ active ? 'Ativo' : 'Inativo' }}</DsBadge>
          <DsBadge v-if="script.origemConteudos" variant="primary">Sincronizado</DsBadge>
        </div>
        <p class="mt-3 flex items-center gap-1.5 text-xs text-gray-500">
          <i class="bi bi-clock-history" /> Última modificação: {{ modificationLabel }}
        </p>
      </div>
      <DsDropdown align="right">
        <template #trigger>
          <span class="inline-flex min-h-9 items-center gap-2 rounded-lg border border-gray-200 bg-white px-3 py-2 text-xs font-semibold text-ds-text shadow-sm hover:bg-gray-50">
            Gerenciar <i class="bi bi-chevron-down text-[10px]" />
          </span>
        </template>
        <template #default="{ close }">
          <DsDropdownItem @click="$emit('links'); close()"><i class="bi bi-diagram-3" /> Vínculos</DsDropdownItem>
          <DsDropdownItem v-if="exportable" @click="$emit('export'); close()"><i class="bi bi-download" /> Exportar</DsDropdownItem>
          <template v-if="admin">
            <DsDropdownDivider />
            <DsDropdownItem v-if="script.origemConteudos" @click="navigateTo(`/scripts/${script.origemConteudos}/editar`); close()"><i class="bi bi-box-arrow-up-right" /> Editar em Conteúdos</DsDropdownItem>
            <DsDropdownItem v-else @click="$emit('edit'); close()"><i class="bi bi-pencil" /> Editar</DsDropdownItem>
            <DsDropdownItem @click="$emit('toggle-status'); close()">
              <i :class="`bi bi-${active ? 'slash-circle' : 'check-circle'}`" /> {{ active ? 'Inativar' : 'Ativar' }}
            </DsDropdownItem>
            <DsDropdownItem danger @click="$emit('delete'); close()"><i class="bi bi-trash" /> Excluir</DsDropdownItem>
          </template>
        </template>
      </DsDropdown>
    </div>
  </article>
</template>

<script setup lang="ts">
import type { AssistenteEntity } from '~/composables/useAssistenteApi'

const props = defineProps<{
  script: AssistenteEntity
  selected: boolean
  admin: boolean
}>()

defineEmits<{
  'toggle-selection': []
  links: []
  export: []
  edit: []
  'toggle-status': []
  delete: []
}>()

const scriptId = computed(() => Number(props.script.id ?? props.script.codigo ?? props.script.codigoscriptlaudo ?? props.script.CODSCRIPTLAUDO ?? 0))
const title = computed(() => String(props.script.titulo ?? props.script.TITULO ?? 'Script sem título'))
const typeValue = computed(() => Number(props.script.tipoScript ?? props.script.TIPOSCRIPT))
const active = computed(() => props.script.status === undefined || props.script.status === true || Number(props.script.status) === -1 || Number(props.script.status) === 1)
const exportable = computed(() => [1, 2, 3].includes(typeValue.value))
const typeLabel = computed(() => ({ 1: 'VB (legado)', 2: 'C#', 3: 'JSON' } as Record<number, string>)[typeValue.value] || 'Outro')
const typeVariant = computed<'default' | 'primary' | 'warning' | 'purple'>(() => ({ 1: 'warning', 2: 'primary', 3: 'purple' } as const)[typeValue.value as 1 | 2 | 3] || 'default')
const modificationLabel = computed(() => {
  const value = props.script.dataModificacao ?? props.script.DATAMODIFICACAO
  if (!value) return 'não informada'
  const match = /^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})/.exec(String(value))
  return match ? `${match[3]}/${match[2]}/${match[1]} às ${match[4]}:${match[5]}` : String(value)
})
</script>
