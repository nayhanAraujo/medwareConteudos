<template>
  <div>
    <DsPageHeader title="Clientes do Admin" subtitle="Consulta oficial e importação controlada, sem substituir registros locais." icon="building" />
    <DsButton to="/biblioteca/orcamentos" variant="secondary" class="mb-4">Voltar aos orçamentos</DsButton>
    <DsAlert v-if="!allowed" variant="warning">Seu perfil não pode importar clientes do Admin.</DsAlert>
    <template v-else>
      <DsAlert variant="info" class="mb-4">Cliente não identificado no e-mail deve permanecer pendente. Não utilize o cliente padrão nem escolha somente pelo nome. A consulta não cadastra o cliente nem cria um orçamento.</DsAlert>
      <DsPageShell><form class="space-y-4" @submit.prevent="consult">
        <fieldset :disabled="busy"><div class="grid md:grid-cols-2 gap-4"><DsInput id="admin-client-code" v-model="code" type="number" label="Código Admin confiável" required /><DsInput id="admin-client-cnpj" v-model="cnpj" label="CNPJ esperado (opcional, para conferir)" :maxlength="18" /></div></fieldset>
        <DsButton type="submit" :loading="busy" :disabled="busy || !code">Consultar dados oficiais</DsButton>
      </form></DsPageShell>
      <DsAlert v-if="error" variant="error" role="alert" class="mt-4">{{ error }}</DsAlert>
      <p v-if="busy" role="status" aria-live="polite" class="mt-3">{{ importing ? 'Importando e reconferindo a identidade…' : 'Consultando a identidade…' }}</p>
      <DsPageShell v-if="preview" class="mt-4">
        <DsAlert :variant="preview.resolution.canImport ? 'info' : preview.resolution.state === 'encontrado_local' ? 'success' : 'warning'">{{ preview.resolution.message }}</DsAlert>
        <dl v-if="preview.resolution.candidate" class="mt-4 text-sm space-y-2 break-words"><div>Código: <strong>{{ preview.resolution.candidate.codigoAdmin }}</strong></div><div>Nome oficial: <strong>{{ preview.resolution.candidate.name }}</strong></div><div>CNPJ: <strong>{{ preview.resolution.candidate.cnpj }}</strong></div><div>Estado no Admin: <strong>{{ preview.resolution.candidate.active ? 'Ativo' : 'Inativo' }}</strong></div></dl>
        <form v-if="canImportPreview" class="mt-4 space-y-4" @submit.prevent="importClient">
          <p class="text-xs text-gray-500">Consulta válida até {{ quoteDate(preview.expiresAtUtc!) }}. O servidor consultará o Admin novamente e impedirá conflitos. Código, nome e CNPJ não são editáveis nesta confirmação.</p>
          <fieldset :disabled="busy || attempted" class="space-y-3"><DsInput id="admin-client-reason" v-model="reason" label="Justificativa da importação ou vinculação" :maxlength="1000" required /><label class="flex gap-2 text-sm"><input v-model="confirmed" type="checkbox" required>Conferi o código, o CNPJ e o cliente solicitante. Autorizo o cadastro/vínculo sem sobrescrever registros existentes.</label></fieldset>
          <p class="text-xs text-gray-500">Mensalidade, condições comerciais e destinatário do orçamento não serão importados.</p>
          <DsButton type="submit" :loading="busy" :disabled="busy || attempted || !confirmed || !reason.trim()">Confirmar importação do cliente</DsButton>
        </form>
      </DsPageShell>
      <DsAlert v-if="success" variant="success" class="mt-4" role="status">{{ success }} O bot poderá retomar a mensagem pendente. O orçamento ainda exigirá confirmação humana do escopo e destinatário.</DsAlert>
    </template>
  </div>
</template>
<script setup lang="ts">
import {quoteDate} from '~/utils/orcamentos'
import {canImportAdminClient,adminClientImportPayload,type AdminClientPreviewData as Preview} from '~/utils/adminClients'
definePageMeta({layout:'default'})
const api=useApi(),auth=useAuthStore(),swal=useSwal(),allowed=computed(()=>auth.can('orcamentos','visualizar')&&auth.can('orcamentos','importar_cliente_admin'))
const code=ref(''),cnpj=ref(''),reason=ref(''),confirmed=ref(false),attempted=ref(false),busy=ref(false),importing=ref(false),error=ref(''),success=ref(''),preview=ref<Preview|null>(null)
let epoch=0,alive=true
const canImportPreview=computed(()=>canImportAdminClient(preview.value)&&!success.value)
watch([code,cnpj],()=>{if(!busy.value){epoch++;preview.value=null;confirmed.value=false;error.value='';success.value=''}})
async function consult(){
  if(busy.value||!allowed.value)return
  const codigoAdmin=Number(code.value);if(!Number.isSafeInteger(codigoAdmin)||codigoAdmin<=0){error.value='Informe um código Admin positivo.';return}
  const token=++epoch;busy.value=true;error.value='';success.value='';preview.value=null;confirmed.value=false;attempted.value=false;reason.value=''
  try{const r=await api.post<{data:Preview}>('/api/web/orcamentos/clientes/admin/consultar',{codigoAdmin,cnpj:cnpj.value.trim()||null});if(alive&&token===epoch)preview.value=r.data}
  catch(e){if(alive&&token===epoch)error.value=e instanceof Error?e.message:'Não foi possível consultar.'}
  finally{if(alive&&token===epoch)busy.value=false}
}
async function importClient(){
  if(busy.value||attempted.value||!confirmed.value||!canImportPreview.value||!allowed.value)return
  const token=epoch;busy.value=true;importing.value=true;attempted.value=true;error.value=''
  try{const r=await api.post<{data:{id:number;codigoAdmin:number;created:boolean}}>('/api/web/orcamentos/clientes/importar-admin',adminClientImportPayload(preview.value,reason.value,confirmed.value));if(alive&&token===epoch){success.value=`Cliente #${r.data.id} ${r.data.created?'importado':'vinculado/verificado'} com sucesso.`;void swal.toast('Cliente Admin confirmado').catch(()=>{})}}
  catch(e){if(alive&&token===epoch)error.value=(e instanceof Error?e.message:'Não foi possível confirmar a importação.')+' Consulte novamente e confira o catálogo antes de repetir.'}
  finally{if(alive&&token===epoch){busy.value=false;importing.value=false}}
}
onBeforeRouteLeave(()=>!busy.value)
watch(()=>auth.token,()=>{epoch++;preview.value=null;error.value='';success.value='';busy.value=false})
onBeforeUnmount(()=>{alive=false;epoch++})
</script>
