<template>
  <div>
    <div class="d-flex justify-content-between align-items-center mb-3">
      <h2>Usuários</h2>
    </div>
    <table class="table table-striped bg-white">
      <thead>
        <tr>
          <th>Nome</th>
          <th>Login</th>
          <th>Perfil</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="u in users" :key="u.codusuario">
          <td>{{ u.nome }}</td>
          <td>{{ u.identificacao }}</td>
          <td>{{ u.perfil }}</td>
          <td>{{ u.status === -1 ? 'Ativo' : 'Inativo' }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })
const api = useApi()
const users = ref<Array<{ codusuario: number; nome: string; identificacao: string; perfil: string; status: number }>>([])

onMounted(async () => {
  const res = await api.get<{ data: typeof users.value }>('/api/web/users')
  users.value = res.data || []
})
</script>

