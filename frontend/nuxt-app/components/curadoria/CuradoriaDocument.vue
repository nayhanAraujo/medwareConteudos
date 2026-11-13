<template>
  <article aria-labelledby="document-title" class="curadoria-document">
    <h1 id="document-title">{{ document.title }}</h1>
    <p class="curadoria-edition">Atualização: <time datetime="2026-11-13">13 de novembro de 2026</time><span aria-hidden="true"> | </span>Versão 1.0</p>
    <section v-for="(section, index) in document.sections" :key="section.title" :aria-labelledby="`section-${index + 1}`">
      <h2 :id="`section-${index + 1}`">{{ index + 1 }}. {{ section.title }}</h2>
      <p>{{ section.text }}</p>
    </section>
  </article>
</template>

<script setup lang="ts">
import type { CuradoriaDocument } from '~/content/curadoriaDocuments'
defineProps<{ document: CuradoriaDocument }>()
</script>

<style scoped>
.curadoria-document { overflow-wrap: anywhere; }
h1 { margin: 0; max-width: 25ch; color: #172033; font-size: clamp(1.8rem, 4.5vw, 2.4rem); font-weight: 700; line-height: 1.25; letter-spacing: -.025em; }
.curadoria-edition { margin: 1rem 0 2.5rem; color: #475569; font-size: .9rem; }
section + section { margin-top: 1.8rem; }
h2 { margin: 0 0 .65rem; font-size: 1.15rem; font-weight: 650; line-height: 1.45; color: #172033; }
section p { margin: 0; font-size: 1rem; }
</style>
