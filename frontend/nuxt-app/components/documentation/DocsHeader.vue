<script setup lang="ts">
const store = useApiDocsStore()
defineProps<{ admin: boolean }>()
defineEmits<{ authorize: []; menu: [] }>()
</script>
<template>
  <header class="docs-header">
    <div class="docs-brand"><span class="docs-brand-icon"><i class="bi bi-braces-asterisk" aria-hidden="true" /></span><div><strong>MEDWARE</strong><span>CONTEÚDO · DEVELOPERS</span></div></div>
    <div class="docs-heading"><button class="docs-icon-btn docs-mobile-menu" aria-label="Abrir navegação" @click="$emit('menu')"><i class="bi bi-list" /></button><div><div class="docs-eyebrow">PORTAL DO DESENVOLVEDOR</div><h1>API MEDWARE CONTEUDO</h1><div class="docs-version"><span>v{{ store.spec?.info?.version || '1.0.0' }}</span><span class="docs-dot">·</span><span class="docs-oas">OAS {{ store.spec?.openapi?.startsWith('3.0') ? '3.0' : store.spec?.openapi || '3.0' }}</span></div></div></div>
    <div class="docs-header-controls">
      <label class="docs-sr-only" for="docs-definition">Definição da API</label><select id="docs-definition" v-model="store.definition"><option value="parceiros">API de Parceiros</option><option value="interna">API Interna{{ !admin ? ' · restrita' : '' }}</option><option value="web">Web Admin{{ !admin ? ' · restrita' : '' }}</option></select>
      <label class="docs-sr-only" for="docs-environment">Ambiente</label><select id="docs-environment" v-model="store.environment"><option v-for="env in store.environments" :key="env.id" :value="env.id">{{ env.label }}</option><option v-if="!store.environments.length" value="atual">Ambiente atual</option></select>
      <button class="docs-btn docs-btn-authorize" @click="$emit('authorize')"><i :class="store.authorized ? 'bi bi-shield-check' : 'bi bi-lock'" />{{ store.authorized ? 'Autorizado' : 'Autorizar' }}</button>
    </div>
  </header>
</template>
