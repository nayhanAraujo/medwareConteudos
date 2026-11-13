<script setup lang="ts">
import {studioActionSummary} from '~/utils/studioActionSummary'
import type {StudioVariable} from '~/types/studioDraft'
const props = defineProps<{ friendly?: boolean; awaitingChoice?: boolean; sectionName?: string; catalog?: StudioVariable[]; draftId?: string; context?: { revision: number; sectionId: string | null; itemId: string | null } }>()
const emit = defineEmits<{ active: [value: boolean]; updated: [draft: any]; choice: [choice: any]; blocked: [block:any]; fallback: [] }>()
const { connected, listening, starting, saving, speaking, processing, error, activity, proposal, start, pause, listen, stop, confirmSave, setContext } = useRealtimeVoice({ interactiveChoices: props.friendly, draftId: () => props.draftId, onDraft: draft => emit('updated', draft), onChoice: choice => emit('choice', choice), onBlocked: block=>emit('blocked',block), summarize: (name,result)=>props.friendly ? studioActionSummary(name,result,props.catalog) : '' })
const status = computed(()=>starting.value?'Conectando':error.value&&!connected.value?'Falha de conexão':props.awaitingChoice?'Aguardando sua escolha':processing.value?'Aplicando alteração':connected.value&&listening.value?'Ouvindo':connected.value?'Microfone pausado':'Pronto para ouvir')
async function fallback(){await stop();emit('fallback')}
defineExpose({stop})
const models = ref<Array<{ codModelo: number; nome: string }>>([])
const voiceStore = useVoiceSessionStore()
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
  <StudioDsCard :title="friendly ? 'Assistente de voz' : 'Voz em tempo real · xAI'">
    <p class="mb-3 text-sm text-ds-muted">{{ friendly ? 'Fale a alteração desejada, com pausas naturais, e confira o resultado no modelo.' : 'Fale com pausas naturais. O agente organiza o rascunho enquanto continua ouvindo. Salvar no banco exige sua confirmação.' }}</p>
    <p v-if="friendly" role="status" aria-live="polite" class="mb-3 rounded-lg border border-ds-divider p-3 font-semibold">{{ status }}</p>
    <p v-if="friendly && sectionName" class="mb-3 text-sm">Seção selecionada: <strong>{{ sectionName }}</strong></p>
    <label v-if="!draftId && !voiceStore.isImported" class="block text-sm text-ds-text-secondary">Modelo de destino para salvamento (opcional)
      <select v-model="target" :disabled="connected || starting" class="mt-1 mb-3 w-full rounded border border-ds-divider bg-ds-surface p-2">
        <option :value="null">Somente rascunho</option>
        <option v-for="model in models" :key="model.codModelo" :value="model.codModelo">{{ model.nome }}</option>
      </select>
    </label>
    <p v-if="target" class="mb-3 text-xs text-ds-muted">Ao confirmar um salvamento, a composição do modelo selecionado será substituída pelo rascunho atual.</p>
    <div class="flex flex-wrap gap-2">
      <StudioDsButton v-if="!connected" :loading="starting" icon="bi-mic" :class="friendly?'w-full min-h-14 text-lg':''" @click="start(target)">{{ friendly ? 'Iniciar edição por voz' : 'Conectar agente' }}</StudioDsButton>
      <template v-else>
        <StudioDsButton v-if="listening" variant="secondary" @click="pause">Pausar microfone</StudioDsButton>
        <StudioDsButton v-else variant="success" @click="listen">Retomar microfone</StudioDsButton>
        <StudioDsButton variant="ghost" @click="stop">Encerrar e cancelar pendências</StudioDsButton>
      </template>
    </div>
    <p v-if="connected" role="status" class="mt-3 text-sm text-ds-text">{{ speaking ? 'Ouvindo sua fala…' : listening ? 'Microfone aberto — aguardando fala' : 'Microfone pausado' }}</p>
    <p v-if="error" role="alert" class="mt-3 text-sm text-ds-danger">{{ error }}</p>
    <button v-if="friendly && error" class="mt-3 rounded border border-ds-divider p-2" @click="fallback">Usar gravação por etapas</button>
    <details v-if="friendly" class="mt-3 text-sm"><summary>Exemplos do que dizer</summary><ul class="mt-2 space-y-2"><li>“Crie uma seção chamada Valvas.”</li><li>“Adicione Altura em Dados do paciente.”</li><li>“Mova Peso para Dados do paciente.”</li></ul><p class="mt-2">Para remover medidas, use o botão Remover no modelo.</p></details>
    <div v-if="proposal" class="mt-4 rounded border border-amber-500 p-3">
      <p class="text-sm">Salvar “{{ proposal.nome }}” no modelo #{{ proposal.modeloId }}? Isso substitui sua composição salva pela revisão {{ proposal.revision }} deste rascunho.</p>
      <StudioDsButton class="mt-2" :loading="saving" @click="confirmSave">Confirmar salvamento no banco</StudioDsButton>
      <StudioDsButton variant="ghost" :disabled="saving" @click="proposal = null">Não salvar</StudioDsButton>
    </div>
    <ul aria-live="polite" class="mt-3 space-y-1 text-sm text-ds-muted"><li v-for="(item, i) in activity.slice(0,friendly?4:15)" :key="i">{{ item }}</li></ul>
  </StudioDsCard>
</template>
