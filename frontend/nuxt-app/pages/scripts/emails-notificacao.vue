<template>
  <div>
    <LayoutAppPageHeader title="E-mails de Notificação" icon="envelope" />
    <div class="row g-3">
      <div class="col-lg-8">
        <div class="card shadow-sm">
          <div class="card-body">
            <form @submit.prevent="salvar">
              <label class="form-label fw-semibold">E-mails (separados por vírgula)</label>
              <textarea
                v-model="emails"
                class="form-control mb-2"
                rows="5"
                placeholder="ex: equipe@empresa.com, gestor@empresa.com"
              />
              <div class="form-text mb-3">
                Estes e-mails recebem notificações de aprovação e ações importantes dos scripts.
              </div>
              <button type="submit" class="btn btn-success" :disabled="loading">
                <i class="bi bi-check-circle-fill me-1" />Salvar
              </button>
              <NuxtLink to="/biblioteca" class="btn btn-outline-secondary ms-2">Voltar</NuxtLink>
            </form>
          </div>
        </div>
      </div>
      <div class="col-lg-4">
        <div class="card border-info shadow-sm">
          <div class="card-body">
            <h6 class="text-info fw-bold"><i class="bi bi-info-circle-fill me-2" />Regras</h6>
            <ul class="small mb-0 ps-3">
              <li>Separe múltiplos e-mails por vírgula.</li>
              <li>Evite e-mails duplicados.</li>
              <li>Use contas válidas para receber alertas.</li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default', middleware: ['admin'] })

const scriptsApi = useScriptsApi()
const swal = useSwal()
const emails = ref('')
const loading = ref(false)

onMounted(async () => {
  const res = await scriptsApi.getEmails()
  emails.value = res.emails || ''
})

async function salvar() {
  loading.value = true
  try {
    await scriptsApi.saveEmails(emails.value)
    await swal.toast('E-mails salvos!')
  } finally {
    loading.value = false
  }
}
</script>
