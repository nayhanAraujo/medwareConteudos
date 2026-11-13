<script setup lang="ts">
import type {StudioDraft} from '~/types/studioDraft'
const props=defineProps<{draft:StudioDraft}>()
const emit=defineEmits<{updated:[draft:StudioDraft];blocked:[block:any];active:[active:boolean]}>()
const api=useApi()
const {transcribeAudio}=useVoiceApi()
const {isSupported,isListening,transcript,start,stop,error:speechError}=useSpeechRecognition()
const busy=ref(false),error=ref(''),message=ref('')
const attempt=ref<{commandId:string;expectedRevision:number;transcript:string}|null>(null)
watch([busy,isListening,attempt],()=>emit('active',busy.value||isListening.value||!!attempt.value))
async function apply(){
  if(busy.value||!transcript.value.trim()&&!attempt.value)return
  stop();busy.value=true;error.value=''
  attempt.value??={commandId:crypto.randomUUID(),expectedRevision:props.draft.revision,transcript:transcript.value.trim()}
  try{emit('updated',await api.post<StudioDraft>(`/api/voice/rascunhos/${props.draft.id}/utterance`,attempt.value));attempt.value=null;transcript.value='';message.value='Alteração aplicada ao modelo.'}
  catch(e){if((e as any)?.code==='measure_in_use'){emit('blocked',(e as any).details);attempt.value=null;message.value='A estrutura foi mantida.'}else{if([400,404,409].includes((e as any)?.status))attempt.value=null;error.value=e instanceof Error?e.message:'Não foi possível aplicar a fala.'}}
  finally{busy.value=false}
}
async function discard(){if(busy.value)return;busy.value=true;try{emit('updated',await api.get<StudioDraft>(`/api/voice/rascunhos/${props.draft.id}`));attempt.value=null;error.value='';message.value='Estado atual conferido. Você pode gravar novamente.'}catch(e){error.value=e instanceof Error?e.message:'Falha ao conferir o modelo.'}finally{busy.value=false}}
async function upload(e:Event){const input=e.target as HTMLInputElement,file=input.files?.[0];input.value='';if(!file||busy.value||attempt.value)return;busy.value=true;try{transcript.value=await transcribeAudio(file,file.name)}catch(e){error.value=e instanceof Error?e.message:'Não foi possível transcrever.'}finally{busy.value=false}}
onBeforeUnmount(()=>{stop();emit('active',false)})
defineExpose({stop,hasPending:computed(()=>!!attempt.value)})
</script>
<template>
  <StudioDsCard title="Gravação por etapas">
    <p role="status" class="mb-3 font-semibold">{{ busy?'Aplicando alteração':isListening?'Ouvindo':'Microfone pausado' }}</p>
    <p class="mb-3 text-sm">Fale, pare a gravação e confira a frase antes de aplicar. Você também pode escrever a instrução.</p>
    <button v-if="!isListening" class="min-h-14 w-full rounded bg-ds-primary-accent p-3 text-white" :disabled="busy||!!attempt||!isSupported" @click="start">Falar alteração</button><button v-else class="min-h-14 w-full rounded border border-ds-divider p-3" @click="stop">Parar gravação</button>
    <p v-if="!isSupported" class="my-2 text-sm">Use o envio de áudio ou escreva a instrução abaixo.</p>
    <label class="my-3 block text-sm">Sua instrução<textarea v-model="transcript" rows="3" maxlength="8000" :disabled="busy||!!attempt" class="mt-2 w-full rounded border border-ds-divider bg-ds-surface p-2" placeholder="Ex.: crie uma seção chamada Valvas"/></label>
    <button class="w-full rounded border border-ds-divider p-3" :disabled="busy||isListening||!transcript.trim()&&!attempt" @click="apply">{{ attempt?'Tentar mesma alteração novamente':'Aplicar alteração' }}</button>
    <button v-if="attempt" class="mt-2 rounded border border-ds-divider p-2" :disabled="busy" @click="discard">Conferir estado e gravar novamente</button>
    <label class="my-3 block text-sm">Enviar áudio <input type="file" accept="audio/*" :disabled="busy||isListening||!!attempt" @change="upload"></label>
    <p v-if="error||speechError" role="alert" class="my-2 text-ds-danger">{{ error||speechError }}</p><p v-if="message" role="status" class="my-2 text-sm">{{ message }}</p>
    <p class="mt-2 text-xs">Para remover medidas, use o botão Remover no modelo.</p>
  </StudioDsCard>
</template>
