<template>
  <div>
    <DsPageHeader title="Versões do painel" subtitle="Publicações, arquivos e documentação" icon="layers" />
    <DsPageShell>
      <div class="flex flex-wrap gap-2 mb-6">
        <DsButton variant="secondary" size="sm" icon="arrow-left" :to="`/paineis/${painelId}`">Painel</DsButton>
        <DsButton v-if="auth.isAdmin" size="sm" icon="plus-lg" :to="`/paineis/${painelId}/versoes/nova`">Nova versão</DsButton>
      </div>
      <div v-if="loading" class="py-10 text-center text-gray-500">Carregando...</div>
      <DsAlert v-else-if="error" variant="error">{{ error }}</DsAlert>
      <DsAlert v-else-if="!items.length" variant="info">Nenhuma versão cadastrada.</DsAlert>
      <DsTable v-else>
        <template #head><tr><th>Versão</th><th>Data</th><th>Publicado</th><th>Arquivos</th><th>Imagens</th><th></th></tr></template>
        <tr v-for="item in items" :key="item.codVersaoPainel">
          <td class="font-medium">{{ item.numeroVersao }}</td>
          <td>{{ formatDate(item.dataCriacao) }}</td>
          <td>{{ item.publicado ? 'Sim' : 'Não' }}</td>
          <td>{{ item.totalArquivos }}</td><td>{{ item.totalImagens }}</td>
          <td class="text-right"><DsButton size="sm" variant="secondary" :to="`/paineis/${painelId}/versoes/${item.codVersaoPainel}`">Abrir</DsButton></td>
        </tr>
      </DsTable>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { VersaoResumo } from '~/composables/usePaineisVersoesApi'
definePageMeta({ layout: 'default' })
const route = useRoute(); const auth = useAuthStore(); const api = usePaineisVersoesApi()
const painelId = computed(() => Number(route.params.id)); const items = ref<VersaoResumo[]>([]); const loading = ref(true); const error = ref('')
const formatDate = (value?: string) => value ? new Date(value).toLocaleString('pt-BR') : '—'
onMounted(async () => { try { items.value = (await api.list(painelId.value)).data } catch (e) { error.value = e instanceof Error ? e.message : 'Erro ao carregar versões.' } finally { loading.value = false } })
</script>
