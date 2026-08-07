<template><div><DsPageHeader title="Assistente" subtitle="Gerenciamento do banco de modelos de laudos" icon="database"><template #actions><DsButton v-if="auth.isAdmin" variant="success" to="/assistente/modelos/importar">Importar modelo de laudo</DsButton></template></DsPageHeader><DsPageShell><AssistenteNav /><DsAlert v-if="error" variant="error" class="mb-4">{{ error }}</DsAlert><div class="grid gap-4 sm:grid-cols-2 xl:grid-cols-4"><DsModuleCard v-for="item in modules" :key="item.to" :title="item.title" :description="item.description" :icon="item.icon" :icon-color="item.iconColor" :to="item.to"><template #badge>{{ stats[item.key] ?? '—' }} registros</template></DsModuleCard></div></DsPageShell></div></template>
<script setup lang="ts">
import type { AssistenteDashboard } from '~/composables/useAssistenteApi'
definePageMeta({ layout: 'default' }); const auth = useAuthStore(); const api = useAssistenteApi(); const stats = ref<Partial<AssistenteDashboard>>({}); const error = ref('')
const modules = [
  { key: 'procedimentos', title: 'Procedimentos / TUSS', description: 'Procedimentos e vínculos com scripts e frases.', icon: 'clipboard2-pulse', iconColor: '#2563eb', to: '/assistente/procedimentos' },
  { key: 'scripts', title: 'Scripts de laudo', description: 'Scripts legados, DLL e JSON.', icon: 'code-square', iconColor: '#7c3aed', to: '/assistente/scripts' },
  { key: 'paginasFotos', title: 'Modelos MRD', description: 'Páginas de impressão e fotos.', icon: 'file-earmark-richtext', iconColor: '#0891b2', to: '/assistente/paginas-fotos' },
  { key: 'frases', title: 'Frases e grupos', description: 'Banco de frases organizado por grupos.', icon: 'chat-left-text', iconColor: '#d97706', to: '/assistente/frases' },
  { key: 'especialidades', title: 'Especialidades', description: 'Catálogo de especialidades médicas.', icon: 'heart-pulse', iconColor: '#e11d48', to: '/assistente/especialidades' },
  { key: 'referencias', title: 'Referências', description: 'Textos e referências por especialidade.', icon: 'journal-medical', iconColor: '#059669', to: '/assistente/referencias' },
  { key: 'esquemas', title: 'Esquemas', description: 'Esquemas gerais e de fotografias.', icon: 'diagram-3', iconColor: '#4f46e5', to: '/assistente/esquemas' },
  { key: 'operadoras', title: 'Operadoras', description: 'Operadoras e seus grupos.', icon: 'buildings', iconColor: '#475569', to: '/assistente/operadoras' },
  { key: 'vinculos', title: 'Vínculos', description: 'Explore e gerencie as relações entre os cadastros.', icon: 'diagram-3-fill', iconColor: '#0f766e', to: '/assistente/vinculos' }
] as const
onMounted(async () => { try { stats.value = await api.dashboard() } catch (reason) { error.value = reason instanceof Error ? reason.message : 'Não foi possível consultar o Assistente.' } })
</script>
