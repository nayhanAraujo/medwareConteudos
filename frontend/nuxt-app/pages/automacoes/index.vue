<script setup lang="ts">
definePageMeta({ compactWorkspace: true })
useHead({ title: 'Automações | MDW' })
</script>
<template>
  <div class="space-y-6">
    <DsPageHeader title="Automações" subtitle="Integrações e acompanhamento das rotinas do sistema." icon="diagram-3" />
    <section class="rounded-2xl border border-slate-200 bg-white p-6 max-w-3xl">
      <h2 class="font-semibold text-xl mb-2">ChatFast</h2>
      <p class="text-slate-600 mb-5">Relatórios do bot e registros de respostas dos gestores. A operação dos cartões continua no Power Automate.</p>
      <div class="flex gap-4 flex-wrap"><NuxtLink to="/automacoes/chatfast/gestao">Gestão e importação</NuxtLink><NuxtLink to="/automacoes/chatfast/diario">Relatório diário público</NuxtLink><NuxtLink to="/automacoes/chatfast/semanal">Relatório semanal público</NuxtLink></div>
    </section>
  </div>
</template>
