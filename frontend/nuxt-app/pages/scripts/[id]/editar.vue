<template>
  <div>
    <DsPageHeader title="Editar Script" icon="pencil-fill" />
    <ScriptsPublicationStatus :script-id="id" />
    <DsPageShell panel-class="!p-6 md:!p-8">
      <div v-if="loadingData" class="text-gray-500 py-8 text-center">Carregando...</div>
      <ScriptsScriptForm
        v-else
        v-model="form"
        :pacotes="pacotes"
        :existing-imagens="existingImagens"
        edit-mode
        :loading="loading"
        @submit="salvar"
        @cancel="voltar"
        @files="(f) => (fileRefs = f)"
        @delete-existing-image="excluirImagem"
      />
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { ScriptFormModel, ScriptImagePreview } from '~/components/scripts/ScriptForm.vue'
import { buildScriptFormData } from '~/utils/scriptFormData'

definePageMeta({ layout: 'default' })

const route = useRoute()
const router = useRouter()
const id = Number(route.params.id)
const scriptsApi = useScriptsApi()
const swal = useSwal()

const loading = ref(false)
const loadingData = ref(true)
const pacotes = ref<{ codPacote: number; nome: string }[]>([])
const fileRefs = ref<Record<string, File | FileList | null>>({})
const existingImagens = ref<ScriptImagePreview[]>([])

const form = ref<ScriptFormModel>({
  nome: '',
  criado_por: '',
  codpacote: 0,
  sistema: '',
  linguagem: '',
  descricao: '',
  link_teste: '',
  caminho_azure: '',
  caminho_projeto: '',
  ativo: true,
  aprovado: false,
  aprovado_por: ''
})

onMounted(async () => {
  const [pacRes, scriptRes] = await Promise.all([scriptsApi.getPacotes(), scriptsApi.getScript(id)])
  pacotes.value = pacRes.data
  const s = scriptRes.data as Record<string, unknown>
  form.value = {
    nome: String(s.nome || ''),
    criado_por: String(s.criadoPor || ''),
    codpacote: Number(s.codPacote || 0),
    sistema: String(s.sistema || ''),
    linguagem: String(s.linguagem || ''),
    descricao: String(s.descricao || ''),
    link_teste: String(s.linkTeste || ''),
    caminho_azure: String(s.caminhoAzure || ''),
    caminho_projeto: String(s.caminhoProjeto || ''),
    ativo: Number(s.ativo) === 1,
    aprovado: Number(s.aprovado) === 1,
    aprovado_por: String(s.aprovadoPor || '')
  }
  const imgs = Array.isArray(s.imagens) ? (s.imagens as Array<Record<string, unknown>>) : []
  existingImagens.value = imgs
    .map((x) => ({ codArquivo: Number(x.codArquivo || 0), caminho: String(x.caminho || ''), nomeArquivo: String(x.nomeArquivo || '') }))
    .filter((x) => x.codArquivo > 0 && !!x.caminho)
  loadingData.value = false
})

async function salvar() {
  loading.value = true
  try {
    const fd = buildScriptFormData(form.value, fileRefs.value)
    await scriptsApi.updateScript(id, fd)
    await swal.toast('Script atualizado!')
    voltar()
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  } finally {
    loading.value = false
  }
}

async function excluirImagem(imagem: ScriptImagePreview) {
  const { isConfirmed } = (await swal.confirm('Excluir imagem?', `A imagem ${imagem.nomeArquivo} será removida permanentemente.`)) || {}
  if (!isConfirmed) return
  try {
    await scriptsApi.deleteScriptImage(imagem.codArquivo)
    existingImagens.value = existingImagens.value.filter((item) => item.codArquivo !== imagem.codArquivo)
    await swal.toast('Imagem excluída com sucesso.', 'success')
  } catch (error: unknown) {
    await swal.error('Erro ao excluir imagem', error instanceof Error ? error.message : 'Tente novamente.')
  }
}

function voltar() {
  router.push({
    path: '/scripts',
    query: { sistema: form.value.sistema, pacote: String(form.value.codpacote) }
  })
}
</script>
