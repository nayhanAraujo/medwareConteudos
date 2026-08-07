<template>
  <nav class="mb-5 flex flex-wrap gap-2" aria-label="Seções do Assistente">
    <DsButton v-for="item in items" :key="item.to" :to="item.to" :icon="item.icon" :variant="route.path === item.to ? 'primary' : 'secondary'" size="sm">
      {{ item.label }}
    </DsButton>
  </nav>
</template>

<script setup lang="ts">
const route = useRoute()
const items = [
  { label: 'Visão geral', icon: 'grid', to: '/assistente' },
  { label: 'Procedimentos', icon: 'clipboard2-pulse', to: '/assistente/procedimentos' },
  { label: 'Scripts', icon: 'code-square', to: '/assistente/scripts' },
  { label: 'Modelos MRD', icon: 'file-earmark-richtext', to: '/assistente/paginas-fotos' },
  { label: 'Frases', icon: 'chat-left-text', to: '/assistente/frases' },
  { label: 'Especialidades', icon: 'heart-pulse', to: '/assistente/especialidades' },
  { label: 'Referências', icon: 'journal-medical', to: '/assistente/referencias' },
  { label: 'Esquemas', icon: 'diagram-3', to: '/assistente/esquemas' },
  { label: 'Operadoras', icon: 'buildings', to: '/assistente/operadoras' },
  { label: 'Vínculos', icon: 'diagram-3-fill', to: '/assistente/vinculos' }
]
</script>
