<script setup lang="ts">
const props = defineProps<{ draftId?: string; context?: { revision: number; sectionId: string | null; itemId: string | null } }>()
const emit = defineEmits<{ active: [value: boolean]; updated: [draft: any]; choice: [choice: any] }>()
const { connected, listening, starting, saving, speaking, error, activity, proposal, start, pause, listen, stop, confirmSave, setContext } = useRealtimeVoice({ draftId: () => props.draftId, onDraft: draft => emit('updated', draft), onChoice: choice => emit('choice', choice) })
const models = ref<Array<{ codModelo: number; nome: string }>>([])
const target = ref<number | null>(null)
watch([connected, starting], () => emit('active', connected.value || starting.value), { immediate: true })
watch([connected, () => props.context], () => { if (props.context) void setContext(props.context).catch(() => {}) }, { deep: true })
onBeforeUnmount(() => emit('active', false))
onMounted(async () => {
  try {
    const result = await useApi().get<{ data: Array<{ codModelo: number; nome: string }> }>('/api/web/modelos?pageSize=100')
    models.value = result.data || []
  } catch { /* Draft editing still works without selecting a persistence target. */ }
})
</script>

<template>
  <StudioDsCard title="Voz em tempo real · xAI">
    <p class="mb-3 text-sm text-ds-muted">Fale com pausas naturais. O agente organiza o rascunho enquanto continua ouvindo. Salvar no banco exige sua confirmação.</p>
    <label v-if="!draftId" class="block text-sm text-ds-text-secondary">Modelo de destino para salvamento (opcional)
      <select v-model="target" :disabled="connected || starting" class="mt-1 mb-3 w-full rounded border border-ds-divider bg-ds-surface p-2">
        <option :value="null">Somente rascunho</option>
        <option v-for="model in models" :key="model.codModelo" :value="model.codModelo">{{ model.nome }}</option>
      </select>
    </label>
    <p v-if="target" class="mb-3 text-xs text-ds-muted">Ao confirmar um salvamento, a composição do modelo selecionado será substituída pelo rascunho atual.</p>
    <div class="flex flex-wrap gap-2">
      <StudioDsButton v-if="!connected" :loading="starting" icon="bi-mic" @click="start(target)">Conectar agente</StudioDsButton>
      <template v-else>
        <StudioDsButton v-if="listening" variant="secondary" @click="pause">Pausar microfone</StudioDsButton>
        <StudioDsButton v-else variant="success" @click="listen">Retomar microfone</StudioDsButton>
        <StudioDsButton variant="ghost" @click="stop">Encerrar e cancelar pendências</StudioDsButton>
      </template>
    </div>
    <p v-if="connected" role="status" class="mt-3 text-sm text-ds-text">{{ speaking ? 'Ouvindo sua fala…' : listening ? 'Microfone aberto — aguardando fala' : 'Microfone pausado' }}</p>
    <p v-if="error" role="alert" class="mt-3 text-sm text-ds-danger">{{ error }}</p>
    <div v-if="proposal" class="mt-4 rounded border border-amber-500 p-3">
      <p class="text-sm">Salvar “{{ proposal.nome }}” no modelo #{{ proposal.modeloId }}? Isso substitui sua composição salva pela revisão {{ proposal.revision }} deste rascunho.</p>
      <StudioDsButton class="mt-2" :loading="saving" @click="confirmSave">Confirmar salvamento no banco</StudioDsButton>
      <StudioDsButton variant="ghost" :disabled="saving" @click="proposal = null">Não salvar</StudioDsButton>
    </div>
    <ul aria-live="polite" class="mt-3 space-y-1 text-xs text-ds-muted"><li v-for="(item, i) in activity" :key="i">{{ item }}</li></ul>
  </StudioDsCard>
</template>
