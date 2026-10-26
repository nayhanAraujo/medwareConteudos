<template>
  <div class="mt-4">
    <DsButton variant="secondary" icon="envelope" aria-label="Enviar proposta por e-mail" :loading="busy" :disabled="disabled || busy" @click="preview">Enviar proposta por e-mail</DsButton>
    <DsAlert v-if="notice" variant="success" class="mt-3" role="status" aria-live="polite">{{ notice }}</DsAlert>
    <p v-if="error" role="alert" class="text-sm text-red-700 mt-2">{{ error }}</p>
    <dialog ref="dialog" class="email-dialog" aria-labelledby="proposal-email-title" @cancel.prevent="close">
      <form class="p-5 space-y-4" @submit.prevent="send">
        <h2 id="proposal-email-title" class="text-lg font-semibold">Enviar proposta aprovada · r{{ revision }}</h2>
        <fieldset :disabled="busy || attempted" class="space-y-3">
          <label class="flex gap-2 text-sm"><input v-model="target" type="radio" value="client" :disabled="!clientEmail">{{ fixedRecipient ? 'Destinatário confirmado' : 'E-mail do cliente' }}: {{ clientEmail || 'não cadastrado nesta revisão' }}</label>
          <label v-if="!fixedRecipient" class="flex gap-2 text-sm"><input v-model="target" type="radio" value="other">Outro destinatário</label>
          <DsInput v-if="target === 'other'" id="proposal-recipient" v-model="otherEmail" type="email" label="E-mail do destinatário" :maxlength="254" required />
          <p class="text-sm break-words">Para: <strong>{{ recipient || 'Informe o destinatário' }}</strong></p>
          <p class="text-sm">Anexo: <strong>orcamento-{{ id }}-r{{ revision }}.pdf</strong>. Apenas a proposta pública aprovada será enviada.</p>
          <label class="block text-sm">Pré-visualização do texto<textarea :value="body" readonly rows="10" class="w-full border rounded-xl p-3 mt-2 text-sm" /></label>
          <p class="text-xs text-gray-500">A aceitação pelo SMTP não garante entrega ou leitura. Este envio não registra aceite nem pagamento. Use “Registrar envio” para atualizar o marco comercial. Antes de reenviar, confira o histórico e o recebimento.</p>
          <label class="flex gap-2 text-sm"><input v-model="confirmed" type="checkbox" required>Conferi o destinatário e autorizo o envio real desta proposta.</label>
        </fieldset>
        <p role="status" aria-live="polite" class="text-sm">{{ busy ? (attempted ? 'Enviando… aguarde a confirmação.' : 'Preparando pré-visualização…') : notice }}</p>
        <p v-if="error" role="alert" class="text-sm text-red-700">{{ error }}</p>
        <div class="flex flex-wrap justify-end gap-2"><DsButton variant="secondary" :disabled="busy" @click="close">Fechar</DsButton><DsButton type="submit" :loading="busy" :disabled="!confirmed || !recipient || busy || attempted">Confirmar e enviar</DsButton></div>
      </form>
    </dialog>
  </div>
</template>
<script setup lang="ts">
import type {QuoteDetail} from '~/utils/orcamentos'
const props=defineProps<{id:string;revision:number;version:string;clientEmail:string;disabled:boolean;fixedRecipient?:boolean}>()
const emit=defineEmits<{updated:[data:QuoteDetail];busy:[value:boolean]}>()
const api=useApi(),swal=useSwal(),dialog=ref<HTMLDialogElement|null>(null),busy=ref(false),error=ref(''),notice=ref(''),body=ref(''),target=ref('client'),otherEmail=ref(''),confirmed=ref(false),attempted=ref(false)
let requestId='',sendVersion='',epoch=0
const recipient=computed(()=>target.value==='client'?props.clientEmail:otherEmail.value.trim())
function running(value:boolean){busy.value=value;emit('busy',value)}
function close(){if(!busy.value)dialog.value?.close()}
async function preview(){
  if(props.disabled||busy.value)return
  const token=++epoch,quoteId=props.id,revision=props.revision
  error.value='';notice.value='';confirmed.value=false;attempted.value=false;body.value='';target.value=props.clientEmail?'client':'other';otherEmail.value='';requestId=crypto.randomUUID();running(true)
  try{
    const prepared=await api.post<{data:QuoteDetail}>(`/api/web/orcamentos/${quoteId}/proposta`,{version:props.version})
    if(token!==epoch||props.id!==quoteId||props.revision!==revision)return
    sendVersion=prepared.data.header.version
    const result=await api.get<{email:string}>(`/api/web/orcamentos/${quoteId}/proposta?revision=${revision}`)
    if(token!==epoch)return
    body.value=result.email;dialog.value?.showModal();emit('updated',prepared.data)
  }catch(e){if(token===epoch)error.value=e instanceof Error?e.message:'Não foi possível preparar a proposta.'}
  finally{if(token===epoch)running(false)}
}
async function send(){
  if(!confirmed.value||!recipient.value||busy.value||attempted.value)return
  const token=epoch;attempted.value=true;error.value='';running(true)
  try{
    const result=await api.post<{data:QuoteDetail}>(`/api/web/orcamentos/${props.id}/proposta/enviar-email`,{version:sendVersion,revision:props.revision,recipient:recipient.value,requestId})
    if(token===epoch){
      notice.value=`E-mail encaminhado com sucesso para ${recipient.value}. Aceito pelo servidor SMTP; a entrega e a leitura não são garantidas.`
      dialog.value?.close()
      emit('updated',result.data)
      // A notification failure must never turn an accepted send into an apparent SMTP failure.
      void swal.toast('E-mail encaminhado com sucesso', 'success', `Destinatário: ${recipient.value}`).catch(()=>{})
    }
  }catch(e){if(token===epoch)error.value=(e instanceof Error?e.message:'Não foi possível confirmar o envio.')+' Confira o histórico e o recebimento antes de iniciar outro envio.'}
  finally{if(token===epoch){running(false);try{const result=await api.get<{data:QuoteDetail}>(`/api/web/orcamentos/${props.id}`);if(token===epoch)emit('updated',result.data)}catch{/* Keep the original send outcome; refresh remains available. */}}}
}
watch(()=>[props.id,props.revision],()=>{epoch++;dialog.value?.close();running(false);error.value='';notice.value='';body.value=''})
onBeforeUnmount(()=>{epoch++;dialog.value?.close();emit('busy',false)})
</script>
<style scoped>
.email-dialog{position:fixed;inset:0;margin:auto;width:min(42rem,calc(100vw - 2rem));max-height:88vh;overflow:auto;border:1px solid #d1d5db;border-radius:20px;padding:0}
.email-dialog::backdrop{background:rgb(15 23 42 / .3)}
</style>
