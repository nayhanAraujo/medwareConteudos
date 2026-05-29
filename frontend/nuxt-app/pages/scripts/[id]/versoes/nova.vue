<template>
  <div>
    <LayoutAppPageHeader title="Nova Versão" icon="plus-circle" />
    <div class="card shadow-sm">
      <div class="card-body">
        <form @submit.prevent="salvar">
          <div class="mb-3">
            <label class="form-label">Número da versão</label>
            <input v-model="numeroVersao" class="form-control" required />
          </div>
          <div class="mb-3">
            <label class="form-label">Observações</label>
            <textarea v-model="observacoes" class="form-control" rows="3" />
          </div>
          <button type="submit" class="btn btn-success" :disabled="loading">Salvar</button>
          <NuxtLink class="btn btn-outline-secondary ms-2" :to="`/scripts/${id}/versoes`">Cancelar</NuxtLink>
        </form>
      </div>
    </div>
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
