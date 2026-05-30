<template>
  <div>
    <DsPageHeader title="Cadastrar Referência" subtitle="Preencha os dados básicos da referência" icon="journal-plus" />
    <DsPageShell>
      <ReferenciaForm :form="form" :especialidades="especialidades" :tipos="tipos" @submit="onSubmit">
        <template #actions>
          <DsButton variant="secondary" icon="arrow-left" to="/referencias">Voltar</DsButton>
          <DsButton type="submit" icon="check-circle" :loading="saving" :disabled="saving">Salvar Referência</DsButton>
        </template>
      </ReferenciaForm>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

const referenciasApi = useReferenciasApi()
const swal = useSwal()
const router = useRouter()

const saving = ref(false)
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

async function loadMeta() {
  const meta = await referenciasApi.getMeta()
  especialidades.value = meta.especialidades || []
  tipos.value = meta.tipos || []
}

async function onSubmit() {
  saving.value = true
  try {
    const res = await referenciasApi.createReferencia(buildPayload())
    await swal.toast('Referência cadastrada com sucesso.')
    await router.push(`/referencias/${res.codReferencia}/anexos`)
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao cadastrar referência', 'error')
  } finally {
    saving.value = false
  }
}

onMounted(loadMeta)
</script>
