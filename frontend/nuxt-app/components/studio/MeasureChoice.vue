<script setup lang="ts">
import type {StudioSection,StudioVariable} from '~/types/studioDraft'
const props=defineProps<{token:string;candidates:StudioVariable[];sections:StudioSection[];section:string|null;busy:boolean;error?:string;stale:boolean;retryCode:number|null}>()
const emit=defineEmits<{section:[value:string|null];choose:[code:number];cancel:[]}>()
const selected=ref<number|null>(null)
watch(()=>props.token,()=>{selected.value=null},{immediate:true})
watch(()=>props.retryCode,value=>{if(value!==null)selected.value=value})
</script>
<template>
  <section class="rounded-xl border border-amber-500 bg-ds-surface-elevated p-4" aria-labelledby="measure-choice-title">
    <h3 id="measure-choice-title" class="font-semibold">Qual medida deseja incluir?</h3>
    <p class="my-2 text-sm">O assistente aguarda sua escolha. Nenhuma opção foi adicionada automaticamente.</p>
    <p v-if="stale&&retryCode===null" role="status" class="my-2 text-amber-500">O modelo mudou. Cancele estas sugestões e solicite a medida novamente.</p>
    <p v-if="error" role="alert" class="my-2 text-ds-danger">{{ error }}</p>
    <label class="block text-sm">Incluir na seção<select :value="section" class="my-2 w-full rounded border border-ds-divider bg-ds-surface p-2" :disabled="busy||retryCode!==null" @change="emit('section',($event.target as HTMLSelectElement).value||null)"><option value="" disabled>Selecione uma seção</option><option v-for="s in sections" :key="s.id" :value="s.id">{{ s.nome }}</option></select></label>
    <div class="max-h-80 space-y-2 overflow-auto"><label v-for="v in candidates" :key="v.codVariavel" class="flex cursor-pointer gap-3 rounded border border-ds-divider p-3"><input v-model="selected" type="radio" :value="v.codVariavel" name="studio-measure-choice" :disabled="busy||retryCode!==null||stale"><span class="min-w-0 text-sm"><strong>{{ v.nome }}</strong><small class="block">{{ v.unidade || 'Unidade não informada' }} · {{ v.grupo || 'Sem grupo' }}</small></span></label></div>
    <button class="mt-3 w-full rounded bg-ds-primary-accent p-3 font-semibold text-white disabled:opacity-50" :disabled="busy||!section||selected===null||(stale&&retryCode===null)" @click="selected!==null&&emit('choose',retryCode??selected)">{{ busy?'Incluindo…':retryCode!==null?'Tentar inclusão novamente':'Incluir medida' }}</button>
    <button class="mt-2 w-full rounded border border-ds-divider p-2" :disabled="busy" @click="emit('cancel')">Cancelar sugestões</button>
  </section>
</template>
