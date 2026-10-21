<template>
  <DsAuthShell size="sm" background-image="/images/auth/login-bg.webp">
    <h1 class="text-xl font-semibold font-manrope text-ds-text mb-4">Definir nova senha</h1>
    <p v-if="!ready" role="status">Carregando...</p>
    <DsAlert v-else-if="done" variant="success" role="status">Senha redefinida. Entre com sua nova senha.</DsAlert>
    <template v-else>
      <DsAlert v-if="error" variant="error" role="alert">{{ error }}</DsAlert>
      <form v-if="token" class="space-y-4 mt-4" @submit.prevent="submit">
        <DsInput v-model="password" label="Nova senha" type="password" autocomplete="new-password" hint="Mínimo de 6 caracteres." :disabled="loading" required />
        <DsInput v-model="confirmation" label="Confirmar nova senha" type="password" autocomplete="new-password" :disabled="loading" required />
        <DsButton type="submit" block :loading="loading" :disabled="loading">Salvar nova senha</DsButton>
      </form>
      <p class="text-center mt-4 text-sm"><NuxtLink to="/forgot-password" class="text-blue-600 hover:underline">Solicitar novo link</NuxtLink></p>
    </template>
    <p class="text-center mt-4 mb-0 text-sm"><NuxtLink to="/login" class="text-blue-600 hover:underline">Voltar ao login</NuxtLink></p>
  </DsAuthShell>
</template>

<script setup lang="ts">
import { readRecoveryToken, validateRecoveryPassword } from '~/utils/passwordRecovery'

definePageMeta({ layout: false })
useHead({ meta: [{ name: 'referrer', content: 'no-referrer' }] })
const api = useApi()
const swal = useSwal()
const router = useRouter()
const route = useRoute()
const token = ref(''), password = ref(''), confirmation = ref(''), error = ref('')
const loading = ref(false), ready = ref(false), done = ref(false)
function consumeLink(hash: string) {
  token.value = readRecoveryToken(hash)
  password.value = ''; confirmation.value = ''; done.value = false
  error.value = token.value ? '' : 'Link inválido ou ausente. Solicite um novo link de recuperação.'
  void router.replace({ hash: '' })
}
onMounted(() => {
  consumeLink(window.location.hash)
  ready.value = true
})
watch(() => route.hash, (hash) => {
  if (ready.value && hash) consumeLink(hash)
})
async function submit() {
  if (loading.value || !token.value) return
  error.value = validateRecoveryPassword(password.value, confirmation.value)
  if (error.value) return
  loading.value = true
  try {
    await api.post('/api/web/auth/reset-password', { token: token.value, newPassword: password.value, confirmPassword: confirmation.value })
    token.value = ''; password.value = ''; confirmation.value = ''; done.value = true
    await swal.toast('Senha redefinida. Entre com sua nova senha.')
    await navigateTo('/login')
  } catch (e) { error.value = e instanceof Error ? e.message : 'Não foi possível redefinir a senha. Tente novamente.' }
  finally { loading.value = false }
}
</script>
