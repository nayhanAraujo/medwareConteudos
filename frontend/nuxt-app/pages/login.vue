<template>
  <DsAuthShell size="sm" background-image="/images/auth/login-bg.webp">
    <div class="text-center mb-6">
      <div class="text-3xl font-bold font-manrope text-ds-text mb-2">MDW</div>
      <p class="text-sm text-gray-600">Sistema de Gestão de Conteúdos</p>
    </div>
    <form class="space-y-4" @submit.prevent="onSubmit">
      <DsInput v-model="usuario" placeholder="Usuário" autocomplete="username" required />
      <DsInput v-model="senha" type="password" placeholder="Senha" autocomplete="current-password" required />
      <DsButton type="submit" block :loading="loading" :disabled="loading">
        {{ loading ? 'Entrando...' : 'Entrar' }}
      </DsButton>
    </form>
    <p class="text-center mt-4 mb-0 text-sm">
      <NuxtLink to="/forgot-password" class="text-blue-600 hover:underline">Esqueci minha senha</NuxtLink>
    </p>
  </DsAuthShell>
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

function isLucas(nome: string, login: string) {
  const normalized = (value: string) => value.trim().toLowerCase()
  const n = normalized(nome)
  const l = normalized(login)
  return l === 'lucas' || n === 'lucas' || n.startsWith('lucas ')
}

async function onSubmit() {
  loading.value = true
  try {
    const res = await api.post<{ success: boolean; token: string; user: { codusuario: number; nome: string; role: string } }>(
      '/api/web/auth/login',
      { usuario: usuario.value, senha: senha.value }
    )

    if (isLucas(res.user.nome, usuario.value)) {
      const ruleResult = await swal.designSystemRuleAcceptance(
        'Olá Lucas!',
        'O seu mestre Nayhan pediu para informar que você deve utilizar uma rule no Cursor para criar os próximos templates, utilizando o design system definido pelo mestre.'
      )
      if (!ruleResult.isConfirmed) {
        await swal.warning(
          'Acesso não permitido',
          'Você só entrará no sistema após aceitar essa regra.'
        )
        return
      }
    }

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
