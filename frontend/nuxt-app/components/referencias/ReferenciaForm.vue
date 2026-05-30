<template>
  <form class="space-y-6" @submit.prevent="$emit('submit')">
    <div class="grid grid-cols-1 md:grid-cols-12 gap-4">
      <div class="md:col-span-8">
        <DsInput v-model="form.titulo" label="Título *" placeholder="Digite o título da referência" required />
      </div>
      <div class="md:col-span-4">
        <DsInput v-model="form.ano" type="number" label="Ano *" placeholder="2024" required />
      </div>
    </div>

    <DsTextarea v-model="form.descricao" label="Descrição" rows="3" placeholder="Digite uma descrição da referência" />

    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <DsInput v-model="form.doi" label="DOI" placeholder="10.1000/123456" />
      <DsInput v-model="form.isbn" label="ISBN" placeholder="978-0-123456-47-2" />
    </div>

    <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
      <DsInput v-model="form.volume" label="Volume" placeholder="Vol. 1" />
      <DsInput v-model="form.paginas" label="Páginas" placeholder="1-15" />
      <DsSelect v-model="form.codEspecialidade" label="Especialidade">
        <option value="">Selecione uma especialidade</option>
        <option v-for="esp in especialidades" :key="esp.codEspecialidade" :value="String(esp.codEspecialidade)">
          {{ esp.nome }}
        </option>
      </DsSelect>
    </div>

    <DsSelect v-model="form.codTipoRef" label="Tipo de Referência">
      <option value="">Selecione o tipo</option>
      <option v-for="tipo in tipos" :key="tipo.codTipoRef" :value="String(tipo.codTipoRef)">
        {{ tipo.descricao }}
      </option>
    </DsSelect>

    <div class="flex justify-end gap-2 pt-2">
      <slot name="actions" />
    </div>
  </form>
</template>

<script setup lang="ts">
defineProps<{
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
  tipos: Array<{ codTipoRef: number; descricao: string }>
}>()

defineEmits<{ submit: [] }>()
</script>
