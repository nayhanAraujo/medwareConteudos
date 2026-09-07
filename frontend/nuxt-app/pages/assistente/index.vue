<template>
  <div>
    <DsPageHeader title="Assistente" subtitle="Gerenciamento do banco de modelos de laudos" icon="database">
      <template #actions>
        <DsButton v-if="canImport" variant="success" to="/assistente/modelos/importar">
          Importar modelo de laudo
        </DsButton>
      </template>
    </DsPageHeader>

    <div class="max-w-[90rem] mx-auto">
      <DsPageShell>
        <AssistenteNav />
        <DsAlert v-if="error" variant="error" class="mb-4">{{ error }}</DsAlert>
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          <DsHubCard
            v-for="(item, index) in modules"
            :key="item.to"
            :title="item.title"
            :desc="item.description"
            :icon="item.icon"
            :theme-name="item.themeName"
            :badge="badgeFor(item.key)"
            :delay-index="index"
            @click="navigateTo(item.to)"
          />
        </div>
      </DsPageShell>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { AssistenteDashboard } from '~/composables/useAssistenteApi'
import type { DsThemeName } from '~/composables/useDsTheme'

definePageMeta({ layout: 'default' })

const auth = useAuthStore()
const api = useAssistenteApi()
const stats = ref<Partial<AssistenteDashboard>>({})
const error = ref('')
const canImport = computed(() => auth.can('assistente', 'importar'))

const modules = [
  { key: 'procedimentos', title: 'Procedimentos / TUSS', description: 'Procedimentos e vínculos com scripts e frases.', icon: 'clipboard2-pulse', themeName: 'blue' as DsThemeName, to: '/assistente/procedimentos' },
  { key: 'scripts', title: 'Scripts de laudo', description: 'Scripts legados, DLL e JSON.', icon: 'code-square', themeName: 'purple' as DsThemeName, to: '/assistente/scripts' },
  { key: 'paginasFotos', title: 'Modelos MRD', description: 'Páginas de impressão e fotos.', icon: 'file-earmark-richtext', themeName: 'slate' as DsThemeName, to: '/assistente/paginas-fotos' },
  { key: 'frases', title: 'Banco de frases', description: 'Banco de frases organizado por grupos.', icon: 'chat-left-text', themeName: 'orange' as DsThemeName, to: '/assistente/frases' },
  { key: 'especialidades', title: 'Especialidades', description: 'Catálogo de especialidades médicas.', icon: 'heart-pulse', themeName: 'rose' as DsThemeName, to: '/assistente/especialidades' },
  { key: 'referencias', title: 'Referências', description: 'Textos e referências por especialidade.', icon: 'journal-medical', themeName: 'green' as DsThemeName, to: '/assistente/referencias' },
  { key: 'esquemas', title: 'Esquemas', description: 'Esquemas gerais e de fotografias.', icon: 'diagram-3', themeName: 'purple' as DsThemeName, to: '/assistente/esquemas' },
  { key: 'operadoras', title: 'Operadoras', description: 'Operadoras e seus grupos.', icon: 'buildings', themeName: 'gray' as DsThemeName, to: '/assistente/operadoras' },
  { key: 'vinculos', title: 'Vínculos', description: 'Explore e gerencie as relações entre os cadastros.', icon: 'diagram-3-fill', themeName: 'dark' as DsThemeName, to: '/assistente/vinculos' }
] as const

function badgeFor(key: keyof AssistenteDashboard | 'vinculos') {
  if (key === 'vinculos') return undefined
  const total = stats.value[key]
  return total === undefined ? undefined : `${total} registros`
}

onMounted(async () => {
  try {
    stats.value = await api.dashboard()
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'Não foi possível consultar o Assistente.'
  }
})
</script>
