<template>
  <div>
    <LayoutAppPageHeader title="Pacotes Disponíveis" icon="box-seam" />
    <ScriptsScriptsBreadcrumb :sistema="sistema" />
    <div class="card shadow-sm">
      <div class="card-body">
        <div class="d-flex justify-content-between align-items-center mb-3">
          <h5 class="fw-bold mb-0">Escolha o Pacote</h5>
          <button type="button" class="btn btn-outline-secondary btn-sm" @click="goSistema()">
            <i class="bi bi-arrow-left" /> Voltar
          </button>
        </div>
        <div class="input-group mb-3">
          <span class="input-group-text"><i class="bi bi-search" /></span>
          <input v-model="busca" type="text" class="form-control" placeholder="Buscar pacote..." />
        </div>
        <div v-if="loading" class="text-center py-4 text-muted">Carregando pacotes...</div>
        <div v-else class="row row-cols-1 row-cols-md-2 row-cols-lg-3 g-3">
          <div v-for="p in pacotesFiltrados" :key="p.codPacote" class="col">
            <div class="card package-card h-100" role="button" @click="select(p.codPacote)">
              <div class="card-body text-center">
                <i :class="`bi ${getPacoteVisual(p.nome).icon} ${getPacoteVisual(p.nome).iconColor} fs-1 mb-3`" />
                <h6 class="card-title">{{ p.nome }}</h6>
                <p v-if="p.descricao" class="text-muted small mb-0">{{ p.descricao }}</p>
              </div>
            </div>
          </div>
          <div v-if="!pacotesFiltrados.length" class="col-12 text-center text-muted py-4">
            Nenhum pacote encontrado.
          </div>
        </div>
      </div>
    </div>
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

function getPacoteVisual(nomePacote: string) {
  const n = (nomePacote || '').toLowerCase()
  if (n.includes('angiologia') || n.includes('vascular')) return { icon: 'bi-activity', iconColor: 'text-danger' }
  if (n.includes('cardiologia')) return { icon: 'bi-heart-fill', iconColor: 'text-danger' }
  if (n.includes('consulta')) return { icon: 'bi-clipboard2-pulse-fill', iconColor: 'text-primary' }
  if (n.includes('ultrassonografia')) return { icon: 'bi-soundwave', iconColor: 'text-info' }
  if (n.includes('pediatria')) return { icon: 'bi-emoji-smile-fill', iconColor: 'text-warning' }
  if (n.includes('oftalmologia')) return { icon: 'bi-eye-fill', iconColor: 'text-primary' }
  if (n.includes('nutri')) return { icon: 'bi-egg-fill', iconColor: 'text-success' }
  return { icon: 'bi-box-seam-fill', iconColor: 'text-secondary' }
}
</script>
