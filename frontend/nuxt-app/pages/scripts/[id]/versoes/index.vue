<template>
  <div>
    <DsPageHeader title="Gerenciar Versões" icon="layers" />
    <DsPageShell>
      <div class="flex gap-2 mb-6">
        <DsButton variant="success" size="sm" icon="plus-circle" :to="`/scripts/${id}/versoes/nova`">Nova Versão</DsButton>
        <DsButton variant="secondary" size="sm" :to="voltarPath">Voltar</DsButton>
      </div>
      <DsTable>
        <template #head>
          <tr>
            <th>Versão</th>
            <th>Data</th>
            <th>Ativa</th>
            <th>Ações</th>
          </tr>
        </template>
        <tr v-for="v in versoes" :key="v.codVersao">
          <td>{{ v.numeroVersao }}</td>
          <td>{{ formatDate(v.dataCriacao) }}</td>
          <td>
            <DsBadge v-if="v.ativo === 'T'" variant="success">Ativa</DsBadge>
            <DsButton v-else variant="secondary" size="sm" @click="ativar(v.codVersao)">Ativar</DsButton>
          </td>
          <td>
            <DsButton variant="ghost" size="sm" :to="`/scripts/${id}/versoes/${v.codVersao}`">Detalhes</DsButton>
          </td>
        </tr>
      </DsTable>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default', middleware: ['admin'] })

const route = useRoute()
const id = Number(route.params.id)
const scriptsApi = useScriptsApi()
const swal = useSwal()

const versoes = ref<{ codVersao: number; numeroVersao: string; dataCriacao?: string; ativo?: string }[]>([])
const voltarPath = ref('/scripts')

function formatDate(d?: string) {
  if (!d) return '—'
  return new Date(d).toLocaleString('pt-BR')
}

onMounted(async () => {
  const res = await scriptsApi.listVersoes(id)
  versoes.value = res.data
  const s = await scriptsApi.getScript(id)
  const d = s.data as Record<string, unknown>
  voltarPath.value = `/scripts?sistema=${d.sistema}&pacote=${d.codPacote}`
})

async function ativar(codVersao: number) {
  await scriptsApi.ativarVersao(codVersao)
  await swal.toast('Versão ativada!')
  const res = await scriptsApi.listVersoes(id)
  versoes.value = res.data
}
</script>
