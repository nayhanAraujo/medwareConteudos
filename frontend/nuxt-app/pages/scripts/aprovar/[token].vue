<template>
  <DsAuthShell size="lg">
    <h2 class="text-xl font-semibold font-manrope text-ds-text mb-4 flex items-center gap-2">
      <i class="bi bi-patch-check-fill text-blue-600" />Aprovação de Script
    </h2>
    <div v-if="loading" class="text-gray-500">Carregando...</div>
    <DsAlert v-else-if="status === 'expirado'" variant="warning" title="Link expirado ou inválido." />
    <DsAlert v-else-if="status === 'ja_aprovado'" variant="success">
      O modelo <strong>{{ info?.nomeScript }}</strong> já foi aprovado.
    </DsAlert>
    <div v-else-if="status === 'ok'" class="space-y-4">
      <p class="text-sm">Script: <strong>{{ info?.nomeScript }}</strong></p>
      <p class="text-xs text-gray-600">Sistema: {{ info?.sistema }} · Linguagem: {{ info?.linguagem || '—' }}</p>
      <DsAlert variant="info">
        Revise o script no link de teste antes de aprovar. Esta ação será registrada no sistema.
      </DsAlert>
      <DsButton v-if="info?.linkTeste" variant="secondary" size="sm" :href="info.linkTeste" icon="box-arrow-up-right">
        Abrir link de teste
      </DsButton>
      <form @submit.prevent="enviar('aprovar')">
        <DsButton type="submit" variant="success" block icon="hand-thumbs-up-fill" :loading="submitting" class="mb-2">
          Aprovar
        </DsButton>
      </form>
      <DsButton variant="danger" block icon="hand-thumbs-down-fill" :loading="submitting" @click="enviar('rejeitar')">
        Rejeitar
      </DsButton>
    </div>
    <DsAlert v-else-if="status === 'sucesso'" variant="success" title="Aprovado com sucesso!" />
    <DsAlert v-else-if="status === 'rejeitado'" variant="warning" title="Rejeitado." />
  </DsAuthShell>
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
