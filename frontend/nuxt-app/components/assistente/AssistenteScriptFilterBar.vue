<template>
  <div class="mb-5 space-y-3">
    <div class="grid gap-2 sm:grid-cols-2 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.3fr)_minmax(0,1fr)_auto]">
      <DsSelect v-model="tipoModel" input-class="!rounded-lg !py-2.5">
        <option value="">Tipo: Todos</option>
        <option value="2">Tipo: C#</option>
        <option value="1">Tipo: VB legado</option>
        <option value="3">Tipo: JSON</option>
      </DsSelect>
      <DsSelect v-model="especialidadeModel" input-class="!rounded-lg !py-2.5">
        <option value="">Especialidade: Todas</option>
        <option value="0">Especialidade: Sem especialidade</option>
        <option v-for="option in especialidades" :key="option.id" :value="String(option.id)">
          Especialidade: {{ option.nome }}
        </option>
      </DsSelect>
      <DsSelect v-model="statusModel" input-class="!rounded-lg !py-2.5">
        <option value="-1">Status: Ativo</option>
        <option value="0">Status: Inativo</option>
        <option value="">Status: Todos</option>
      </DsSelect>
      <DsButton variant="secondary" size="sm" icon="sliders" class="min-h-10 !rounded-lg" @click="$emit('clear')">
        Limpar filtros
      </DsButton>
    </div>

    <DsSearchInput
      v-model="searchModel"
      wrapper-class="!mb-0"
      placeholder="Pesquisar em títulos, códigos ou especialidades..."
      @enter="$emit('search')"
    />
  </div>
</template>

<script setup lang="ts">
import type { AssistenteOption } from '~/composables/useAssistenteApi'

defineProps<{ especialidades: AssistenteOption[] }>()
defineEmits<{ clear: []; search: [] }>()

const searchModel = defineModel<string>('search', { required: true })
const tipoModel = defineModel<string>('tipo', { required: true })
const especialidadeModel = defineModel<string>('especialidade', { required: true })
const statusModel = defineModel<string>('status', { required: true })
</script>
