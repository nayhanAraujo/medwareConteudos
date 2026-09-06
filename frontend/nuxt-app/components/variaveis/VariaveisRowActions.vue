<template>
  <div class="flex items-center justify-center gap-1">
    <DsButton
      v-if="canEdit"
      variant="secondary"
      size="sm"
      icon="pencil-square"
      title="Editar Variável"
      :to="`/variaveis/${item.codVariavel}/editar`"
    />
    <DsButton
      :variant="item.possuiNormalidades ? 'success' : 'secondary'"
      size="sm"
      icon="graph-up"
      :title="item.possuiNormalidades ? 'Visualizar Normalidades (possui normalidades)' : 'Visualizar Normalidades'"
      :to="`/variaveis/${item.codVariavel}/complementos#normalidades`"
    />

    <DsDropdown
      align="right"
      trigger-class="inline-flex items-center gap-1 text-sm font-medium px-3 py-1.5 rounded-full border border-gray-200 bg-white hover:bg-gray-50 text-ds-text"
    >
      <template #trigger>
        <i class="bi bi-three-dots" />
        <span class="hidden xl:inline">Mais</span>
      </template>
      <template #default="{ close }">
        <DsDropdownItem v-if="canLink" :to="`/variaveis/${item.codVariavel}/complementos#anexos`" @click="close">
          <i class="bi bi-paperclip" /> Vincular Anexo
        </DsDropdownItem>
        <DsDropdownItem @click="emitAction('detalhes', close)">
          <i class="bi bi-search-heart text-cyan-600" /> Detalhes
        </DsDropdownItem>
        <DsDropdownItem v-if="item.formula" @click="emitAction('formula', close)">
          <i class="bi bi-calculator text-green-600" /> Ver Fórmula
        </DsDropdownItem>
        <DsDropdownItem :to="`/variaveis/${item.codVariavel}/complementos#estudos`" @click="close">
          <i class="bi bi-book text-orange-500" /> Consultar Estudos
        </DsDropdownItem>
        <DsDropdownItem @click="emitAction('dicom', close)">
          <i class="bi bi-qr-code-scan text-cyan-600" /> Códigos DICOM
        </DsDropdownItem>
        <DsDropdownItem v-if="canLink" :to="`/variaveis/${item.codVariavel}/complementos#especialidades`" @click="close">
          <i class="bi bi-link-45deg text-blue-600" /> Vincular Especialidades
        </DsDropdownItem>
        <DsDropdownDivider v-if="canDelete" />
        <DsDropdownItem v-if="canDelete" danger @click="emitAction('excluir', close)">
          <i class="bi bi-trash" /> Excluir Variável
        </DsDropdownItem>
      </template>
    </DsDropdown>
  </div>
</template>

<script setup lang="ts">
import type { VariavelListItem } from '~/composables/useVariaveisApi'

const props = defineProps<{ item: VariavelListItem }>()
const auth = useAuthStore()
const canEdit = computed(() => auth.can('variaveis', 'editar'))
const canLink = computed(() => auth.can('variaveis', 'vincular'))
const canDelete = computed(() => auth.can('variaveis', 'excluir'))
const emit = defineEmits<{
  detalhes: [item: VariavelListItem]
  formula: [item: VariavelListItem]
  dicom: [item: VariavelListItem]
  excluir: [item: VariavelListItem]
}>()

function emitAction(
  action: 'detalhes' | 'formula' | 'dicom' | 'excluir',
  close: () => void
) {
  close()
  emit(action, props.item)
}

</script>
