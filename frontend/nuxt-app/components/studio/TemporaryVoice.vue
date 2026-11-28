<script setup lang="ts">
import { describeVoiceChange, type VoiceChangeSection } from '~/utils/voiceChange'
import type {VoiceSessionResponse} from '~/types/voice'
import type {RemovalBlock} from '~/utils/studioDependencies'
import type {StudioSection,StudioVariable} from '~/types/studioDraft'
import type { MeasureMove } from '~/utils/studioGesture'
const props=defineProps<{realtimeAvailable:boolean;busy:boolean;applying?:boolean;listening:boolean;speechSupported:boolean;speechError:string|null;transcript:string;download:()=>Promise<boolean>;measureNotice?:string;gesturesEnabled?:boolean}>()
const emit=defineEmits<{started:[response:VoiceSessionResponse];speak:[];stop:[];apply:[];upload:[file:File];blocked:[block:RemovalBlock];'update:transcript':[text:string]}>()
const store=useVoiceSessionStore(),api=useApi(),voiceApi=useVoiceApi()
const source=ref<'new'|'open'|'json'|'image'>('new'),sourceOpen=ref(false),creating=ref(false),error=ref(''),mode=ref('steps')
const pane=ref<{stop:()=>Promise<void>}|null>(null),active=ref(false)
const models=ref<{codModelo:number;nome:string}[]>([]),model=ref<number|null>(null)
const column=ref(1),lastBlocked=ref('')
const manualMessage = ref('')
const pendingManual=ref<{commandId:string;expectedHash:string;operation:string;itemIds:string[];sectionId?:string;beforeItemId?:string|null}|null>(null)
const choice=ref<{token:string;revision:number;candidates:StudioVariable[];sections:StudioSection[]}|null>(null)
const choiceSection=ref<string|null>(null),choiceBusy=ref(false),choiceError=ref('')
const choiceRequest=ref<{token:string;codVariavel:number;sectionId:string;commandId:string}|null>(null)
const choiceStale=computed(()=>!!choice.value&&choice.value.revision!==store.revision)
const phase = ref<'edit' | 'review'>('edit')
const workspaceHeader = ref<{ reset: () => void; focus: () => void } | null>(null)
const feedback = useVoiceFeedback()
const highlights = feedback.highlighted
const lastChange = feedback.message
function snapshot(fields: typeof store.camposScript.camposScript): VoiceChangeSection[] {
  return fields.filter(field => field.tipo === 'etiqueta').map(section => ({
    id: String(section.id), name: section.descricao || section.nome || 'Seção', column: Number(section.coluna ?? 1),
    items: fields.filter(field => field.tipo === 'numero' && field.etiquetaPai === section.nome)
      .sort((a, b) => Number(a.ordem ?? 0) - Number(b.ordem ?? 0))
      .map(field => ({ id: String(field.id), name: field.descricao || field.nome || 'Medida' }))
  }))
}
const cannotLeave = computed(() => creating.value || active.value || props.busy || props.listening || !!pendingManual.value || !!choice.value)
const gestureModel = ref<HTMLElement | null>(null)
const gesturesSuspended = computed(() => {
  if (active.value || props.listening) return 'Gestos pausados enquanto a edição por voz está ativa.'
  if (cannotLeave.value || props.applying) return 'Gestos pausados até concluir a ação em andamento.'
})
const destinations = computed(() => store.camposScript.camposScript
  .filter(field => field.tipo === 'etiqueta' && field.id != null && [1, 2].includes(Number(field.coluna ?? 1)))
  .map(field => ({ id: String(field.id), title: field.descricao || field.nome || 'Seção' })))
const recent = computed(() => store.transcriptHistory.filter(entry => entry.summary !== 'Fala recebida em tempo real').slice(0, 3))
function returnToModel() { sourceOpen.value = false; void nextTick(() => workspaceHeader.value?.focus()) }
function editing() { phase.value = 'edit'; workspaceHeader.value?.reset() }
watch(() => [store.sessionId, store.camposScript.camposScript] as const, ([id, fields], [previousId, oldFields]) => {
  if (id !== previousId) {
    column.value = 1
    mode.value = 'steps'
    feedback.clear()
    editing()
    if (id) void nextTick(() => workspaceHeader.value?.focus())
  } else if (id && JSON.stringify(fields) !== JSON.stringify(oldFields)) {
    const change = describeVoiceChange(snapshot(oldFields), snapshot(fields))
    feedback.show(change.ids, change.message)
    phase.value = 'review'
    workspaceHeader.value?.reset()
  }
})
function uploadAudio(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (file && !cannotLeave.value) emit('upload', file)
}
function receiveChoice(value:any){if(choice.value||value.sessionId!==store.sessionId)return;choice.value=value;choiceSection.value=value.sections?.length===1?value.sections[0].id:null;choiceError.value='';choiceRequest.value=null}
async function includeChoice(code:number){
  if(!choice.value||!choiceSection.value||choiceBusy.value||(!choiceRequest.value&&choiceStale.value))return
  choiceRequest.value??={token:choice.value.token,codVariavel:code,sectionId:choiceSection.value,commandId:crypto.randomUUID()}
  choiceBusy.value=true;choiceError.value=''
  try{await pane.value?.stop();await api.post(`/api/voice/sessions/${store.sessionId}/manual/escolhas`,choiceRequest.value);store.applySession(await voiceApi.getSession(store.sessionId!));choice.value=null;choiceRequest.value=null}
  catch(e){choiceError.value=(e instanceof Error?e.message:'Não foi possível incluir.')+' As sugestões foram mantidas; tentar novamente usa a mesma operação.'}
  finally{choiceBusy.value=false}
}
async function cancelChoice(){if(!choice.value||choiceBusy.value)return;choiceBusy.value=true;try{if(choiceRequest.value)store.applySession(await voiceApi.getSession(store.sessionId!));await api.post(`/api/voice/sessions/${store.sessionId}/manual/escolhas/cancelar`,{token:choice.value.token});choice.value=null;choiceRequest.value=null;choiceError.value=''}catch(e){choiceError.value=e instanceof Error?e.message:'Não foi possível cancelar.'}finally{choiceBusy.value=false}}
onMounted(async()=>{try{models.value=(await api.get<{data:typeof models.value}>('/api/web/modelos?pageSize=100')).data}catch{}})
async function createSession(image?:File){creating.value=true;error.value='';try{const response=await voiceApi.createSession(image?'fromImage':'fromScratch',image,source.value==='open'?model.value??undefined:undefined);store.applySession(response);sourceOpen.value=false;emit('started',response)}catch(e){error.value=e instanceof Error?e.message:'Não foi possível abrir o modelo.'}finally{creating.value=false}}
function imported(result:{session?:VoiceSessionResponse}){if(result.session){store.applySession(result.session);sourceOpen.value=false;emit('started',result.session)}}
async function switchMode(value:string){if(value===mode.value||choice.value||pendingManual.value||creating.value||props.busy)return;await pane.value?.stop();emit('stop');active.value=false;mode.value=value;editing()}
async function image(event:Event){const input=event.target as HTMLInputElement,file=input.files?.[0];input.value='';if(file)await createSession(file)}
async function remove(sourceId: string) {
  const result = await useStudioSwal().confirm('Remover medida?', 'A medida será removida deste modelo.')
  if (result.isConfirmed) await manual(sourceId)
}
async function moveMeasure(value: MeasureMove) {
  if (cannotLeave.value) return
  await manual(value.sourceId, 'move', value)
}
async function manual(sourceId:string|null,operation='remove',target?:{sectionId:string;beforeItemId:string|null}){
  if(creating.value||props.busy||props.listening||!store.sessionId)return
  creating.value=true;error.value=''
  try{
    await pane.value?.stop();emit('stop')
    if(!pendingManual.value){
      const latest=await voiceApi.getSession(store.sessionId)
      if(store.documentHash&&store.documentHash!==latest.documentHash){store.applySession(latest);throw new Error('O modelo foi atualizado. Confira o conteúdo antes de repetir a ação.')}
      store.applySession(latest)
      const node=latest.dependencies?.find(n=>n.sourceId===sourceId||n.itemId===sourceId)
      if((operation==='remove'||operation==='move')&&!node)throw new Error('Não foi possível identificar esta medida. Atualize o trabalho e tente novamente.')
      pendingManual.value={commandId:crypto.randomUUID(),expectedHash:latest.documentHash!,operation,itemIds:node?[node.itemId]:[],...(target?{sectionId:target.sectionId,beforeItemId:target.beforeItemId}:{})}
    }
    const request = pendingManual.value
    const response=await api.post<VoiceSessionResponse>(`/api/voice/sessions/${store.sessionId}/manual`,request)
    store.applySession(response)
    const moved = pendingManual.value?.operation === 'move'
    pendingManual.value = null
    if (moved) {
      manualMessage.value = 'Medida movida.'
      const section = store.camposScript.camposScript.find(field => String(field.id) === request?.sectionId)
      if (section) column.value = Number(section.coluna ?? 1)
    }
  }catch(e){if((e as any)?.code==='measure_in_use'){lastBlocked.value='Alteração bloqueada: a medida necessária aos cálculos foi mantida.';emit('blocked',(e as any).details);pendingManual.value=null}else{if([400,404,409].includes((e as any)?.status))pendingManual.value=null;error.value=e instanceof Error?e.message:'Não foi possível aplicar a ação.'}}
  finally{creating.value=false}
}
function locate(block:RemovalBlock){const target=block.dependents[0]?.measure;if(!target)return;const fields=store.camposScript.camposScript;const field=fields.find(f=>String(f.id)===target.sourceId||String(f.id)===target.itemId);if(field?.coluna)column.value=Number(field.coluna);if(target.preserved){document.querySelector('[data-temporary-components]')?.parentElement?.setAttribute('open','');document.querySelector('[data-temporary-components]')?.setAttribute('open','');document.querySelector('[data-temporary-components]')?.scrollIntoView({behavior:'smooth'});return}nextTick(()=>{const element=[...document.querySelectorAll<HTMLElement>('[data-source-id]')].find(e=>e.dataset.sourceId===(target.sourceId??target.itemId));element?.classList.add('ring-2','ring-amber-500');element?.scrollIntoView({behavior:'smooth',block:'center'});element?.focus()})}
defineExpose({locate, receiveChoice})
</script>
<template>
  <div class="space-y-5">
    <StudioVoiceStart v-if="!store.sessionId || sourceOpen" v-model="source" :disabled="cannotLeave" :can-cancel="!!store.sessionId" @cancel="returnToModel">
      <button v-if="source==='new'" class="rounded border border-ds-divider p-3" :disabled="creating||busy" @click="createSession()">Começar</button>
      <div v-if="source==='open'" class="flex flex-wrap gap-3"><select v-model="model" aria-label="Modelo para abrir" class="rounded border border-ds-divider bg-ds-surface p-3"><option :value="null">Selecione um modelo</option><option v-for="m in models" :key="m.codModelo" :value="m.codModelo">{{ m.nome }}</option></select><button class="rounded border border-ds-divider p-3" :disabled="!model||creating||busy" @click="createSession()">Abrir</button></div>
      <StudioJsonImport v-if="source==='json'" destination="conventional" :disabled="creating||active||busy" @confirmed="imported" />
      <label v-if="source==='image'" class="block rounded border border-ds-divider p-3">Selecione a imagem<input type="file" accept="image/*" :disabled="creating||busy" @change="image"></label>
      <details class="mt-4 text-sm"><summary>Opções avançadas</summary><slot name="advanced" :disabled="cannotLeave" /></details>
    </StudioVoiceStart>
    <p v-if="error" role="alert" class="text-ds-danger">{{ error }}</p>
    <StudioMeasureChoice v-if="choice" :token="choice.token" :candidates="choice.candidates" :sections="choice.sections" :section="choiceSection" :busy="choiceBusy" :error="choiceError" :stale="choiceStale" :retry-code="choiceRequest?.codVariavel??null" @section="choiceSection=$event" @choose="includeChoice" @cancel="cancelChoice" />
    <div v-if="store.sessionId" v-show="!sourceOpen" class="voice-workspace">
      <StudioVoiceWorkspaceHeader :key="store.sessionId" ref="workspaceHeader"
        :title="store.sourceFileName || 'Seu modelo'" temporary :imported="store.isImported" :phase="phase"
        storage-status="Trabalho temporário — baixe antes de sair" :download="download" :open-disabled="cannotLeave"
        :disabled="cannotLeave || !store.camposScript.camposScript.length" @open="sourceOpen = true" />
      <section ref="gestureModel" class="voice-workspace-model space-y-4 rounded-xl border border-ds-divider bg-ds-surface-elevated p-3" aria-label="Seu modelo">
        <h2 class="text-lg font-semibold">Seu modelo</h2>
        <div v-if="lastChange" class="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-ds-divider p-3">
          <p role="status" class="text-sm">{{ lastChange }}</p>
          <button v-if="store.isImported" type="button" class="rounded border border-ds-divider px-3 py-2 text-sm"
            :disabled="cannotLeave" @click="manual(null, 'undo')">Desfazer última alteração</button>
        </div>
        <div class="flex gap-2 lg:hidden"><button v-for="col in [1,2]" :key="col" class="rounded border border-ds-divider p-2" :aria-pressed="column === col" @click="column = col">{{ col === 1 ? 'Coluna esquerda' : 'Coluna direita' }}</button></div>
        <div class="grid items-start gap-4 lg:grid-cols-2">
          <StudioVoiceModelPreview v-for="col in [1,2]" :key="col" friendly removable :title="col === 1 ? 'Coluna esquerda' : 'Coluna direita'"
            :class="{ 'voice-hidden-column': column !== col }" :highlighted="highlights" movable :drag-context="store.sessionId" :destinations="destinations"
            :campos="store.camposScript.camposScript.filter(f => Number(f.coluna ?? 1) === col)"
            :disabled="cannotLeave" @remove="remove" @move="moveMeasure" />
        </div>
      </section>
      <aside class="voice-workspace-assistant space-y-4 rounded-xl border border-ds-divider bg-ds-surface-elevated p-3" aria-label="Assistente de voz">
        <StudioGestureControls v-if="gesturesEnabled && !sourceOpen" :key="store.sessionId" :model="gestureModel" :revision="store.camposScriptJson" :suspended="gesturesSuspended" @move="moveMeasure" />
        <StudioRealtimeVoicePanel v-if="mode === 'continuous'" :key="store.sessionId" ref="pane" friendly :awaiting-choice="!!choice"
          @choice="receiveChoice" @active="value => { active = value; if (value) editing() }" @blocked="emit('blocked', $event)" @fallback="switchMode('steps')" />
        <StudioDsCard v-else title="Assistente de voz">
          <StudioVoiceStepControls :model-value="transcript" :listening="listening" :supported="speechSupported" :busy="busy" :applying="applying"
            :disabled="creating || !!pendingManual || !!choice" @speak="editing(); emit('speak')" @stop="emit('stop')" @apply="emit('apply')"
            @update:model-value="value => { editing(); emit('update:transcript', value) }" />
          <p v-if="speechError" role="alert" class="mt-2 text-ds-danger">{{ speechError }}</p>
          <p v-if="measureNotice" role="alert" class="mt-2 rounded border border-amber-500 p-3 text-sm">{{ measureNotice }}</p>
        </StudioDsCard>
          <p v-if="lastBlocked" role="status" class="text-sm text-amber-500">{{ lastBlocked }}</p>
          <p v-if="manualMessage" role="status" class="text-sm">{{ manualMessage }}</p>
          <div v-if="pendingManual" class="rounded border border-amber-500 p-3 text-sm"><p>A confirmação desta ação ainda não chegou. Tentar novamente usa a mesma operação.</p><button class="mt-2 rounded border border-ds-divider p-2" :disabled="creating" @click="manual(null)">Tentar novamente</button></div>
          <div v-if="store.isImported" class="flex gap-2"><button class="rounded border border-ds-divider p-2" :disabled="busy||creating||!!pendingManual||!!choice" @click="manual(null,'undo')">Desfazer</button><button class="rounded border border-ds-divider p-2" :disabled="busy||creating||!!pendingManual||!!choice" @click="manual(null,'redo')">Refazer</button></div>
        <section v-if="recent.length && mode !== 'continuous'" class="rounded-lg border border-ds-divider p-3 text-sm">
          <h2 class="mb-2 font-semibold">Últimas alterações</h2>
          <ul class="space-y-2"><li v-for="(entry, index) in recent" :key="index">{{ entry.summary }}</li></ul>
        </section>
        <details class="rounded border border-ds-divider p-4 text-sm">
          <summary>Opções avançadas</summary>
          <p v-if="realtimeAvailable" class="mt-2">Na conversação contínua, as alterações são aplicadas durante a fala. Sugestões ainda exigem sua confirmação.</p>
          <div class="mt-3 flex flex-wrap gap-2">
            <button v-if="realtimeAvailable" class="rounded border border-ds-divider p-2" :aria-pressed="mode === 'continuous'" :disabled="busy || creating || !!choice || !!pendingManual" @click="switchMode('continuous')">Conversação contínua</button>
            <button class="rounded border border-ds-divider p-2" :aria-pressed="mode === 'steps'" :disabled="busy || creating || !!choice || !!pendingManual" @click="switchMode('steps')">Gravação por etapas</button>
          </div>
          <label v-if="mode === 'steps'" class="mt-3 block">Enviar áudio <input type="file" accept="audio/*" :disabled="cannotLeave" @change="uploadAudio"></label>
          <details class="mt-3"><summary>Transcrição completa</summary><p v-for="(entry, index) in store.transcriptHistory" :key="index" class="mt-2">{{ entry.transcript }}</p></details>
          <details data-temporary-components class="mt-3"><summary>Componentes preservados e detalhes</summary><pre class="mt-2 max-h-64 overflow-auto whitespace-pre-wrap text-xs">{{ store.camposScriptJson }}</pre></details>
          <slot name="advanced" :disabled="cannotLeave" />
        </details>
      </aside>
    </div>
  </div>
</template>
