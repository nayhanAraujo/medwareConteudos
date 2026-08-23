<template>
  <div>
    <DsPageHeader title="Pacotes Disponíveis" icon="box-seam" />
    <ScriptsBreadcrumb :sistema="sistema" />
    <DsPageShell>
      <div class="flex justify-between items-center mb-6">
        <DsSectionTitle title="Escolha o Pacote" />
        <DsButton variant="secondary" size="sm" icon="arrow-left" @click="goSistema()">Voltar</DsButton>
      </div>
      <DsSearchInput v-model="busca" placeholder="Buscar pacote..." wrapper-class="mb-6" />
      <div v-if="loading" class="text-center py-8 text-gray-500">Carregando pacotes...</div>
      <DsEmptyState
        v-else-if="!pacotesFiltrados.length"
        title="Nenhum pacote encontrado"
        icon="box-seam"
      />
      <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
        <DsHubCard
          v-for="(p, index) in pacotesFiltrados"
          :key="p.codPacote"
          :title="p.nome"
          :desc="p.descricao || 'Pacote de scripts'"
          :icon="getPacoteIcon(p.nome)"
          theme-name="gray"
          :delay-index="index"
          @click="select(p.codPacote)"
        />
      </div>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

const route = useRoute()
const { isSistemaValido, goSistema, goLista } = useScriptsNav()
const scriptsApi = useScriptsApi()

const sistema = computed(() => String(route.query.sistema || ''))
const busca = ref('')
const loading = ref(true)
const pacotes = ref<{ codPacote: number; nome: string; descricao?: string }[]>([])

const pacotesFiltrados = computed(() => {
  const q = busca.value.trim().toLowerCase()
  if (!q) return pacotes.value
  return pacotes.value.filter(
    (p) => p.nome.toLowerCase().includes(q) || (p.descricao || '').toLowerCase().includes(q)
  )
})

onMounted(async () => {
  if (!isSistemaValido(sistema.value)) {
    await goSistema()
    return
  }
  try {
    const res = await scriptsApi.getPacotes()
    pacotes.value = res.data
  } finally {
    loading.value = false
  }
})

function select(codPacote: number) {
  goLista(sistema.value, codPacote)
}

function getPacoteIcon(nomePacote: string) {
  const n = (nomePacote || '').toLowerCase()
  if (n.includes('cardiologia')) return 'heart-fill'
  if (n.includes('consulta')) return 'clipboard2-pulse-fill'
  if (n.includes('ultrassonografia')) return 'soundwave'
  if (n.includes('pediatria')) return 'emoji-smile-fill'
  if (n.includes('oftalmologia')) return 'eye-fill'
  if (n.includes('nutri')) return 'egg-fill'
  return 'box-seam-fill'
}
</script>
