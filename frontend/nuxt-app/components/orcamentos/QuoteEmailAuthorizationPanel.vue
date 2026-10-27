<template>
  <section class="mt-4 rounded-2xl border border-blue-200 p-4">
    <h3 class="font-semibold">Envio pelo bot Gerar Orçamento</h3>
    <p class="text-sm mt-2 break-words">Destinatário confirmado: {{ recipient }}. Autorizar libera uma única tentativa de envio da proposta atual. O bot poderá executá-la assim que consultar as pendências.</p>
    <DsAlert v-if="pending" variant="info" class="mt-3">Autorização pendente. Você pode revogá-la enquanto o envio não tiver sido iniciado.</DsAlert>
    <div class="flex flex-wrap gap-2 mt-3"><DsButton v-if="!pending" variant="secondary" :disabled="disabled || busy" :loading="busy" @click="authorize">Autorizar envio pelo bot</DsButton><DsButton v-else variant="secondary" :disabled="disabled || busy" :loading="busy" @click="revoke">Revogar autorização de envio</DsButton></div>
    <p v-if="error" role="alert" class="text-sm text-red-700 mt-3">{{ error }}</p>
  </section>
</template>
<script setup lang="ts">
import type {QuoteDetail} from '~/utils/orcamentos'
import {pendingQuoteEmailAuthorization} from '~/utils/orcamentoEmail'
const props=defineProps<{id:string;revision:number;version:string;recipient:string;events:QuoteDetail['events'];disabled:boolean}>(),emit=defineEmits<{updated:[data:QuoteDetail];busy:[value:boolean]}>()
const api=useApi(),swal=useSwal(),busy=ref(false),error=ref('');let epoch=0
const latest=computed(()=>props.events.find(e=>e.revision===props.revision&&e.action==='email_autorizado'))
const pending=computed(()=>!!pendingQuoteEmailAuthorization(props.events,props.revision))
async function run(revoke:boolean){
  if(props.disabled||busy.value)return
  if(!(await swal.confirm(revoke?'Revogar o envio pendente?':'Autorizar o bot a enviar esta proposta?',revoke?'A revogação só funciona antes do início do envio.':`O PDF aprovado será enviado para ${props.recipient}. Confira o destinatário e o histórico para evitar reenvio.`)).isConfirmed)return
  const token=epoch;busy.value=true;emit('busy',true);error.value=''
  try{
    let result:{data:QuoteDetail}
    if(revoke){result=await api.post(`/api/web/orcamentos/${props.id}/origem-email/revogar-envio`,{version:props.version,requestId:latest.value!.reference.slice(6),reason:'Revogação confirmada pelo usuário antes da execução do bot.'})}
    else{const prepared=await api.post<{data:QuoteDetail}>(`/api/web/orcamentos/${props.id}/proposta`,{version:props.version});if(token!==epoch)return;emit('updated',prepared.data);result=await api.post(`/api/web/orcamentos/${props.id}/origem-email/autorizar-envio`,{version:prepared.data.header.version,revision:props.revision,recipient:props.recipient})}
    if(token===epoch){emit('updated',result.data);void swal.toast(revoke?'Autorização revogada':'Envio pelo bot autorizado').catch(()=>{})}
  }catch(e){if(token===epoch)error.value=e instanceof Error?e.message:'Não foi possível registrar a autorização.'}
  finally{if(token===epoch){busy.value=false;emit('busy',false)}}
}
const authorize=()=>run(false),revoke=()=>run(true)
watch(()=>[props.id,props.revision],()=>{epoch++;error.value='';busy.value=false;emit('busy',false)})
onBeforeUnmount(()=>{epoch++;emit('busy',false)})
</script>
