<template>
  <div>
    <DsPageHeader title="Editar Referência" subtitle="Atualize as informações da referência" icon="pencil-square" />
    <DsPageShell>
      <div v-if="loading" class="text-center py-8 text-gray-500">Carregando referência...</div>
      <DsAlert v-else-if="errorMsg" variant="error">{{ errorMsg }}</DsAlert>
      <ReferenciaForm v-else :form="form" :especialidades="especialidades" :tipos="tipos" @submit="onSubmit">
        <template #actions>
          <DsButton variant="secondary" icon="arrow-left" to="/referencias">Voltar</DsButton>
          <DsButton variant="secondary" icon="paperclip" :to="`/referencias/${id}/anexos`">Anexos</DsButton>
          <DsButton type="submit" icon="check-circle" :loading="saving" :disabled="saving">Salvar Alterações</DsButton>
        </template>
      </ReferenciaForm>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

const route = useRoute()
const id = computed(() => Number(route.params.id))
const referenciasApi = useReferenciasApi()
const swal = useSwal()
const router = useRouter()

const loading = ref(true)
const saving = ref(false)
const errorMsg = ref('')
const especialidades = ref<Array<{ codEspecialidade: number; nome: string }>>([])
const tipos = ref<Array<{ codTipoRef: number; descricao: string }>>([])
const form = reactive({
  titulo: '',
  ano: '',
  descricao: '',
  doi: '',
  isbn: '',
  volume: '',
  paginas: '',
  codEspecialidade: '',
  codTipoRef: ''
})

function buildPayload() {
  return {
    titulo: form.titulo.trim(),
    ano: Number(form.ano),
    descricao: form.descricao.trim() || undefined,
    doi: form.doi.trim() || undefined,
    isbn: form.isbn.trim() || undefined,
    volume: form.volume.trim() || undefined,
    paginas: form.paginas.trim() || undefined,
    codEspecialidade: form.codEspecialidade ? Number(form.codEspecialidade) : null,
    codTipoRef: form.codTipoRef ? Number(form.codTipoRef) : null
  }
}

async function loadAll() {
  loading.value = true
  errorMsg.value = ''
  try {
    const [meta, referencia] = await Promise.all([referenciasApi.getMeta(), referenciasApi.getReferencia(id.value)])
    especialidades.value = meta.especialidades || []
    tipos.value = meta.tipos || []
    const r = referencia.data
    form.titulo = r.titulo || ''
    form.ano = r.ano ? String(r.ano) : ''
    form.descricao = r.descricao || ''
    form.doi = r.doi || ''
    form.isbn = r.isbn || ''
    form.volume = r.volume || ''
    form.paginas = r.paginas || ''
    form.codEspecialidade = r.codEspecialidade ? String(r.codEspecialidade) : ''
    form.codTipoRef = r.codTipoRef ? String(r.codTipoRef) : ''
  } catch (err) {
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar referência.'
  } finally {
    loading.value = false
  }
}

async function onSubmit() {
  saving.value = true
  try {
    await referenciasApi.updateReferencia(id.value, buildPayload())
    await swal.toast('Referência atualizada com sucesso.')
    await router.push(`/referencias/${id.value}/anexos`)
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao atualizar referência', 'error')
  } finally {
    saving.value = false
  }
}

onMounted(loadAll)
</script>
