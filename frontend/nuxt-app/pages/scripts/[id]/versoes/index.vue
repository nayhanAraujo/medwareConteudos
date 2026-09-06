<template>
  <div>
    <DsPageHeader :title="pageTitle" icon="layers" />
    <DsPageShell>
      <div class="flex flex-wrap gap-2 mb-4">
        <DsButton variant="success" size="sm" icon="plus-circle" :to="`/scripts/${id}/versoes/nova`">Nova Versão</DsButton>
        <DsButton variant="secondary" size="sm" icon="arrow-left-right" :to="`/scripts/${id}/comparar-versoes`">Comparar versões</DsButton>
        <DsButton variant="secondary" size="sm" :to="voltarPath">Voltar</DsButton>
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-3 gap-3 mb-6">
        <DsInput v-model="filtroNumero" label="Nº versão" placeholder="Ex: V1" />
        <DsSelect v-model="filtroAprovado" label="Aprovação">
          <option value="">Todas</option>
          <option value="1">Aprovadas</option>
          <option value="0">Pendentes</option>
        </DsSelect>
        <DsSelect v-model="filtroAtivo" label="Status">
          <option value="">Todas</option>
          <option value="1">Ativas</option>
          <option value="0">Inativas</option>
        </DsSelect>
      </div>
      <DsButton variant="secondary" size="sm" class="mb-4" @click="carregar">Filtrar</DsButton>

      <DsTable>
        <template #head>
          <tr>
            <th>Versão</th>
            <th>Descrição</th>
            <th>Data</th>
            <th>Aprovação</th>
            <th>Ativa</th>
            <th>Ações</th>
          </tr>
        </template>
        <tr v-for="v in versoes" :key="v.codVersao">
          <td class="font-medium">{{ v.numeroVersao }}</td>
          <td class="max-w-xs truncate text-sm text-gray-600">{{ v.descricaoAlteracoes || '—' }}</td>
          <td class="text-sm">{{ formatDate(v.dataCriacao) }}</td>
          <td>
            <DsBadge :variant="v.aprovado === 'T' ? 'primary' : 'warning'">
              {{ v.aprovado === 'T' ? 'Aprovada' : 'Pendente' }}
            </DsBadge>
          </td>
          <td>
            <DsBadge v-if="v.ativo === 'T'" variant="success">Ativa</DsBadge>
            <DsButton v-else variant="secondary" size="sm" @click="ativar(v.codVersao)">Ativar</DsButton>
          </td>
          <td>
            <div class="flex flex-wrap gap-1">
              <DsButton variant="ghost" size="sm" :to="`/scripts/${id}/versoes/${v.codVersao}`">Detalhes</DsButton>
              <DsButton variant="ghost" size="sm" :to="`/scripts/${id}/versoes/${v.codVersao}/editar`">Editar</DsButton>
              <DsButton
                v-if="v.aprovado !== 'T'"
                variant="ghost"
                size="sm"
                @click="aprovar(v.codVersao)"
              >
                Aprovar
              </DsButton>
            </div>
          </td>
        </tr>
      </DsTable>
      <p v-if="!versoes.length && !loading" class="text-center text-gray-500 py-6 text-sm">Nenhuma versão encontrada.</p>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { VersaoDto } from '~/composables/useScriptsApi'

definePageMeta({ layout: 'default' })

const route = useRoute()
const id = Number(route.params.id)
const scriptsApi = useScriptsApi()
const swal = useSwal()

const versoes = ref<VersaoDto[]>([])
const loading = ref(false)
const pageTitle = ref('Gerenciar Versões')
const voltarPath = ref('/scripts')
const filtroNumero = ref('')
const filtroAprovado = ref('')
const filtroAtivo = ref('')

function formatDate(d?: string) {
  if (!d) return '—'
  return new Date(d).toLocaleString('pt-BR')
}

async function carregar() {
  loading.value = true
  try {
    const res = await scriptsApi.listVersoes(id, {
      numeroVersao: filtroNumero.value || undefined,
      aprovado: filtroAprovado.value || undefined,
      ativo: filtroAtivo.value || undefined
    })
    versoes.value = res.data
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro ao listar', 'error')
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  try {
    const s = await scriptsApi.getScript(id)
    const d = s.data as Record<string, unknown>
    pageTitle.value = `Versões — ${d.nome}`
    voltarPath.value = `/scripts?sistema=${d.sistema}&pacote=${d.codPacote}`
  } catch {
    /* ignore */
  }
  await carregar()
})

async function ativar(codVersao: number) {
  try {
    await scriptsApi.ativarVersao(codVersao)
    await swal.toast('Versão ativada!')
    await carregar()
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  }
}

async function aprovar(codVersao: number) {
  const { isConfirmed } = (await swal.confirm('Aprovar versão?', 'Confirma a aprovação desta versão?')) || {}
  if (!isConfirmed) return
  try {
    await scriptsApi.aprovarVersao(codVersao)
    await swal.toast('Versão aprovada!')
    await carregar()
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  }
}
</script>
