<template>
 <DsPageShell class="my-4">
  <h2 class="text-lg font-semibold">O que falta para concluir este orçamento</h2>
  <p class="text-sm mt-2"><strong>{{ blockers }} impedimento(s)</strong> · {{ issues.length-blockers }} aviso(s)<span v-if="dirty" class="block text-amber-800 mt-2">Pendências da revisão salva. Salve as alterações para revalidar os dados; valores não foram recalculados no navegador.</span></p>
  <p v-if="!issues.length" class="text-sm text-gray-500 mt-3">Nenhuma pendência identificada nesta revisão. A decisão de aprovação continua explícita.</p>
  <ul class="mt-4 space-y-3"><li v-for="(issue,index) in issues" :key="`${issue.code}-${issue.itemIndex}-${index}`" class="rounded-xl border p-3" :class="issue.severity==='impedimento'?'border-amber-300 bg-amber-50':'border-blue-200 bg-blue-50'">
   <strong class="text-sm">{{ issue.severity==='impedimento'?'Impedimento':'Aviso' }}{{ issue.itemIndex!=null ? ` · Item ${issue.itemIndex+1}` : '' }}</strong>
   <p class="text-sm mt-1 whitespace-pre-wrap break-words">{{ issue.message }}</p><p class="text-xs text-gray-500 mt-1">{{ issue.effect }}</p>
   <DsButton size="sm" variant="secondary" class="mt-2" :disabled="busy" @click="$emit('navigate',issue)">{{ issue.action }}</DsButton>
  </li></ul>
 </DsPageShell>
</template>
<script setup lang="ts">
import type {QuoteIssue} from '~/utils/orcamentoTechnical'
const props=defineProps<{issues:QuoteIssue[];dirty:boolean;busy:boolean}>();defineEmits<{navigate:[issue:QuoteIssue]}>()
const blockers=computed(()=>props.issues.filter(i=>i.severity==='impedimento').length)
</script>
