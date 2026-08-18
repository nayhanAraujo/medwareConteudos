<template><AssistenteDomainCrud domain="scripts" title="Scripts de laudo" singular="script" subtitle="Scripts importados de /scripts/pacotes" icon="code-square" :hide-create="true" :columns="columns" :fields="fields" /></template>
<script setup lang="ts">
definePageMeta({ layout: 'default' })

function formatTipo(value: unknown) {
  const map: Record<number, string> = { 1: 'VB (legado)', 2: 'C#', 3: 'JSON' }
  const n = Number(value)
  return map[n] ?? String(value ?? '—')
}

const columns = [
  { key: 'codigo', label: 'Código' },
  { key: 'titulo', label: 'Título', primary: true },
  { key: 'tipoScript', label: 'Tipo', format: formatTipo },
  { key: 'especialidades', label: 'Especialidade' }
]

const fields = [
  { key: 'titulo', label: 'Título', required: true },
  { key: 'tipoScript', label: 'Tipo', kind: 'select' as const, required: true, options: [{ label: 'VB (legado)', value: 1 }, { label: 'C#', value: 2 }, { label: 'JSON', value: 3 }] },
  { key: 'estruturaScript', label: 'Estrutura do script', kind: 'textarea' as const, required: true },
  { key: 'status', label: 'Status', kind: 'select' as const, required: true, defaultValue: -1, options: [{ label: 'Ativo', value: -1 }, { label: 'Inativo', value: 0 }] },
  { key: 'especialidades', label: 'Especialidades', kind: 'multi' as const, optionsDomain: 'especialidades' },
  { key: 'procedimentos', label: 'Procedimentos', kind: 'multi' as const, optionsDomain: 'procedimentos' }
]
</script>
