<script setup lang="ts">
const store = useApiDocsStore()
defineProps<{ open: boolean }>()
const emit = defineEmits<{ close: [] }>()
const guides = [{ id: 'visao-geral', name: 'Visão Geral', icon: 'house' }, { id: 'inicio-rapido', name: 'Guia de Início Rápido', icon: 'rocket-takeoff' }, { id: 'autenticacao', name: 'Autenticação', icon: 'shield-lock' }]
const order = ['Sistema', 'Normalidades', 'Fórmulas', 'Variáveis', 'Referências', 'Especialidades', 'Relatórios', 'Scripts', 'Painéis']
const tags = computed(() => [...new Set(store.operations.map(op => op.tag))].sort((a, b) => {
  const ai = order.indexOf(a), bi = order.indexOf(b)
  return (ai < 0 ? 99 : ai) - (bi < 0 ? 99 : bi) || a.localeCompare(b)
}))
const icons: Record<string, string> = { Sistema: 'activity', Normalidades: 'sliders2', Fórmulas: 'calculator', Variáveis: 'braces', Referências: 'journal-bookmark', Especialidades: 'heart-pulse', Relatórios: 'file-earmark-text', Scripts: 'filetype-json', Painéis: 'bar-chart-line' }
function navigate(id: string) { store.section = id; emit('close') }
</script>
<template>
  <button v-if="open" class="docs-nav-backdrop" aria-label="Fechar navegação" @click="$emit('close')" />
  <nav class="docs-navigation" :class="{ 'is-open': open }" aria-label="Documentação da API">
    <label class="docs-search"><i class="bi bi-search" /><span class="docs-sr-only">Pesquisar endpoints</span><input v-model="store.search" placeholder="Buscar na documentação" @input="store.section = 'endpoints'"><span class="docs-key">/</span></label>
    <div class="docs-nav-label">COMECE AQUI</div>
    <button v-for="item in guides" :key="item.id" class="docs-nav-item" :class="{ active: store.section === item.id }" @click="navigate(item.id)"><i :class="`bi bi-${item.icon}`" />{{ item.name }}</button>
    <div class="docs-nav-divider" /><div class="docs-nav-label">REFERÊNCIA DA API <span>{{ store.operations.length }}</span></div>
    <button class="docs-nav-item" :class="{ active: store.section === 'endpoints' }" @click="navigate('endpoints')"><i class="bi bi-grid" />Todos os endpoints</button>
    <button v-for="tag in tags" :key="tag" class="docs-nav-item" :class="{ active: store.section === `tag:${tag}` }" @click="navigate(`tag:${tag}`)"><i :class="`bi bi-${icons[tag] || 'collection'}`" /><span>{{ tag === 'Sistema' ? 'Sistema & Saúde' : tag }}</span><small>{{ store.operations.filter(op => op.tag === tag).length }}</small></button>
    <div class="docs-nav-divider" /><button class="docs-nav-item" :class="{ active: store.section === 'esquemas' }" @click="navigate('esquemas')"><i class="bi bi-diagram-3" />Esquemas de Dados</button><button class="docs-nav-item" :class="{ active: store.section === 'suporte' }" @click="navigate('suporte')"><i class="bi bi-question-circle" />Suporte</button>
    <div class="docs-nav-footer"><span class="docs-status-dot" /> REST API · JSON<br><small>Documentação gerada por OpenAPI</small><NuxtLink to="/biblioteca">Voltar ao sistema <i class="bi bi-arrow-up-right" /></NuxtLink></div>
  </nav>
</template>
