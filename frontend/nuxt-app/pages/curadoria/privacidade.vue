<template>
  <CuradoriaDocument :document="curadoriaPrivacy" />
</template>

<script setup lang="ts">
import CuradoriaDocument from '~/components/curadoria/CuradoriaDocument.vue'
import { curadoriaPrivacy } from '~/content/curadoriaDocuments'

definePageMeta({ layout: 'curadoria' })
useHead({ title: curadoriaPrivacy.title, titleTemplate: null })
</script>
