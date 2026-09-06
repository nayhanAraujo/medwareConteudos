<template>
  <div>
    <DsPageHeader title="Cadastrar Novo Script" icon="file-earmark-plus-fill" />
    <DsPageShell panel-class="!p-6 md:!p-8">
      <ScriptsScriptForm
        v-model="form"
        :pacotes="pacotes"
        :loading="loading"
        @submit="salvar"
        @cancel="voltar"
        @files="(f) => (fileRefs = f)"
      />
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { ScriptFormModel } from '~/components/scripts/ScriptForm.vue'
import { buildScriptFormData } from '~/utils/scriptFormData'

definePageMeta({ layout: 'default' })

const route = useRoute()
const router = useRouter()
const scriptsApi = useScriptsApi()
const swal = useSwal()
const auth = useAuthStore()

const loading = ref(false)
const pacotes = ref<{ codPacote: number; nome: string }[]>([])
const fileRefs = ref<Record<string, File | FileList | null>>({})

const form = ref<ScriptFormModel>({
  nome: '',
  criado_por: auth.user?.nome || '',
  codpacote: Number(route.query.pacote) || 0,
  sistema: String(route.query.sistema || ''),
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
  const res = await scriptsApi.getPacotes()
  pacotes.value = res.data
})

async function salvar() {
  loading.value = true
  try {
    const exists = await scriptsApi.verificarNome(form.value.nome)
    if (exists.exists) {
      await swal.toast('Já existe um script com este nome.', 'warning')
      return
    }
    const fd = buildScriptFormData(form.value, fileRefs.value)
    await scriptsApi.createScript(fd)
    await swal.toast('Script criado com sucesso!')
    await router.push({
      path: '/scripts',
      query: { sistema: form.value.sistema, pacote: String(form.value.codpacote) }
    })
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro ao salvar', 'error')
  } finally {
    loading.value = false
  }
}

function voltar() {
  router.back()
}
</script>
