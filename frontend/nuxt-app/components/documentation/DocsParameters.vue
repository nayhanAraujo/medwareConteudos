<script setup lang="ts">
import { resolveSchema } from '~/utils/apiDocs'
defineProps<{ parameters: any[]; spec: any }>()
</script>
<template>
  <div class="docs-table-wrap"><table class="docs-table"><thead><tr><th>Parâmetro</th><th>Descrição</th><th>Esquema</th></tr></thead><tbody><tr v-for="raw in parameters" :key="raw.name || raw.$ref"><td><code>{{ resolveSchema(spec, raw).name }}</code><small>{{ resolveSchema(spec, raw).in }}</small><span v-if="resolveSchema(spec, raw).required" class="docs-required">obrigatório</span></td><td>{{ resolveSchema(spec, raw).description || '—' }}</td><td><code>{{ resolveSchema(spec, raw).schema?.type || 'string' }}</code><small v-if="resolveSchema(spec, raw).schema?.default !== undefined">Padrão: {{ resolveSchema(spec, raw).schema.default }}</small><small v-if="resolveSchema(spec, raw).schema?.enum">{{ resolveSchema(spec, raw).schema.enum.join(' · ') }}</small><small v-if="resolveSchema(spec, raw).example !== undefined">Exemplo: {{ resolveSchema(spec, raw).example }}</small></td></tr><tr v-if="!parameters.length"><td colspan="3" class="docs-muted">Esta operação não possui parâmetros de URL ou cabeçalho.</td></tr></tbody></table></div>
</template>
