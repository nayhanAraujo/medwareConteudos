<template>
  <div>
    <DsPageHeader title="Usuários" icon="people" />
    <DsPageShell>
      <DsTable>
        <template #head>
          <tr>
            <th>Nome</th>
            <th>Login</th>
            <th>Perfil</th>
            <th>Status</th>
          </tr>
        </template>
        <tr v-for="u in users" :key="u.codusuario">
          <td>{{ u.nome }}</td>
          <td>{{ u.identificacao }}</td>
          <td>{{ u.perfil }}</td>
          <td>
            <DsBadge :variant="u.status === -1 ? 'success' : 'default'">
              {{ u.status === -1 ? 'Ativo' : 'Inativo' }}
            </DsBadge>
          </td>
        </tr>
      </DsTable>
    </DsPageShell>
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
