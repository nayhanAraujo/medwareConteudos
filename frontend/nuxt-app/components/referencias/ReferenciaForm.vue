<template>
  <form class="space-y-6" @submit.prevent="$emit('submit')">
    <DsAlert variant="info" class="mb-2">
      Campos marcados com <span class="font-semibold text-rose-600">*</span> são obrigatórios.
    </DsAlert>

    <DsSectionTitle title="Informações básicas" />
    <div class="grid grid-cols-1 md:grid-cols-12 gap-4">
      <div class="md:col-span-8">
        <DsInput
          v-model="form.titulo"
          label="Título *"
          placeholder="Digite o título da referência"
          required
          :input-class="fieldClass(!form.titulo.trim())"
          hint="Obrigatório"
        />
      </div>
      <div class="md:col-span-4">
        <DsInput
          v-model="form.ano"
          type="number"
          label="Ano *"
          placeholder="2024"
          required
          :input-class="fieldClass(!anoValido)"
          hint="Obrigatório · entre 1800 e 2200"
        />
      </div>
    </div>

    <DsTextarea
      v-model="form.descricao"
      label="Descrição"
      rows="3"
      placeholder="Digite uma descrição da referência"
      hint="Opcional · máx. 255 caracteres"
    />

    <DsSectionTitle title="Identificadores" />
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <DsInput
        :model-value="form.doi"
        label="DOI"
        placeholder="10.1093/eurheartj/ehab184"
        hint="Opcional · letras, números e / . - _ ( )"
        @update:model-value="form.doi = String($event).replace(/[^A-Za-z0-9./\-_():;+]/g, '')"
      />
      <DsInput
        :model-value="form.isbn"
        label="ISBN"
        placeholder="978-0-123456-47-2"
        hint="Opcional · máx. 20 caracteres"
        @update:model-value="form.isbn = String($event).replace(/[^0-9Xx-]/g, '')"
      />
    </div>

    <DsSectionTitle title="Detalhes da publicação" />
    <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
      <DsInput
        v-model="form.volume"
        label="Volume"
        placeholder="Vol. 1"
        hint="Opcional · máx. 10 caracteres"
      />
      <DsInput
        :model-value="form.paginas"
        label="Páginas"
        placeholder="1-15"
        hint="Opcional · máx. 20 caracteres"
        @update:model-value="form.paginas = String($event).replace(/[^0-9,-]/g, '')"
      />
      <DsSelect v-model="form.codEspecialidade" label="Especialidade" hint="Opcional">
        <option value="">Selecione uma especialidade</option>
        <option v-for="esp in especialidades" :key="esp.codEspecialidade" :value="String(esp.codEspecialidade)">
          {{ esp.nome }}
        </option>
      </DsSelect>
    </div>

    <DsSelect v-model="form.codTipoRef" label="Tipo de referência" hint="Opcional">
      <option value="">Selecione o tipo</option>
      <option v-for="tipo in tipos" :key="tipo.codTipoRef" :value="String(tipo.codTipoRef)">
        {{ tipo.nome || tipo.descricao }}
      </option>
    </DsSelect>

    <slot name="before-actions" />

    <div class="flex flex-wrap justify-end gap-2 pt-2">
      <slot name="actions" />
    </div>
  </form>
</template>

<script setup lang="ts">
const props = defineProps<{
  form: {
    titulo: string
    ano: string
    descricao: string
    doi: string
    isbn: string
    volume: string
    paginas: string
    codEspecialidade: string
    codTipoRef: string
  }
  especialidades: Array<{ codEspecialidade: number; nome: string }>
  tipos: Array<{ codTipoRef: number; nome?: string; descricao: string }>
  showValidation?: boolean
}>()

defineEmits<{ submit: [] }>()

const anoValido = computed(() => {
  const ano = Number(props.form.ano)
  return Number.isFinite(ano) && ano >= 1800 && ano <= 2200
})

function fieldClass(invalid: boolean) {
  if (!props.showValidation || !invalid) return ''
  return 'border-rose-400 ring-2 ring-rose-200'
}
</script>
