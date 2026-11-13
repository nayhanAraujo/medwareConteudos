<script setup lang="ts">
import type { StudioDraft, StudioSection, StudioItem, StudioVariable } from '~/types/studioDraft'
import type {RemovalBlock} from '~/utils/studioDependencies'
const props=defineProps<{guided?:boolean;realtimeAvailable?:boolean}>()
const { api, draft, entries, history, catalog, problems, busy, error, blocked, sync, pending, selectedSection, selectedItem, preview, outdated, accept, refreshDetails, list, open, create, send, reconcile, discardPending, generate } = useStudioDraft()
const { confirm, toast } = useStudioSwal()
const { downloadText, downloadJson, downloadHtml } = useHtmlPreview()
const voiceSession = useVoiceSessionStore()
const voiceActive = ref(false)
const enableVoice = ref(false)
const archived = ref(false)
const models = ref<Array<{codModelo: number; nome: string}>>([])
const modelId = ref<number | null>(null)
const newName = ref('Novo modelo')
const sectionName = ref('Nova seção')
const search = ref('')
const group = ref('')
const selectedMeasures = ref<number[]>([])
const selectedVersion = ref<number | null>(null)
const editingSection = ref<StudioSection | null>(null)
const editingItem = ref<StudioItem | null>(null)
const proposal = ref<{proposalId: string; nome: string; modeloId: number | null; revision: number; sections: number; items: number} | null>(null)
const publishing = ref(false)
const imageReview = ref<{revision: number; sections: StudioSection[]} | null>(null)
const { choices, choiceSection, choiceBusy, choiceError, choiceStale, choiceCode, receiveChoice, resolveChoice, cancelChoice } = useStudioChoice({draft, selectedSection, api, accept})
const found = ref<StudioVariable[] | null>(null)
const bank = ref(false)
const bankPosition = reactive({x: 20, y: 100})
const drag = ref<{sectionId?: string; itemId?: string; ids?: number[]} | null>(null)
const highlight = ref<string[]>([])
const source=ref<'new'|'open'|'json'|'image'>('new'),sourceOpen=ref(false)
const voiceMode=ref<'continuous'|'steps'>(props.realtimeAvailable===false?'steps':'continuous')
const voicePanel=ref<{stop:()=>Promise<void>}|null>(null),stepPanel=ref<{stop:()=>void;hasPending:boolean}|null>(null)
const mobileColumn=ref(1),downloadFormat=ref('txt'),preservedOpen=ref(false),reviewOpen=ref(false),lastBlocked=ref('')
const sectionLabel=computed(()=>draft.value?.document.secoes.find(s=>s.id===selectedSection.value)?.nome)
watch(()=>draft.value?.id,()=>{sourceOpen.value=false})
watch(()=>draft.value?.document,doc=>{if(props.guided&&!selectedSection.value&&doc?.secoes.length)selectedSection.value=doc.secoes[0]!.id})
watch(blocked,block=>{if(block)lastBlocked.value='Alteração bloqueada: as medidas necessárias aos cálculos foram mantidas.'})
async function switchVoice(mode:'continuous'|'steps'){if(stepPanel.value?.hasPending){toast('Confira a tentativa pendente antes de trocar de modo.','warning');return}await voicePanel.value?.stop();stepPanel.value?.stop();voiceActive.value=false;voiceMode.value=mode}
function locateDependents(block:RemovalBlock){highlight.value=block.dependents.map(d=>d.measure.itemId);const target=block.dependents[0]?.measure;if(!target)return;if(target.preserved){reviewOpen.value=true;preservedOpen.value=true;nextTick(()=>document.querySelector('[data-preserved-components]')?.scrollIntoView({behavior:'smooth',block:'center'}));return}const section=draft.value?.document.secoes.find(s=>s.id===target.sectionId);if(section)mobileColumn.value=section.coluna;nextTick(()=>{const element=[...document.querySelectorAll<HTMLElement>('[data-studio-id]')].find(e=>e.dataset.studioId===target.itemId);element?.scrollIntoView({behavior:'smooth',block:'center'});element?.focus()})}
async function downloadModel(){const format=draft.value?.document.importedJson?'json':downloadFormat.value;if(await generate(format))await downloadPreview()}
async function startImage(event:Event){const input=event.target as HTMLInputElement,file=input.files?.[0];input.value='';if(!file)return;await create(newName.value);const data=new FormData();data.append('image',file);imageReview.value=await api.postForm(`/api/voice/rascunhos/${draft.value!.id}/imagem`,data)}
const byId = computed(() => new Map(catalog.value.map(v => [v.codVariavel, v])))
const groups = computed(() => [...new Set(catalog.value.map(v => v.grupo || 'Sem grupo'))].sort())
const visible = computed(() => (found.value ?? catalog.value).filter(v => !group.value || (v.grupo || 'Sem grupo') === group.value))
const used = computed(() => new Set(draft.value?.document.secoes.flatMap(s => s.itens.map(i => i.codVariavel)) || []))
const context = computed(() => ({revision: draft.value?.revision || 0, sectionId: selectedSection.value, itemId: selectedItem.value}))
const compared = ref<{revision: number; document: import('~/types/studioDraft').StudioDocument} | null>(null)
watch(selectedVersion, async revision => {
  compared.value = null
  if (revision == null || !draft.value) return
  const id = draft.value.id
  try { const value = await api.get<typeof compared.value>(`/api/voice/rascunhos/${id}/historico/${revision}`); if (draft.value?.id === id && selectedVersion.value === revision) compared.value = value } catch {}
})
const publicationHistory = ref<Array<{id: string; nome: string; modeloId: number | null}>>([])
const publicationId = ref('')
let searchTimer: ReturnType<typeof setTimeout> | undefined
watch(search, value => {
  clearTimeout(searchTimer)
  if (!value.trim()) { found.value = null; return }
  searchTimer = setTimeout(async () => { try { const matches = await api.get<StudioVariable[]>(`/api/voice/rascunhos/catalogo?term=${encodeURIComponent(value)}`); if (search.value === value) found.value = matches } catch {} }, 250)
})
watch(() => draft.value?.revision, () => { proposal.value = null })
watch(() => draft.value?.document, (current, before) => {
  if (!current) return
  const old = new Map(before?.secoes.flatMap(s => [[s.id, JSON.stringify({...s, itens: []})], ...s.itens.map(i => [i.id, JSON.stringify(i)])]) as [string,string][] || [])
  highlight.value = current.secoes.flatMap(s => [s.id, ...s.itens.map(i => i.id)]).filter(id => {
    const item = current.secoes.find(s => s.id === id) || current.secoes.flatMap(s => s.itens).find(i => i.id === id)
    return old.get(id) !== JSON.stringify(item)
  })
}, {deep: true})
onMounted(async () => { try { models.value = (await api.get<{data: typeof models.value}>('/api/web/modelos?pageSize=100')).data; publicationHistory.value = await api.get('/api/voice/rascunhos/publicacoes') } catch {} })
onBeforeUnmount(() => clearTimeout(searchTimer))
const label = (item: StudioItem) => item.imported ? item.descricao || String(item.imported.properties.descricao || item.imported.properties.nome || 'Campo importado') : byId.value.get(item.codVariavel || 0)?.nome || item.descricao || 'Campo pendente'
async function guarded(action: () => Promise<unknown>) { try { await action() } catch(e) { error.value = e instanceof Error ? e.message : 'Falha na operação.' } }
async function add(ids: number[], sectionId = selectedSection.value) {
  if (!sectionId) { toast('Selecione uma seção de destino.', 'warning'); return }
  const unique = ids.filter(id => !used.value.has(id))
  if (!unique.length) { toast('Essas medidas já estão no rascunho.', 'info'); return }
  const dependencyIds = new Set<number>()
  function dependencies(id: number, seen = new Set<number>()) { if (seen.has(id)) return; seen.add(id); for (const next of byId.value.get(id)?.dependencias || []) { if (!used.value.has(next) && !unique.includes(next)) dependencyIds.add(next); dependencies(next, seen) } }
  unique.forEach(id => dependencies(id))
  if (unique.length > 1 || dependencyIds.size) {
    const result = await confirm('Incluir medidas?', `${unique.length} selecionadas e ${dependencyIds.size} dependências serão incluídas na seção selecionada.`)
    if (!result.isConfirmed) return
  }
  await send('addMeasures', {sectionId, ids: unique}); selectedMeasures.value = []
}
async function drop(section: StudioSection, position: number) {
  const payload = drag.value; drag.value = null
  if (!payload) return
  if (payload.itemId) await send('moveItem', {itemId: payload.itemId, sectionId: section.id, position})
  else if (payload.ids) await add(payload.ids, section.id)
}
async function deleteDraft() {
  if (!draft.value) return
  const value = draft.value
  const result = await confirm('Excluir rascunho?', 'Todo o histórico de trabalho deste rascunho será removido. Modelos e publicações serão preservados.')
  if (!result.isConfirmed) return
  await api.del(`/api/voice/rascunhos/${value.id}?revision=${value.revision}&confirm=true`); draft.value = null; await list(archived.value)
}
async function prepare(copy: boolean) {
  if (!draft.value) return
  proposal.value = await api.post(`/api/voice/rascunhos/${draft.value.id}/publicacoes`, {expectedRevision: draft.value.revision, modeloId: copy ? null : draft.value.document.modeloOrigem, nome: draft.value.document.nome})
}
async function publish() {
  if (!draft.value || !proposal.value) return
  publishing.value = true
  try { await api.post(`/api/voice/rascunhos/${draft.value.id}/publicacoes/confirmar`, {proposalId: proposal.value.proposalId}); proposal.value = null; await open(draft.value.id); toast('Modelo publicado.', 'success') }
  catch(e) { error.value = e instanceof Error ? e.message : 'Não foi possível publicar.' }
  finally { publishing.value = false }
}
async function downloadPreview() {
  if (!(await refreshDetails()) || outdated.value || !preview.value) { toast('Confira a revisão e gere novamente o preview antes de baixar.', 'warning'); return }
  const value = preview.value
  if (value.format === 'txt') downloadText(value.content, 'laudo.txt')
  else if (value.format === 'json') downloadJson(value.content, 'laudo.json')
  else downloadHtml(value.content, 'laudo.html')
}
async function importImage(event: Event) {
  const input = event.target as HTMLInputElement; const file = input.files?.[0]; input.value = ''
  if (!file || !draft.value) return
  const data = new FormData(); data.append('image', file)
  imageReview.value = await api.postForm(`/api/voice/rascunhos/${draft.value.id}/imagem`, data)
}
async function importLocalOverrides() {
  const model = draft.value?.document.modeloOrigem
  if (!model) return
  // Legacy keys are read only, never removed on import failure.
  const key = `studio-modelos-norm-overrides:v1:${model}`
  const raw = localStorage.getItem(key)
  if (!raw) { toast('Nenhum ajuste local identificado para este modelo.', 'info'); return }
  const values = JSON.parse(raw) as Record<string,string>
  if (!(await confirm('Importar ajustes locais?', `${Object.keys(values).length} ajustes serão copiados. O armazenamento antigo será preservado.`)).isConfirmed) return
  for (const item of draft.value!.document.secoes.flatMap(s => s.itens)) if (item.codVariavel && typeof values[item.codVariavel] === 'string') {
    await send('editItem', {itemId: item.id, normality: values[item.codVariavel]}); if (pending.value) break
  }
}
function moveBank(event: PointerEvent) {
  const element = event.currentTarget as HTMLElement; element.setPointerCapture(event.pointerId)
  const start = {x: event.clientX - bankPosition.x, y: event.clientY - bankPosition.y}
  const move = (e: PointerEvent) => { bankPosition.x = Math.max(0, Math.min(window.innerWidth - 250, e.clientX - start.x)); bankPosition.y = Math.max(0, Math.min(window.innerHeight - 60, e.clientY - start.y)) }
  element.addEventListener('pointermove', move)
  element.addEventListener('pointerup', () => element.removeEventListener('pointermove', move), {once: true})
}
function keyboard(event: KeyboardEvent) {
  if (choices.value||blocked.value) return
  if ((event.target as HTMLElement)?.matches('input,textarea,select,[contenteditable]') || !(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== 'z') return
  event.preventDefault(); void send(event.shiftKey ? 'redo' : 'undo', {})
}
function scrollToItem(id?: string) { if (id) document.querySelector(`[data-studio-id='${id}']`)?.scrollIntoView({behavior: 'smooth'}) }
onMounted(() => window.addEventListener('keydown', keyboard))
onBeforeUnmount(() => window.removeEventListener('keydown', keyboard))
</script>

<template>
  <div class="space-y-4" :class="{'guided-editor':guided}">
    <StudioDependencyNotice :block="blocked" @close="blocked=null" @locate="locateDependents" />
    <StudioDsCard v-if="!guided||!draft||sourceOpen" :title="guided?'Como deseja começar?':'Studio integrado · rascunhos privados'">
      <div v-if="guided" class="mb-4 grid gap-3 sm:grid-cols-4"><button v-for="option in [{id:'new',label:'Novo modelo'},{id:'open',label:'Abrir modelo'},{id:'json',label:'Importar JSON'},{id:'image',label:'Usar imagem'}]" :key="option.id" class="rounded-xl border p-4 text-left font-semibold" :class="source===option.id?'border-blue-500 bg-ds-surface':'border-ds-divider'" :disabled="voiceActive||busy||!!pending" @click="source=option.id as typeof source">{{ option.label }}</button></div>
      <StudioJsonImport v-if="!guided||source==='json'" destination="integrated" :disabled="voiceActive || busy || !!pending" @confirmed="result => { if (result.draft) {accept(result.draft); list()} }" />
      <label v-if="guided&&source==='image'" class="my-3 block rounded-lg border border-ds-divider p-4">Selecione uma imagem do modelo<input type="file" accept="image/png,image/jpeg,image/webp" :disabled="busy||voiceActive||!!pending" @change="guarded(()=>startImage($event))"></label>
      <div v-if="!guided||source==='new'||source==='open'" class="flex flex-wrap gap-2">
        <input v-model="newName" aria-label="Nome do novo rascunho" class="field" maxlength="120">
        <select v-if="!guided||source==='open'" v-model="modelId" class="field" aria-label="Modelo de origem"><option :value="null">Selecione um modelo</option><option v-for="m in models" :key="m.codModelo" :value="m.codModelo">{{ m.nome }}</option></select>
        <StudioDsButton :disabled="voiceActive || busy || !!pending || (guided&&source==='open'&&!modelId)" @click="guarded(() => create(newName, guided&&source==='new'?null:modelId))">{{ guided?(source==='open'?'Abrir como novo trabalho':'Começar modelo'):'Criar rascunho' }}</StudioDsButton>
        <StudioDsButton v-if="voiceSession.sessionId" variant="secondary" :disabled="voiceActive || busy || !!pending" @click="guarded(() => create(newName, null, voiceSession.sessionId!))">Importar sessão de voz aberta</StudioDsButton>
      </div>
      <label class="mt-4 block font-semibold">Continuar um trabalho</label>
      <select :value="draft?.id || ''" aria-label="Continuar um trabalho" class="field mt-2 w-full" :disabled="voiceActive || busy || !!pending" @change="guarded(() => open(($event.target as HTMLSelectElement).value))">
        <option value="" disabled>Escolha um trabalho salvo</option><option v-for="d in entries" :key="d.id" :value="d.id">{{ d.document.nome }} · {{ new Date(d.updatedAt).toLocaleString('pt-BR') }}</option>
      </select>
      <details class="mt-3 text-sm"><summary>Outras opções</summary><label class="mt-3 block"><input v-model="archived" type="checkbox" @change="guarded(() => list(archived))"> Mostrar arquivados</label>
      <div v-if="publicationHistory.length" class="mt-3 flex flex-wrap gap-2"><select v-model="publicationId" class="field"><option value="">Versões publicadas preservadas</option><option v-for="p in publicationHistory" :key="p.id" :value="p.id">{{ p.nome }} · {{ p.id.slice(0,8) }}</option></select><button class="action" :disabled="!publicationId || voiceActive || busy || !!pending" @click="guarded(async () => {accept(await api.post('/api/voice/rascunhos', {nome: newName, publicacaoId: publicationId})); await list()})">Abrir publicação como novo rascunho</button></div>
      </details>
    </StudioDsCard>
    <p v-if="error" role="alert" class="text-ds-danger">{{ error }}</p>
    <div v-if="pending" class="rounded border border-amber-500 p-3 text-sm">
      Comando pendente: {{ pending.type }}. O estado exibido é o último confirmado.
      <button class="action" :disabled="busy" @click="reconcile(true)">Consultar resultado e tentar novamente</button>
      <button class="action" :disabled="busy" @click="discardPending">Conferir estado e descartar tentativa</button>
    </div>
    <div v-if="draft" :class="guided?'guided-workspace':'space-y-4'">
      <header v-if="guided" class="guided-header rounded-xl border border-ds-divider bg-ds-surface-elevated p-4"><div class="flex flex-wrap items-center justify-between gap-3"><div><h1 class="text-2xl font-semibold">{{ draft.document.nome }}</h1><p role="status" class="mt-1 text-sm">{{ sync }}</p></div><div class="flex flex-wrap gap-2"><button class="action" :disabled="voiceActive||busy||!!pending" @click="sourceOpen=!sourceOpen">Abrir outro modelo</button><button v-if="!draft.document.importedJson" class="action" :disabled="busy||!!pending||problems.length>0" @click="guarded(()=>prepare(!draft!.document.modeloOrigem))">Salvar modelo</button><select v-if="!draft.document.importedJson" v-model="downloadFormat" aria-label="Formato para baixar o modelo" class="field"><option value="txt">Modo texto</option><option value="json">JSON Studio</option><option value="html">HTML</option></select><button class="action" :disabled="busy||!!pending||(!draft.document.importedJson&&problems.length>0)" @click="guarded(downloadModel)">Baixar modelo</button></div></div><p v-if="draft.document.importedJson" class="mt-2 text-sm">Modelo importado: o download JSON preserva todos os componentes e configurações.</p></header>
      <StudioDsCard v-if="!guided" :title="draft.document.nome">
        <p role="status" class="mb-3 text-sm">{{ sync }} · revisão {{ draft.revision }}</p>
        <div class="flex flex-wrap gap-2">
          <input :value="draft.document.nome" class="field" aria-label="Nome do rascunho" :disabled="busy || !!pending" @change="send('rename', {name: ($event.target as HTMLInputElement).value})">
          <button class="action" :disabled="busy || !!pending" @click="send('undo', {})">Desfazer</button><button class="action" :disabled="busy || !!pending" @click="send('redo', {})">Refazer</button>
          <button class="action" @click="bank = !bank">Banco de medidas</button>
          <button class="action" :disabled="voiceActive || busy || !!pending" @click="send(draft.archived ? 'unarchive' : 'archive', {}).then(() => list(archived))">{{ draft.archived ? 'Desarquivar' : 'Arquivar' }}</button>
          <button class="action" :disabled="voiceActive || busy || !!pending" @click="guarded(deleteDraft)">Excluir rascunho e histórico</button>
          <button v-if="draft.document.modeloOrigem" class="action" :disabled="busy || !!pending" @click="guarded(importLocalOverrides)">Importar normalidades locais</button>
        </div>
        <label class="mt-3 block text-sm"><input v-model="enableVoice" type="checkbox" :disabled="voiceActive"> Editar este rascunho por voz</label>
        <StudioRealtimeVoicePanel v-if="enableVoice" :key="draft.id" :draft-id="draft.id" :context="context" @active="voiceActive = $event" @updated="accept($event)" @choice="receiveChoice" @blocked="blocked=$event" />
      </StudioDsCard>
      <aside v-if="guided" class="guided-assistant space-y-4">
        <StudioRealtimeVoicePanel v-if="voiceMode==='continuous'" ref="voicePanel" :key="draft.id" friendly :draft-id="draft.id" :context="context" :catalog="catalog" :section-name="sectionLabel" :awaiting-choice="!!choices" @active="voiceActive=$event" @updated="accept($event)" @choice="receiveChoice" @blocked="blocked=$event" @fallback="switchVoice('steps')" />
        <StudioDraftVoiceSteps v-else ref="stepPanel" :key="draft.id" :draft="draft" @updated="accept($event)" @blocked="blocked=$event" @active="voiceActive=$event" />
        <StudioMeasureChoice v-if="choices" :token="choices.token" :candidates="choices.candidates" :sections="draft.document.secoes" :section="choiceSection" :busy="choiceBusy" :error="choiceError" :stale="choiceStale" :retry-code="choiceCode" @section="choiceSection=$event" @choose="resolveChoice" @cancel="cancelChoice" />
        <div class="rounded-xl border border-ds-divider p-4 text-sm"><p>Seção selecionada: <strong>{{ sectionLabel||'selecione uma seção no modelo' }}</strong></p><div class="mt-2 flex flex-wrap gap-2"><button class="action" :disabled="busy||!!pending||!!choices" @click="send('undo',{})">Desfazer</button><button class="action" :disabled="busy||!!pending||!!choices" @click="send('redo',{})">Refazer</button></div><p v-if="lastBlocked" role="status" class="mt-2 text-amber-500">{{ lastBlocked }}</p></div>
        <details class="rounded-xl border border-ds-divider p-4 text-sm"><summary>Opções de edição</summary><div class="mt-3 space-y-2"><button v-if="realtimeAvailable!==false" class="action" :aria-pressed="voiceMode==='continuous'" @click="switchVoice('continuous')">Conversação contínua</button><button class="action" :aria-pressed="voiceMode==='steps'" @click="switchVoice('steps')">Gravação por etapas</button><label class="block">Nome do modelo<input :value="draft.document.nome" class="field w-full" :disabled="busy||!!pending" @change="send('rename',{name:($event.target as HTMLInputElement).value})"></label><button v-if="!draft.document.importedJson&&draft.document.modeloOrigem" class="action" :disabled="busy||!!pending||problems.length>0" @click="guarded(()=>prepare(true))">Salvar como cópia</button><button class="action" :disabled="voiceActive||busy||!!pending" @click="send(draft!.archived?'unarchive':'archive',{}).then(()=>list(archived))">{{ draft.archived?'Desarquivar':'Arquivar' }}</button><button class="action" :disabled="voiceActive||busy||!!pending" @click="guarded(deleteDraft)">Excluir trabalho e histórico</button></div></details>
        <details class="rounded-xl border border-ds-divider p-4 text-sm"><summary>Transcrição completa</summary><p v-for="(entry,n) in voiceSession.transcriptHistory" :key="n" class="mt-2">{{ entry.transcript }}</p><p v-if="!voiceSession.transcriptHistory.length" class="mt-2">Nenhuma fala registrada nesta sessão.</p></details>
      </aside>
      <div :class="guided?'guided-model space-y-4':'space-y-4'">
      <div v-if="guided" class="flex flex-wrap items-center justify-between gap-2"><h2 class="text-lg font-semibold">Seu modelo</h2><button class="action" @click="bank=!bank">Adicionar medida</button></div>
      <div class="flex flex-wrap gap-2"><input v-model="sectionName" class="field" aria-label="Nome da nova seção"><button v-for="col in [1,2]" :key="col" class="action" :disabled="busy || !!pending || draft.archived" @click="send('createSection', {name: sectionName, column: col})">Criar seção na coluna {{ col }}</button></div>
      <div v-if="guided" class="flex gap-2 lg:hidden"><button v-for="col in [1,2]" :key="col" class="action" :aria-pressed="mobileColumn===col" @click="mobileColumn=col">Coluna {{ col }}</button></div>
      <div class="grid gap-4 lg:grid-cols-2">
        <div v-for="column in [1,2]" :key="column" class="min-h-32 rounded border border-ds-divider p-3" :class="{'guided-hidden-column':guided&&mobileColumn!==column}" @dragover.prevent @drop.prevent="drag?.sectionId && send('editSection', {sectionId: drag.sectionId, name: draft.document.secoes.find(s => s.id === drag?.sectionId)?.nome, column, position: draft.document.secoes.length}).then(() => drag = null)">
          <h3 class="mb-3">{{ column === 1 ? 'Coluna esquerda' : 'Coluna direita' }}</h3>
          <section v-for="section in draft.document.secoes.filter(s => s.coluna === column)" :key="section.id" :data-studio-id="section.id" tabindex="0" :aria-label="`Seção ${section.nome}`" @keydown.enter.self="selectedSection=section.id" class="mb-3 rounded border p-3" :class="selectedSection === section.id ? 'border-blue-500' : 'border-ds-divider'" @click="selectedSection = section.id">
            <header draggable="true" class="mb-2 flex flex-wrap items-center gap-2" @dragstart.stop="drag = {sectionId: section.id}">
              <strong class="flex-1">{{ section.nome }}</strong><button class="action" @click.stop="editingSection = JSON.parse(JSON.stringify(section))">Editar / mover</button>
              <button class="action" :disabled="busy || !!pending" @click.stop="confirm('Esvaziar seção?', 'As medidas serão removidas apenas do rascunho.').then(r => r.isConfirmed && send('clearSection', {sectionId: section.id}))">Esvaziar</button>
              <button class="action" :disabled="busy || !!pending" @click.stop="confirm('Excluir seção?', 'Os campos desta seção sairão do rascunho.').then(r => r.isConfirmed && send('deleteSection', {sectionId: section.id}))">Excluir</button>
            </header>
            <div v-for="(item, index) in section.itens" :key="item.id" :data-studio-id="item.id" tabindex="-1" draggable="true" class="mb-2 rounded bg-ds-surface p-2 text-sm" :class="{'ring-1 ring-blue-500': highlight.includes(item.id), 'border border-amber-500': !item.codVariavel}" @dragstart.stop="drag = {itemId: item.id}" @dragover.stop.prevent @drop.stop.prevent="drop(section, index)" @click.stop="selectedSection = section.id; selectedItem = item.id">
              <span>{{ label(item) }}</span><small class="ml-2"><span v-if="!guided">{{ item.imported?.properties.nome ?? byId.get(item.codVariavel || 0)?.sigla }} </span>{{ item.imported?.properties.medida ?? byId.get(item.codVariavel || 0)?.unidade }}</small>
              <small v-if="item.imported&&!guided" class="ml-2">{{ item.imported.properties.funcao || item.imported.properties.formula ? 'ƒ' : '' }} Configuração do arquivo {{ item.exibirGrafico ? 'Gráfico' : '' }}</small>
              <small v-else-if="!guided" class="ml-2">{{ byId.get(item.codVariavel || 0)?.formula ? 'ƒ' : '' }} {{ item.normalidadeOverride != null ? 'Normalidade personalizada' : byId.get(item.codVariavel || 0)?.normalidade ? 'Normalidade do banco' : '' }} {{ item.exibirGrafico ? 'Gráfico' : '' }}</small>
              <button class="action" @click.stop="editingItem = JSON.parse(JSON.stringify(item))">Detalhes / editar / mover</button>
              <button class="action" :disabled="busy || !!pending" @click.stop="send('deleteItem', {itemId: item.id})">Remover</button>
            </div>
            <div class="rounded border border-dashed border-ds-divider p-3 text-sm" @dragover.stop.prevent @drop.stop.prevent="drop(section, section.itens.length)">Solte medidas aqui ou selecione esta seção e use o banco de medidas.</div>
          </section>
        </div>
      </div>
      </div>
      <details :open="!guided||reviewOpen" :class="{'guided-review':guided}"><summary v-if="guided" class="cursor-pointer rounded border border-ds-divider p-3">Revisão, prévia e detalhes do modelo</summary>
      <StudioDsCard title="Revisão e exportação">
        <p v-if="draft.document.importedJson" class="mb-3 text-sm">JSON importado: somente download JSON completo e rascunho. Publicação, TXT, HTML e mesclagem por imagem estão desabilitados para preservar todos os componentes.</p>
        <details v-if="draft.document.importedJson" :open="preservedOpen" data-preserved-components class="my-3"><summary>Componentes preservados (somente leitura)</summary><pre class="max-h-64 overflow-auto whitespace-pre-wrap text-xs">{{ JSON.stringify(draft.document.importedJson.components, null, 2) }}</pre></details>
        <ul><li v-for="(problem, n) in problems" :key="n"><button class="text-amber-500" @click="scrollToItem(problem.itemId)">{{ problem.message }}</button></li></ul>
        <div class="mt-3 flex flex-wrap gap-2">
          <button v-for="format in draft.document.importedJson ? ['json'] : ['txt','json','html']" :key="format" class="action" :disabled="busy || !!pending || (!draft.document.importedJson && problems.length > 0)" @click="generate(format)">Gerar {{ format.toUpperCase() }}</button>
          <button v-if="!draft.document.importedJson" class="action" :disabled="busy || !!pending || problems.length > 0" @click="guarded(() => prepare(true))">Salvar como novo modelo</button>
          <button v-if="!draft.document.importedJson&&draft.document.modeloOrigem" class="action" :disabled="busy || !!pending || problems.length > 0" @click="guarded(() => prepare(false))">Atualizar modelo original</button>
          <label v-if="!draft.document.importedJson" class="action">Importar imagem <input type="file" accept="image/png,image/jpeg,image/webp" :disabled="busy || !!pending" @change="guarded(() => importImage($event))"></label>
        </div>
        <div v-if="preview" class="mt-3"><p>Revisão {{ preview.revision }} <span v-if="outdated" class="text-amber-500">— Preview desatualizado: gere novamente</span></p><StudioModoTextoPreview v-if="preview.format === 'txt'" :text="preview.content"/><StudioHtmlPreviewFrame v-else-if="preview.format === 'html'" :html="preview.content"/><pre v-else class="max-h-64 overflow-auto text-xs">{{ preview.content }}</pre>
          <button class="action" :disabled="outdated || busy" @click="downloadPreview">Baixar exatamente esta geração</button>
        </div>
      </StudioDsCard>
      </details>
      <details :open="!guided" :class="{'guided-history':guided}"><summary v-if="guided" class="cursor-pointer rounded border border-ds-divider p-3">Histórico e versões anteriores</summary>
      <StudioDsCard title="Histórico de ações e versões">
        <select v-model="selectedVersion" class="field"><option :value="null">Comparar uma versão</option><option v-for="h in history" :key="h.revision" :value="h.revision">{{ h.revision }} · {{ h.description }} · {{ h.source }}</option></select>
        <div v-if="compared" class="mt-2"><p>{{ compared.document.secoes.length }} seções na revisão {{ compared.revision }}; {{ draft.document.secoes.length }} na atual. Metadados são consultados no banco atual.</p><div class="grid gap-3 lg:grid-cols-2"><pre class="max-h-64 overflow-auto text-xs">{{ JSON.stringify(compared.document, null, 2) }}</pre><pre class="max-h-64 overflow-auto text-xs">{{ JSON.stringify(draft.document, null, 2) }}</pre></div><button class="action" :disabled="busy || !!pending" @click="confirm('Restaurar esta versão?', 'A restauração cria outra revisão e preserva o histórico.').then(r => r.isConfirmed && send('restore', {revision: compared!.revision}))">Restaurar como nova revisão</button></div>
        <ol class="mt-3 text-sm"><li v-for="h in [...history].reverse()" :key="h.revision">{{ h.revision }} · {{ h.description }} · {{ h.source }} · {{ new Date(h.createdAt).toLocaleString('pt-BR') }}</li></ol>
      </StudioDsCard>
      </details>
    </div>
    <Teleport to="body">
      <aside v-if="bank && draft" class="fixed z-40 w-[min(32rem,95vw)] rounded border border-ds-divider bg-ds-surface-elevated p-4 shadow-xl" :style="{left: `${bankPosition.x}px`, top: `${bankPosition.y}px`}">
        <header class="mb-2 flex cursor-move justify-between" style="touch-action:none" @pointerdown="moveBank"><strong>Banco de medidas</strong><button @pointerdown.stop @click="bank = false">Fechar</button></header>
        <input v-model="search" class="field w-full" placeholder="Nome, sigla, abreviação ou alternativa"><select v-model="group" class="field my-2 w-full"><option value="">Todos os grupos</option><option v-for="g in groups" :key="g">{{ g }}</option></select>
        <div class="max-h-[45vh] overflow-auto"><label v-for="v in visible" :key="v.codVariavel" draggable="true" class="mb-2 block text-sm" @dragstart="drag = {ids: selectedMeasures.includes(v.codVariavel) ? [...selectedMeasures] : [v.codVariavel]}"><input v-model="selectedMeasures" type="checkbox" :value="v.codVariavel" :disabled="used.has(v.codVariavel)"> {{ v.nome }} ({{ v.sigla }}) {{ v.unidade }} <small>{{ v.grupo }} {{ used.has(v.codVariavel) ? '— já incluída' : '' }}</small></label></div>
        <button class="action" :disabled="busy || !!pending" @click="add(selectedMeasures)">Incluir selecionadas na seção selecionada</button><button v-if="group" class="action" :disabled="busy || !!pending" @click="add(visible.map(v => v.codVariavel))">Revisar inclusão do grupo</button>
      </aside>
      <div v-if="editingSection || editingItem || proposal || imageReview || (!guided&&choices)" class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
        <div role="dialog" aria-modal="true" aria-label="Revisar alteração do Studio" class="max-h-[90vh] w-full max-w-xl overflow-auto rounded-lg border border-ds-divider bg-ds-surface-elevated p-5 text-ds-text">
          <template v-if="editingSection"><h3>Editar seção</h3><input v-model="editingSection.nome" class="field w-full"><select v-model="editingSection.coluna" class="field"><option :value="1">Esquerda</option><option :value="2">Direita</option></select><div class="mt-3 flex flex-wrap gap-2"><button class="action" @click="send('editSection', {sectionId: editingSection.id, name: editingSection.nome, column: editingSection.coluna, position: 0}).then(() => editingSection = null)">Salvar e mover para o início</button><button class="action" @click="send('editSection', {sectionId: editingSection.id, name: editingSection.nome, column: editingSection.coluna, position: draft!.document.secoes.length}).then(() => editingSection = null)">Salvar e mover para o fim</button><button class="action" @click="editingSection = null">Cancelar</button></div></template>
          <template v-else-if="editingItem">
            <h3>{{ label(editingItem) }}</h3>
            <pre class="my-2 max-h-48 overflow-auto whitespace-pre-wrap text-xs">{{ JSON.stringify(editingItem.imported?.properties || byId.get(editingItem.codVariavel || 0), null, 2) }}</pre>
            <label class="block">Vínculo com o catálogo<select v-model="editingItem.codVariavel" class="field w-full"><option :value="null">Pendente</option><option v-for="v in catalog" :key="v.codVariavel" :value="v.codVariavel">{{ v.nome }} — {{ v.sigla }}</option></select></label>
            <button v-if="draft?.document.importedJson" class="action" @click="send('editItem', {itemId: editingItem.id, codVariavel: editingItem.codVariavel}).then(() => editingItem = null)">Salvar associação (preservar configurações)</button>
            <template v-else>
              <label class="block mt-2"><input v-model="editingItem.exibirGrafico" type="checkbox"> Exibir gráfico</label>
              <label class="block mt-2">Normalidade personalizada (apenas neste modelo)<textarea v-model="editingItem.normalidadeOverride" class="field w-full"/></label>
              <button class="action" @click="editingItem.normalidadeOverride = null">Usar normalidade do banco</button>
              <button class="action" @click="send('editItem', {itemId: editingItem.id, codVariavel: editingItem.codVariavel, chart: editingItem.exibirGrafico, normality: editingItem.normalidadeOverride}).then(() => editingItem = null)">Salvar ajustes</button>
            </template>
            <h4 class="mt-3">Mover para</h4><button v-for="s in draft!.document.secoes" :key="s.id" class="action" @click="send('moveItem', {itemId: editingItem.id, sectionId: s.id, position: 0}).then(() => editingItem = null)">{{ s.nome }} · início</button><button class="action" @click="editingItem = null">Fechar</button>
          </template>
          <template v-else-if="proposal"><h3>Confirmar publicação</h3><p>{{ proposal.nome }} · revisão {{ proposal.revision }} · {{ proposal.sections }} seções · {{ proposal.items }} medidas.</p><p>{{ proposal.modeloId ? 'A composição do modelo de destino será substituída.' : 'Será criado um novo modelo; o original será preservado.' }}</p><button class="action" :disabled="publishing" @click="publish">Confirmar publicação</button><button class="action" :disabled="publishing" @click="proposal = null">Cancelar</button></template>
          <template v-else-if="imageReview"><h3>Revisar estrutura extraída</h3><p>Campos sem associação continuarão pendentes. Nenhuma alteração foi aplicada ainda.</p><ul><li v-for="s in imageReview.sections" :key="s.id">{{ s.nome }}<ul><li v-for="i in s.itens" :key="i.id">{{ i.descricao }} · {{ i.codVariavel ? 'associação encontrada' : 'vínculo pendente' }}</li></ul></li></ul><button class="action" :disabled="imageReview.revision !== draft?.revision" @click="send('importSections', {sections: imageReview.sections}).then(() => imageReview = null)">Aplicar como operação reversível</button><button class="action" @click="imageReview = null">Cancelar</button></template>
          <StudioMeasureChoice v-else-if="choices" :token="choices.token" :candidates="choices.candidates" :sections="draft!.document.secoes" :section="choiceSection" :busy="choiceBusy" :error="choiceError" :stale="choiceStale" :retry-code="choiceCode" @section="choiceSection=$event" @choose="resolveChoice" @cancel="cancelChoice" />
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.field { border:1px solid var(--color-ds-divider); border-radius:6px; padding:8px; background:var(--color-ds-surface); color:var(--color-ds-text); }
.action { border:1px solid var(--color-ds-divider); border-radius:6px; padding:6px 10px; font-size:.8rem; margin:3px; }
.action:hover:not(:disabled) { background:var(--color-ds-surface-elevated); }
.action:disabled { opacity:.45; cursor:not-allowed; }
.guided-workspace{display:grid;gap:1rem;grid-template-areas:'header' 'assistant' 'model' 'review' 'history';grid-template-columns:minmax(0,1fr)}
.guided-header{grid-area:header}.guided-model{grid-area:model;min-width:0}.guided-assistant{grid-area:assistant;min-width:0;align-self:start}.guided-review{grid-area:review;min-width:0}.guided-history{grid-area:history;min-width:0}
@media(min-width:1280px){.guided-workspace{grid-template-columns:minmax(0,1fr) minmax(320px,380px);grid-template-areas:'header header' 'model assistant' 'review assistant' 'history assistant';align-items:start}.guided-assistant{position:sticky;top:1rem;max-height:calc(100vh - 2rem);overflow:auto}}
@media(max-width:1023px){.guided-assistant{position:sticky;top:0;z-index:20;max-height:45vh;overflow:auto;background:var(--color-ds-surface)}.guided-hidden-column{display:none}.guided-assistant :deep(button.bi-mic),.guided-assistant :deep(.min-h-14){position:sticky;top:.5rem;z-index:10}}
</style>
