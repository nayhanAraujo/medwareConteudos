<template>
  <div>
    <DsPageHeader :title="`MRD — ${nomeScript}`" icon="file-earmark-binary" />
    <DsPageShell>
      <p class="text-sm text-gray-600 mb-4">Sistema: <strong class="text-ds-text">{{ sistema }}</strong></p>
      <ul v-if="mrdList.length" class="space-y-2 mb-6">
        <li
          v-for="m in mrdList"
          :key="m.codScriptMrd"
          class="flex justify-between items-center rounded-xl border border-gray-200 bg-white px-4 py-3"
        >
          <span class="text-sm">
            {{ m.nomeArquivo }}
            <DsBadge v-if="m.padrao" variant="primary" class="ml-2">padrão</DsBadge>
          </span>
          <DsButton variant="danger" size="sm" icon="trash" @click="excluir(m.codScriptMrd)" />
        </li>
      </ul>
      <p v-else class="text-gray-500 mb-6">Nenhum MRD cadastrado.</p>
      <div class="mb-4">
        <label class="block text-sm font-medium text-ds-text mb-1.5">Adicionar arquivos MRD</label>
        <input
          type="file"
          multiple
          class="w-full text-sm file:mr-4 file:py-2 file:px-4 file:rounded-full file:border-0 file:bg-ds-surface file:text-ds-text hover:file:bg-gray-200"
          @change="onFiles"
        />
      </div>
      <div class="flex gap-2">
        <DsButton :disabled="!pendingFiles.length || loading" @click="enviar">Enviar</DsButton>
        <DsButton variant="secondary" :to="voltarPath">Voltar</DsButton>
      </div>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { ScriptMrdDto } from '~/composables/useScriptsApi'

definePageMeta({ layout: 'default' })

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
