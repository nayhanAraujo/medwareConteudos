<template>
  <div>
    <DsPageHeader
      :title="`Editar Variável${form.nome ? ` - ${form.nome}` : ''}`"
      subtitle="Atualize as informações básicas da variável clínica"
      icon="pencil-square"
    />
    <DsPageShell>
      <div v-if="loading" class="text-center py-8 text-gray-500">Carregando variável...</div>
      <DsAlert v-else-if="errorMsg" variant="error">{{ errorMsg }}</DsAlert>
      <template v-else>
        <DsTabs v-model="activeTab" :tabs="tabs" />

        <form v-if="activeTab === 'informacoes'" class="space-y-4" @submit.prevent="onSubmit">
          <DsSectionTitle title="Informações da Variável" />

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <DsInput v-model="form.nome" label="Nome Clínico" required />
            <DsInput
              v-model="form.variavel"
              label="Variável"
              placeholder="Ex: VR_AE"
              hint="Deve começar com VR_"
              required
            />
          </div>

          <div>
            <label class="block text-sm font-medium text-ds-text mb-1.5">Variáveis Alternativas</label>
            <div class="space-y-2">
              <DsInput
                v-for="(_, idx) in form.alternativas"
                :key="idx"
                v-model="form.alternativas[idx]"
                placeholder="Ex: VR_AO1"
              />
            </div>
            <DsButton variant="secondary" size="sm" icon="plus-circle" class="mt-2" type="button" @click="addAlternativa">
              Adicionar outra
            </DsButton>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <DsInput v-model="form.sigla" label="Sigla" required />
            <DsInput v-model="form.abreviacao" label="Abreviação" required />
          </div>

          <DsTextarea v-model="form.descricao" label="Descrição" :rows="3" />

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <DsSelect v-model="form.codUnidadeMedida" label="Unidade de Medida" required>
              <option value="">Selecione...</option>
              <option v-for="u in unidades" :key="u.codUnidadeMedida" :value="String(u.codUnidadeMedida)">
                {{ u.descricao }}
              </option>
            </DsSelect>
            <DsInput
              v-model="form.casasDecimais"
              label="Casas Decimais"
              type="number"
              input-class="max-w-xs"
              required
            />
          </div>

          <div class="flex flex-wrap justify-end gap-2 pt-4 border-t border-gray-100">
            <DsButton variant="secondary" icon="arrow-left" to="/variaveis">Voltar</DsButton>
            <DsButton type="submit" icon="check-circle" :loading="saving" :disabled="saving">Salvar Alterações</DsButton>
          </div>
        </form>

        <DsAlert v-else-if="activeTab === 'formulas'" variant="info">
          Fórmulas podem ser configuradas na edição avançada (legado) enquanto esta aba é migrada.
        </DsAlert>
        <DsAlert v-else-if="activeTab === 'normalidades'" variant="info">
          Normalidades simples podem ser gerenciadas pelo botão de normalidades na listagem de variáveis.
        </DsAlert>
        <DsAlert v-else variant="info">
          Normalidades por classificações ainda estão disponíveis na edição avançada (legado).
        </DsAlert>
      </template>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { UnidadeMedidaDto } from '~/composables/useVariaveisApi'

definePageMeta({ layout: 'default' })

const route = useRoute()
const auth = useAuthStore()
const swal = useSwal()
const router = useRouter()
const variaveisApi = useVariaveisApi()

const codVariavel = computed(() => Number(route.params.id))
const loading = ref(true)
const saving = ref(false)
const errorMsg = ref('')
const unidades = ref<UnidadeMedidaDto[]>([])
const activeTab = ref('informacoes')

const tabs = [
  { id: 'informacoes', label: 'Informações da Variável', icon: 'info-circle' },
  { id: 'formulas', label: 'Fórmulas', icon: 'calculator' },
  { id: 'normalidades', label: 'Normalidades Simples', icon: 'graph-up' },
  { id: 'classificacoes', label: 'Normalidades por Classificações', icon: 'diagram-3' }
]

const form = reactive({
  nome: '',
  variavel: '',
  sigla: '',
  abreviacao: '',
  descricao: '',
  codUnidadeMedida: '',
  casasDecimais: '0',
  alternativas: ['']
})

function addAlternativa() {
  form.alternativas.push('')
}

function buildPayload() {
  return {
    nome: form.nome.trim(),
    variavel: form.variavel.trim(),
    sigla: form.sigla.trim().toUpperCase(),
    abreviacao: form.abreviacao.trim(),
    descricao: form.descricao.trim() || undefined,
    codUnidadeMedida: Number(form.codUnidadeMedida),
    casasDecimais: Number(form.casasDecimais),
    alternativas: form.alternativas.map((a) => a.trim()).filter(Boolean)
  }
}

async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    const [metaRes, varRes] = await Promise.all([
      variaveisApi.getMeta(),
      variaveisApi.getVariavelEdicao(codVariavel.value)
    ])
    unidades.value = metaRes.data?.unidades || []
    const v = varRes.data
    form.nome = v.nome || ''
    form.variavel = v.variavel || ''
    form.sigla = v.sigla || ''
    form.abreviacao = v.abreviacao || ''
    form.descricao = v.descricao || ''
    form.codUnidadeMedida = v.codUnidadeMedida ? String(v.codUnidadeMedida) : ''
    form.casasDecimais = v.casasDecimais != null ? String(v.casasDecimais) : '0'
    form.alternativas = v.alternativas?.length ? [...v.alternativas] : ['']
  } catch (err) {
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar variável.'
  } finally {
    loading.value = false
  }
}

async function onSubmit() {
  if (!auth.isAdmin) {
    await swal.warning('Acesso negado', 'Apenas administradores podem editar variáveis.')
    return
  }

  saving.value = true
  try {
    await variaveisApi.updateVariavel(codVariavel.value, buildPayload())
    await swal.toast('Variável atualizada com sucesso.')
    await router.push('/variaveis')
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao atualizar variável.', 'error')
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  if (!auth.isAdmin) {
    await swal.warning('Acesso negado', 'Apenas administradores podem editar variáveis.')
    await router.push('/variaveis')
    return
  }
  if (!Number.isFinite(codVariavel.value) || codVariavel.value <= 0) {
    errorMsg.value = 'Código da variável inválido.'
    loading.value = false
    return
  }
  await load()
})
</script>
