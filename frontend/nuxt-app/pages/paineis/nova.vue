<template>
  <div>
    <DsPageHeader title="Novo Painel" subtitle="Cadastre um painel Power BI ou API" icon="plus-circle" />
    <DsPageShell>
      <div v-if="!tipoEscolhido" class="grid grid-cols-1 sm:grid-cols-2 gap-6 max-w-3xl mx-auto">
        <DsHubCard
          title="Power BI"
          desc="Painel com arquivo PBIX e pacotes"
          icon="graph-up"
          theme-name="green"
          @click="tipoEscolhido = 'POWERBI'"
        />
        <DsHubCard
          title="API"
          desc="Painel baseado em endpoint/JSON"
          icon="braces"
          theme-name="orange"
          @click="tipoEscolhido = 'API'"
        />
      </div>

      <template v-else>
        <div class="mb-4">
          <DsButton variant="secondary" size="sm" icon="arrow-left" @click="tipoEscolhido = ''">Trocar tipo</DsButton>
          <span class="ml-3 text-sm text-gray-600">Tipo: <strong>{{ tipoEscolhido }}</strong></span>
        </div>
        <PainelForm
          v-model:form="form"
          :tipo-painel="tipoEscolhido"
          :clientes="clientes"
          :modulos="modulos"
          :pacotes="pacotes"
          @submit="onSubmit"
        >
          <template #actions>
            <DsButton variant="secondary" to="/paineis">Cancelar</DsButton>
            <DsButton type="submit" icon="check-circle" :loading="saving" :disabled="saving">Salvar</DsButton>
          </template>
        </PainelForm>
      </template>
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

const saving = ref(false)
const tipoEscolhido = ref('')
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
  fd.append('tipoPainel', tipoEscolhido.value)
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

async function onSubmit() {
  if (!form.codModulo) {
    await swal.toast('Selecione o módulo.', 'error')
    return
  }
  saving.value = true
  try {
    const res = await api.createPainel(buildFormData())
    await swal.toast(res.message || 'Painel criado.')
    await router.push(`/paineis/${res.codPainel}`)
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao criar painel', 'error')
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  const q = String(route.query.tipo || '').toLowerCase()
  if (q === 'powerbi') tipoEscolhido.value = 'POWERBI'
  if (q === 'api') tipoEscolhido.value = 'API'
  const [c, m, p] = await Promise.all([api.listClientes(), api.listModulos(), api.listPacotes()])
  clientes.value = c.data || []
  modulos.value = m.data || []
  pacotes.value = p.data || []
})
</script>
