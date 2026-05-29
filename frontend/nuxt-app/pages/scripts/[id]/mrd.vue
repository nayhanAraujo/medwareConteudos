<template>
  <div>
    <LayoutAppPageHeader :title="`MRD — ${nomeScript}`" icon="file-earmark-binary" />
    <div class="card shadow-sm mb-4">
      <div class="card-body">
        <p class="text-muted">Sistema: <strong>{{ sistema }}</strong></p>
        <ul v-if="mrdList.length" class="list-group mb-3">
          <li v-for="m in mrdList" :key="m.codScriptMrd" class="list-group-item d-flex justify-content-between align-items-center">
            <span>
              {{ m.nomeArquivo }}
              <span v-if="m.padrao" class="badge bg-primary ms-1">padrão</span>
            </span>
            <button type="button" class="btn btn-sm btn-outline-danger" @click="excluir(m.codScriptMrd)">
              <i class="bi bi-trash" />
            </button>
          </li>
        </ul>
        <p v-else class="text-muted">Nenhum MRD cadastrado.</p>
        <div class="mb-3">
          <label class="form-label">Adicionar arquivos MRD</label>
          <input type="file" class="form-control" multiple @change="onFiles" />
        </div>
        <button class="btn btn-primary" :disabled="!pendingFiles.length || loading" @click="enviar">
          Enviar
        </button>
        <NuxtLink class="btn btn-outline-secondary ms-2" :to="voltarPath">Voltar</NuxtLink>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { ScriptMrdDto } from '~/composables/useScriptsApi'

definePageMeta({ layout: 'default', middleware: ['admin'] })

const route = useRoute()
const id = Number(route.params.id)
const scriptsApi = useScriptsApi()
const swal = useSwal()

const nomeScript = ref('')
const sistema = ref('')
const mrdList = ref<ScriptMrdDto[]>([])
const pendingFiles = ref<File[]>([])
const loading = ref(false)
const voltarPath = ref('/scripts')

function onFiles(e: Event) {
  pendingFiles.value = Array.from((e.target as HTMLInputElement).files || [])
}

async function load() {
  const res = await scriptsApi.getMrd(id)
  nomeScript.value = res.nomeScript
  sistema.value = res.sistema
  mrdList.value = res.data
}

onMounted(async () => {
  await load()
  const s = await scriptsApi.getScript(id)
  const d = s.data as Record<string, unknown>
  voltarPath.value = `/scripts?sistema=${d.sistema}&pacote=${d.codPacote}`
})

async function enviar() {
  loading.value = true
  try {
    await scriptsApi.addMrd(id, sistema.value, pendingFiles.value)
    await swal.toast('MRD adicionado!')
    pendingFiles.value = []
    await load()
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  } finally {
    loading.value = false
  }
}

async function excluir(cod: number) {
  const r = await swal.confirm('Excluir este MRD?')
  if (!r.isConfirmed) return
  await scriptsApi.deleteMrd(cod)
  await swal.toast('MRD excluído')
  await load()
}
</script>
