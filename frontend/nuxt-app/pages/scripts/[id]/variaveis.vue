<template>
  <div>
    <LayoutAppPageHeader :title="`Vincular Variáveis — ${nomeScript}`" icon="link" />
    <div class="card shadow-sm">
      <div class="card-body">
        <input v-model="busca" type="text" class="form-control mb-3" placeholder="Pesquisar por nome ou sigla..." />
        <div class="border rounded p-2 mb-3" style="max-height: 400px; overflow-y: auto">
          <div v-for="v in filtradas" :key="v.codVariavel" class="form-check border-bottom py-2">
            <input :id="`v-${v.codVariavel}`" v-model="selecionadas" class="form-check-input" type="checkbox" :value="v.codVariavel" />
            <label class="form-check-label w-100" :for="`v-${v.codVariavel}`">
              <strong>{{ v.nome }}</strong> ({{ v.sigla }})
              <div class="small text-muted">Fórmula: {{ v.formula || '—' }} · Normalidade: {{ v.normalidade || '—' }}</div>
            </label>
          </div>
        </div>
        <button class="btn btn-success" :disabled="loading" @click="salvar">
          <i class="bi bi-save" /> Salvar
        </button>
        <NuxtLink class="btn btn-outline-secondary ms-2" :to="voltarPath">Voltar</NuxtLink>
      </div>
    </div>
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
