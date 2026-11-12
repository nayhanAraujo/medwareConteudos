<script setup lang="ts">
import type { StudioDocument, StudioDraft } from '~/types/studioDraft'
import type { VoiceSessionResponse } from '~/types/voice'
const props = defineProps<{destination: 'conventional' | 'integrated'; disabled?: boolean}>()
const emit = defineEmits<{confirmed: [result: {draft?: StudioDraft; session?: VoiceSessionResponse} ]}>()
const api = useApi()
type Candidate = {codVariavel: number; nome: string; unidade?: string; grupo?: string}
type Review = {id: string; expiresAt: string; document: StudioDocument; elementCount: number; warnings: {message: string}[]; associations: {itemId: string; selected: number | null; candidates: Candidate[]}[]}
const review = ref<Review | null>(null)
const busy = ref(false)
const error = ref('')
const name = ref('')
const associations = ref<Record<string, number>>({})
const dialog = ref<HTMLElement | null>(null)
let returnFocus: HTMLElement | null = null
let confirmation: {destination: string; commandId: string; nome: string; associations: Record<string, number>} | null = null
const locked = ref(false)
function close() { if (busy.value) return; review.value = null; error.value = ''; returnFocus?.focus() }
async function upload(event: Event) {
  const input = event.target as HTMLInputElement; const file = input.files?.[0]; input.value = ''
  if (!file || props.disabled || busy.value) return
  error.value = ''
  if (!file.name.toLowerCase().endsWith('.json') || file.size > 1024 * 1024 || !file.size) { error.value = 'Selecione um JSON UTF-8 de até 1 MiB.'; return }
  busy.value = true; returnFocus = document.activeElement as HTMLElement
  try {
    const data = new FormData(); data.append('file', file)
    review.value = await api.postForm<Review>('/api/voice/importacoes/json', data)
    name.value = review.value.document.nome; associations.value = {}; confirmation = null; locked.value = false
    await nextTick(); dialog.value?.focus()
  } catch(e) { error.value = e instanceof Error ? e.message : 'Não foi possível analisar o arquivo.' }
  finally { busy.value = false }
}
async function confirmImport() {
  if (!review.value || busy.value || props.disabled) return
  busy.value = true; error.value = ''
  confirmation ??= {destination: props.destination, commandId: crypto.randomUUID(), nome: name.value.trim(), associations: {...associations.value}}
  locked.value = true
  try {
    const result = await api.post<{draft?: StudioDraft; session?: VoiceSessionResponse}>(`/api/voice/importacoes/json/${review.value.id}/confirmar`, confirmation)
    emit('confirmed', result); review.value = null; returnFocus?.focus()
  } catch(e) { error.value = `${e instanceof Error ? e.message : 'Falha ao confirmar.'} Tentar novamente consulta a mesma confirmação, sem duplicar o trabalho.` }
  finally { busy.value = false }
}
function keys(event: KeyboardEvent) {
  if (event.key === 'Escape') { event.preventDefault(); close() }
  if (event.key !== 'Tab' || !dialog.value) return
  const targets = [...dialog.value.querySelectorAll<HTMLElement>('button:not(:disabled),input:not(:disabled),select:not(:disabled),summary')]
  const first = targets[0]; const last = targets.at(-1)
  if (event.shiftKey && (document.activeElement === first || document.activeElement === dialog.value)) { event.preventDefault(); last?.focus() }
  else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus() }
}
</script>

<template>
  <div class="my-3">
    <label class="inline-flex cursor-pointer items-center gap-2 rounded border border-ds-divider px-3 py-2 text-sm">
      <i class="bi bi-filetype-json"/> {{ busy ? 'Processando JSON…' : 'Importar modelo JSON' }}
      <input type="file" accept=".json,application/json" :disabled="disabled || busy" class="max-w-52" aria-label="Arquivo JSON Studio" @change="upload">
    </label>
    <p v-if="error && !review" role="alert" class="mt-2 text-ds-danger">{{ error }}</p>
    <Teleport to="body">
      <div v-if="review" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
        <div ref="dialog" role="dialog" aria-modal="true" aria-labelledby="json-import-title" tabindex="-1" class="max-h-[90vh] w-full max-w-3xl overflow-auto rounded border border-ds-divider bg-ds-surface-elevated p-5 text-ds-text shadow-xl" @keydown="keys">
          <h2 id="json-import-title" class="text-lg font-semibold">Revisar importação do modelo</h2>
          <p class="my-2 text-sm">{{ review.elementCount }} elementos · {{ review.document.secoes.length }} seções · {{ review.document.secoes.reduce((n,s) => n + s.itens.length, 0) }} medidas · {{ review.document.importedJson?.components.length }} componentes preservados.</p>
          <p class="mb-3 text-sm">Será aberto um trabalho separado. Fórmulas, normalidades e configurações do arquivo serão preservadas. Somente o JSON completo poderá ser exportado nesta versão.</p>
          <p class="mb-3 text-sm">{{ destination === 'conventional' ? 'A sessão convencional expira e não sobrevive ao reinício da API. Baixe o JSON para guardar seu trabalho.' : 'O rascunho integrado terá salvamento automático e histórico.' }}</p>
          <label class="block">Nome do trabalho <input v-model="name" maxlength="120" :disabled="locked" class="my-2 w-full rounded border border-ds-divider bg-ds-surface p-2"></label>
          <ul class="my-3 text-sm"><li v-for="s in review.document.secoes" :key="s.id">Coluna {{ s.coluna }} · {{ s.nome }} · {{ s.itens.length }} medidas</li></ul>
          <details class="my-3"><summary>Associações sugeridas ao catálogo</summary>
            <div v-for="association in review.associations" :key="association.itemId" class="my-2 text-sm">
              <span>{{ review.document.secoes.flatMap(s => s.itens).find(i => i.id === association.itemId)?.descricao }}</span>
              <span v-if="association.selected"> — {{ association.candidates[0]?.nome }} · {{ association.candidates[0]?.unidade }} · {{ association.candidates[0]?.grupo }} (associação sugerida)</span>
              <select v-else-if="association.candidates.length" v-model="associations[association.itemId]" :disabled="locked" class="ml-2 rounded border border-ds-divider bg-ds-surface p-2" :aria-label="`Associar campo ${association.itemId}`">
                <option :value="undefined">Manter sem vínculo</option><option v-for="c in association.candidates" :key="c.codVariavel" :value="c.codVariavel">{{ c.nome }} · {{ c.unidade }} · {{ c.grupo }}</option>
              </select><span v-else> — sem vínculo; conteúdo preservado</span>
            </div>
          </details>
          <details class="my-3"><summary>Componentes preservados (somente leitura)</summary><pre class="max-h-64 overflow-auto whitespace-pre-wrap text-xs">{{ JSON.stringify(review.document.importedJson?.components, null, 2) }}</pre></details>
          <ul class="my-3 text-sm text-amber-500"><li v-for="(w,i) in review.warnings" :key="i">{{ w.message }}</li></ul>
          <p v-if="error" role="alert" class="my-3 text-ds-danger">{{ error }}</p>
          <div class="flex gap-3"><button class="rounded border border-ds-divider px-3 py-2" :disabled="disabled || busy || !name.trim()" @click="confirmImport">{{ locked ? 'Tentar mesma confirmação novamente' : 'Confirmar e abrir novo trabalho' }}</button><button class="rounded border border-ds-divider px-3 py-2" :disabled="busy" @click="close">Cancelar</button></div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
