<template>
  <div>
    <DsPageHeader :title="`Vincular Variáveis — ${nomeScript}`" icon="link" />
    <DsPageShell>
      <DsSearchInput v-model="busca" placeholder="Pesquisar por nome ou sigla..." wrapper-class="mb-4" />
      <div class="border border-gray-200 rounded-2xl bg-white p-3 mb-4 max-h-[400px] overflow-y-auto">
        <label
          v-for="v in filtradas"
          :key="v.codVariavel"
          class="flex items-start gap-3 border-b border-gray-100 py-3 last:border-0 cursor-pointer"
        >
          <input
            v-model="selecionadas"
            type="checkbox"
            class="mt-1 rounded border-gray-300"
            :value="v.codVariavel"
          />
          <span class="flex-1">
            <strong class="text-ds-text">{{ v.nome }}</strong> ({{ v.sigla }})
            <span class="block text-xs text-gray-500 mt-0.5">
              Fórmula: {{ v.formula || '—' }} · Normalidade: {{ v.normalidade || '—' }}
            </span>
          </span>
        </label>
      </div>
      <div class="flex gap-2">
        <DsButton variant="success" icon="save" :loading="loading" @click="salvar">Salvar</DsButton>
        <DsButton variant="secondary" :to="voltarPath">Voltar</DsButton>
      </div>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default', middleware: ['admin'] })

const route = useRoute()
const router = useRouter()
const id = Number(route.params.id)
const scriptsApi = useScriptsApi()
const swal = useSwal()

const nomeScript = ref('')
const busca = ref('')
const loading = ref(false)
const variaveis = ref<{ codVariavel: number; nome: string; sigla: string; formula: string; normalidade: string; associada: boolean }[]>([])
const selecionadas = ref<number[]>([])
const voltarPath = ref('/scripts')

const filtradas = computed(() => {
  const q = busca.value.toLowerCase()
  return variaveis.value.filter(
    (v) => !q || v.nome.toLowerCase().includes(q) || v.sigla.toLowerCase().includes(q)
  )
})

onMounted(async () => {
  const res = await scriptsApi.getVariaveis(id)
  nomeScript.value = res.nomeScript
  variaveis.value = res.data
  selecionadas.value = res.data.filter((v) => v.associada).map((v) => v.codVariavel)
  const s = await scriptsApi.getScript(id)
  const d = s.data as Record<string, unknown>
  voltarPath.value = `/scripts?sistema=${d.sistema}&pacote=${d.codPacote}`
})

async function salvar() {
  loading.value = true
  try {
    await scriptsApi.saveVariaveis(id, selecionadas.value)
    await swal.toast('Variáveis salvas!')
    await router.push(voltarPath.value)
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  } finally {
    loading.value = false
  }
}
</script>
