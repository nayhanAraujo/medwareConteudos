<script setup lang="ts">
import DocsHeader from '~/components/documentation/DocsHeader.vue'
import DocsNavigation from '~/components/documentation/DocsNavigation.vue'
import DocsOperation from '~/components/documentation/DocsOperation.vue'
import DocsConsole from '~/components/documentation/DocsConsole.vue'
import DocsAuthorization from '~/components/documentation/DocsAuthorization.vue'
import DocsSchema from '~/components/documentation/DocsSchema.vue'
import { docsGuides } from '~/content/apiDocsGuides'
import { readDocsLocation, writeDocsLocation } from '~/utils/docsNavigation'

definePageMeta({ layout: 'documentation' })
useHead({ title: 'API MEDWARE CONTEUDO · Portal do Desenvolvedor' })
const store = useApiDocsStore()
const auth = useAuthStore()
const route = useRoute()
const router = useRouter()
const requestFetch = useRequestFetch()
const menuOpen = ref(false)
const authorizeOpen = ref(false)
function applyQuery() {
  const state = readDocsLocation(route.query)
  const operationChanged = store.operationId !== state.operationId
  store.$patch(state)
  if (operationChanged) { store.cancel(); store.initializeForm() }
}
applyQuery()
await store.load(requestFetch as typeof $fetch, auth.token)
watch(() => [store.definition, store.environment], () => { authorizeOpen.value = false; void store.load(requestFetch as typeof $fetch, auth.token) })
watch(() => auth.token, () => { store.clearCredentials(); void store.load(requestFetch as typeof $fetch, auth.token) })
watch(() => [store.definition, store.environment, store.section, store.operationId, store.tab, store.search], () => {
  const query = writeDocsLocation(store)
  if (JSON.stringify(route.query) !== JSON.stringify(query)) void router.replace({ query })
})
watch(() => route.query, applyQuery)
const groups = computed(() => {
  const map = new Map<string, any[]>()
  for (const op of store.visibleOperations) {
    if (store.section.startsWith('tag:') && op.tag !== store.section.slice(4)) continue
    if (!map.has(op.tag)) map.set(op.tag, [])
    map.get(op.tag)!.push(op)
  }
  return [...map.entries()]
})
const guide = computed(() => docsGuides[store.section])
const schemas = computed(() => Object.entries(store.spec?.components?.schemas || {}))
const quickLinks = [{ title: 'Guia de início rápido', text: 'Sua primeira requisição em poucos passos.', icon: 'rocket-takeoff', section: 'inicio-rapido' }, { title: 'Autenticação', text: 'Obtenha e utilize seu token JWT.', icon: 'shield-lock', section: 'autenticacao' }, { title: 'Explore os endpoints', text: 'Parâmetros, esquemas e exemplos reais.', icon: 'code-square', section: 'endpoints' }, { title: 'Esquemas de dados', text: 'Conheça a estrutura de cada resposta.', icon: 'diagram-3', section: 'esquemas' }]
function searchShortcut(event: KeyboardEvent) {
  if (event.key === 'Escape') menuOpen.value = false
  if (event.key === '/' && !['INPUT', 'TEXTAREA', 'SELECT'].includes((event.target as HTMLElement).tagName)) { event.preventDefault(); menuOpen.value = true; nextTick(() => document.querySelector<HTMLInputElement>('.docs-search input')?.focus()) }
}
onMounted(() => document.addEventListener('keydown', searchShortcut))
onUnmounted(() => { store.cancel(); store.clearCredentials(); document.removeEventListener('keydown', searchShortcut) })
</script>
<template>
  <a href="#docs-main" class="docs-skip-link">Ir para o conteúdo</a>
  <DocsHeader :admin="auth.isAdmin" @authorize="authorizeOpen = true" @menu="menuOpen = !menuOpen" />
  <DocsNavigation :open="menuOpen" @close="menuOpen = false" />
  <main id="docs-main" class="docs-main" tabindex="-1">
    <div class="docs-breadcrumb"><span>Documentação</span><i class="bi bi-chevron-right" /><span>{{ store.definition === 'parceiros' ? 'API de Parceiros' : store.definition === 'interna' ? 'API Interna' : 'Web Admin' }}</span></div>
    <div v-if="store.error" class="docs-error docs-loading-error" role="alert"><i class="bi bi-exclamation-circle" /><h2>Não foi possível carregar a definição</h2><p>{{ store.error }}</p><div class="docs-inline-actions"><button class="docs-btn" @click="store.load(requestFetch as typeof $fetch, auth.token)">Tentar novamente</button><NuxtLink v-if="store.definition !== 'parceiros'" class="docs-btn docs-btn-primary" :to="{ path: '/login', query: { redirect: route.fullPath } }">Entrar como administrador</NuxtLink></div></div>
    <div v-if="store.loading" class="docs-skeleton" role="status" aria-label="Carregando documentação"><div /><div /><div /><div /></div>
    <template v-else-if="store.section === 'visao-geral'">
      <section class="docs-hero"><span class="docs-eyebrow"><span class="docs-status-dot" /> CONECTE. CONSULTE. INTEGRE.</span><h2>O conteúdo Medware.<br><span>Ao alcance da sua API.</span></h2><p>Integre variáveis, normalidades, fórmulas e modelos à sua aplicação. Explore os contratos, copie exemplos e teste cada operação em um só lugar.</p><div class="docs-inline-actions"><button class="docs-btn docs-btn-primary" @click="store.section = 'inicio-rapido'">Começar integração <i class="bi bi-arrow-right" /></button><button class="docs-btn" @click="store.section = 'endpoints'">Ver endpoints</button></div></section>
      <div class="docs-base-url"><div><span class="docs-eyebrow">ENDEREÇO DO AMBIENTE</span><code>{{ store.publicBase || 'Carregando endereço…' }}</code></div><span class="docs-pill">{{ store.environment === 'homologacao' ? 'HOMOLOGAÇÃO' : 'ATUAL' }}</span></div>
      <div class="docs-quick-grid"><button v-for="item in quickLinks" :key="item.section" class="docs-quick-card" @click="store.section = item.section"><i :class="`bi bi-${item.icon}`" /><h3>{{ item.title }}</h3><p>{{ item.text }}</p><span>Explorar <i class="bi bi-arrow-up-right" /></span></button></div>
      <section class="docs-overview-note"><i class="bi bi-git" /><div><h3>Contrato v1, integração contínua</h3><p>Os endereços /apiconteudos/v1 e /api/v1 preservam os fluxos de integração. Selecione a definição correspondente ao seu uso para conferir autenticação e recursos disponíveis.</p></div></section>
    </template>
    <template v-else-if="guide"><section class="docs-guide"><span class="docs-eyebrow">{{ guide.eyebrow }}</span><h2>{{ guide.title }}</h2><p class="docs-lead">{{ guide.intro }}</p><article v-for="item in guide.sections" :key="item.title"><h3>{{ item.title }}</h3><p>{{ item.text }}</p><pre v-if="item.code" class="docs-guide-code"><code>{{ item.code }}</code></pre></article><a v-if="store.section === 'suporte' && /^https?:\/\//i.test(store.supportUrl)" class="docs-btn docs-btn-primary" :href="store.supportUrl" target="_blank" rel="noopener noreferrer">Acessar suporte <i class="bi bi-arrow-up-right" /></a></section></template>
    <template v-else-if="store.section === 'esquemas'"><div class="docs-page-intro"><span class="docs-eyebrow">MODELOS E CONTRATOS</span><h2>Esquemas de Dados</h2><p>Estruturas utilizadas nesta definição. Expanda as propriedades para consultar detalhes.</p></div><details v-for="[name, schema] in schemas" :key="name" class="docs-schema-card"><summary><i class="bi bi-braces" /><strong>{{ name }}</strong></summary><DocsSchema :schema="schema" :spec="store.spec" /></details></template>
    <template v-else><div class="docs-page-intro"><span class="docs-eyebrow">REFERÊNCIA DA API</span><h2>{{ store.section.startsWith('tag:') ? store.section.slice(4) : 'Explore os endpoints' }}</h2><p>Selecione uma operação para consultar seu contrato e experimentar uma requisição.</p></div><section v-for="[tag, ops] in groups" :key="tag" class="docs-endpoint-group"><h3 class="docs-group-heading">{{ tag.toLocaleUpperCase('pt-BR') }}<span>{{ ops.length }} {{ ops.length === 1 ? 'operação' : 'operações' }}</span></h3><DocsOperation v-for="op in ops" :key="op.id" :operation="op" /></section><div v-if="!groups.length && !store.error" class="docs-empty"><i class="bi bi-search" /><h3>Nenhum endpoint encontrado</h3><p>Experimente buscar por outro método, domínio ou nome.</p><button class="docs-btn" @click="store.search = ''; store.section = 'endpoints'">Limpar pesquisa</button></div></template>
    <footer class="docs-main-footer">MEDWARE CONTEÚDO <span>Documentação para desenvolvedores · v{{ store.spec?.info?.version || '1.0.0' }}</span></footer>
  </main>
  <DocsConsole @authorize="authorizeOpen = true" />
  <DocsAuthorization :open="authorizeOpen" @close="authorizeOpen = false" />
</template>
