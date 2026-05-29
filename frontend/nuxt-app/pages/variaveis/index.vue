<template>
  <div>
    <h2 class="mb-3">Variáveis</h2>
    <input v-model="search" class="form-control mb-3" placeholder="Buscar..." @input="load" />
    <table class="table table-hover bg-white">
      <thead><tr><th>Código</th><th>Nome</th><th>Variável</th><th>Sigla</th></tr></thead>
      <tbody>
        <tr v-for="v in items" :key="v.CODVARIAVEL">
          <td>{{ v.CODVARIAVEL }}</td>
          <td>{{ v.NOME }}</td>
          <td>{{ v.VARIAVEL }}</td>
          <td>{{ v.SIGLA }}</td>
        </tr>
      </tbody>
    </table>
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
</script>

