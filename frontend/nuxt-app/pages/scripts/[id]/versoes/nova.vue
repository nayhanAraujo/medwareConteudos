<template>
  <div>
    <DsPageHeader title="Nova Versão" icon="plus-circle" />
    <DsPageShell panel-class="!p-6 md:!p-8 max-w-xl">
      <form class="space-y-4" @submit.prevent="salvar">
        <DsInput v-model="numeroVersao" label="Número da versão" required />
        <DsTextarea v-model="observacoes" label="Observações" :rows="3" />
        <div class="flex gap-2">
          <DsButton type="submit" variant="success" :loading="loading">Salvar</DsButton>
          <DsButton variant="secondary" :to="`/scripts/${id}/versoes`">Cancelar</DsButton>
        </div>
      </form>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default', middleware: ['admin'] })

const route = useRoute()
const router = useRouter()
const id = Number(route.params.id)
const auth = useAuthStore()
const scriptsApi = useScriptsApi()
const swal = useSwal()

const numeroVersao = ref('')
const observacoes = ref('')
const loading = ref(false)

async function salvar() {
  loading.value = true
  try {
    await scriptsApi.createVersao(id, {
      numeroVersao: numeroVersao.value,
      observacoes: observacoes.value,
      criadoPor: auth.user?.nome
    })
    await swal.toast('Versão criada!')
    await router.push(`/scripts/${id}/versoes`)
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  } finally {
    loading.value = false
  }
}
</script>
