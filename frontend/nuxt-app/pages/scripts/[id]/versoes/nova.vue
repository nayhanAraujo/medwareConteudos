<template>
  <div>
    <DsPageHeader :title="pageTitle" icon="plus-circle" />
    <DsPageShell panel-class="!p-6 md:!p-8">
      <p v-if="meta" class="text-sm text-gray-600 mb-4">
        {{ meta.nomeScript }} — {{ meta.sistema }}
        <span v-if="meta.nomePacote"> · {{ meta.nomePacote }}</span>
      </p>
      <div v-if="loadingMeta" class="text-gray-500 py-8 text-center">Carregando...</div>
      <ScriptVersaoForm
        v-else-if="meta"
        v-model="form"
        mode="create"
        :sistema="meta.sistema"
        :linguagem="meta.linguagem"
        :loading="saving"
        submit-label="Criar versão"
        @files="formFiles = $event"
        @submit="salvar"
        @cancel="router.push(`/scripts/${id}/versoes`)"
      />
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { VersaoCreateMetaDto, VersaoFormFiles } from '~/composables/useScriptsApi'
import type { VersaoFormModel } from '~/components/scripts/ScriptVersaoForm.vue'

definePageMeta({ layout: 'default', middleware: ['admin'] })

const route = useRoute()
const router = useRouter()
const id = Number(route.params.id)
const auth = useAuthStore()
const scriptsApi = useScriptsApi()
const swal = useSwal()

const meta = ref<VersaoCreateMetaDto | null>(null)
const loadingMeta = ref(true)
const saving = ref(false)
const formFiles = ref<VersaoFormFiles>({})

const form = ref<VersaoFormModel>({
  numeroVersao: '',
  descricaoAlteracoes: '',
  alteracoesInterface: '',
  alteracoesCodigo: '',
  observacoes: ''
})

const pageTitle = computed(() => (meta.value ? `Nova versão — ${meta.value.nomeScript}` : 'Nova Versão'))

onMounted(async () => {
  try {
    const res = await scriptsApi.getVersaoCreateMeta(id)
    meta.value = res.data
    form.value.numeroVersao = res.data.proximaVersao
  } catch (e: unknown) {
    await swal.error('Erro', e instanceof Error ? e.message : 'Não foi possível carregar dados.')
    await router.push(`/scripts/${id}/versoes`)
  } finally {
    loadingMeta.value = false
  }
})

async function salvar() {
  if (!form.value.descricaoAlteracoes.trim()) {
    await swal.warning('Validação', 'Descrição das alterações é obrigatória.')
    return
  }
  saving.value = true
  try {
    const res = await scriptsApi.createVersao(
      id,
      {
        numeroVersao: form.value.numeroVersao,
        descricaoAlteracoes: form.value.descricaoAlteracoes,
        alteracoesInterface: form.value.alteracoesInterface,
        alteracoesCodigo: form.value.alteracoesCodigo,
        observacoes: form.value.observacoes,
        usuarioResponsavel: auth.user?.nome
      },
      formFiles.value
    )
    await swal.toast('Versão criada!')
    await router.push(`/scripts/${id}/versoes/${res.codVersao}`)
  } catch (e: unknown) {
    await swal.error('Erro', e instanceof Error ? e.message : 'Erro ao criar versão')
  } finally {
    saving.value = false
  }
}
</script>
