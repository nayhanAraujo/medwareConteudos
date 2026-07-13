<template>
  <form class="space-y-4" @submit.prevent="$emit('submit')">
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <DsInput v-model="form.nome" label="Nome *" placeholder="Nome do relatório" required />
      <DsSelect v-model="form.modulo" label="Módulo *" required>
        <option value="" disabled>Selecione o módulo</option>
        <option v-for="m in modulos" :key="m.nome" :value="m.nome">{{ m.nome }}</option>
      </DsSelect>
      <DsSelect v-model="form.formato" label="Formato *" required>
        <option value="XML">XML</option>
        <option value="JSON">JSON</option>
      </DsSelect>
      <DsSelect v-model="form.ativo" label="Status">
        <option value="1">Ativo</option>
        <option value="0">Inativo</option>
      </DsSelect>
    </div>

    <div>
      <label class="block text-sm font-medium text-gray-700 mb-1">Arquivo (opcional)</label>
      <input
        type="file"
        accept=".xml,.json,application/json,text/xml"
        class="block w-full text-sm text-gray-600"
        @change="onFile"
      >
      <p class="text-xs text-gray-500 mt-1">Selecione um .xml ou .json para preencher o conteúdo</p>
    </div>

    <div>
      <label class="block text-sm font-medium text-gray-700 mb-1">Conteúdo *</label>
      <textarea
        v-model="form.conteudo"
        rows="16"
        required
        class="w-full rounded-xl border border-gray-200 px-3 py-2 font-mono text-sm bg-white"
        placeholder="Cole o XML ou JSON do relatório"
      />
    </div>

    <div class="flex flex-wrap justify-end gap-2 pt-2">
      <slot name="actions" />
    </div>
  </form>
</template>

<script setup lang="ts">
import type { RelatorioModuloSimples } from '~/composables/useRelatoriosApi'
import { readRelatorioFileText } from '~/utils/relatorioEncoding'

const form = defineModel<{
  nome: string
  modulo: string
  formato: string
  conteudo: string
  ativo: number | string
}>({ required: true })

defineProps<{
  modulos: RelatorioModuloSimples[]
}>()

defineEmits<{ submit: [] }>()

async function onFile(ev: Event) {
  const input = ev.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  const text = await readRelatorioFileText(file)
  const ext = file.name.split('.').pop()?.toUpperCase()
  form.value.conteudo = text
  if (ext === 'XML' || ext === 'JSON') form.value.formato = ext
  if (!form.value.nome.trim()) {
    form.value.nome = file.name.replace(/\.(xml|json)$/i, '')
  }
}
</script>
