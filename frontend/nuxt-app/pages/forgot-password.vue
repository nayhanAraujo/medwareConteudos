<template>
  <DsAuthShell size="sm" background-image="/images/auth/login-bg.webp">
    <h1 class="text-xl font-semibold font-manrope text-ds-text mb-4">Recuperar senha</h1>
    <DsAlert v-if="sent" variant="success" role="status">{{ confirmation }}</DsAlert>
    <form v-else class="space-y-4" @submit.prevent="submit">
      <p class="text-sm text-ds-text-secondary">Informe o e-mail cadastrado para receber um link de recuperação. Se você ainda não possui um e-mail cadastrado, procure o administrador.</p>
      <DsInput v-model="email" label="E-mail cadastrado" type="email" autocomplete="email" :maxlength="254" :disabled="loading" required />
      <DsAlert v-if="error" variant="error" role="alert">{{ error }}</DsAlert>
      <DsButton type="submit" block :loading="loading" :disabled="loading">Enviar link de recuperação</DsButton>
    </form>
    <p class="text-center mt-4 mb-0 text-sm"><NuxtLink to="/login" class="text-blue-600 hover:underline">Voltar ao login</NuxtLink></p>
  </DsAuthShell>
</template>

<script setup lang="ts">
definePageMeta({ layout: false })
const api = useApi()
const email = ref(''), loading = ref(false), sent = ref(false), error = ref('')
const confirmation = 'Se houver uma conta ativa com esse e-mail, enviaremos as instruções de recuperação.'
async function submit() {
  if (loading.value) return
  error.value = ''
  loading.value = true
  try {
    await api.post('/api/web/auth/forgot-password', { email: email.value.trim() })
    sent.value = true
  } catch (e) { error.value = e instanceof Error ? e.message : 'Não foi possível solicitar a recuperação. Tente novamente.' }
  finally { loading.value = false }
}
</script>
