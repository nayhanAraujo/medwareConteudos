<template>
  <div>
    <DsPageHeader title="Editar Painel" subtitle="Atualize metadados e arquivo PBIX" icon="pencil-square" />
    <DsPageShell>
      <div v-if="loading" class="text-center py-8 text-gray-500">Carregando...</div>
      <DsAlert v-else-if="errorMsg" variant="error">{{ errorMsg }}</DsAlert>
      <PainelForm
        v-else
        v-model:form="form"
        :tipo-painel="tipoPainel"
        :clientes="clientes"
        :modulos="modulos"
        :pacotes="pacotes"
        :nome-pbix-atual="nomePbixAtual"
        :show-configuration="false"
        @submit="onSubmit"
      >
        <template #actions>
          <DsButton variant="secondary" :to="`/paineis/${id}`">Cancelar</DsButton>
          <DsButton type="submit" icon="check-circle" :loading="saving" :disabled="saving">Salvar</DsButton>
        </template>
      </PainelForm>
      <div v-if="!loading && !errorMsg && latestVersionId" class="mt-6 rounded-xl border border-gray-200 p-4">
        <p class="mb-3 text-sm text-gray-600">Configurações de API/Power BI e imagens são gerenciadas por versão.</p>
        <DsButton variant="secondary" icon="gear" :to="`/paineis/${id}/versoes/${latestVersionId}/editar`">Editar configurações da última versão</DsButton>
      </div>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import PainelForm from '~/components/paineis/PainelForm.vue'
definePageMeta({ layout: 'default' })

const route = useRoute()
const router = useRouter()
const api = usePaineisApi()
const swal = useSwal()

const id = computed(() => Number(route.params.id))
const loading = ref(true)
const saving = ref(false)
const errorMsg = ref('')
const tipoPainel = ref('POWERBI')
const nomePbixAtual = ref<string | null>(null)
const latestVersionId = ref<number | null>(null)
const clientes = ref<{ codCliente: number; nome: string }[]>([])
const modulos = ref<{ codModulo: number; nome: string }[]>([])
const pacotes = ref<{ codPacote: number; nome: string }[]>([])

const form = reactive({
  nome: '',
  codModulo: '' as string | number,
  codCliente: '' as string | number,
  ativo: '1' as string | number,
  descricao: '',
  responsavelApi: '',
  diretorioPbix: '',
  nomeArquivoPbixMeta: '',
  workspacePowerbi: '',
  responsavelAtualizacao: '',
  frequenciaAtualizacao: '',
  pacotes: [] as number[],
  arquivoPbix: null as File | null
})

function buildFormData() {
  const fd = new FormData()
  fd.append('nome', form.nome.trim())
  fd.append('codModulo', String(form.codModulo))
  fd.append('tipoPainel', tipoPainel.value)
  fd.append('descricao', form.descricao.trim())
  fd.append('ativo', String(form.ativo))
  if (form.codCliente) fd.append('codCliente', String(form.codCliente))
  fd.append('responsavelApi', form.responsavelApi)
  fd.append('diretorioPbix', form.diretorioPbix)
  fd.append('nomeArquivoPbixMeta', form.nomeArquivoPbixMeta)
  fd.append('workspacePowerbi', form.workspacePowerbi)
  fd.append('responsavelAtualizacao', form.responsavelAtualizacao)
  fd.append('frequenciaAtualizacao', form.frequenciaAtualizacao)
  form.pacotes.forEach((p) => fd.append('pacotes', String(p)))
  if (form.arquivoPbix) fd.append('arquivoPbix', form.arquivoPbix)
  return fd
}

async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    const [detail, c, m, p] = await Promise.all([
      api.getPainel(id.value),
      api.listClientes(),
      api.listModulos(),
      api.listPacotes()
    ])
    clientes.value = c.data || []
    modulos.value = m.data || []
    pacotes.value = p.data || []
    const d = detail.data
    latestVersionId.value = d.versoes?.[0]?.codversaopainel || null
    tipoPainel.value = d.tipo_painel
    nomePbixAtual.value = d.nome_arquivo_pbix || null
    form.nome = d.nome
    form.codModulo = d.codmodulo || ''
    form.codCliente = d.codcliente || ''
    form.ativo = d.ativo
    form.descricao = d.descricao || ''
    if (Array.isArray(d.pacotes) && d.pacotes.length && typeof d.pacotes[0] !== 'string') {
      form.pacotes = (d.pacotes as { codpacotecomercial: number }[]).map((x) => x.codpacotecomercial)
    }
  } catch (err) {
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar painel.'
  } finally {
    loading.value = false
  }
}

async function onSubmit() {
  saving.value = true
  try {
    await api.updatePainel(id.value, buildFormData())
    await swal.toast('Painel atualizado.')
    await router.push(`/paineis/${id.value}`)
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao atualizar', 'error')
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>
