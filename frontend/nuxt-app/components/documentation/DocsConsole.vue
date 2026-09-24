<script setup lang="ts">
import { buildCodeSamples, exampleForSchema, resolveSchema } from '~/utils/apiDocs'
const store = useApiDocsStore()
const auth = useAuthStore()
const emit = defineEmits<{ authorize: [] }>()
const copied = ref('')
const formError = ref('')
const files = ref<Record<string, File[]>>({})
const confirmation = ref<HTMLDialogElement>()
const samples = computed(() => {
  if (!store.selected) return null
  try { return buildCodeSamples(store.selected, store.parameters, store.body, store.publicBase, store.contentType) }
  catch { return { curl: 'Preencha parâmetros de caminho válidos para gerar o exemplo.', javascript: 'Preencha parâmetros de caminho válidos para gerar o exemplo.', python: 'Preencha parâmetros de caminho válidos para gerar o exemplo.' } }
})
const selectedSample = computed(() => samples.value?.[store.language as keyof typeof samples.value] || '')
const response = computed(() => resolveSchema(store.spec, store.selected?.responses?.[store.responseCode] || {}))
const responseMedia = computed(() => Object.values(response.value.content || {})[0] as any)
const responseExample = computed(() => {
  if (!responseMedia.value) return 'Esta resposta não possui corpo.'
  if (responseMedia.value.schema?.format === 'binary') return 'Conteúdo binário · disponível para download'
  return JSON.stringify(responseMedia.value.example ?? Object.values(responseMedia.value.examples || {}).map((value: any) => value.value)[0] ?? exampleForSchema(store.spec, responseMedia.value.schema || {}), null, 2)
})
const bodySchema = computed(() => resolveSchema(store.spec, resolveSchema(store.spec, store.selected?.requestBody || {}).content?.[store.contentType]?.schema || {}))
const fileFields = computed(() => Object.entries(bodySchema.value.properties || {}).filter(([, value]: any) => value.format === 'binary' || value.items?.format === 'binary') as [string, any][])
const requestContentTypes = computed(() => Object.keys(resolveSchema(store.spec, store.selected?.requestBody || {}).content || {}))
const formattedResult = computed(() => { const body = store.result?.body; try { return JSON.stringify(typeof body === 'string' ? JSON.parse(body) : body, null, 2) } catch { return String(body ?? '') } })
watch(() => store.operationId, () => { files.value = {}; formError.value = '' })
async function copy(value: string, key: string) { try { await navigator.clipboard.writeText(value); copied.value = key; setTimeout(() => { copied.value = '' }, 1800) } catch { formError.value = 'Não foi possível copiar automaticamente. Selecione o texto do exemplo.' } }
function selectFiles(event: Event, name: string) { files.value[name] = Array.from((event.target as HTMLInputElement).files || []) }
function prepare() {
  formError.value = ''
  for (const raw of store.selected?.parameters || []) {
    const p = resolveSchema(store.spec, raw)
    if (p.required && !store.parameters[p.in === 'header' ? 'headers' : p.in]?.[p.name]?.trim()) { formError.value = `Preencha o parâmetro obrigatório ${p.name}.`; return }
  }
  if (store.body.trim() && /json/.test(store.contentType)) { try { JSON.parse(store.body) } catch { formError.value = 'O corpo precisa conter um JSON válido.'; return } }
  if (store.requiresToken && !store.authorized && !(store.definition !== 'parceiros' && store.environment === 'atual' && auth.token)) { emit('authorize'); return }
  if (store.writeOperation) confirmation.value?.showModal()
  else void store.execute(auth.token, files.value, false)
}
function confirm() { confirmation.value?.close(); void store.execute(auth.token, files.value, true) }
function download() {
  const binary = store.result?.binary
  if (!binary) return
  const bytes = Uint8Array.from(atob(binary.base64), c => c.charCodeAt(0))
  const url = URL.createObjectURL(new Blob([bytes], { type: binary.contentType }))
  const link = document.createElement('a'); link.href = url; link.download = binary.filename || 'download'; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000)
}
</script>
<template>
  <aside class="docs-console" aria-label="Amostras de código e testes">
    <div class="docs-console-banner"><span class="docs-eyebrow">DESENVOLVA COM CONFIANÇA</span><h3><i class="bi bi-terminal" /> Integre sua aplicação</h3><p>Exemplos prontos para a sua próxima requisição.</p></div>
    <div class="docs-auth-card"><div><i class="bi bi-shield-lock" /><strong>{{ store.definition === 'web' ? 'Autenticação Web' : 'Autenticação JWT' }}</strong></div><button class="docs-text-link" @click="store.section = 'autenticacao'">Guia de autenticação <i class="bi bi-arrow-up-right" /></button><small v-if="store.definition === 'interna'">As consultas internas não exigem JWT de parceiro.</small></div>
    <template v-if="store.selected">
      <div v-if="store.result?.url" class="docs-request-url" aria-label="URL efetivamente consultada"><span>Última chamada</span><code>{{ store.result.url }}</code></div>
      <section class="docs-code-section"><h3>Amostras de código <i class="bi bi-code-slash" /></h3><div class="docs-language-tabs" role="tablist" aria-label="Linguagem do exemplo"><button v-for="[id, label] in [['curl', 'cURL'], ['javascript', 'JavaScript'], ['python', 'Python']]" :key="id" role="tab" :aria-selected="store.language === id" :class="{ active: store.language === id }" @click="store.language = id">{{ label }}</button></div><div class="docs-code"><button class="docs-copy" :aria-label="copied === 'code' ? 'Copiado' : 'Copiar exemplo'" @click="copy(selectedSample, 'code')"><i :class="copied === 'code' ? 'bi bi-check2' : 'bi bi-copy'" /></button><pre><code>{{ selectedSample }}</code></pre></div></section>
      <section class="docs-code-section"><h3>Amostras de resposta <select v-model="store.responseCode" aria-label="Código HTTP da amostra"><option v-for="(_, code) in store.selected.responses" :key="code" :value="String(code)">{{ code }}</option></select></h3><div class="docs-code"><button class="docs-copy" aria-label="Copiar amostra JSON" @click="copy(responseExample, 'response')"><i :class="copied === 'response' ? 'bi bi-check2' : 'bi bi-copy'" /></button><pre><code>{{ responseExample }}</code></pre></div><small class="docs-caption"><i class="bi bi-info-circle" /> Dados ilustrativos. Nenhuma requisição foi enviada.</small></section>
      <section class="docs-test-section"><div class="docs-test-heading"><h3>Teste em tempo real</h3><button class="docs-btn docs-btn-primary" :aria-expanded="store.tryOpen" @click="store.tryOpen = !store.tryOpen"><i class="bi bi-play" />Experimentar</button></div>
        <form v-if="store.tryOpen" class="docs-test-form" @submit.prevent="prepare">
          <div class="docs-environment-note" :class="{ sandbox: store.environment === 'homologacao' }"><i class="bi bi-hdd-network" /><span>{{ store.environment === 'homologacao' ? 'Homologação · dados de teste' : 'Ambiente atual · consultas' }}</span></div>
          <div class="docs-request-url"><span>{{ store.selected.method.toUpperCase() }}</span><code>{{ store.publicBase }}{{ store.selected.path }}</code></div>
          <template v-for="raw in store.selected.parameters" :key="raw.name || raw.$ref"><label class="docs-field">{{ resolveSchema(store.spec, raw).name }} <span v-if="resolveSchema(store.spec, raw).required" class="docs-required">*</span><small>{{ resolveSchema(store.spec, raw).in }} · {{ resolveSchema(store.spec, raw).description }}</small><input v-if="store.parameters[resolveSchema(store.spec, raw).in === 'header' ? 'headers' : resolveSchema(store.spec, raw).in]" v-model="store.parameters[resolveSchema(store.spec, raw).in === 'header' ? 'headers' : resolveSchema(store.spec, raw).in][resolveSchema(store.spec, raw).name]" :required="resolveSchema(store.spec, raw).required" :placeholder="resolveSchema(store.spec, raw).schema?.example?.toString() || ''"></label></template>
          <template v-if="requestContentTypes.length"><label class="docs-field">Content-Type<select v-model="store.contentType"><option v-for="type in requestContentTypes" :key="type">{{ type }}</option></select></label><label class="docs-field">{{ store.contentType.includes('multipart') ? 'Campos do formulário (JSON)' : 'Corpo da requisição' }}<textarea v-model="store.body" rows="8" spellcheck="false" :placeholder="store.contentType.includes('json') ? '{}' : ''" /></label><label v-for="[name, schema] in fileFields" :key="name" class="docs-field">{{ name }}<input type="file" :multiple="schema.type === 'array'" @change="selectFiles($event, name)"></label></template>
          <p v-if="!store.canExecute" class="docs-notice">{{ store.externalOperation ? 'Integrações externas não são executadas pelo console.' : 'Esta operação altera dados. Selecione uma homologação isolada e configurada para executá-la.' }}</p><p v-if="formError" class="docs-error" role="alert">{{ formError }}</p>
          <button v-if="!store.running" type="submit" class="docs-btn docs-btn-primary docs-full" :disabled="!store.canExecute"><i class="bi bi-send" /> Enviar requisição</button><button v-else type="button" class="docs-btn docs-full" @click="store.cancel()">Cancelar requisição</button>
        </form>
      </section>
      <section v-if="store.result" class="docs-code-section docs-real-response" aria-live="polite"><h3>Resposta real <span class="docs-code-status" :data-success="store.result.status >= 200 && store.result.status < 300">{{ store.result.status || 'Falha' }}</span></h3><small class="docs-caption">{{ store.result.durationMs }} ms · {{ store.result.statusText }}</small><button v-if="store.result.binary" class="docs-btn docs-full" @click="download()"><i class="bi bi-download" />{{ store.result.binary.filename || 'Baixar arquivo' }}</button><div v-else class="docs-code"><pre><code>{{ formattedResult }}</code></pre></div><details class="docs-response-headers"><summary>Cabeçalhos da resposta</summary><dl><template v-for="(value, name) in store.result.headers" :key="name"><dt>{{ name }}</dt><dd>{{ value }}</dd></template></dl></details></section>
    </template>
    <div v-else class="docs-console-empty"><span><i class="bi bi-cursor" /></span><h4>Selecione um endpoint</h4><p>Explore uma operação para ver seus exemplos de código, respostas e opções de teste.</p></div>
    <dialog ref="confirmation" class="api-docs docs-dialog"><h2>Confirmar alteração em homologação</h2><p>Esta requisição modificará os dados da cópia de testes.</p><p class="docs-request-url"><code>{{ store.selected?.method.toUpperCase() }} {{ store.publicBase }}{{ store.selected?.path }}</code></p><pre class="docs-confirm-body">{{ store.body }}</pre><div class="docs-dialog-actions"><button class="docs-btn" @click="confirmation?.close()">Cancelar</button><button class="docs-btn docs-btn-primary" @click="confirm">Confirmar e enviar</button></div></dialog>
  </aside>
</template>
