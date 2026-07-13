<template>
  <div>
    <DsPageHeader title="Novo Relatório" subtitle="Cadastre um relatório XML ou JSON" icon="file-earmark-plus" />
    <DsPageShell>
      <div v-if="!auth.isAdmin" class="text-center py-10">
        <DsAlert variant="error">Apenas administradores podem cadastrar relatórios.</DsAlert>
        <div class="mt-4">
          <DsButton variant="secondary" to="/relatorios">Voltar</DsButton>
        </div>
      </div>
      <RelatoriosRelatorioForm v-else v-model="form" :modulos="modulos" @submit="onSubmit">
        <template #actions>
          <DsButton variant="secondary" icon="arrow-left" to="/relatorios?view=lista">Voltar</DsButton>
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

const saving = ref(false)
const modulos = ref<{ nome: string; descricao?: string }[]>([])
const form = ref({
  nome: '',
  modulo: typeof route.query.modulo === 'string' ? route.query.modulo : '',
  formato: 'XML',
  conteudo: '',
  ativo: '1' as number | string
})

async function onSubmit() {
  if (!form.value.modulo?.trim()) {
    await swal.toast('Selecione o módulo.', 'error')
    return
  }
  if (!form.value.conteudo?.trim()) {
    await swal.toast('Informe o conteúdo do relatório.', 'error')
    return
  }
  saving.value = true
  try {
    const res = await api.createRelatorio({
      nome: form.value.nome.trim(),
      modulo: form.value.modulo,
      formato: String(form.value.formato).toUpperCase(),
      conteudo: form.value.conteudo,
      ativo: Number(form.value.ativo)
    })
    await swal.toast('Relatório cadastrado com sucesso.')
    await router.push({
      path: '/relatorios',
      query: { view: 'lista', modulo: form.value.modulo }
    })
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao cadastrar', 'error')
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  try {
    const res = await api.listModulosSimples()
    modulos.value = res.modulos || []
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao carregar módulos', 'error')
  }
})
</script>
