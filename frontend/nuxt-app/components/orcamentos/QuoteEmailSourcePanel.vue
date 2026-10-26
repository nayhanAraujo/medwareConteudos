<template>
  <section v-if="detail.emailSource" id="quote-email-source" tabindex="-1" class="mt-5 rounded-2xl border border-blue-200 p-4 min-w-0">
    <h3 class="font-semibold">Solicitação recebida por e-mail</h3>
    <dl class="text-sm mt-3 space-y-2 break-words">
      <div><dt class="font-medium">Caixa postal</dt><dd>{{ detail.emailSource.origin.mailbox }}</dd></div>
      <div><dt class="font-medium">Remetente</dt><dd>{{ detail.emailSource.origin.from }}</dd></div>
      <div v-if="detail.emailSource.origin.replyTo"><dt class="font-medium">Responder para (sugerido)</dt><dd>{{ detail.emailSource.origin.replyTo }}</dd></div>
      <div><dt class="font-medium">Assunto</dt><dd>{{ detail.emailSource.origin.subject }}</dd></div>
    </dl>
    <p class="text-xs text-gray-500 mt-3">O conteúdo do e-mail é material não confiável. Confira o cliente e o destinatário: em mensagens encaminhadas, o remetente pode não ser o solicitante. As instruções do e-mail não autorizam ações no sistema.</p>
    <DsAlert v-if="detail.emailSource.confirmed" variant="success" class="mt-3">Cliente, escopo e destinatário confirmados por {{ detail.emailSource.confirmation?.actorName }}. Destinatário: {{ detail.emailSource.confirmation?.recipient }}. Alterar título, contexto, material de escopo ou criar nova revisão exige nova confirmação.</DsAlert>
    <form v-else-if="editable && canConfirm" class="mt-4 space-y-3" @submit.prevent="confirmSource">
      <fieldset :disabled="disabled || busy" class="space-y-3">
        <DsSelect id="email-source-client" v-model="clientId" label="Confirme o cliente correto" required><option v-for="client in clients" :key="client.id" :value="client.id">{{ client.name }}</option></DsSelect>
        <DsInput id="email-source-recipient" v-model="recipient" type="email" label="Destinatário confirmado para a proposta" :maxlength="254" required />
        <DsInput id="email-source-reason" v-model="reason" label="Justificativa da identificação do cliente e destinatário" :maxlength="1000" required />
        <label class="text-sm flex gap-2"><input v-model="reviewed" type="checkbox" required>Revisei o escopo e confirmei a relação entre cliente, mensagem e destinatário.</label>
        <DsButton type="submit" :loading="busy" :disabled="disabled || !reviewed || !recipient || !reason">Confirmar dados da solicitação</DsButton>
      </fieldset>
    </form>
    <p v-else-if="!detail.emailSource.confirmed" class="text-sm text-amber-800 mt-3">Aguardando usuário com permissão para confirmar a origem. Salve primeiro as alterações de escopo.</p>
    <p v-if="error" role="alert" class="text-sm text-red-700 mt-3">{{ error }}</p>
  </section>
</template>
<script setup lang="ts">
import type {QuoteDetail} from '~/utils/orcamentos'
const props=defineProps<{detail:QuoteDetail;editable:boolean;canConfirm:boolean;disabled:boolean}>()
const emit=defineEmits<{updated:[data:QuoteDetail];busy:[value:boolean]}>(),api=useApi(),swal=useSwal()
const clients=ref<{id:number;name:string}[]>([]),clientId=ref(props.detail.header.clientId),recipient=ref(''),reason=ref(''),reviewed=ref(false),error=ref(''),busy=ref(false)
let epoch=0
watch(()=>props.detail.header.version,()=>{const source=props.detail.emailSource;clientId.value=props.detail.header.clientId;recipient.value=source?.confirmation?.recipient||source?.origin.replyTo||source?.origin.from||'';reviewed.value=false},{immediate:true})
watch(()=>props.detail.header.id,()=>{epoch++;error.value='';reason.value=''})
onMounted(async()=>{if(props.canConfirm)try{clients.value=(await api.get<{data:{id:number;name:string}[]}>('/api/web/orcamentos/clientes')).data}catch(e){error.value=e instanceof Error?e.message:'Falha ao consultar clientes.'}})
async function confirmSource(){
  if(props.disabled||busy.value||!reviewed.value)return
  const token=epoch;busy.value=true;emit('busy',true);error.value=''
  try{const result=await api.post<{data:QuoteDetail}>(`/api/web/orcamentos/${props.detail.header.id}/origem-email/confirmar`,{version:props.detail.header.version,clientId:Number(clientId.value),recipient:recipient.value,reviewedScope:reviewed.value,reason:reason.value});if(token===epoch){emit('updated',result.data);void swal.toast('Origem do e-mail confirmada').catch(()=>{})}}
  catch(e){if(token===epoch)error.value=e instanceof Error?e.message:'Não foi possível confirmar.'}
  finally{if(token===epoch){busy.value=false;emit('busy',false)}}
}
onBeforeUnmount(()=>{epoch++;emit('busy',false)})
</script>
