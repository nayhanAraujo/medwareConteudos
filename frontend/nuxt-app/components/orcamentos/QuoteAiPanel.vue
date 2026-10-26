<template>
  <DsPageShell class="mt-4">
    <h2 class="text-lg font-semibold">Análise por IA · Composer</h2>
    <p class="text-sm text-gray-500">Primeiro identificamos a solicitação. Após sua confirmação, uma segunda chamada analisa o escopo com as regras técnicas. As duas chamadas podem gerar cobrança pelo fornecedor.</p>
    <DsAlert v-if="error || refreshError" variant="error" class="my-3">{{ error || refreshError }}</DsAlert>
    <form v-if="editable" class="space-y-3 mt-4" @submit.prevent="queue">
      <fieldset :disabled="disabled || busy" class="space-y-3">
        <label for="quote-ai-briefing" class="block text-sm font-medium">O que você deseja que a IA analise?<textarea id="quote-ai-briefing" v-model="briefing" rows="4" maxlength="4000" required class="ai-area" placeholder="Ex.: incluir em outro modelo somente as medidas circuladas em vermelho, sem reconstruir todo o laudo." /></label>
        <legend class="text-sm font-medium">Materiais de escopo desta revisão (até 5 anexos / 8 MB)</legend>
        <label v-for="file in scopeFiles" :key="file.id" class="flex items-start gap-2 text-sm break-all"><input v-model="files" type="checkbox" :value="file.id">{{ file.name }}</label>
        <p v-if="!scopeFiles.length" class="text-sm text-gray-500">Sem anexos de escopo. A identificação utilizará o briefing e o escopo salvo.</p>
        <label class="flex items-start gap-2 text-sm"><input v-model="confirmed" type="checkbox" required>Autorizo o envio do briefing, escopo e anexos selecionados ao Cursor; não contêm dados pessoais desnecessários.</label>
      </fieldset>
      <DsButton type="submit" variant="secondary" :disabled="disabled || busy || !confirmed || !briefing.trim() || active || files.length > 5"><span v-if="busy && operation === 'queue'" class="quote-spinner" aria-hidden="true" />{{ busy && operation === 'queue' ? 'Enviando solicitação…' : 'Identificar solicitação' }}</DsButton>
      <p v-if="disabled" class="text-xs text-amber-800">Salve as alterações antes de solicitar ou aplicar uma análise.</p>
    </form>
    <div class="flex justify-end mt-3"><DsButton size="sm" variant="ghost" :disabled="busy || refreshing" @click="refresh"><span v-if="refreshing" class="quote-spinner" aria-hidden="true" />{{ refreshing ? 'Atualizando status…' : 'Atualizar análises' }}</DsButton></div>
    <p class="text-sm text-blue-800" role="status" aria-live="polite">{{ statusMessage }}</p>
    <p v-if="!jobs.length && !refreshing" class="text-sm text-gray-500">Nenhuma análise registrada.</p>
    <article v-for="job in jobs" :key="job.id" class="border-t border-gray-200 mt-4 pt-4 space-y-3">
      <div class="flex justify-between flex-wrap gap-2"><strong class="text-sm flex gap-2 items-center"><span v-if="['pendente','executando'].includes(job.status)" class="quote-spinner" aria-hidden="true" />#{{ job.id }} · r{{ job.revision }} · {{ jobLabel(job) }}</strong><span class="text-xs text-gray-500">{{ job.model }}</span></div>
      <p v-if="job.error" class="text-sm text-red-700">{{ job.error }}</p>
      <details v-if="job.context?.briefing" class="text-sm"><summary class="cursor-pointer">Briefing utilizado · catálogo #{{ job.context.catalogVersion || 'legado' }}</summary><p class="whitespace-pre-wrap break-words mt-2">{{ job.context.briefing }}</p></details>
      <DsButton v-if="editable && ['pendente','executando'].includes(job.status)" size="sm" variant="secondary" :disabled="busy" @click="run(`/${job.id}/cancelar`,undefined,'cancel')">Cancelar análise</DsButton>
      <DsButton v-if="editable && current(job) && !children.has(job.id)" size="sm" variant="secondary" :disabled="busy" @click="discardResult(job)">{{ job.phase==='classificacao'?'Descartar classificação e seguir manualmente':'Descartar sugestões e seguir manualmente' }}</DsButton>
      <template v-if="job.result">
        <p class="text-sm whitespace-pre-wrap break-words">{{ job.result.summary }}</p>
        <ul class="text-sm text-amber-800 list-disc pl-5"><li v-for="(warning,i) in [...job.result.warnings,...(job.result.classification?.warnings || [])]" :key="i" class="break-words">{{ warning }}</li></ul>
        <p v-if="!current(job)" class="text-xs text-amber-800">O orçamento mudou desde esta solicitação. O resultado permanece no histórico, mas não pode ser aplicado ou confirmado.</p>
        <div v-if="job.phase === 'classificacao' && drafts[job.id] && job.context?.catalog" class="space-y-3">
          <p class="text-sm font-medium">Confirme ou corrija cada frente. Tecnologia, perfil e serviços desconhecidos devem ser informados para laudos.</p>
          <fieldset :disabled="!current(job) || !editable || busy || disabled || children.has(job.id)" class="space-y-3">
            <article v-for="(front,index) in drafts[job.id]" :key="front.id" class="border rounded-xl p-3 space-y-3">
              <div class="flex justify-between gap-2"><strong class="text-sm">Frente {{ index + 1 }}</strong><DsButton size="sm" variant="ghost" @click="drafts[job.id]!.splice(index,1)">Retirar frente</DsButton></div>
              <div class="grid md:grid-cols-3 gap-3">
                <DsSelect :id="`front-${job.id}-${index}-category`" v-model="front.category" label="Categoria"><option v-for="(label,key) in quoteCategories" :key="key" :value="key">{{ label }}</option></DsSelect>
                <DsSelect :id="`front-${job.id}-${index}-technology`" :model-value="front.technologyId ?? ''" label="Tecnologia" @update:model-value="front.technologyId=$event ? Number($event) : null"><option value="">Não identificada — confirmar</option><option v-for="t in job.context.technologies" :key="t.id" :value="t.id">{{ t.name }}</option></DsSelect>
                <DsSelect :id="`front-${job.id}-${index}-profile`" :model-value="front.profileId ?? ''" label="Perfil do laudo" @update:model-value="front.profileId=$event || null"><option value="">Não identificado — confirmar</option><option v-for="p in job.context.catalog.profiles.filter(p=>p.active)" :key="p.id" :value="p.id">{{ p.name }}</option></DsSelect>
              </div>
              <div class="space-y-2"><span class="text-sm font-medium">Serviços solicitados</span><label v-for="service in job.context.catalog.services.filter(s=>s.active)" :key="service.id" class="text-sm flex gap-2"><input v-model="front.serviceIds" type="checkbox" :value="service.id">{{ service.name }}</label></div>
              <label class="text-sm block">Descrição da frente<textarea v-model="front.description" rows="2" maxlength="2000" class="ai-area" /></label>
            </article>
            <DsButton size="sm" variant="secondary" :disabled="drafts[job.id]!.length>=30" @click="addFront(job.id)">Adicionar frente</DsButton>
            <label class="text-sm flex gap-2 items-start"><input v-model="generic[job.id]" type="checkbox">Estou ciente de que frentes sem regras específicas publicadas receberão análise genérica.</label>
            <label class="text-sm flex gap-2 items-start"><input v-model="secondConsent[job.id]" type="checkbox">Confirmo a classificação e autorizo a segunda chamada ao Cursor com as regras correspondentes.</label>
          </fieldset>
          <DsButton v-if="current(job) && editable && !children.has(job.id)" variant="secondary" :disabled="busy || disabled || !secondConsent[job.id] || !drafts[job.id]!.length" @click="confirmClassification(job)"><span v-if="busy && operation === 'confirm'" class="quote-spinner" aria-hidden="true" />Confirmar classificação e analisar</DsButton>
        </div>
        <template v-else-if="job.phase !== 'classificacao'">
          <label v-for="(item,index) in job.result.items" :key="index" class="block rounded-xl border border-gray-200 p-3 text-sm">
            <span class="flex gap-2"><input v-if="applicable(job)" v-model="selected" type="checkbox" :value="`${job.id}:${index}`"><strong class="break-words">{{ item.description }}</strong></span>
            <span class="block text-gray-500 mt-2">{{ quoteCategories[item.category] }} · {{ item.complexity }} · estimativa: {{ hours(item) }} h</span>
            <span v-if="item.frontId" class="block text-xs text-gray-500">Frente: {{ job.context?.contexts.find(c=>c.front.id===item.frontId)?.front.description || item.frontId }}</span>
            <span class="block whitespace-pre-wrap break-words mt-2">Justificativa: {{ item.reason }}</span>
            <span v-if="item.dependencies" class="block break-words mt-1">Dependências: {{ item.dependencies }}</span>
            <span v-if="item.pending" class="block text-amber-800 break-words mt-1">{{ item.blockingPending ? 'Impedimento' : 'Aviso' }}: {{ item.pending }}</span>
          </label>
          <DsButton v-if="applicable(job)" size="sm" variant="secondary" :disabled="busy || disabled || !indices(job.id).length" @click="run(`/${job.id}/aplicar`,{version,items:indices(job.id)},'apply')">Adicionar itens selecionados ao rascunho</DsButton>
        </template>
      </template>
    </article>
  </DsPageShell>
</template>
<script setup lang="ts">
import {quoteCategories} from '~/utils/orcamentos'
import type {TechnicalCatalog,TechnicalFront,Technology,TechnicalContext} from '~/utils/orcamentoTechnical'
import type {QuoteAiProgress} from '~/utils/orcamentoSteps'
interface AiItem {frontId?:string|null;category:string;description:string;complexity:string;reason:string;hours:Record<string,number>;dependencies:string;pending:string;blockingPending:boolean}
interface AiJob {id:string;revision:number;version:string;phase?:string;origin?:string|null;status:string;model:string;error?:string;context?:{briefing:string|null;catalogVersion:string|null;catalog:TechnicalCatalog|null;technologies:Technology[];contexts:TechnicalContext[]};result:{summary:string;warnings:string[];items:AiItem[];classification?:{fronts:TechnicalFront[];warnings:string[]}|null}|null}
const props=defineProps<{id:string;version:string;revision:number;editable:boolean;disabled:boolean;initialBriefing:string;attachments:{id:string;revision:number;kind:string;name:string}[]}>()
const emit=defineEmits<{updated:[];busy:[value:boolean];progress:[value:QuoteAiProgress]}>(),api=useApi(),auth=useAuthStore(),swal=useSwal()
const loaded=ref(false)
const jobs=ref<AiJob[]>([]),files=ref<string[]>([]),selected=ref<string[]>([]),briefing=ref(props.initialBriefing),confirmed=ref(false),busy=ref(false),refreshing=ref(false),error=ref(''),refreshError=ref(''),operation=ref('')
const drafts=ref<Record<string,TechnicalFront[]>>({}),generic=ref<Record<string,boolean>>({}),secondConsent=ref<Record<string,boolean>>({})
const scopeFiles=computed(()=>props.attachments.filter(f=>f.revision===props.revision&&f.kind==='escopo'))
const active=computed(()=>jobs.value.some(j=>['pendente','executando'].includes(j.status))),children=computed(()=>new Set(jobs.value.map(j=>j.origin).filter(Boolean)))
const current=(j:AiJob)=>j.version===props.version&&j.revision===props.revision&&j.status==='concluida'
const applicable=(j:AiJob)=>props.editable&&current(j)&&j.phase!=='classificacao'
const progress=computed<QuoteAiProgress>(()=>({loading:!loaded.value||refreshing.value||!!refreshError.value,dirty:briefing.value!==props.initialBriefing||files.value.length>0||confirmed.value||selected.value.length>0||Object.values(secondConsent.value).some(Boolean)||Object.values(generic.value).some(Boolean)||jobs.value.some(j=>j.result?.classification&&drafts.value[j.id]&&JSON.stringify(drafts.value[j.id])!==JSON.stringify(j.result.classification.fronts)),processing:jobs.value.some(j=>j.revision===props.revision&&['pendente','executando'].includes(j.status)),awaitingClassification:jobs.value.some(j=>current(j)&&j.phase==='classificacao'&&!children.value.has(j.id)),awaitingApplication:jobs.value.some(j=>current(j)&&j.phase!=='classificacao')}))
watch(progress,value=>emit('progress',value),{immediate:true,flush:'sync'})
const indices=(job:string)=>selected.value.filter(s=>s.startsWith(job+':')).map(s=>Number(s.split(':')[1]))
const hours=(item:AiItem)=>Object.entries(item.hours).filter(([k])=>k!=='total').reduce((a,[,b])=>a+b,0)
function jobLabel(j:AiJob){if(j.status==='pendente')return 'Na fila';if(j.status==='executando')return j.phase==='classificacao'?'Identificando solicitação':'Analisando escopo';if(j.status==='concluida')return j.phase==='classificacao'?(children.value.has(j.id)?'Classificação confirmada':'Aguardando sua confirmação'):'Pronta para revisão';return ({erro:'Falhou',cancelada:'Cancelada',aplicada:'Aplicada ao rascunho'} as Record<string,string>)[j.status]||j.status}
const statusMessage=computed(()=>busy.value?operation.value==='queue'?'Enviando solicitação…':operation.value==='confirm'?'Enviando análise técnica…':'Registrando ação…':refreshing.value?'Atualizando status…':jobs.value.filter(j=>['pendente','executando'].includes(j.status)).map(jobLabel).join(' · '))
let epoch=0,sequence=0,alive=true,timer:ReturnType<typeof setInterval>|undefined
async function refresh(){const e=epoch,s=++sequence;refreshing.value=true;try{const r=await api.get<{data:AiJob[]}>(`/api/web/orcamentos/${props.id}/analises`);if(alive&&e===epoch&&s===sequence){jobs.value=r.data;loaded.value=true;refreshError.value='';for(const job of r.data)if(job.result?.classification&&!drafts.value[job.id])drafts.value[job.id]=JSON.parse(JSON.stringify(job.result.classification.fronts))}}catch(ex){if(alive&&e===epoch&&s===sequence)refreshError.value=ex instanceof Error?ex.message:'Falha ao atualizar análises'}finally{if(alive&&e===epoch&&s===sequence)refreshing.value=false}}
async function run(path:string,body?:unknown,kind=''){const e=epoch;busy.value=true;operation.value=kind;emit('busy',true);error.value='';try{await api.post(`/api/web/orcamentos/${props.id}/analises${path}`,body);if(alive&&e===epoch){selected.value=[];await refresh();busy.value=false;emit('busy',false);emit('updated')}}catch(ex){if(alive&&e===epoch)error.value=ex instanceof Error?ex.message:'Operação não concluída'}finally{if(alive&&e===epoch){busy.value=false;emit('busy',false)}}}
async function queue(){if(!confirmed.value||props.disabled||active.value||busy.value)return;await run('',{version:props.version,attachmentIds:files.value,confirmExternalProcessing:true,briefing:briefing.value},'queue')}
async function confirmClassification(job:AiJob){if(props.disabled||busy.value)return;await run(`/${job.id}/confirmar`,{version:props.version,fronts:drafts.value[job.id],confirmExternalProcessing:!!secondConsent.value[job.id],acceptGenericAnalysis:!!generic.value[job.id]},'confirm')}
function addFront(job:string){drafts.value[job]!.push({id:crypto.randomUUID(),category:'laudos',technologyId:null,profileId:null,serviceIds:[],description:''})}
function discardDraft(){briefing.value=props.initialBriefing;files.value=[];confirmed.value=false;selected.value=[];generic.value={};secondConsent.value={};for(const job of jobs.value)if(job.result?.classification)drafts.value[job.id]=JSON.parse(JSON.stringify(job.result.classification.fronts))}
async function discardResult(job:AiJob){if((await swal.confirm('Descartar este resultado e seguir com elaboração manual? O histórico será preservado.')).isConfirmed)await run(`/${job.id}/cancelar`,undefined,'cancel')}
defineExpose({discardDraft})
watch(()=>[props.id,auth.token],()=>{epoch++;sequence++;jobs.value=[];drafts.value={};selected.value=[];files.value=[];confirmed.value=false;briefing.value=props.initialBriefing;if(auth.token)void refresh()})
watch(()=>props.revision,()=>{files.value=[];selected.value=[];confirmed.value=false;briefing.value=props.initialBriefing})
onMounted(()=>{void refresh();timer=setInterval(()=>{if(document.visibilityState==='visible'&&active.value&&!busy.value&&!refreshing.value)void refresh()},4000)})
onBeforeUnmount(()=>{alive=false;epoch++;sequence++;clearInterval(timer)})
</script>
<style scoped>
.ai-area{display:block;width:100%;border:1px solid #d1d5db;border-radius:12px;padding:.75rem;background:white;margin-top:.4rem;}
.ai-area:focus-visible{outline:2px solid #2563eb;outline-offset:2px;}
.quote-spinner{display:inline-block;flex-shrink:0;width:1rem;height:1rem;border:2px solid #93c5fd;border-top-color:#1d4ed8;border-radius:50%;animation:quote-spin .8s linear infinite;}
@keyframes quote-spin{to{transform:rotate(360deg)}}
@media(prefers-reduced-motion:reduce){.quote-spinner{animation:none;border-style:dotted;}}
</style>
