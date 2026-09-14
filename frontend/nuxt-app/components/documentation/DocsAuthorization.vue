<script setup lang="ts">
const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ close: [] }>()
const store = useApiDocsStore()
const auth = useAuthStore()
const requestFetch = useRequestFetch()
const modal = ref<HTMLDialogElement>()
const method = ref('token')
const value = ref('')
const password = ref('')
const error = ref('')
const busy = ref(false)
const issuedToken = ref('')
const copyFeedback = ref('')
watch(() => props.open, async open => { await nextTick(); if (open) { value.value = ''; password.value = ''; issuedToken.value = ''; copyFeedback.value = ''; error.value = ''; modal.value?.showModal() } else modal.value?.close() })
async function save() {
  busy.value = true; error.value = ''
  try {
    if (method.value === 'password' && store.definition === 'parceiros') {
      issuedToken.value = await store.obtainToken(password.value)
      copyFeedback.value = ''
    } else {
      store.setToken(value.value); value.value = ''; password.value = ''
      if (store.definition !== 'parceiros') await store.load(requestFetch as typeof $fetch, auth.token)
      emit('close')
    }
  }
  catch (e: any) { error.value = e.message || 'Não foi possível autorizar.' }
  finally { busy.value = false; password.value = '' }
}
async function copyIssuedToken() {
  try { await navigator.clipboard.writeText(issuedToken.value); copyFeedback.value = 'Token copiado.' }
  catch { copyFeedback.value = 'Não foi possível copiar automaticamente. Selecione o token e copie-o.' }
}
function remove() { store.setToken(''); emit('close') }
</script>
<template><dialog ref="modal" class="api-docs docs-dialog" @cancel="$emit('close')" @close="$emit('close')"><form @submit.prevent="save"><div class="docs-dialog-title"><h2><i class="bi bi-shield-lock" /> Autorizar requisições</h2><button class="docs-icon-btn" type="button" aria-label="Fechar" @click="$emit('close')"><i class="bi bi-x-lg" /></button></div><p class="docs-muted">{{ store.definition === 'web' ? 'JWT administrativo' : 'JWT de parceiro' }} · {{ store.environment === 'homologacao' ? 'Homologação' : 'Ambiente atual' }}</p><div v-if="store.definition === 'parceiros'" class="docs-tabs"><button type="button" :class="{ active: method === 'token' }" @click="method = 'token'">Informar token</button><button type="button" :class="{ active: method === 'password' }" @click="method = 'password'">Obter pela senha</button></div><template v-if="issuedToken"><p class="docs-token-success" role="status"><i class="bi bi-check-circle" /> Token gerado e autorizado para esta sessão.</p><label class="docs-field">Token JWT gerado<textarea :value="issuedToken" rows="5" readonly spellcheck="false" aria-label="Token JWT gerado" /></label><p v-if="copyFeedback" class="docs-caption" role="status">{{ copyFeedback }}</p><div class="docs-dialog-actions"><button type="button" class="docs-btn" @click="remove">Remover token</button><button type="button" class="docs-btn docs-btn-primary" @click="copyIssuedToken"><i class="bi bi-copy" /> Copiar token</button></div></template><template v-else><label v-if="method === 'token' || store.definition !== 'parceiros'" class="docs-field">Token JWT<textarea v-model="value" rows="4" autocomplete="off" spellcheck="false" placeholder="Cole o token, com ou sem o prefixo Bearer" required /></label><label v-else class="docs-field">Senha de parceiro<input v-model="password" type="password" autocomplete="off" required></label><p class="docs-caption">Usado apenas neste ambiente e definição, durante esta sessão. Os exemplos de código não incluem sua credencial.</p><p v-if="store.definition === 'web'" class="docs-notice">Use um token obtido em /api/web/auth/login no ambiente selecionado. O login do sistema continua sendo necessário para acessar esta definição.</p><p v-if="error" class="docs-error" role="alert">{{ error }}</p><div class="docs-dialog-actions"><button type="button" class="docs-btn" @click="remove">Remover token</button><button class="docs-btn docs-btn-primary" :disabled="busy">{{ busy ? 'Autorizando…' : 'Autorizar' }}</button></div></template></form></dialog></template>
