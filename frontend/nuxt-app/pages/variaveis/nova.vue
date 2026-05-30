<template>
  <div>
    <DsPageHeader
      title="Cadastrar Nova Variável"
      subtitle="Informações básicas da variável clínica"
      icon="input-cursor-text"
    />
    <DsPageShell>
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
          <DsButton type="submit" icon="check-circle" :loading="saving" :disabled="saving">Salvar Variável</DsButton>
        </div>
      </form>

      <DsAlert v-else-if="activeTab === 'formulas'" variant="info">
        Fórmulas, normalidades simples e classificações podem ser configuradas após o cadastro, na edição da variável.
      </DsAlert>
      <DsAlert v-else-if="activeTab === 'normalidades'" variant="info">
        Normalidades simples podem ser cadastradas após salvar a variável, na tela de edição.
      </DsAlert>
      <DsAlert v-else variant="info">
        Normalidades por classificações podem ser cadastradas após salvar a variável, na tela de edição.
      </DsAlert>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { UnidadeMedidaDto } from '~/composables/useVariaveisApi'

definePageMeta({ layout: 'default' })

const auth = useAuthStore()
const swal = useSwal()
const router = useRouter()
const variaveisApi = useVariaveisApi()

const saving = ref(false)
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

async function loadMeta() {
  const res = await variaveisApi.getMeta()
  unidades.value = res.data?.unidades || []
}

async function onSubmit() {
  if (!auth.isAdmin) {
    await swal.warning('Acesso negado', 'Apenas administradores podem cadastrar variáveis.')
    return
  }

  saving.value = true
  try {
    await variaveisApi.createVariavel(buildPayload())
    await swal.toast('Variável cadastrada com sucesso.')
    await router.push('/variaveis')
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao cadastrar variável.', 'error')
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  if (!auth.isAdmin) {
    await swal.warning('Acesso negado', 'Apenas administradores podem cadastrar variáveis.')
    await router.push('/variaveis')
    return
  }
  try {
    await loadMeta()
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao carregar formulário.', 'error')
  }
})
</script>
