<template>
  <div>
    <DsPageHeader title="Configurações de Orçamentos" subtitle="Política comercial versionada e complemento dos clientes existentes." icon="gear" />
    <DsButton to="/biblioteca/orcamentos" variant="secondary" icon="arrow-left" class="mb-4">Carteira</DsButton>
    <DsButton v-if="auth.can('orcamentos','importar_cliente_admin')" to="/biblioteca/orcamentos/clientes-admin" variant="secondary" class="mb-4 ml-2">Clientes do Admin</DsButton>
    <DsAlert v-if="error" variant="error" class="mb-4">{{ error }}</DsAlert>
    <DsAlert v-if="message" variant="success" class="mb-4">{{ message }}</DsAlert>
    <QuoteTechnicalSettings v-if="auth.can('orcamentos','configurar_regras')" class="mb-4" />
    <DsPageShell v-if="auth.can('orcamentos','configurar_precos') && policy">
      <form class="space-y-4" @submit.prevent="savePolicy">
        <h2 class="text-lg font-semibold">Política comercial · versão #{{ policyVersion }}</h2><p class="text-sm text-gray-500">Não há preço-hora, validade ou pagamento presumidos. Mudanças não alteram revisões existentes. Para adotar esta política em um rascunho, use “Atualizar política e recalcular”.</p>
        <div class="grid md:grid-cols-3 gap-3"><DsInput id="quote-rate" v-model="policy.hourlyRate" label="Preço-hora comercial (R$)" type="number" step="0.01" /><DsInput id="quote-cost" v-model="policy.internalHourlyCost" label="Custo-hora interno opcional (R$)" type="number" step="0.01" /><DsInput id="quote-validity-days" v-model="policy.validityDays" label="Referência de validade (dias)" type="number" step="1" /></div>
        <label class="block text-sm">Referência de condições de pagamento<textarea v-model="policy.paymentTerms" class="quote-area" rows="3" maxlength="4000" /></label>
        <h3 class="text-base font-semibold">Regras comerciais</h3><p class="text-sm text-amber-800">Desmarcar a exigência de enquadramento manual significa que a gestão definiu a regra. Não remova essa proteção apenas para obter um total automático.</p>
        <article v-for="(rule,index) in policy.rules" :key="index" class="border rounded-xl p-3 space-y-3"><div class="grid md:grid-cols-2 gap-3"><DsInput :id="`rule-${index}-id`" v-model="rule.id" label="Identificador (letras, números e hífen)" :maxlength="60" /><DsInput :id="`rule-${index}-label`" v-model="rule.label" label="Descrição / referência da regra" :maxlength="300" /></div><div class="grid md:grid-cols-3 gap-3"><DsSelect :id="`rule-${index}-category`" v-model="rule.category" label="Categoria"><option v-for="(label,key) in quoteCategories" :key="key" :value="key">{{ label }}</option></DsSelect><DsSelect :id="`rule-${index}-formula`" v-model="rule.formula" label="Fórmula"><option value="fixo">Valor fixo por item</option><option value="unitario">Valor × quantidade</option><option value="mensalidade">Maior entre valor e mensalidade</option></DsSelect><DsInput :id="`rule-${index}-value`" v-model="rule.value" label="Valor de referência (R$)" type="number" step="0.01" /></div><div class="flex flex-wrap justify-between gap-3"><label class="text-sm flex gap-2 items-center"><input v-model="rule.requiresManual" type="checkbox">Exige enquadramento manual justificado</label><DsButton variant="ghost" size="sm" @click="policy.rules.splice(index,1)">Retirar da próxima versão</DsButton></div></article>
        <DsButton variant="secondary" @click="policy.rules.push({id:'',label:'',category:'outras',formula:'fixo',value:0,requiresManual:true})">Adicionar regra</DsButton>
        <DsInput id="quote-policy-reason" v-model="policyReason" label="Motivo da nova versão" :maxlength="1000" required /><DsButton type="submit" :loading="busy">Salvar nova versão da política</DsButton>
      </form>
    </DsPageShell>
    <DsPageShell v-if="auth.can('orcamentos','configurar_clientes')" class="mt-4">
      <form class="space-y-4" @submit.prevent="saveClient"><h2 class="text-lg font-semibold">Complemento comercial do cliente</h2><DsSelect id="commercial-client" v-model="clientId" label="Cliente existente"><option value="">Selecione</option><option v-for="c in clients" :key="c.id" :value="c.id">{{ c.name }} · #{{ c.id }}</option></DsSelect>
        <template v-if="commercial"><div class="grid md:grid-cols-2 gap-3"><DsInput id="commercial-fiscal" v-model="commercial.fiscalId" label="Identificação fiscal (opcional)" :maxlength="254" /><DsInput id="commercial-contact" v-model="commercial.contact" label="Contato (opcional)" :maxlength="254" /><DsInput id="commercial-email" v-model="commercial.email" label="E-mail (opcional)" type="email" :maxlength="254" /><DsInput id="commercial-monthly" v-model="commercial.monthlyFee" label="Mensalidade (opcional, R$)" type="number" step="0.01" /></div><p class="text-sm text-gray-500">Atualizar o cliente não altera os dados congelados nas revisões de orçamento.</p><DsButton type="submit" :loading="busy">Salvar complemento comercial</DsButton></template>
      </form>
    </DsPageShell>
    <DsAlert v-if="!auth.can('orcamentos','configurar_precos') && !auth.can('orcamentos','configurar_clientes') && !auth.can('orcamentos','configurar_regras')" variant="error">Você não possui permissão para configurar este módulo.</DsAlert>
  </div>
</template>
<script setup lang="ts">
import { quoteCategories, type QuotePolicy, type CommercialClient } from '~/utils/orcamentos'
import QuoteTechnicalSettings from '~/components/orcamentos/QuoteTechnicalSettings.vue'
definePageMeta({layout:'default',compactWorkspace:true})
useHead({title:'Configurações de Orçamentos'})
const api=useApi(),auth=useAuthStore(),policy=ref<QuotePolicy|null>(null),policyVersion=ref(''),policyReason=ref(''),busy=ref(false),error=ref(''),message=ref('')
const clients=ref<{id:number;name:string}[]>([]),clientId=ref(''),commercial=ref<CommercialClient|null>(null),clientVersion=ref('0')
const optional=(n:unknown)=>n==null||n===''?null:Number(n)
let clientSequence=0
async function loadPolicy(){const r=await api.get<{data:{version:string;policy:QuotePolicy}}>('/api/web/orcamentos/configuracoes');policy.value=r.data.policy;policyVersion.value=r.data.version}
async function savePolicy(){if(!policy.value)return;busy.value=true;error.value='';message.value='';try{await api.put('/api/web/orcamentos/configuracoes',{version:policyVersion.value,reason:policyReason.value,policy:{...policy.value,hourlyRate:optional(policy.value.hourlyRate),internalHourlyCost:optional(policy.value.internalHourlyCost),validityDays:optional(policy.value.validityDays),rules:policy.value.rules.map(r=>({...r,value:Number(r.value)}))}});await loadPolicy();policyReason.value='';message.value='Nova versão da política salva.'}catch(e){error.value=e instanceof Error?e.message:'Falha ao salvar política.'}finally{busy.value=false}}
watch(clientId,async()=>{const seq=++clientSequence;commercial.value=null;error.value='';if(!clientId.value)return;try{const r=await api.get<{data:{version:string;client:{commercial:CommercialClient}}}>(`/api/web/orcamentos/clientes/${clientId.value}/comercial`);if(seq===clientSequence){commercial.value=r.data.client.commercial;clientVersion.value=r.data.version}}catch(e){if(seq===clientSequence)error.value=e instanceof Error?e.message:'Falha ao consultar cliente.'}})
async function saveClient(){if(!commercial.value)return;const target=clientId.value;busy.value=true;error.value='';message.value='';try{const r=await api.put<{data:{version:string;client:{commercial:CommercialClient}}}>(`/api/web/orcamentos/clientes/${target}/comercial`,{version:clientVersion.value,commercial:{...commercial.value,monthlyFee:optional(commercial.value.monthlyFee)}});if(clientId.value===target){commercial.value=r.data.client.commercial;clientVersion.value=r.data.version;message.value='Dados comerciais salvos.'}}catch(e){error.value=e instanceof Error?e.message:'Falha ao salvar cliente.'}finally{busy.value=false}}
onMounted(async()=>{try{if(auth.can('orcamentos','configurar_precos'))await loadPolicy();if(auth.can('orcamentos','configurar_clientes'))clients.value=(await api.get<{data:{id:number;name:string}[]}>('/api/web/orcamentos/clientes')).data}catch(e){error.value=e instanceof Error?e.message:'Falha ao carregar configurações.'}})
onBeforeUnmount(()=>{clientSequence++;commercial.value=null;policy.value=null})
</script>
<style scoped>.quote-area{display:block;width:100%;margin-top:.4rem;padding:.75rem;border:1px solid #d1d5db;border-radius:12px;background:white;font-size:.875rem;color:#111827;}.quote-area:focus-visible{outline:2px solid #2563eb;outline-offset:2px;}</style>
