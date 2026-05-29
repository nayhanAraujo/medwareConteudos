<template>
  <div class="min-vh-100 d-flex align-items-center justify-content-center bg-secondary bg-opacity-10">
    <div class="card shadow" style="width: 420px">
      <div class="card-body p-4">
        <h4 class="mb-3">Recuperar senha</h4>
        <form @submit.prevent="onSubmit">
          <div class="mb-3">
            <label class="form-label">Identificação (login)</label>
            <input v-model="identificacao" class="form-control" required />
          </div>
          <div class="mb-3">
            <label class="form-label">Nova senha</label>
            <input v-model="newPassword" type="password" class="form-control" required minlength="6" />
          </div>
          <div class="mb-3">
            <label class="form-label">Confirmar senha</label>
            <input v-model="confirmPassword" type="password" class="form-control" required />
          </div>
          <button class="btn btn-primary w-100" type="submit">Alterar senha</button>
        </form>
        <p class="text-center mt-3 mb-0"><NuxtLink to="/login">Voltar ao login</NuxtLink></p>
      </div>
    </div>
  </div>
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

