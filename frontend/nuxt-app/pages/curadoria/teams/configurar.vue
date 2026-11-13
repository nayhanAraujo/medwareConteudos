<template>
  <CuradoriaTeamsPage configuration />
</template>

<script setup lang="ts">
import CuradoriaTeamsPage from '~/components/curadoria/CuradoriaTeamsPage.vue'
definePageMeta({ layout: 'curadoria' })
useHead({ title: 'Configurar no Teams — Curadoria de Conhecimento', titleTemplate: null })
</script>
