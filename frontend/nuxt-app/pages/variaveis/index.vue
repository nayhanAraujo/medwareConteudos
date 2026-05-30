<template>
  <div>
    <DsPageHeader title="Variáveis" icon="calculator" />
    <DsPageShell>
      <DsSearchInput v-model="search" placeholder="Buscar variável..." @enter="load" />
      <DsTable>
        <template #head>
          <tr>
            <th>Código</th>
            <th>Nome</th>
            <th>Variável</th>
            <th>Sigla</th>
          </tr>
        </template>
        <tr v-for="v in items" :key="String(v.CODVARIAVEL)">
          <td>{{ v.CODVARIAVEL }}</td>
          <td>{{ v.NOME }}</td>
          <td>{{ v.VARIAVEL }}</td>
          <td>{{ v.SIGLA }}</td>
        </tr>
      </DsTable>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })
const api = useApi()
const search = ref('')
const items = ref<Record<string, unknown>[]>([])

async function load() {
  const q = search.value ? `?search=${encodeURIComponent(search.value)}` : ''
  const res = await api.get<{ data: Record<string, unknown>[] }>(`/api/web/variaveis${q}`)
  items.value = res.data || []
}
onMounted(load)
watch(search, load)
</script>
