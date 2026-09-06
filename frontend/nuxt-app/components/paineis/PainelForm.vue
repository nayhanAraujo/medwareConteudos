<template>
  <form class="space-y-4" @submit.prevent="$emit('submit')">
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <DsInput v-model="form.nome" label="Nome *" placeholder="Nome do painel" required />
      <DsSelect v-model="form.codModulo" label="Módulo *" required>
        <option value="" disabled>Selecione</option>
        <option v-for="m in modulos" :key="m.codModulo" :value="m.codModulo">{{ m.nome }}</option>
      </DsSelect>
      <DsSelect v-model="form.codCliente" label="Cliente">
        <option value="">Padrão (sem cliente)</option>
        <option v-for="c in clientes" :key="c.codCliente" :value="c.codCliente">{{ c.nome }}</option>
      </DsSelect>
      <DsSelect v-model="form.ativo" label="Status">
        <option value="1">Ativo</option>
        <option value="0">Inativo</option>
      </DsSelect>
    </div>

    <div>
      <label class="block text-sm font-medium text-gray-700 mb-1">Descrição</label>
      <textarea
        v-model="form.descricao"
        rows="3"
        class="w-full rounded-xl border border-gray-200 px-3 py-2 text-sm"
      />
    </div>

    <template v-if="isPowerBi">
      <div class="rounded-2xl border border-gray-100 p-4 space-y-3 bg-gray-50/50">
        <p class="text-sm font-semibold text-ds-text">Power BI</p>
        <template v-if="showConfiguration !== false">
        <DsInput v-model="form.diretorioPbix" label="Diretório PBIX" />
        <DsInput v-model="form.nomeArquivoPbixMeta" label="Nome arquivo (metadado)" />
        <DsInput v-model="form.workspacePowerbi" label="Workspace" />
        <DsInput v-model="form.responsavelAtualizacao" label="Responsável" />
        <DsSelect v-model="form.frequenciaAtualizacao" label="Frequência">
          <option value="">—</option>
          <option value="Diária">Diária</option>
          <option value="Semanal">Semanal</option>
          <option value="Mensal">Mensal</option>
          <option value="Manual">Manual</option>
        </DsSelect>
        </template>
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">Arquivo PBIX</label>
          <input type="file" accept=".pbix" class="block w-full text-sm" @change="onPbix">
          <p v-if="nomePbixAtual" class="text-xs text-gray-500 mt-1">Atual: {{ nomePbixAtual }}</p>
        </div>
        <div>
          <p class="text-sm font-medium mb-2">Pacotes comerciais</p>
          <label v-for="p in pacotes" :key="p.codPacote" class="flex items-center gap-2 text-sm mb-1">
            <input v-model="form.pacotes" type="checkbox" :value="p.codPacote">
            {{ p.nome }}
          </label>
        </div>
      </div>
    </template>

    <template v-else-if="showConfiguration !== false">
      <DsInput v-model="form.responsavelApi" label="Responsável API" />
    </template>

    <div class="flex flex-wrap justify-end gap-2 pt-2">
      <slot name="actions" />
    </div>
  </form>
</template>

<script setup lang="ts">
const form = defineModel<{
  nome: string
  codModulo: string | number
  codCliente: string | number
  ativo: string | number
  descricao: string
  responsavelApi: string
  diretorioPbix: string
  nomeArquivoPbixMeta: string
  workspacePowerbi: string
  responsavelAtualizacao: string
  frequenciaAtualizacao: string
  pacotes: number[]
  arquivoPbix: File | null
}>('form', { required: true })

const props = defineProps<{
  tipoPainel: string
  clientes: { codCliente: number; nome: string }[]
  modulos: { codModulo: number; nome: string }[]
  pacotes: { codPacote: number; nome: string }[]
  nomePbixAtual?: string | null
  showConfiguration?: boolean
}>()

defineEmits<{ submit: [] }>()

const isPowerBi = computed(() => String(props.tipoPainel).toUpperCase() === 'POWERBI')

function onPbix(ev: Event) {
  const input = ev.target as HTMLInputElement
  form.value.arquivoPbix = input.files?.[0] || null
}
</script>
