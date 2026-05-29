<template>
  <div class="login-page">
    <div class="login-container">
      <div class="logo-mdw">MDW</div>
      <h5 class="mb-4">Sistema de Gestão de Conteúdos</h5>
      <form @submit.prevent="onSubmit">
        <div class="mb-3">
          <input
            v-model="usuario"
            type="text"
            class="form-control bg-dark text-white border-secondary"
            placeholder="Usuário"
            required
            autocomplete="username"
          />
        </div>
        <div class="mb-3">
          <input
            v-model="senha"
            type="password"
            class="form-control bg-dark text-white border-secondary"
            placeholder="Senha"
            required
            autocomplete="current-password"
          />
        </div>
        <button class="btn btn-info w-100" type="submit" :disabled="loading">
          {{ loading ? 'Entrando...' : 'Entrar' }}
        </button>
      </form>
      <p class="text-center mt-3 mb-0">
        <NuxtLink to="/forgot-password" class="text-info">Esqueci minha senha</NuxtLink>
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: false })

const usuario = ref('')
const senha = ref('')
const loading = ref(false)
const api = useApi()
const auth = useAuthStore()
const swal = useSwal()
const router = useRouter()
const route = useRoute()

async function onSubmit() {
  loading.value = true
  try {
    const res = await api.post<{ success: boolean; token: string; user: { codusuario: number; nome: string; role: string } }>(
      '/api/web/auth/login',
      { usuario: usuario.value, senha: senha.value }
    )
    auth.setSession(res.token, res.user)
    await swal.toast(`Bem-vindo, ${res.user.nome}!`)
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/'
    await router.push(redirect.startsWith('/') ? redirect : '/')
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Falha no login', 'error')
  } finally {
    loading.value = false
  }
}
</script>
