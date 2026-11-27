<script setup lang="ts">
import type {StudioSection,StudioVariable} from '~/types/studioDraft'
const props=defineProps<{token:string;candidates:StudioVariable[];sections:StudioSection[];section:string|null;busy:boolean;error?:string;stale:boolean;retryCode:number|null}>()
const emit=defineEmits<{section:[value:string|null];choose:[code:number];cancel:[]}>()
const dialog = ref<HTMLDialogElement | null>(null)
const selected = ref<number | null>(props.retryCode)
watch(()=>props.token,()=>{selected.value=props.retryCode})
watch(()=>props.retryCode,value=>{if(value!==null)selected.value=value})

onMounted(() => dialog.value?.showModal())
onBeforeUnmount(() => dialog.value?.close())

function cancel() {
  if (!props.busy) emit('cancel')
}
</script>
<template>
  <Teleport to="body">
    <dialog ref="dialog" class="measure-choice rounded-xl border border-ds-divider bg-ds-surface-elevated p-0 text-ds-text shadow-xl" aria-labelledby="measure-choice-title" aria-describedby="measure-choice-description" @cancel.prevent="cancel">
      <header class="shrink-0 border-b border-ds-divider px-4 py-4 sm:px-6">
        <h2 id="measure-choice-title" class="text-xl font-semibold" tabindex="-1" autofocus>Qual medida deseja incluir?</h2>
        <p id="measure-choice-description" class="mt-1 text-sm">Escolha uma medida e confirme a inclusão na seção.</p>
      </header>

      <div class="min-h-0 space-y-3 overflow-y-auto overscroll-contain px-4 py-4 sm:px-6">
        <p v-if="stale && retryCode === null" role="status" class="text-amber-500">O modelo mudou. Cancele estas sugestões e solicite a medida novamente.</p>
        <p v-if="error" role="alert" class="text-ds-danger">{{ error }}</p>
        <label class="block text-sm">
          Incluir na seção
          <select :value="section" class="mt-1 w-full rounded border border-ds-divider bg-ds-surface p-2" :disabled="busy || retryCode !== null" @change="emit('section', ($event.target as HTMLSelectElement).value || null)">
            <option value="" disabled>Selecione uma seção</option>
            <option v-for="s in sections" :key="s.id" :value="s.id">{{ s.nome }}</option>
          </select>
        </label>
        <fieldset class="min-w-0 space-y-2">
          <legend class="sr-only">Medidas sugeridas</legend>
          <label v-for="v in candidates" :key="v.codVariavel" class="flex cursor-pointer items-center gap-3 rounded-lg border border-ds-divider p-3 has-checked:border-ds-primary-accent">
            <input v-model="selected" type="radio" :value="v.codVariavel" name="studio-measure-choice" :disabled="busy || retryCode !== null || stale">
            <span class="min-w-0 break-words text-sm"><strong>{{ v.nome }}</strong><small class="block">{{ v.unidade || 'Unidade não informada' }} · {{ v.grupo || 'Sem grupo' }}</small></span>
          </label>
        </fieldset>
      </div>

      <footer class="flex shrink-0 flex-wrap justify-end gap-2 border-t border-ds-divider px-4 py-3 sm:px-6">
        <button type="button" class="rounded border border-ds-divider px-4 py-2 disabled:opacity-50" :disabled="busy" @click="cancel">Cancelar sugestões</button>
        <button type="button" class="rounded bg-ds-primary-accent px-4 py-2 font-semibold text-white disabled:opacity-50" :disabled="busy || !section || selected === null || (stale && retryCode === null)" @click="selected !== null && emit('choose', retryCode ?? selected)">
          {{ busy ? 'Incluindo…' : retryCode !== null ? 'Tentar inclusão novamente' : 'Incluir medida' }}
        </button>
      </footer>
    </dialog>
  </Teleport>
</template>

<style scoped>
.measure-choice {
  width: min(560px, calc(100vw - 32px));
  max-height: calc(100dvh - 32px);
  margin: auto;
}
.measure-choice[open] { display: flex; flex-direction: column; }
.measure-choice::backdrop { background: #0008; }
.measure-choice :is(button, select, input):focus-visible {
  outline: 2px solid var(--color-ds-primary-accent, #0f6cbd);
  outline-offset: 2px;
}
</style>
