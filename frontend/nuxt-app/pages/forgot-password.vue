<template>
  <DsAuthShell background-image="/images/auth/login-bg.webp">
    <h2 class="text-xl font-semibold font-manrope text-ds-text mb-6">Recuperar senha</h2>
    <form class="space-y-4" @submit.prevent="onSubmit">
      <DsInput v-model="identificacao" label="Identificação (login)" required />
      <DsInput v-model="newPassword" label="Nova senha" type="password" required />
      <DsInput v-model="confirmPassword" label="Confirmar senha" type="password" required />
      <DsButton type="submit" block>Alterar senha</DsButton>
    </form>
    <p class="text-center mt-4 mb-0 text-sm">
      <NuxtLink to="/login" class="text-blue-600 hover:underline">Voltar ao login</NuxtLink>
    </p>
  </DsAuthShell>
</template>

<script setup lang="ts">
definePageMeta({ layout: false })

const identificacao = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const api = useApi()
const swal = useSwal()
const router = useRouter()

async function onSubmit() {
  try {
    await api.post('/api/web/auth/forgot-password', {
      identificacao: identificacao.value,
      newPassword: newPassword.value,
      confirmPassword: confirmPassword.value
    })
    await swal.toast('Senha alterada. Faça login.')
    await router.push('/login')
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  }
}
</script>
