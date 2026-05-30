<template>
  <div>
    <DsPageHeader title="E-mails de Notificação" icon="envelope" />
    <DsPageShell panel-class="!p-6 md:!p-8">
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2">
          <form @submit.prevent="salvar">
            <DsTextarea
              v-model="emails"
              label="E-mails (separados por vírgula)"
              :rows="5"
              placeholder="ex: equipe@empresa.com, gestor@empresa.com"
              hint="Estes e-mails recebem notificações de aprovação e ações importantes dos scripts."
            />
            <div class="flex gap-2 mt-4">
              <DsButton type="submit" variant="success" icon="check-circle-fill" :loading="loading">Salvar</DsButton>
              <DsButton variant="secondary" to="/biblioteca">Voltar</DsButton>
            </div>
          </form>
        </div>
        <DsAlert variant="info" title="Regras">
          <ul class="list-disc pl-4 space-y-1 text-sm">
            <li>Separe múltiplos e-mails por vírgula.</li>
            <li>Evite e-mails duplicados.</li>
            <li>Use contas válidas para receber alertas.</li>
          </ul>
        </DsAlert>
      </div>
    </DsPageShell>
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
