<template>
  <div>
    <LayoutAppPageHeader title="Gerenciar Versões" icon="layers" />
    <div class="d-flex gap-2 mb-3">
      <NuxtLink class="btn btn-success btn-sm" :to="`/scripts/${id}/versoes/nova`">
        <i class="bi bi-plus-circle" /> Nova Versão
      </NuxtLink>
      <NuxtLink class="btn btn-outline-secondary btn-sm" :to="voltarPath">Voltar</NuxtLink>
    </div>
    <div class="card shadow-sm">
      <div class="table-responsive">
        <table class="table table-striped mb-0">
          <thead>
            <tr>
              <th>Versão</th>
              <th>Data</th>
              <th>Ativa</th>
              <th>Ações</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="v in versoes" :key="v.codVersao">
              <td>{{ v.numeroVersao }}</td>
              <td>{{ formatDate(v.dataCriacao) }}</td>
              <td>
                <span v-if="v.ativo === 'T'" class="badge bg-success">Ativa</span>
                <button v-else type="button" class="btn btn-sm btn-outline-primary" @click="ativar(v.codVersao)">Ativar</button>
              </td>
              <td>
                <NuxtLink class="btn btn-sm btn-outline-info" :to="`/scripts/${id}/versoes/${v.codVersao}`">Detalhes</NuxtLink>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
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
