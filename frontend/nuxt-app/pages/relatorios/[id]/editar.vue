<template>
  <div>
    <DsPageHeader title="Editar Relatório" subtitle="Atualize metadados e conteúdo" icon="pencil-square" />
    <DsPageShell>
      <div v-if="loading" class="text-center py-8 text-gray-500">Carregando...</div>
      <DsAlert v-else-if="errorMsg" variant="error">{{ errorMsg }}</DsAlert>
      <RelatoriosRelatorioForm v-else v-model="form" :modulos="modulos" @submit="onSubmit">
        <template #actions>
          <DsButton variant="secondary" icon="arrow-left" to="/relatorios?view=lista">Voltar</DsButton>
          <DsButton variant="secondary" icon="sliders" :to="`/relatorios/${id}/configuracoes`">Validações e filtros</DsButton>
          <DsButton type="submit" icon="check-circle" :loading="saving" :disabled="saving">Salvar</DsButton>
        </template>
      </RelatoriosRelatorioForm>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

const route = useRoute()
const router = useRouter()
const api = useRelatoriosApi()
const swal = useSwal()
const auth = useAuthStore()

const id = computed(() => Number(route.params.id))
const loading = ref(true)
const saving = ref(false)
const errorMsg = ref('')
const modulos = ref<{ nome: string; descricao?: string }[]>([])
const form = ref({
  nome: '',
  modulo: '',
  formato: 'XML',
  conteudo: '',
  ativo: '1' as number | string
})

async function load() {
  if (!auth.isAdmin) {
    errorMsg.value = 'Apenas administradores podem editar relatórios.'
    loading.value = false
    return
  }
  loading.value = true
  errorMsg.value = ''
  try {
    const [detail, mods] = await Promise.all([
      api.getRelatorio(id.value),
      api.listModulosSimples()
    ])
    modulos.value = mods.modulos || []
    const d = detail.data
    form.value = {
      nome: d.nome,
      modulo: d.modulo,
      formato: d.formato,
      conteudo: d.conteudo,
      ativo: String(d.ativo)
    }
  } catch (err) {
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar relatório.'
  } finally {
    loading.value = false
  }
}

async function onSubmit() {
  saving.value = true
  try {
    await api.updateRelatorio(id.value, {
      nome: form.value.nome.trim(),
      modulo: form.value.modulo,
      formato: String(form.value.formato).toUpperCase(),
      conteudo: form.value.conteudo,
      ativo: Number(form.value.ativo)
    })
    await swal.toast('Relatório atualizado.')
    await router.push({ path: '/relatorios', query: { view: 'lista', modulo: form.value.modulo } })
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao atualizar', 'error')
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>
