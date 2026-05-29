<template>
  <div class="login-page">
    <div class="login-container" style="max-width: 620px; text-align: left">
      <h4 class="text-info mb-3"><i class="bi bi-patch-check-fill me-2" />Aprovação de Script</h4>
      <div v-if="loading" class="text-muted">Carregando...</div>
      <div v-else-if="status === 'expirado'" class="alert alert-warning">
        <i class="bi bi-exclamation-triangle-fill me-2" />Link expirado ou inválido.
      </div>
      <div v-else-if="status === 'ja_aprovado'" class="alert alert-success">
        <i class="bi bi-check-circle-fill me-2" />O modelo <strong>{{ info?.nomeScript }}</strong> já foi aprovado.
      </div>
      <div v-else-if="status === 'ok'">
        <p class="mb-1">Script: <strong>{{ info?.nomeScript }}</strong></p>
        <p class="small text-muted">Sistema: {{ info?.sistema }} · Linguagem: {{ info?.linguagem || '—' }}</p>
        <div class="alert alert-light border small">
          Revise o script no link de teste antes de aprovar. Esta ação será registrada no sistema.
        </div>
        <p v-if="info?.linkTeste">
          <a :href="info.linkTeste" target="_blank" class="btn btn-outline-info btn-sm">
            <i class="bi bi-box-arrow-up-right me-1" />Abrir link de teste
          </a>
        </p>
        <form @submit.prevent="enviar('aprovar')">
          <button type="submit" class="btn btn-success w-100 mb-2" :disabled="submitting">
            <i class="bi bi-hand-thumbs-up-fill me-1" />Aprovar
          </button>
        </form>
        <button type="button" class="btn btn-outline-danger w-100" :disabled="submitting" @click="enviar('rejeitar')">
          <i class="bi bi-hand-thumbs-down-fill me-1" />Rejeitar
        </button>
      </div>
      <div v-else-if="status === 'sucesso'" class="alert alert-success">
        <i class="bi bi-check-circle-fill me-2" />Aprovado com sucesso!
      </div>
      <div v-else-if="status === 'rejeitado'" class="alert alert-secondary">
        <i class="bi bi-x-circle-fill me-2" />Rejeitado.
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: false })

const route = useRoute()
const token = String(route.params.token)
const scriptsApi = useScriptsApi()
const swal = useSwal()

const loading = ref(true)
const submitting = ref(false)
const status = ref<'loading' | 'expirado' | 'ja_aprovado' | 'ok' | 'sucesso' | 'rejeitado'>('loading')
const info = ref<{ nomeScript?: string; sistema?: string; linguagem?: string; linkTeste?: string } | null>(null)

onMounted(async () => {
  try {
    const res = await scriptsApi.getAprovar(token)
    if (!res.success || res.status === 'expirado') {
      status.value = 'expirado'
      return
    }
    const d = res.data as Record<string, unknown>
    info.value = {
      nomeScript: String(d.nomeScript || ''),
      sistema: String(d.sistema || ''),
      linguagem: String(d.linguagem || ''),
      linkTeste: String(d.linkTeste || '')
    }
    if (d.jaAprovado) status.value = 'ja_aprovado'
    else if (d.tokenUsado) status.value = 'expirado'
    else status.value = 'ok'
  } catch {
    status.value = 'expirado'
  } finally {
    loading.value = false
  }
})

async function enviar(acao: 'aprovar' | 'rejeitar') {
  submitting.value = true
  try {
    await scriptsApi.postAprovar(token, acao)
    status.value = acao === 'aprovar' ? 'sucesso' : 'rejeitado'
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  } finally {
    submitting.value = false
  }
}
</script>
