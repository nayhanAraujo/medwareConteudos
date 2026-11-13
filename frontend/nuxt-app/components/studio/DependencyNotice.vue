<script setup lang="ts">
import type {RemovalBlock} from '~/utils/studioDependencies'
const props=defineProps<{block: RemovalBlock | null}>()
const emit=defineEmits<{close:[];locate:[block:RemovalBlock]}>()
const dialog=ref<HTMLElement|null>(null)
let previous: HTMLElement|null=null
watch(()=>props.block,async block=>{if(block){previous=document.activeElement as HTMLElement;await nextTick();dialog.value?.focus()}else previous?.focus()},{immediate:true})
function keys(e:KeyboardEvent){
  if(e.key==='Escape'){e.preventDefault();emit('close')}
  if(e.key==='Tab'&&dialog.value){const buttons=[...dialog.value.querySelectorAll<HTMLElement>('button,summary')];const first=buttons[0],last=buttons.at(-1);if(e.shiftKey&&(document.activeElement===first||document.activeElement===dialog.value)){e.preventDefault();last?.focus()}else if(!e.shiftKey&&document.activeElement===last){e.preventDefault();first?.focus()}}
}
async function locate(){const block=props.block;emit('close');await nextTick();if(block)emit('locate',block)}
</script>
<template>
  <Teleport to="body"><div v-if="block" class="fixed inset-0 z-[80] flex items-center justify-center bg-black/60 p-4">
    <section ref="dialog" tabindex="-1" role="alertdialog" aria-modal="true" aria-labelledby="dependency-title" aria-describedby="dependency-description" class="max-h-[90vh] w-full max-w-xl overflow-auto rounded-xl border border-amber-500 bg-ds-surface-elevated p-6 text-ds-text shadow-xl" @keydown="keys">
      <h2 id="dependency-title" class="text-xl font-semibold">Não é possível remover {{ block.removed.length===1 ? block.removed[0]?.nome : 'estas medidas' }}</h2>
      <p id="dependency-description" class="my-3">{{ block.removed.length===1 ? 'A medida foi mantida no modelo.' : 'A estrutura foi mantida no modelo.' }} Os cálculos abaixo precisam {{ block.removed.length===1 ? 'dela' : 'dessas medidas' }} para funcionar:</p>
      <p v-if="block.removed.length>1||['deleteSection','clearSection','updateSection'].includes(block.operation)" class="my-3 text-sm"><strong>Medidas necessárias:</strong> {{ block.removed.map(n=>`${n.nome}${n.sectionName?' — '+n.sectionName:''}`).join('; ') }}. A seção e seus campos foram preservados integralmente.</p>
      <ul class="space-y-2"><li v-for="(d,i) in block.dependents" :key="i" class="rounded border border-ds-divider p-3"><strong>{{ d.measure.nome }}</strong><span v-if="d.indirect"> · dependência indireta</span><small v-if="d.measure.sectionName" class="block">Seção {{ d.measure.sectionName }}</small></li></ul>
      <p class="my-3 text-sm">Para concluir a remoção, retire primeiro os cálculos que precisam dessas medidas. Nenhuma medida dependente será removida automaticamente.</p>
      <details class="my-3 text-xs"><summary>Detalhes técnicos</summary><pre class="max-h-32 overflow-auto whitespace-pre-wrap">{{ JSON.stringify(block.removed.map(n=>({nome:n.nome,token:n.token})),null,2) }}</pre></details>
      <div class="flex flex-wrap gap-3"><button class="rounded border border-ds-divider px-4 py-2 focus-visible:outline focus-visible:outline-2" @click="locate">Ver medidas dependentes</button><button class="rounded bg-ds-primary-accent px-4 py-2 font-semibold text-white focus-visible:outline focus-visible:outline-2" @click="emit('close')">Entendi</button></div>
    </section>
  </div></Teleport>
</template>
