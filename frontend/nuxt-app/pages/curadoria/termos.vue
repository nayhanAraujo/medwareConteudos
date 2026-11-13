<template>
  <CuradoriaDocument :document="curadoriaTerms" />
</template>

<script setup lang="ts">
import CuradoriaDocument from '~/components/curadoria/CuradoriaDocument.vue'
import { curadoriaTerms } from '~/content/curadoriaDocuments'

definePageMeta({ layout: 'curadoria' })
useHead({ title: curadoriaTerms.title, titleTemplate: null })
</script>
