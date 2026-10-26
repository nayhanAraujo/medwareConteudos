<template>
  <div>
    <DsPageHeader :title="`Orçamento #${id}`" subtitle="Da solicitação ao acompanhamento, uma etapa por vez." icon="receipt" />
    <div class="flex flex-wrap gap-2 mb-4"><DsButton to="/biblioteca/orcamentos" variant="secondary" icon="arrow-left">Carteira</DsButton><DsButton variant="secondary" icon="arrow-clockwise" :disabled="busy || store.loading || dirty" @click="load">Atualizar</DsButton></div>
    <DsAlert v-if="error || store.error" variant="error" class="mb-4">{{ error || store.error }}</DsAlert>
    <DsAlert v-if="navigationError" variant="warning" class="mb-4" role="alert">{{ navigationError }}</DsAlert>
    <p v-if="store.loading" role="status" class="py-10 text-center">Carregando orçamento...</p>
    <template v-else-if="store.detail && draft">
      <section aria-label="Resumo e etapas do orçamento" class="rounded-2xl border border-white/70 bg-white/50 p-4 md:p-5 mt-4">
        <div class="flex flex-wrap justify-between items-start gap-4"><div><h2 class="text-xl font-semibold m-0">{{ store.detail.header.clientName }}</h2><p class="text-sm text-gray-500 mt-2">r{{ selectedRevision }} · {{ quoteStatuses[viewStatus] }}<span v-if="store.detail.header.archived"> · Arquivado</span></p></div><DsSelect id="quote-revision" v-model="selectedRevision" label="Revisão" :disabled="dirty || busy"><option v-for="r in store.detail.revisions" :key="r.number" :value="r.number">r{{ r.number }} · {{ quoteStatuses[r.status] }}</option></DsSelect></div>
        <nav aria-label="Etapas do orçamento" class="mt-5">
          <ol class="quote-steps">
            <li v-for="(stage,index) in quoteSteps" :key="stage.title">
              <button type="button" :aria-current="step === index + 1 ? 'step' : undefined" :disabled="busy" :aria-label="`Etapa ${index + 1}: ${stage.title}`" :aria-disabled="!!stepBlock(index+1)" :title="stepBlock(index+1)?.reason" @click="goStep(index + 1)">
                <span class="quote-step-number" aria-hidden="true">{{ index + 1 }}</span><span><strong class="block">{{ stage.title }}</strong><span class="block text-xs mt-1">{{ stepHints[index] }}</span></span>
              </button>
            </li>
          </ol>
        </nav>
        <div class="flex flex-wrap justify-between items-center gap-3 mt-5 border-t border-gray-200 pt-4"><p class="text-xs text-gray-500 m-0">Salve cada etapa e resolva seus impedimentos antes de avançar. Aprovação e envio continuam explícitos.</p><DsButton v-if="editable" size="sm" icon="save" :loading="busy || store.saving" @click="save()">Salvar alterações</DsButton></div>
      </section>
      <DsAlert v-if="dirty" variant="warning" class="my-4">Há alterações não salvas. Você pode voltar para revisar, mas precisa salvar antes de avançar.</DsAlert>
      <div class="mt-5 mb-4" :id="`quote-stage-${step}`"><p class="text-xs uppercase tracking-wide text-gray-500">Etapa {{ step }} de 5</p><h2 ref="stageHeading" tabindex="-1" class="quote-stage-title text-xl font-semibold mt-1">{{ quoteSteps[step - 1]!.title }}</h2><p class="text-sm text-gray-500 mt-2">{{ quoteSteps[step - 1]!.description }}</p></div>
      <DsPageShell v-show="step === 1">
        <fieldset :disabled="!editable || busy" class="space-y-4 min-w-0">
          <DsInput id="quote-title" v-model="draft.title" label="Título da solicitação" :maxlength="200" />
          <label class="text-sm block">Solicitação e contexto internos<textarea v-model="draft.request" rows="5" maxlength="4000" class="quote-area" /></label>
        </fieldset>
        <p class="text-xs text-gray-500 mt-4">O cliente foi escolhido na criação do orçamento. Os anexos abaixo são privados e não são enviados automaticamente à IA.</p>
      </DsPageShell>
      <QuoteEmailSourcePanel v-if="current && store.detail.emailSource" v-show="step === 1" :detail="store.detail" :editable="!store.detail.header.archived && quoteEditable(viewStatus)" :can-confirm="can('confirmar_email')" :disabled="busy || dirty" @busy="busy=$event" @updated="applyDetail($event,viewEpoch)" />
      <DsPageShell v-show="step === 2">
        <h3 class="text-base font-semibold">Como deseja elaborar o escopo?</h3>
        <p class="text-sm text-gray-500 mt-2">Use o Composer abaixo para obter sugestões ou siga com elaboração manual na próxima etapa. A IA é opcional.</p>
        <DsButton variant="secondary" class="mt-3" :disabled="busy" @click="chooseManual">Elaborar itens manualmente</DsButton>
        <p v-if="!can('usar_ia')" class="text-sm text-gray-500 mt-3">Seu perfil não possui permissão de análise por IA. A elaboração manual continua disponível quando não houver análises pendentes de outro operador.</p>
        <DsButton v-if="current && commands.includes('analise')" variant="secondary" class="mt-3" :disabled="busy || dirty" @click="openCommand('analise')">Registrar início da análise</DsButton>
      </DsPageShell>
      <QuoteAiPanel ref="aiPanel" @progress="aiProgress=$event" v-show="step === 2" v-if="can('usar_ia')" :id="id" :version="store.detail.header.version" :revision="Number(selectedRevision)" :editable="editable" :disabled="dirty || busy" :initial-briefing="draft.request" :attachments="store.detail.attachments" @busy="busy = $event" @updated="load" />
      <QuoteIssuesPanel v-show="step === 3 || step === 4" :issues="visibleIssues" :dirty="dirty" :busy="busy" @navigate="focusIssue" />
      <div v-show="step === 3">
      <section class="space-y-4 mt-4"><QuoteItemEditor v-for="(item,index) in draft.items" :key="index" :item="item" :index="index" :readonly="!editable || busy" :policy="snapshot.policy" :total="snapshot.calculation.items[index]" @remove="draft.items.splice(index,1)" /></section>
      <div v-if="editable" class="flex flex-wrap gap-3 my-4"><DsButton variant="secondary" id="quote-add-item" icon="plus" :disabled="busy || draft.items.length >= 100" @click="draft.items.push(newQuoteItem())">Adicionar item</DsButton><DsButton icon="save" :loading="busy || store.saving" @click="save()">Salvar e recalcular</DsButton><DsButton id="quote-refresh-policy" variant="secondary" :disabled="busy" @click="save(true,false)">Atualizar política e recalcular</DsButton><DsButton id="quote-refresh-client" variant="secondary" :disabled="busy" @click="save(false,true)">Atualizar dados do cliente</DsButton></div>

        <DsPageShell class="mt-4">
          <h3 class="text-base font-semibold mb-4">Condições da proposta</h3>
          <fieldset :disabled="!editable || busy" class="grid md:grid-cols-2 gap-4 min-w-0">
            <DsInput id="quote-validity" v-model="draft.validUntil" label="Validade até" type="date" />
            <DsInput id="quote-deadline" v-model="draft.deadline" label="Prazo de entrega (somente se validado)" :maxlength="500" />
            <label class="text-sm md:col-span-2">Condições de pagamento (públicas)<textarea id="quote-payment-terms" v-model="draft.paymentTerms" rows="2" maxlength="4000" class="quote-area" /></label>
            <label class="flex gap-2 items-center text-sm md:col-span-2"><input v-model="draft.deadlineValidated" type="checkbox">Prazo validado com a equipe responsável</label>
          </fieldset>
          <p class="text-xs text-gray-500 mt-4">Política comercial congelada: #{{ snapshot.policyVersion }} · Mensalidade da revisão: {{ quoteMoney(snapshot.client.commercial.monthlyFee) }}</p>
        </DsPageShell>
      </div>
      <DsPageShell v-show="step === 4">
        <h3 class="text-base font-semibold">Conferência antes da aprovação</h3>
        <ul class="text-sm mt-3 space-y-2"><li v-for="check in reviewChecks" :key="check.label" :class="check.ok ? 'text-gray-600' : 'text-amber-800'"><span aria-hidden="true">{{ check.ok ? '✓' : '○' }}</span> {{ check.label }} — {{ check.ok ? 'Conferido' : 'Revisar' }}</li></ul>
        <p class="text-xs text-gray-500 mt-4">Este checklist orienta a revisão. A aprovação continua sujeita às validações do servidor e deve ser feita por outro usuário autorizado.</p>
        <details class="mt-4 text-sm"><summary class="cursor-pointer font-medium">Resumo do escopo · {{ draft.items.length }} itens</summary><ul class="mt-3 space-y-2"><li v-for="(item,index) in draft.items" :key="index" class="break-words">{{ index + 1 }}. {{ item.description || 'Item sem descrição' }}</li></ul></details>
      </DsPageShell>
      <DsPageShell v-show="step === 5" class="mb-4">
        <h3 class="text-base font-semibold">Proposta e marcos comerciais</h3>
        <p class="text-sm text-gray-500 mt-2">{{ quoteApproved(viewStatus) ? 'Utilize a proposta aprovada e registre os marcos conforme acontecerem.' : 'A proposta será disponibilizada após a aprovação interna. Você pode consultar esta etapa sem alterar o estado do orçamento.' }}</p>
        <p class="text-xs text-gray-500 mt-3">O e-mail só é enviado após confirmação explícita do destinatário. Cobrança, aceite e integração de execução não são automáticos.</p>
      </DsPageShell>
      <DsPageShell v-show="step >= 3" class="mt-4">
        <h2 class="text-lg font-semibold">Consolidado da revisão salva</h2><div class="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm mt-4"><div>Horas estimadas<strong class="block text-lg">{{ snapshot.calculation.hours }}</strong></div><div>Referência<strong class="block text-lg">{{ quoteMoney(snapshot.calculation.gross) }}</strong></div><div>Descontos<strong class="block text-lg">{{ quoteMoney(snapshot.calculation.discount) }}</strong></div><div>Valor final<strong class="block text-xl text-blue-800">{{ quoteMoney(snapshot.calculation.net) }}</strong></div></div><p v-if="!snapshot.calculation.complete" class="text-sm text-amber-800 mt-3">A revisão tem valores ou enquadramentos pendentes. Não está pronta para aprovação.</p>
        <details v-if="snapshot.calculation.internalCost != null" class="text-xs text-gray-500 mt-3"><summary>Custo interno estimado (não integra a proposta)</summary>{{ quoteMoney(snapshot.calculation.internalCost) }}</details>
        <p v-if="isEditor && viewStatus === 'em_aprovacao'" class="text-sm text-amber-800 mt-4">Você elaborou ou editou esta revisão. Outro usuário autorizado deverá aprová-la, mesmo que você seja administrador.</p>
        <div v-if="current" class="flex flex-wrap gap-2 mt-4"><DsButton v-for="action in stageCommands" :key="action" :variant="['cancelar','recusar'].includes(action) ? 'danger' : 'secondary'" :disabled="busy || dirty" @click="openCommand(action)">{{ quoteActions[action] }}</DsButton></div>
        <div v-if="step === 5 && quoteApproved(viewStatus)" class="flex flex-wrap gap-2 mt-4"><DsButton variant="secondary" icon="clipboard" :disabled="busy || dirty" @click="proposal">Texto do e-mail</DsButton><DsButton variant="secondary" icon="file-earmark-pdf" :disabled="busy || dirty" @click="downloadPdf">Baixar proposta PDF</DsButton></div>
        <div v-if="step === 5 && email" class="mt-4"><label for="quote-email" class="text-sm">Texto comercial gerado pelo servidor</label><textarea id="quote-email" :value="email" rows="14" readonly class="quote-area" /><DsButton variant="secondary" size="sm" class="mt-2" @click="copyEmail">Copiar texto</DsButton></div>
        <QuoteEmailPanel v-if="step === 5 && quoteCanEmail(viewStatus,current,store.detail.header.archived,can('registrar_marcos')) && (!store.detail.emailSource || store.detail.emailSource.confirmed)" :id="id" :revision="Number(selectedRevision)" :version="store.detail.header.version" :client-email="store.detail.emailSource?.confirmation?.recipient || snapshot.client.commercial.email" :fixed-recipient="!!store.detail.emailSource" :disabled="busy || dirty" @busy="busy=$event" @updated="applyDetail($event,viewEpoch)" />
        <QuoteEmailAuthorizationPanel v-if="step === 5 && current && !store.detail.header.archived && ['aprovado','enviado'].includes(viewStatus) && store.detail.emailSource?.confirmed && can('confirmar_email') && can('registrar_marcos')" :id="id" :revision="Number(selectedRevision)" :version="store.detail.header.version" :recipient="store.detail.emailSource.confirmation!.recipient" :events="store.detail.events" :disabled="busy || dirty" @busy="busy=$event" @updated="applyDetail($event,viewEpoch)" />
      </DsPageShell>

      <DsPageShell v-show="step === 1 || step === 5" class="mt-4">
        <h2 class="text-lg font-semibold">Anexos privados</h2><p class="text-sm text-gray-500">PDF, DOCX, PNG, JPEG, XLSX ou CSV UTF-8, até 20 MB. Sem bancos, backups, macros ou vínculos externos. Anexar não executa análise por IA.</p>
        <form v-if="current && !store.detail.header.archived && (editable || can('registrar_marcos'))" class="flex flex-wrap gap-3 items-end mt-3" @submit.prevent="upload"><DsSelect id="quote-file-kind" v-model="fileKind" label="Finalidade"><option v-if="editable" value="escopo">Material de escopo</option><option v-if="can('registrar_marcos')" value="evidencia">Evidência comercial</option></DsSelect><label class="text-sm">Arquivo<input ref="fileInput" type="file" accept=".pdf,.docx,.png,.jpg,.jpeg,.xlsx,.csv" class="block mt-2 max-w-full" required></label><DsButton type="submit" variant="secondary" :disabled="busy || dirty">Anexar</DsButton></form>
        <ul class="mt-4 space-y-2"><li v-for="file in store.detail.attachments" :key="file.id" class="border-b py-2 flex flex-wrap justify-between gap-3 text-sm"><div>{{ file.name }} <span class="text-gray-500">· r{{ file.revision }} · {{ file.kind }} · {{ Math.ceil(file.size/1024) }} KB</span><details class="text-xs text-gray-500"><summary>Integridade</summary><code class="break-all">{{ file.sha256 }}</code></details></div><DsButton variant="ghost" size="sm" icon="download" @click="downloadAttachment(file.id,file.name)">Baixar</DsButton></li></ul>
      </DsPageShell>

      <p v-if="forwardBlock" class="mt-4 text-sm text-amber-800" role="status">{{ forwardBlock.reason }}</p><div class="quote-step-footer"><DsButton variant="secondary" :disabled="step === 1 || busy" @click="goStep(step - 1)">Etapa anterior</DsButton><span class="text-xs text-gray-500">Etapa {{ step }} de 5</span><DsButton v-if="step < 5" :disabled="busy || !!forwardBlock" @click="goStep(step + 1)">Continuar</DsButton><DsButton v-else variant="secondary" :disabled="busy" @click="goStep(4)">Revisar orçamento</DsButton></div>
      <div v-if="current && secondaryCommands.length" class="mt-4"><details><summary class="text-sm text-gray-500 cursor-pointer">Outras ações do orçamento</summary><div class="flex flex-wrap gap-2 mt-3"><DsButton v-for="action in secondaryCommands" :key="action" variant="secondary" size="sm" :disabled="busy || dirty" @click="openCommand(action)">{{ quoteActions[action] }}</DsButton></div></details></div>
      <DsPageShell class="mt-4"><details><summary class="font-semibold cursor-pointer">Histórico de decisões · {{ store.detail.events.length }} registros</summary><ol class="mt-4 space-y-4"><li v-for="event in store.detail.events" :key="event.id" class="border-l-2 border-blue-200 pl-4 text-sm"><p class="font-medium">{{ quoteActions[event.action] || event.action }} · r{{ event.revision }} · {{ event.actorName }}</p><p class="text-gray-500 text-xs">{{ quoteDate(event.createdAtUtc) }} · {{ quoteStatuses[event.fromStatus] || 'Novo' }} → {{ quoteStatuses[event.toStatus] }}</p><p v-if="event.reason" class="mt-1 whitespace-pre-wrap break-words">{{ event.reason }}</p><p v-if="event.reference" class="text-gray-500 break-words">Referência: {{ event.reference }}</p><p v-if="event.destination" class="text-gray-500 break-words">Destino: {{ event.destination }}</p></li></ol></details></DsPageShell>
    </template>
    <dialog ref="commandDialog" class="quote-dialog" aria-labelledby="quote-command-title" @cancel.prevent="closeCommand"><form class="p-5 space-y-4" @submit.prevent="executeCommand"><h2 id="quote-command-title" class="text-lg font-semibold">{{ quoteActions[selectedAction] }}</h2><p class="text-sm text-gray-500">A decisão será registrada na revisão atual. Não será enviado e-mail nem executada integração externa.</p><label class="text-sm block">Motivo / observações<textarea v-model="reason" rows="3" maxlength="2000" class="quote-area" :required="['cancelar','recusar','devolver','nova_revisao','arquivar'].includes(selectedAction)" /></label><DsInput id="quote-reference" v-model="reference" label="Referência externa / confirmação" :maxlength="500" :required="selectedAction === 'confirmar_pagamento'" /><DsInput v-if="selectedAction === 'encaminhar'" id="quote-destination" v-model="destination" label="Destino para execução" :maxlength="500" required /><div class="flex justify-end gap-2"><DsButton variant="secondary" :disabled="busy" @click="closeCommand">Voltar</DsButton><DsButton type="submit" :loading="busy">Confirmar</DsButton></div></form></dialog>
  </div>
</template>
<script setup lang="ts">
import QuoteItemEditor from '~/components/orcamentos/QuoteItemEditor.vue'
import QuoteAiPanel from '~/components/orcamentos/QuoteAiPanel.vue'
import QuoteEmailPanel from '~/components/orcamentos/QuoteEmailPanel.vue'
import QuoteEmailSourcePanel from '~/components/orcamentos/QuoteEmailSourcePanel.vue'
import QuoteEmailAuthorizationPanel from '~/components/orcamentos/QuoteEmailAuthorizationPanel.vue'
import {quoteCanEmail} from '~/utils/orcamentoEmail'
import QuoteIssuesPanel from '~/components/orcamentos/QuoteIssuesPanel.vue'
import type {QuoteIssue} from '~/utils/orcamentoTechnical'
import { quoteSteps, initialQuoteStep, quoteStageActions, quoteStepBlock, type QuoteStepState, type QuoteAiProgress } from '~/utils/orcamentoSteps'
import { useOrcamentosStore } from '~/stores/orcamentos'
import { newQuoteItem, quoteEditable, quoteApproved, quoteCommands, quotePayload, quoteStatuses, quoteActions, quoteMoney, quoteDate, type QuoteDraft, type QuoteDetail } from '~/utils/orcamentos'
definePageMeta({ layout: 'default', compactWorkspace: true })
const route = useRoute(), api = useApi(), auth = useAuthStore(), store = useOrcamentosStore(), swal = useSwal()
const id = computed(() => String(route.params.id)), draft = ref<QuoteDraft | null>(null), baseline = ref(''), selectedRevision = ref<number | string>(0), busy = ref(false), error = ref(''), email = ref('')
const commandDialog = ref<HTMLDialogElement | null>(null), selectedAction = ref(''), reason = ref(''), reference = ref(''), destination = ref(''), fileInput = ref<HTMLInputElement | null>(null), fileKind = ref('escopo')
const can = (action:string) => auth.can('orcamentos',action)
const current = computed(() => Number(selectedRevision.value) === store.detail?.header.revision)
const revision = computed(() => store.detail!.revisions.find(r => r.number === Number(selectedRevision.value)) || store.detail!.revisions[0]!)
const snapshot = computed(() => revision.value.snapshot), viewStatus = computed(() => revision.value.status)
const editable = computed(() => !!store.detail && current.value && !store.detail.header.archived && quoteEditable(store.detail.header.status) && can('elaborar'))
const isEditor = computed(() => store.detail?.editors.includes(auth.user?.codusuario || 0) || false)
const commands = computed(() => store.detail ? quoteCommands(store.detail.header.status,can,isEditor.value,store.detail.header.archived) : [])
const step = ref(1), stageHeading = ref<HTMLElement | null>(null)
const navigationError=ref(''),aiProgress=ref<QuoteAiProgress|null>(null),aiPanel=ref<{discardDraft:()=>void}|null>(null)
let stepInitialized = false
const stageCommands = computed(() => quoteStageActions(step.value, commands.value))
const secondaryCommands = computed(() => commands.value.filter(action => ['cancelar','arquivar','nova_revisao'].includes(action)))
const stepHints = computed(() => [draft.value?.title.trim() ? 'Cliente e título definidos' : 'Defina a solicitação', 'IA opcional ou análise manual', snapshot.value.calculation.complete && !dirty.value ? 'Valores calculados' : 'Confira itens e pendências', quoteStatuses[viewStatus.value], quoteApproved(viewStatus.value) ? 'Proposta disponível' : 'Aguarda aprovação'])
const reviewChecks = computed(() => {
  const d = draft.value
  const now = new Date(), today = `${now.getFullYear()}-${String(now.getMonth()+1).padStart(2,'0')}-${String(now.getDate()).padStart(2,'0')}`
  return [
    {label:'Título da solicitação',ok:!!d?.title.trim()},
    {label:'Itens com descrição e justificativa de complexidade',ok:!!d?.items.length && d.items.every(i=>i.description.trim() && i.complexityReason.trim())},
    {label:'Valores positivos calculados e sem pendências bloqueantes',ok:!dirty.value && snapshot.value.calculation.complete && (snapshot.value.calculation.net ?? 0)>0},
    {label:'Validade vigente e condições de pagamento',ok:!!d?.validUntil && d.validUntil>=today && !!d.paymentTerms.trim()},
    {label:'Prazo validado, quando informado',ok:!!d && (!d.deadline.trim() || d.deadlineValidated)},
    {label:'Alterações salvas',ok:!dirty.value}
  ]
})
async function goStep(next:number) { if(busy.value || next<1 || next>quoteSteps.length)return false;const block=stepBlock(next);if(block){navigationError.value=block.reason;return false}navigationError.value='';step.value=next;if(next===5 && can('registrar_marcos'))fileKind.value='evidencia';else if(next===1 && editable.value)fileKind.value='escopo';await nextTick();stageHeading.value?.focus({preventScroll:true});stageHeading.value?.scrollIntoView({block:'nearest'});return true }
const dirty = computed(() => draft.value != null && JSON.stringify(draft.value) !== baseline.value)
const dirtyStep=ref<number|null>(null)
watch(dirty,value=>{if(!value)dirtyStep.value=null;else if(dirtyStep.value==null)dirtyStep.value=step.value},{flush:'sync'})
const navigationState=computed<QuoteStepState>(()=>({status:viewStatus.value,dirty:dirty.value,editStep:dirtyStep.value,hasTitle:!!draft.value?.title.trim(),hasScope:!!draft.value?.request.trim()||!!draft.value?.items.some(i=>i.description.trim())||!!store.detail?.attachments.some(a=>a.revision===Number(selectedRevision.value)&&a.kind==='escopo'),itemCount:draft.value?.items.length||0,calculationComplete:snapshot.value.calculation.complete,issues:visibleIssues.value,analysisLoading:can('usar_ia')?(aiProgress.value?.loading??true):false,analysisDirty:aiProgress.value?.dirty??false,processing:aiProgress.value?.processing??store.detail?.analysisGate?.processing??false,awaitingClassification:aiProgress.value?.awaitingClassification??store.detail?.analysisGate?.awaitingClassification??false,awaitingApplication:aiProgress.value?.awaitingApplication??store.detail?.analysisGate?.awaitingApplication??false}))
const forwardBlock=computed(()=>step.value<5?quoteStepBlock(step.value,step.value+1,navigationState.value):null)
function stepBlock(next:number){return quoteStepBlock(step.value,next,navigationState.value)}
async function chooseManual(){if(busy.value)return;const state=navigationState.value;if(state.processing||state.awaitingClassification||state.awaitingApplication){navigationError.value=quoteStepBlock(2,3,state)?.reason||'Conclua ou descarte a análise antes de continuar.';return}if(state.analysisDirty){if(!(await swal.confirm('Descartar o briefing e as escolhas de análise ainda não enviados e seguir manualmente?')).isConfirmed)return;aiPanel.value?.discardDraft();await nextTick()}await goStep(3)}
watch([dirty,step,()=>aiProgress.value],()=>{navigationError.value=''})
const visibleIssues = computed<QuoteIssue[]>(()=>current.value ? store.detail?.issues || [] : snapshot.value.calculation.items.flatMap(i=>(i as typeof i & {issues?:QuoteIssue[]}).issues || []))
async function focusIssue(issue:QuoteIssue){if(!(await goStep(issue.step)))return;await nextTick();let target:HTMLElement|null=null;if(issue.itemIndex!=null){const n=issue.itemIndex;const fields:Record<string,string>={pending:`#q-${n}-pending textarea`,checklist:`#q-${n}-checklist select`,description:`[aria-label="Descrição pública do item ${n+1}"]`,complexityReason:`#q-${n}-complexity-reason`,unitPrice:`#q-${n}-unit`,manualValue:`#q-${n}-manual`,ruleId:`#q-${n}-rule`,mode:`#q-${n}-mode`,justification:`#q-${n}-justification`,discountReason:`#q-${n}-discount-reason`};if(fields[issue.field])target=document.querySelector<HTMLElement>(fields[issue.field]!)}const globals:Record<string,string>={title:'#quote-title',validUntil:'#quote-validity',paymentTerms:'#quote-payment-terms',deadline:'#quote-deadline',policy:'#quote-refresh-policy',client:'#quote-refresh-client',items:'#quote-add-item'};target ||=document.querySelector<HTMLElement>(globals[issue.field]||'.quote-stage-title');if(target?.matches(':disabled'))target=target.closest<HTMLElement>('article')||stageHeading.value;if(target){let parent=target.parentElement;while(parent){if(parent.tagName==='DETAILS')(parent as HTMLDetailsElement).open=true;parent=parent.parentElement}target.focus({preventScroll:true});target.scrollIntoView({block:'center',behavior:window.matchMedia('(prefers-reduced-motion: reduce)').matches?'auto':'smooth'})}}
let viewEpoch = 0, alive = true
function applyDetail(data:QuoteDetail,epoch:number) { if(alive && epoch===viewEpoch && auth.token && data.header.id===id.value) {store.detail=data;sync()} }
function sync() { if(!store.detail) return; selectedRevision.value = store.detail.header.revision; draft.value = JSON.parse(JSON.stringify(store.detail.snapshot.draft)); baseline.value = JSON.stringify(draft.value); email.value = ''; fileKind.value = editable.value ? 'escopo' : 'evidencia';navigationError.value='';if(!stepInitialized){step.value=initialQuoteStep(store.detail.header.status);stepInitialized=true} }
watch(selectedRevision, () => { if(store.detail && revision.value) { draft.value = JSON.parse(JSON.stringify(revision.value.snapshot.draft)); baseline.value = JSON.stringify(draft.value); email.value = '' } })
async function load() { const epoch=viewEpoch;error.value = ''; await store.load(api,id.value); if(alive && epoch===viewEpoch)sync() }
async function save(refreshPolicy = false,refreshClient = false) { if(!draft.value || !store.detail) return; busy.value = true; error.value = ''; try { await store.save(api,id.value,store.detail.header.version,quotePayload(draft.value),refreshPolicy === true,refreshClient === true); sync(); await swal.toast('Orçamento salvo e recalculado') } catch(e) { error.value = e instanceof Error ? e.message : 'Não foi possível salvar.' } finally {busy.value = false} }
function openCommand(action:string) { selectedAction.value = action; reason.value = ''; reference.value = ''; destination.value = ''; commandDialog.value?.showModal() }
function closeCommand() { if(!busy.value) commandDialog.value?.close() }
async function executeCommand() { if(!store.detail) return; const epoch=viewEpoch;busy.value = true; error.value = ''; try { const result = await api.post<{data:QuoteDetail}>(`/api/web/orcamentos/${id.value}/transicoes`,{version:store.detail.header.version,action:selectedAction.value,reason:reason.value,reference:reference.value,destination:destination.value}); applyDetail(result.data,epoch); commandDialog.value?.close() } catch(e) { if(epoch===viewEpoch)error.value = e instanceof Error ? e.message : 'Não foi possível registrar a decisão.'; commandDialog.value?.close() } finally {if(epoch===viewEpoch)busy.value = false} }
function offer(blob:Blob,name:string) { const url=URL.createObjectURL(blob); const a=document.createElement('a'); a.href=url; a.download=name; a.click(); setTimeout(()=>URL.revokeObjectURL(url),1000) }
async function prepareIfAllowed() { if(current.value && can('registrar_marcos') && store.detail) { const epoch=viewEpoch;const r=await api.post<{data:QuoteDetail}>(`/api/web/orcamentos/${id.value}/proposta`,{version:store.detail.header.version});if(epoch!==viewEpoch||!alive)throw new Error('A sessão foi alterada.');applyDetail(r.data,epoch) } }
async function proposal() { busy.value=true; error.value=''; try { await prepareIfAllowed();email.value=(await api.get<{email:string}>(`/api/web/orcamentos/${id.value}/proposta?revision=${selectedRevision.value}`)).email } catch(e) {error.value=e instanceof Error?e.message:'Falha ao gerar proposta.'} finally {busy.value=false} }
async function downloadPdf() { busy.value=true; error.value=''; try {await prepareIfAllowed();offer(await api.getBlob(`/api/web/orcamentos/${id.value}/proposta.pdf?revision=${selectedRevision.value}`),`orcamento-${id.value}-r${selectedRevision.value}.pdf`)} catch(e){error.value=e instanceof Error?e.message:'Falha ao baixar PDF.'} finally {busy.value=false} }
async function copyEmail() {try{await navigator.clipboard.writeText(email.value);await swal.toast('Texto copiado')}catch{error.value='Não foi possível copiar. Selecione o texto e copie manualmente.'} }
async function upload() { if(!store.detail || !fileInput.value?.files?.[0])return; const epoch=viewEpoch;busy.value=true; error.value=''; const form=new FormData();form.append('version',store.detail.header.version);form.append('kind',fileKind.value);form.append('file',fileInput.value.files[0]);try{applyDetail((await api.postForm<{data:QuoteDetail}>(`/api/web/orcamentos/${id.value}/anexos`,form)).data,epoch);if(fileInput.value)fileInput.value.value=''}catch(e){if(epoch===viewEpoch)error.value=e instanceof Error?e.message:'Falha ao anexar.'}finally{if(epoch===viewEpoch)busy.value=false} }
async function downloadAttachment(file:string,name:string){try{offer(await api.getBlob(`/api/web/orcamentos/${id.value}/anexos/${file}`),name)}catch(e){error.value=e instanceof Error?e.message:'Falha no download.'} }
function beforeUnload(event:BeforeUnloadEvent){if(dirty.value){event.preventDefault();event.returnValue=''}}
onBeforeRouteLeave(async()=>!busy.value && (!dirty.value || (await swal.confirm('Descartar alterações não salvas?')).isConfirmed))
onBeforeRouteUpdate(async()=>!busy.value && (!dirty.value || (await swal.confirm('Descartar alterações não salvas?')).isConfirmed))
onMounted(()=>{store.clear();void load();window.addEventListener('beforeunload',beforeUnload)})
watch(id,()=>{viewEpoch++;stepInitialized=false;aiProgress.value=null;store.clear();void load()})
watch(()=>auth.token,()=>{viewEpoch++;busy.value=false;store.clear();draft.value=null;baseline.value='';email.value='';commandDialog.value?.close()})
onBeforeUnmount(()=>{alive=false;viewEpoch++;store.clear();commandDialog.value?.close();window.removeEventListener('beforeunload',beforeUnload)})
useHead({title:computed(()=>`Orçamento #${id.value}`)})
</script>
<style scoped>
.quote-area{display:block;width:100%;margin-top:.4rem;padding:.75rem;border:1px solid #d1d5db;border-radius:12px;background:white;font-size:.875rem;color:#111827;}
.quote-area:focus-visible{outline:2px solid #2563eb;outline-offset:2px;}
.quote-dialog{position:fixed;inset:0;margin:auto;width:min(36rem,calc(100vw - 2rem));max-height:85vh;overflow:auto;border:1px solid #d1d5db;border-radius:20px;padding:0;}
.quote-dialog::backdrop{background:rgb(15 23 42 / .3);}
fieldset:disabled{opacity:.85;}
.quote-steps{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:.5rem;padding:0;list-style:none;}
.quote-steps button{display:flex;gap:.6rem;width:100%;height:100%;align-items:flex-start;text-align:left;padding:.85rem .65rem;border:1px solid #d1d5db;border-radius:14px;background:#fff;color:#4b5563;font-size:.8rem;}
.quote-steps button[aria-disabled="true"]{opacity:.55;cursor:not-allowed;}
.quote-steps button[aria-current="step"]{border-color:#2563eb;background:#eff6ff;color:#1e40af;}
.quote-step-number{display:grid;place-items:center;flex-shrink:0;width:1.6rem;height:1.6rem;border-radius:50%;background:#f1f5f9;font-weight:600;}
.quote-steps button>span:last-child{min-width:0;overflow-wrap:anywhere;}
.quote-steps button[aria-current="step"] .quote-step-number{background:#2563eb;color:white;}
.quote-steps button:focus-visible,.quote-stage-title:focus-visible{outline:2px solid #2563eb;outline-offset:3px;}
.quote-step-footer{display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:.75rem;margin-top:1.5rem;padding:1rem 0;border-top:1px solid #d1d5db;}
@media(max-width:767px){.quote-steps{display:block;}.quote-steps li{display:none;}.quote-steps li:has(button[aria-current="step"]){display:block;}.quote-step-footer>span{width:100%;text-align:center;order:-1;}}
</style>
