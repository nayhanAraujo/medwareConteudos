<script setup lang="ts">
import type {StudioDraft} from '~/types/studioDraft'
const props=defineProps<{draft:StudioDraft;disabled?:boolean}>()
const emit=defineEmits<{updated:[draft:StudioDraft];blocked:[block:any];active:[active:boolean];editing:[]}>()
const api=useApi()
const {transcribeAudio}=useVoiceApi()
const {isSupported,isListening,transcript,start,stop,error:speechError}=useSpeechRecognition()
const busy=ref(false),applying=ref(false),error=ref(''),message=ref('')
const attempt=ref<{commandId:string;expectedRevision:number;transcript:string}|null>(null)
watch([busy,isListening,attempt],()=>emit('active',busy.value||isListening.value||!!attempt.value))
async function apply(){
  if(props.disabled||busy.value||isListening.value||!transcript.value.trim()&&!attempt.value)return
  stop();busy.value=true;applying.value=true;error.value=''
  attempt.value??={commandId:crypto.randomUUID(),expectedRevision:props.draft.revision,transcript:transcript.value.trim()}
  try{emit('updated',await api.post<StudioDraft>(`/api/voice/rascunhos/${props.draft.id}/utterance`,attempt.value));attempt.value=null;transcript.value='';message.value='Alteração aplicada ao modelo.'}
  catch(e){if((e as any)?.code==='measure_in_use'){emit('blocked',(e as any).details);attempt.value=null;message.value='A estrutura foi mantida.'}else{if([400,404,409].includes((e as any)?.status))attempt.value=null;error.value=e instanceof Error?e.message:'Não foi possível aplicar a fala.'}}
  finally{applying.value=false;busy.value=false}
}
async function discard(){if(busy.value)return;busy.value=true;try{emit('updated',await api.get<StudioDraft>(`/api/voice/rascunhos/${props.draft.id}`));attempt.value=null;error.value='';message.value='Estado atual conferido. Você pode gravar novamente.'}catch(e){error.value=e instanceof Error?e.message:'Falha ao conferir o modelo.'}finally{busy.value=false}}
async function upload(e:Event){const input=e.target as HTMLInputElement,file=input.files?.[0];input.value='';if(!file||busy.value||attempt.value)return;busy.value=true;try{transcript.value=await transcribeAudio(file,file.name)}catch(e){error.value=e instanceof Error?e.message:'Não foi possível transcrever.'}finally{busy.value=false}}
onBeforeUnmount(()=>{stop();emit('active',false)})
defineExpose({stop,hasPending:computed(()=>!!attempt.value||busy.value)})
</script>
<template>
  <StudioDsCard title="Assistente de voz">
    <StudioVoiceStepControls v-model="transcript" :supported="isSupported" :listening="isListening" :busy="busy" :applying="applying"
      :disabled="disabled" :pending="!!attempt" @speak="emit('editing'); start()" @stop="stop" @apply="apply"
      @update:model-value="emit('editing')" />
    <button v-if="attempt" class="mt-2 rounded border border-ds-divider p-2" :disabled="busy" @click="discard">Conferir estado e gravar novamente</button>
    <details class="my-3 text-sm"><summary>Opções de entrada</summary><label class="mt-2 block">Enviar áudio <input type="file" accept="audio/*" :disabled="disabled||busy||isListening||!!attempt" @change="upload"></label></details>
    <p v-if="error||speechError" role="alert" class="my-2 text-ds-danger">{{ error||speechError }}</p><p v-if="message" role="status" class="my-2 text-sm">{{ message }}</p>
    <p class="mt-2 text-xs">Para remover medidas, use o botão Remover no modelo.</p>
  </StudioDsCard>
</template>
