<template>
  <div>
    <DsPageHeader
      :title="detail?.nome || 'Painel'"
      :subtitle="detail ? detail.tipo_painel : 'Detalhes'"
      icon="bar-chart-line"
    />
    <DsPageShell>
      <div class="flex flex-wrap gap-2 mb-6">
        <DsButton variant="secondary" size="sm" icon="arrow-left" to="/paineis?view=lista&tipo=todos">Voltar</DsButton>
        <DsButton v-if="detail" variant="secondary" size="sm" icon="pencil-square" :to="`/paineis/${id}/editar`">
          Editar
        </DsButton>
        <DsButton v-if="detail" variant="secondary" size="sm" icon="layers" :to="`/paineis/${id}/versoes`">
          Gerenciar versões
        </DsButton>
        <DsButton
          v-if="detail?.tem_arquivo_pbix"
          size="sm"
          icon="download"
          @click="baixar"
        >
          Download PBIX
        </DsButton>
      </div>

      <div v-if="loading" class="text-center py-8 text-gray-500">Carregando...</div>
      <DsAlert v-else-if="errorMsg" variant="error">{{ errorMsg }}</DsAlert>
      <div v-else-if="detail" class="space-y-6">
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
          <p><strong>Cliente:</strong> {{ detail.nome_cliente || 'Padrão' }}</p>
          <p><strong>Módulo:</strong> {{ detail.nome_modulo || '—' }}</p>
          <p><strong>Status:</strong> {{ detail.ativo ? 'Ativo' : 'Inativo' }}</p>
          <p><strong>PBIX:</strong> {{ detail.tem_arquivo_pbix ? (detail.nome_arquivo_pbix || 'Sim') : 'Não' }}</p>
          <p class="md:col-span-2"><strong>Descrição:</strong> {{ detail.descricao || '—' }}</p>
          <p class="md:col-span-2">
            <strong>Pacotes:</strong>
            {{ pacotesLabel }}
          </p>
        </div>

        <div>
          <DsSectionTitle title="Versões" :subtitle="`${detail.versoes?.length || 0} versão(ões)`" />
          <DsAlert v-if="!detail.versoes?.length" variant="info">Nenhuma versão cadastrada.</DsAlert>
          <DsTable v-else>
            <template #head>
              <tr>
                <th>#</th>
                <th>Número</th>
                <th>Data</th>
                <th>Publicado</th>
              </tr>
            </template>
            <tr
              v-for="v in detail.versoes"
              :key="v.codversaopainel"
              class="cursor-pointer hover:bg-gray-50"
              @click="navigateTo(`/paineis/${id}/versoes/${v.codversaopainel}`)"
            >
              <td>{{ v.codversaopainel }}</td>
              <td>{{ v.numeroversao }}</td>
              <td>{{ v.datacriacao || '—' }}</td>
              <td>{{ v.publicado ? 'Sim' : 'Não' }}</td>
            </tr>
          </DsTable>
        </div>
      </div>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { PainelDetail } from '~/composables/usePaineisApi'

definePageMeta({ layout: 'default' })

const route = useRoute()
const api = usePaineisApi()
const swal = useSwal()

const id = computed(() => Number(route.params.id))
const loading = ref(true)
const errorMsg = ref('')
const detail = ref<PainelDetail | null>(null)

const pacotesLabel = computed(() => {
  const p = detail.value?.pacotes
  if (!p?.length) return '—'
  if (typeof p[0] === 'string') return (p as string[]).join(', ')
  return (p as { nome: string }[]).map((x) => x.nome).join(', ')
})

async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    const res = await api.getPainel(id.value)
    detail.value = res.data
  } catch (err) {
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar painel.'
  } finally {
    loading.value = false
  }
}

async function baixar() {
  if (!detail.value) return
  try {
    await api.downloadPbix(
      detail.value.codpainel,
      detail.value.nome_arquivo_pbix || `${detail.value.nome}.pbix`
    )
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro no download', 'error')
  }
}

onMounted(load)
</script>
