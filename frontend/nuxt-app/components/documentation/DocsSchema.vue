<script setup lang="ts">
import { resolveSchema } from '~/utils/apiDocs'
defineOptions({ name: 'DocsSchema' })
const props = withDefaults(defineProps<{ schema: any; spec: any; depth?: number }>(), { depth: 0 })
const resolved = computed(() => resolveSchema(props.spec, props.schema || {}))
const fields = computed(() => Object.entries(resolved.value.properties || {}) as [string, any][])
</script>
<template>
  <div class="docs-schema">
    <p v-if="resolved.description" class="docs-muted">{{ resolved.description }}</p>
    <p><code>{{ resolved.type || (resolved.properties ? 'object' : resolved.$ref?.split('/').pop() || 'schema') }}</code><span v-if="resolved.nullable" class="docs-pill">nullable</span><span v-if="resolved.enum" class="docs-muted"> · {{ resolved.enum.join(' | ') }}</span></p>
    <template v-if="depth < 7">
      <div v-for="[name, field] in fields" :key="name" class="docs-schema-field"><details><summary><code>{{ name }}</code><span v-if="resolved.required?.includes(name)" class="docs-required">obrigatório</span><span class="docs-muted">{{ resolveSchema(spec, field).type || (field.$ref ? field.$ref.split('/').pop() : 'object') }}</span></summary><DocsSchema :schema="field" :spec="spec" :depth="depth + 1" /></details></div>
      <DocsSchema v-if="resolved.items" :schema="resolved.items" :spec="spec" :depth="depth + 1" />
      <div v-if="resolved.additionalProperties" class="docs-schema-field"><span class="docs-muted">Chave dinâmica</span><DocsSchema v-if="typeof resolved.additionalProperties === 'object'" :schema="resolved.additionalProperties" :spec="spec" :depth="depth + 1" /></div>
      <div v-for="(variant, i) in (resolved.oneOf || resolved.anyOf || resolved.allOf || [])" :key="i" class="docs-schema-field"><span class="docs-muted">Variação {{ i + 1 }}</span><DocsSchema :schema="variant" :spec="spec" :depth="depth + 1" /></div>
    </template><small v-else class="docs-muted">Consulte a referência no documento OpenAPI para continuar.</small>
  </div>
</template>
