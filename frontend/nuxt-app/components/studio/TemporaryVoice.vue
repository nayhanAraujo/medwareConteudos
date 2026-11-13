<script setup lang="ts">
import type {VoiceSessionResponse} from '~/types/voice'
import type {RemovalBlock} from '~/utils/studioDependencies'
import type {StudioSection,StudioVariable} from '~/types/studioDraft'
const props=defineProps<{realtimeAvailable:boolean;busy:boolean;listening:boolean;speechSupported:boolean;speechError:string|null;transcript:string}>()
const emit=defineEmits<{started:[response:VoiceSessionResponse];speak:[];stop:[];apply:[];download:[];blocked:[block:RemovalBlock];'update:transcript':[text:string]}>()
const store=useVoiceSessionStore(),api=useApi(),voiceApi=useVoiceApi()
const source=ref('new'),sourceOpen=ref(false),creating=ref(false),error=ref(''),mode=ref(props.realtimeAvailable?'continuous':'steps')
const pane=ref<{stop:()=>Promise<void>}|null>(null),active=ref(false)
const models=ref<{codModelo:number;nome:string}[]>([]),model=ref<number|null>(null)
const column=ref(1),lastBlocked=ref('')
const pendingManual=ref<{commandId:string;expectedHash:string;operation:string;itemIds:string[]}|null>(null)
const choice=ref<{token:string;revision:number;candidates:StudioVariable[];sections:StudioSection[]}|null>(null)
const choiceSection=ref<string|null>(null),choiceBusy=ref(false),choiceError=ref('')
const choiceRequest=ref<{token:string;codVariavel:number;sectionId:string;commandId:string}|null>(null)
const choiceStale=computed(()=>!!choice.value&&choice.value.revision!==store.revision)
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
async function switchMode(value:string){if(choice.value||pendingManual.value)return;await pane.value?.stop();emit('stop');active.value=false;mode.value=value}
async function image(event:Event){const input=event.target as HTMLInputElement,file=input.files?.[0];input.value='';if(file)await createSession(file)}
async function manual(sourceId:string|null,operation='remove'){
  if(creating.value||props.busy||!store.sessionId)return
  creating.value=true;error.value=''
  try{
    await pane.value?.stop();emit('stop')
    if(!pendingManual.value){
      const latest=await voiceApi.getSession(store.sessionId)
      if(store.documentHash&&store.documentHash!==latest.documentHash){store.applySession(latest);throw new Error('O modelo foi atualizado. Confira o conteúdo antes de repetir a ação.')}
      store.applySession(latest)
      const node=latest.dependencies?.find(n=>n.sourceId===sourceId||n.itemId===sourceId)
      if(operation==='remove'&&!node)throw new Error('Não foi possível identificar esta medida. Atualize o trabalho e tente novamente.')
      pendingManual.value={commandId:crypto.randomUUID(),expectedHash:latest.documentHash!,operation,itemIds:node?[node.itemId]:[]}
    }
    const response=await api.post<VoiceSessionResponse>(`/api/voice/sessions/${store.sessionId}/manual`,pendingManual.value)
    store.applySession(response);pendingManual.value=null
  }catch(e){if((e as any)?.code==='measure_in_use'){lastBlocked.value='Alteração bloqueada: a medida necessária aos cálculos foi mantida.';emit('blocked',(e as any).details);pendingManual.value=null}else{if([400,404,409].includes((e as any)?.status))pendingManual.value=null;error.value=e instanceof Error?e.message:'Não foi possível aplicar a ação.'}}
  finally{creating.value=false}
}
function locate(block:RemovalBlock){const target=block.dependents[0]?.measure;if(!target)return;const fields=store.camposScript.camposScript;const field=fields.find(f=>String(f.id)===target.sourceId||String(f.id)===target.itemId);if(field?.coluna)column.value=Number(field.coluna);if(target.preserved){document.querySelector('[data-temporary-components]')?.setAttribute('open','');document.querySelector('[data-temporary-components]')?.scrollIntoView({behavior:'smooth'});return}nextTick(()=>{const element=[...document.querySelectorAll<HTMLElement>('[data-source-id]')].find(e=>e.dataset.sourceId===(target.sourceId??target.itemId));element?.classList.add('ring-2','ring-amber-500');element?.scrollIntoView({behavior:'smooth',block:'center'});element?.focus()})}
defineExpose({locate})
</script>
<template>
  <div class="space-y-5">
    <StudioDsCard v-if="!store.sessionId||sourceOpen" title="Como deseja começar?">
      <div class="mb-4 grid gap-3 sm:grid-cols-4"><button v-for="option in [{id:'new',label:'Novo modelo'},{id:'open',label:'Abrir modelo'},{id:'json',label:'Importar JSON'},{id:'image',label:'Usar imagem'}]" :key="option.id" class="rounded-xl border p-4 text-left font-semibold" :class="source===option.id?'border-blue-500':'border-ds-divider'" :disabled="creating||active||busy" @click="source=option.id">{{ option.label }}</button></div>
      <button v-if="source==='new'" class="rounded border border-ds-divider p-3" :disabled="creating||busy" @click="createSession()">Começar modelo</button>
      <div v-if="source==='open'" class="flex flex-wrap gap-3"><select v-model="model" aria-label="Modelo para abrir" class="rounded border border-ds-divider bg-ds-surface p-3"><option :value="null">Selecione um modelo</option><option v-for="m in models" :key="m.codModelo" :value="m.codModelo">{{ m.nome }}</option></select><button class="rounded border border-ds-divider p-3" :disabled="!model||creating||busy" @click="createSession()">Abrir como trabalho temporário</button></div>
      <StudioJsonImport v-if="source==='json'" destination="conventional" :disabled="creating||active||busy" @confirmed="imported" />
      <label v-if="source==='image'" class="block rounded border border-ds-divider p-3">Selecione a imagem<input type="file" accept="image/*" :disabled="creating||busy" @change="image"></label>
    </StudioDsCard>
    <p v-if="error" role="alert" class="text-ds-danger">{{ error }}</p>
    <template v-if="store.sessionId">
      <header class="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-ds-divider bg-ds-surface-elevated p-4"><div><h1 class="text-2xl font-semibold">{{ store.sourceFileName||'Seu modelo' }}</h1><p role="status" class="mt-2 text-sm text-amber-500">Trabalho temporário — baixe o modelo antes de sair.</p></div><div class="flex flex-wrap gap-2"><button class="rounded border border-ds-divider p-3" :disabled="busy||creating||active||listening||!!pendingManual||!!choice" @click="sourceOpen=!sourceOpen">Abrir outro modelo</button><button class="rounded bg-ds-primary-accent p-3 text-white" :disabled="busy||creating||active||!store.camposScript.camposScript.length" @click="emit('download')">Baixar modelo</button></div></header>
      <div class="temporary-workspace">
        <main class="min-w-0 space-y-4"><h2 class="text-lg font-semibold">Seu modelo</h2><div class="flex gap-2 lg:hidden"><button v-for="col in [1,2]" :key="col" class="rounded border border-ds-divider p-2" :aria-pressed="column===col" @click="column=col">Coluna {{ col }}</button></div><div class="grid items-start gap-4 lg:grid-cols-2"><StudioVoiceModelPreview v-for="col in [1,2]" :key="col" friendly removable :title="col===1?'Coluna esquerda':'Coluna direita'" :class="{'temporary-hidden-column':column!==col}" :campos="store.camposScript.camposScript.filter(f=>Number(f.coluna??1)===col)" :disabled="busy||creating||!!pendingManual||!!choice" @remove="manual($event)" /></div><details data-temporary-components class="rounded border border-ds-divider p-4 text-sm"><summary>Componentes preservados e detalhes do modelo</summary><pre class="mt-3 max-h-64 overflow-auto whitespace-pre-wrap text-xs">{{ store.camposScriptJson }}</pre></details></main>
        <aside class="temporary-assistant space-y-4">
          <StudioRealtimeVoicePanel v-if="mode==='continuous'" ref="pane" friendly :awaiting-choice="!!choice" @choice="receiveChoice" @active="active=$event" @blocked="emit('blocked',$event)" @fallback="switchMode('steps')" />
          <StudioDsCard v-else title="Gravação por etapas"><p role="status" class="mb-3 font-semibold">{{ busy?'Aplicando alteração':listening?'Ouvindo':'Microfone pausado' }}</p><p class="mb-3 text-sm">Fale a alteração, pare a gravação e confira a frase antes de aplicar.</p><button class="min-h-14 w-full rounded bg-ds-primary-accent p-3 text-white" :disabled="busy||creating||(!speechSupported&&!listening)" @click="listening?emit('stop'):emit('speak')">{{ listening?'Parar gravação':'Falar alteração' }}</button><label class="my-3 block text-sm">Sua instrução<textarea :value="transcript" rows="3" class="mt-2 w-full rounded border border-ds-divider bg-ds-surface p-2" :disabled="busy" @input="emit('update:transcript',($event.target as HTMLTextAreaElement).value)"/></label><button class="w-full rounded border border-ds-divider p-3" :disabled="busy||listening||!transcript.trim()" @click="emit('apply')">Aplicar alteração</button><p v-if="speechError" role="alert" class="mt-2 text-ds-danger">{{ speechError }}</p></StudioDsCard>
          <StudioMeasureChoice v-if="choice" :token="choice.token" :candidates="choice.candidates" :sections="choice.sections" :section="choiceSection" :busy="choiceBusy" :error="choiceError" :stale="choiceStale" :retry-code="choiceRequest?.codVariavel??null" @section="choiceSection=$event" @choose="includeChoice" @cancel="cancelChoice" />
          <p v-if="lastBlocked" role="status" class="text-sm text-amber-500">{{ lastBlocked }}</p>
          <div v-if="pendingManual" class="rounded border border-amber-500 p-3 text-sm"><p>A confirmação desta ação ainda não chegou. Tentar novamente usa a mesma operação.</p><button class="mt-2 rounded border border-ds-divider p-2" :disabled="creating" @click="manual(null)">Tentar novamente</button></div>
          <div v-if="store.isImported" class="flex gap-2"><button class="rounded border border-ds-divider p-2" :disabled="busy||creating||!!pendingManual||!!choice" @click="manual(null,'undo')">Desfazer</button><button class="rounded border border-ds-divider p-2" :disabled="busy||creating||!!pendingManual||!!choice" @click="manual(null,'redo')">Refazer</button></div>
          <details class="rounded border border-ds-divider p-4 text-sm"><summary>Opções de edição</summary><div class="mt-3 flex flex-wrap gap-2"><button v-if="realtimeAvailable" class="rounded border border-ds-divider p-2" @click="switchMode('continuous')">Conversação contínua</button><button class="rounded border border-ds-divider p-2" @click="switchMode('steps')">Gravação por etapas</button></div></details>
          <details class="rounded border border-ds-divider p-4 text-sm"><summary>Transcrição e últimas alterações</summary><div v-for="(h,i) in store.transcriptHistory" :key="i" class="mt-3"><p>{{ h.transcript }}</p><small>{{ h.summary }}</small></div></details>
        </aside>
      </div>
    </template>
  </div>
</template>
<style scoped>
.temporary-workspace{display:grid;gap:1.25rem;grid-template-columns:minmax(0,1fr)}.temporary-assistant{min-width:0;align-self:start}
@media(min-width:1280px){.temporary-workspace{grid-template-columns:minmax(0,1fr) minmax(320px,380px)}.temporary-assistant{position:sticky;top:1rem;max-height:calc(100vh - 2rem);overflow:auto}}
@media(max-width:1279px){.temporary-assistant{grid-row:1}}
@media(max-width:1023px){.temporary-assistant{position:sticky;top:0;z-index:20;max-height:45vh;overflow:auto;background:var(--color-ds-surface)}.temporary-hidden-column{display:none}}
</style>
