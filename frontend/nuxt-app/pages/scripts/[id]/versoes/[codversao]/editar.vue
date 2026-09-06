<template>
  <div>
    <DsPageHeader :title="pageTitle" icon="pencil" />
    <DsPageShell panel-class="!p-6 md:!p-8">
      <div v-if="loading" class="text-gray-500 py-8 text-center">Carregando...</div>
      <ScriptVersaoForm
        v-else-if="versao"
        v-model="form"
        mode="edit"
        :sistema="versao.sistema"
        :linguagem="versao.linguagem"
        :existing-mrd="versao.mrdList"
        :existing-imagens="versao.imagens"
        :existing-pdfs="versao.pdfs"
        :loading="saving"
        submit-label="Salvar alterações"
        @files="formFiles = $event"
        @download-anexo="baixarAnexo"
        @delete-anexo="excluirAnexo"
        @submit="salvar"
        @cancel="router.push(`/scripts/${id}/versoes/${codversao}`)"
      />
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { ScriptVersionDetailDto, ScriptVersionFileDto, VersaoFormFiles } from '~/composables/useScriptsApi'
import type { VersaoFormModel } from '~/components/scripts/ScriptVersaoForm.vue'

definePageMeta({ layout: 'default' })

const route = useRoute()
const router = useRouter()
const id = Number(route.params.id)
const codversao = Number(route.params.codversao)
const scriptsApi = useScriptsApi()
const swal = useSwal()

const versao = ref<ScriptVersionDetailDto | null>(null)
const loading = ref(true)
const saving = ref(false)
const formFiles = ref<VersaoFormFiles>({})

const form = ref<VersaoFormModel>({
  numeroVersao: '',
  descricaoAlteracoes: '',
  alteracoesInterface: '',
  alteracoesCodigo: '',
  observacoes: ''
})

const pageTitle = computed(() =>
  versao.value ? `Editar ${versao.value.numeroVersao}` : `Editar versão ${codversao}`
)

onMounted(async () => {
  try {
    const res = await scriptsApi.getVersao(id, codversao)
    versao.value = res.data
    form.value = {
      numeroVersao: res.data.numeroVersao,
      descricaoAlteracoes: res.data.descricaoAlteracoes || '',
      alteracoesInterface: res.data.alteracoesInterface || '',
      alteracoesCodigo: res.data.alteracoesCodigo || '',
      observacoes: res.data.observacoes || ''
    }
  } catch (e: unknown) {
    await swal.error('Erro', e instanceof Error ? e.message : 'Versão não encontrada')
    await router.push(`/scripts/${id}/versoes`)
  } finally {
    loading.value = false
  }
})

async function salvar() {
  if (!form.value.descricaoAlteracoes.trim()) {
    await swal.warning('Validação', 'Descrição das alterações é obrigatória.')
    return
  }
  saving.value = true
  try {
    await scriptsApi.updateVersao(
      codversao,
      {
        descricaoAlteracoes: form.value.descricaoAlteracoes,
        alteracoesInterface: form.value.alteracoesInterface,
        alteracoesCodigo: form.value.alteracoesCodigo,
        observacoes: form.value.observacoes
      },
      formFiles.value
    )
    await swal.toast('Versão atualizada!')
    await router.push(`/scripts/${id}/versoes/${codversao}`)
  } catch (e: unknown) {
    await swal.error('Erro', e instanceof Error ? e.message : 'Erro ao salvar')
  } finally {
    saving.value = false
  }
}

function baixarAnexo(arquivo: ScriptVersionFileDto) {
  scriptsApi.downloadVersaoAnexo(arquivo.codArquivo, arquivo.nomeArquivo).catch((e: unknown) =>
    swal.toast(e instanceof Error ? e.message : 'Erro no download', 'error')
  )
}

async function excluirAnexo(codArquivo: number) {
  const { isConfirmed } = (await swal.confirm('Excluir anexo?', 'Esta ação não pode ser desfeita.')) || {}
  if (!isConfirmed) return
  try {
    await scriptsApi.excluirVersaoAnexo(codArquivo)
    await swal.toast('Anexo excluído')
    const res = await scriptsApi.getVersao(id, codversao)
    versao.value = res.data
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  }
}
</script>
