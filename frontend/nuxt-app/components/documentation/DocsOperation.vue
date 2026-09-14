<script setup lang="ts">
import DocsParameters from './DocsParameters.vue'
import DocsSchema from './DocsSchema.vue'
import { resolveSchema } from '~/utils/apiDocs'
const props = defineProps<{ operation: any }>()
const store = useApiDocsStore()
const open = computed(() => store.operationId === props.operation.id)
const tabs = [{ id: 'informacoes', label: 'Informações' }, { id: 'parametros', label: 'Parâmetros' }, { id: 'respostas', label: 'Respostas' }, { id: 'esquema', label: 'Esquema' }]
const request = computed(() => resolveSchema(store.spec, props.operation.requestBody || {}))
function toggle() { if (open.value) store.operationId = ''; else store.selectOperation(props.operation.id) }
</script>
<template>
  <article class="docs-operation" :class="{ 'is-open': open }" :data-method="operation.method.toLowerCase()" :id="`op-${operation.id}`">
    <button class="docs-operation-heading" :aria-expanded="open" :aria-controls="`details-${operation.id}`" @click="toggle"><span class="docs-method" :data-method="operation.method.toLowerCase()">{{ operation.method.toUpperCase() }}</span><code>{{ operation.path }}</code><span class="docs-operation-summary">{{ operation.summary }}</span><i :class="open ? 'bi bi-chevron-up' : 'bi bi-chevron-down'" /></button>
    <div v-if="open" :id="`details-${operation.id}`" class="docs-operation-content">
      <div class="docs-tabs" role="tablist" aria-label="Detalhes da operação"><button v-for="item in tabs" :id="`${operation.id}-${item.id}`" :key="item.id" role="tab" :aria-selected="store.tab === item.id" :class="{ active: store.tab === item.id }" @click="store.tab = item.id">{{ item.label }}</button></div>
      <div class="docs-tab-panel" role="tabpanel" :aria-labelledby="`${operation.id}-${store.tab}`">
        <template v-if="store.tab === 'informacoes'"><h3>{{ operation.summary }}</h3><p class="docs-description">{{ operation.description || 'Consulte os parâmetros e as respostas desta operação.' }}</p><div class="docs-auth-note"><i :class="operation.security?.length ? 'bi bi-lock' : 'bi bi-unlock'" /><span>{{ operation.security?.length ? 'Requer Authorization: Bearer JWT' : 'Não exige token nesta definição' }}</span></div><h4>Respostas esperadas</h4><div class="docs-response-chips"><button v-for="(response, code) in operation.responses" :key="code" class="docs-response-chip" :data-success="String(code).startsWith('2')" @click="store.responseCode = String(code); store.tab = 'respostas'">{{ code }} <span>{{ resolveSchema(store.spec, response).description }}</span></button></div></template>
        <template v-else-if="store.tab === 'parametros'"><h4>Parâmetros da requisição</h4><DocsParameters :parameters="operation.parameters || []" :spec="store.spec" /><template v-if="request.content"><h4>Corpo da requisição <span v-if="request.required" class="docs-required">obrigatório</span></h4><div v-for="(media, type) in request.content" :key="type"><code>{{ type }}</code><DocsSchema :schema="media.schema" :spec="store.spec" /></div></template></template>
        <template v-else-if="store.tab === 'respostas'"><h4>Retornos HTTP</h4><div class="docs-table-wrap"><table class="docs-table"><thead><tr><th>Resposta</th><th>Descrição</th><th>Conteúdo</th></tr></thead><tbody><tr v-for="(raw, code) in operation.responses" :key="code" :class="{ selected: store.responseCode === String(code) }"><td><button class="docs-code-status" :data-success="String(code).startsWith('2')" @click="store.responseCode = String(code)">{{ code }}</button></td><td>{{ resolveSchema(store.spec, raw).description }}</td><td>{{ Object.keys(resolveSchema(store.spec, raw).content || {}).join(', ') || 'Sem corpo' }}</td></tr></tbody></table></div></template>
        <template v-else><h4>Esquema da resposta {{ store.responseCode }}</h4><template v-for="(media, type) in resolveSchema(store.spec, operation.responses[store.responseCode] || {}).content" :key="type"><code>{{ type }}</code><DocsSchema :schema="media.schema" :spec="store.spec" /></template><p v-if="!resolveSchema(store.spec, operation.responses[store.responseCode] || {}).content" class="docs-muted">Resposta sem corpo.</p></template>
      </div>
    </div>
  </article>
</template>
