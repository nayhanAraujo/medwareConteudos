<template>
  <article :id="`quote-item-${index}`" tabindex="-1" class="rounded-2xl border border-gray-200 bg-white p-4 space-y-4">
    <div class="flex flex-wrap justify-between gap-2 items-center"><h3 class="font-semibold text-base m-0">Item {{ index + 1 }}</h3><DsButton v-if="!readonly" variant="ghost" size="sm" icon="trash" @click="$emit('remove')">Remover item</DsButton></div>
    <fieldset :disabled="readonly" class="space-y-4 min-w-0">
      <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
        <DsSelect :id="`q-${index}-category`" v-model="item.category" label="Categoria"><option v-for="(label, key) in quoteCategories" :key="key" :value="key">{{ label }}</option></DsSelect>
        <DsInput :id="`q-${index}-quantity`" v-model="item.quantity" label="Quantidade de entregas / estruturas" type="number" step="0.0001" />
        <DsSelect :id="`q-${index}-complexity`" v-model="item.complexity" label="Complexidade"><option value="simples">Simples</option><option value="intermediario">Intermediário</option><option value="complexo">Complexo</option><option value="muito_complexo">Muito complexo</option></DsSelect>
      </div>
      <label class="block text-sm font-medium">Descrição pública do serviço<textarea v-model="item.description" :aria-label="`Descrição pública do item ${index + 1}`" rows="3" maxlength="4000" class="quote-area" placeholder="Resultado e escopo que poderão aparecer na proposta ao cliente" /></label>
      <DsInput :id="`q-${index}-complexity-reason`" v-model="item.complexityReason" label="Justificativa da complexidade (interna)" :maxlength="4000" />
      <details><summary class="cursor-pointer text-sm font-medium">Reaproveitamento, dependências e premissas internas</summary><div class="grid md:grid-cols-2 gap-3 mt-3">
        <label v-for="f in internalFields" :key="f.key" class="text-sm">{{ f.label }}<textarea v-model="item[f.key]" rows="2" maxlength="4000" class="quote-area" /></label>
      </div></details>
      <div :id="`q-${index}-pending`" class="rounded-xl border border-amber-200 bg-amber-50 p-3 space-y-3" tabindex="-1">
        <h4 class="text-sm font-semibold">Pendência técnica e resolução</h4>
        <label class="block text-sm">O que precisa ser confirmado?<textarea v-model="item.pending" rows="2" maxlength="4000" class="quote-area" /></label>
        <label class="flex gap-2 items-start text-sm"><input :checked="item.blockingPending" type="checkbox" @change="setBlocking(($event.target as HTMLInputElement).checked)">Impede concluir o orçamento e solicitar aprovação</label>
        <DsButton v-if="!item.blockingPending && item.pending && !item.pendingResolution" variant="secondary" size="sm" @click="setBlocking(false)">Registrar esclarecimento desta observação</DsButton>
        <label v-if="!item.blockingPending && item.pendingResolution" class="block text-sm">Justificativa da resolução<textarea v-model="item.pendingResolution.reason" rows="2" maxlength="2000" class="quote-area" placeholder="Explique o esclarecimento obtido. O usuário e a data serão registrados ao salvar." /></label>
        <p v-if="item.pendingResolution?.resolvedAtUtc" class="text-xs text-gray-500">Resolução registrada por #{{ item.pendingResolution.actorId }} · {{ item.pendingResolution.resolvedAtUtc }}</p>
      </div>
      <div v-if="item.technicalContext" :id="`q-${index}-checklist`" tabindex="-1" class="rounded-xl border border-blue-200 p-3 space-y-3">
        <h4 class="font-semibold text-sm">Checklist técnico · catálogo #{{ item.technicalContext.catalogVersion }}</h4>
        <p class="text-xs text-gray-500">{{ item.technicalContext.technologyName || 'Tecnologia não informada' }} · {{ item.technicalContext.profileName }} · {{ item.technicalContext.front.description }}</p>
        <p v-if="!item.technicalContext.rules.length" class="text-sm text-amber-800">Análise genérica: nenhuma regra específica congelada para esta frente.</p>
        <section v-for="rule in item.technicalContext.rules" :key="rule.id" class="space-y-3"><h5 class="font-medium text-sm">{{ rule.name }}</h5><details class="text-xs"><summary>Orientações utilizadas</summary><p class="whitespace-pre-wrap break-words">{{ rule.instructions }}</p></details>
          <div v-for="check in rule.checks" :key="check.id" class="border-l-2 pl-3 space-y-2">
            <p class="text-sm">{{ check.required ? 'Obrigatória' : 'Orientativa' }}: {{ check.label }}</p>
            <DsSelect :id="`q-${index}-check-${rule.id}-${check.id}`" :model-value="getAnswer(rule.id+':'+check.id).status" label="Resposta da verificação" @update:model-value="updateAnswer(rule.id+':'+check.id,{status:$event})"><option value="pendente">Pendente</option><option value="confirmado">Confirmado</option><option value="nao_aplicavel">Não se aplica</option></DsSelect>
            <label v-if="getAnswer(rule.id+':'+check.id).status !== 'pendente'" class="block text-sm">Justificativa<textarea :value="getAnswer(rule.id+':'+check.id).reason" rows="2" maxlength="2000" class="quote-area" @input="updateAnswer(rule.id+':'+check.id,{reason:($event.target as HTMLTextAreaElement).value})" /></label>
            <p v-if="getAnswer(rule.id+':'+check.id).resolvedAtUtc" class="text-xs text-gray-500">Resposta registrada por #{{ getAnswer(rule.id+':'+check.id).actorId }} · {{ getAnswer(rule.id+':'+check.id).resolvedAtUtc }}</p>
          </div>
        </section>
      </div>
      <div class="grid grid-cols-2 md:grid-cols-5 gap-3"><DsInput v-for="f in hourFields" :id="`q-${index}-hours-${f.key}`" :key="f.key" v-model="item.hours[f.key]" :label="f.label" type="number" step="0.01" /></div>
      <div class="grid md:grid-cols-3 gap-3">
        <DsSelect :id="`q-${index}-mode`" v-model="item.mode" label="Forma de precificação"><option value="horas">Horas × preço-hora da revisão</option><option value="tabelado">Regra tabelada</option><option value="unitario">Quantidade × preço unitário</option><option value="manual">Enquadramento manual justificado</option></DsSelect>
        <DsSelect v-if="item.mode === 'tabelado'" :id="`q-${index}-rule`" v-model="item.ruleId" label="Regra comercial"><option value="">Selecione</option><option v-for="r in policy.rules.filter(r => r.category === item.category)" :key="r.id" :value="r.id">{{ r.label }}{{ r.requiresManual ? ' (exige manual)' : '' }}</option></DsSelect>
        <DsInput v-if="item.mode === 'unitario'" :id="`q-${index}-unit`" v-model="item.unitPrice" label="Preço unitário (R$)" type="number" step="0.01" />
        <DsInput v-if="item.mode === 'manual'" :id="`q-${index}-manual`" v-model="item.manualValue" label="Valor manual do item (R$)" type="number" step="0.01" />
        <p v-if="item.mode === 'horas'" class="text-sm text-gray-500 self-end">Preço-hora congelado: {{ quoteMoney(policy.hourlyRate) }}</p>
      </div>
      <DsInput v-if="item.mode === 'manual'" :id="`q-${index}-justification`" v-model="item.justification" label="Justificativa técnica e comercial do valor" :maxlength="4000" />
      <div class="grid md:grid-cols-3 gap-3"><DsInput :id="`q-${index}-discount-percent`" v-model="item.discountPercent" label="Desconto (%) — ou informe reais" type="number" step="0.01" /><DsInput :id="`q-${index}-discount-value`" v-model="item.discountValue" label="Desconto (R$) — ou informe percentual" type="number" step="0.01" /><DsInput :id="`q-${index}-discount-reason`" v-model="item.discountReason" label="Justificativa do desconto" :maxlength="4000" /></div>
    </fieldset>
    <div v-if="total" class="rounded-xl bg-slate-50 p-3 text-sm flex flex-wrap gap-x-5 gap-y-2"><span>Horas: {{ total.hours }}</span><span>Referência: {{ quoteMoney(total.gross) }}</span><span>Desconto: {{ quoteMoney(total.discount) }}</span><strong>Final: {{ quoteMoney(total.net) }}</strong><p v-if="total.pending" class="w-full text-amber-800 mb-0">{{ total.pending }}</p></div>
  </article>
</template>
<script setup lang="ts">
import { quoteCategories, quoteMoney, type QuoteItem, type QuotePolicy, type QuoteSnapshot } from '~/utils/orcamentos'
const props=defineProps<{ item: QuoteItem; index: number; readonly: boolean; policy: QuotePolicy; total?: QuoteSnapshot['calculation']['items'][number] }>()
function setBlocking(value:boolean){props.item.blockingPending=value;if(!value)props.item.pendingResolution={reason:props.item.pendingResolution?.reason || ''};else props.item.pendingResolution=null}
function getAnswer(id:string){return props.item.checkAnswers?.find(a=>a.id===id)||{id,status:'pendente',reason:''}}
function updateAnswer(id:string,patch:{status?:string;reason?:string}){const entries=props.item.checkAnswers || (props.item.checkAnswers=[]);const i=entries.findIndex(a=>a.id===id);const answer={...getAnswer(id),...patch};if(i<0)entries.push(answer);else entries[i]=answer}
defineEmits<{ remove: [] }>()
const internalFields = [{ key: 'reuse', label: 'Reaproveitamento' }, { key: 'dependencies', label: 'Dependências' }, { key: 'exclusions', label: 'Exclusões internas' }, { key: 'assumptions', label: 'Premissas internas' }] as const
const hourFields = [{ key: 'analysis', label: 'Análise (h)' }, { key: 'planning', label: 'Planejamento (h)' }, { key: 'development', label: 'Desenvolvimento/configuração (h)' }, { key: 'tests', label: 'Testes (h)' }, { key: 'validation', label: 'Ajustes/homologação (h)' }] as const
</script>
<style scoped>
.quote-area { display:block; width:100%; margin-top:.4rem; border:1px solid #d1d5db; border-radius:12px; padding:.75rem; font-size:.875rem; background:#fff; color:#111827; }
.quote-area:focus-visible { outline:2px solid #2563eb; outline-offset:2px; }
fieldset:disabled { opacity:.85; }
</style>
