<script setup lang="ts">
import type { CampoScript } from '~/types/voice'

const props = defineProps<{
  campos: CampoScript[]
  friendly?:boolean
  removable?:boolean
  disabled?:boolean
  title?:string
  highlighted?: string[]
  movable?: boolean
  dragContext?: string
  destinations?: { id: string; title: string }[]
}>()
const emit=defineEmits<{remove:[sourceId:string];move:[value:{sourceId:string;sectionId:string;beforeItemId:string|null}]}>()
const dragging = ref<string | null>(null)
const dropTarget = ref<string | null>(null)
function clearDrag() { dragging.value = null; dropTarget.value = null }
onMounted(() => document.addEventListener('dragend', clearDrag, true))
onBeforeUnmount(() => document.removeEventListener('dragend', clearDrag, true))
const destination = ref<Record<string, string>>({})
const dragType = 'application/x-studio-measure'

function startDrag(event: DragEvent, field: CampoScript) {
  if (!props.movable || props.disabled || field.id == null || !event.dataTransfer) { event.preventDefault(); return }
  dragging.value = String(field.id)
  event.dataTransfer.effectAllowed = 'move'
  event.dataTransfer.setData(dragType, JSON.stringify({ sourceId: dragging.value, context: props.dragContext }))
}
function allowDrop(event: DragEvent, sectionId?: string | null, beforeItemId: string | null = null) {
  if (props.movable && !props.disabled && event.dataTransfer?.types.includes(dragType)) {
    event.preventDefault()
    event.dataTransfer.dropEffect = 'move'
    dropTarget.value = sectionId ? sectionId + ':' + (beforeItemId ?? 'end') : null
  }
}
function move(sourceId: string, sectionId: string | null, beforeItemId: string | null) {
  if (props.disabled || !props.movable || !sectionId || sourceId === beforeItemId) return
  emit('move', { sourceId, sectionId, beforeItemId })
}
function drop(event: DragEvent, sectionId: string | null, beforeItemId: string | null = null) {
  if (!props.movable || props.disabled || !event.dataTransfer) return
  try {
    const payload = JSON.parse(event.dataTransfer.getData(dragType))
    if (payload.context === props.dragContext && typeof payload.sourceId === 'string') move(payload.sourceId, sectionId, beforeItemId)
  } catch { /* Other drag sources do not modify the model. */ }
  clearDrag()
}

const grouped = computed(() => {
  const sections: Array<{ id: string | null; title: string; fields: CampoScript[] }> = []
  const byToken = new Map<string, typeof sections[number]>()
  for (const campo of props.campos) if (campo.tipo === 'etiqueta') {
    const title = campo.descricao ?? campo.nome ?? 'Seção'
    const section = { id: campo.id == null ? null : String(campo.id), title: props.friendly ? title : `${title} · coluna ${campo.coluna ?? 1}`, fields: [] as CampoScript[] }
    sections.push(section); if (campo.nome) byToken.set(campo.nome, section)
  }
  let current: typeof sections[number] | null = null

  for (const campo of props.campos) {
    if (campo.tipo === 'etiqueta') {
      current = campo.nome ? byToken.get(campo.nome) ?? null : null
      continue
    }
    if (campo.tipo !== 'numero' || campo.coluna === -1) continue
    const parent = campo.etiquetaPai ? byToken.get(campo.etiquetaPai) : current
    if (parent) { parent.fields.push(campo); continue }
    if (!current) {
      current = { id: null, title: 'Campos', fields: [] }
      sections.push(current)
    }
    current.fields.push(campo)
  }

  return sections
    .map(section => ({ ...section, fields: [...section.fields].sort((a, b) => Number(a.ordem ?? 0) - Number(b.ordem ?? 0)) }))
})
</script>

<template>
  <StudioDsCard :title="title||(friendly?'Seu modelo':'Modelo (camposScript)')">
    <div v-if="!campos.length" class="text-sm text-ds-muted">
      Modelo vazio. Use o microfone para adicionar campos.
    </div>
    <div v-else class="space-y-4">
      <div v-for="(section, si) in grouped" :key="si" class="rounded-ds-btn border border-ds-divider p-3">
        <p class="font-semibold text-ds-text">{{ section.title }}</p>
        <ul class="mt-2 space-y-1">
          <li
            v-for="(field, fi) in section.fields"
            :key="`${field.nome}-${fi}`"
            :data-source-id="field.id" tabindex="-1"
            :draggable="movable && !disabled && field.id != null"
            @dragstart="startDrag($event, field)" @dragend="dragging = null"
            @dragover="allowDrop($event, section.id, String(field.id))" @dragleave.self="dropTarget = null" @drop.stop.prevent="drop($event, section.id, String(field.id))"
            :class="{ 'rounded ring-1 ring-ds-primary-accent': highlighted?.includes(String(field.id)), 'opacity-50': dragging === String(field.id), 'studio-drop-before': dropTarget === section.id + ':' + field.id }"
            class="flex flex-wrap items-center gap-2 text-sm text-ds-text-secondary"
          >
            <span class="flex-1 text-ds-text">{{ field.descricao }}</span>
            <i v-if="movable" class="bi bi-grip-vertical" aria-hidden="true" title="Arraste para mover" />
            <span v-if="field.nome&&!friendly" class="rounded bg-ds-surface-elevated px-1.5 py-0.5 text-xs font-mono">{{ field.nome }}</span>
            <span v-if="field.tipo&&!friendly" class="text-xs text-ds-muted">({{ field.tipo }})</span>
            <span v-if="field.medida" class="text-xs">{{ field.medida }}</span>
            <span
              v-for="(ref, ri) in field.referenciaNormalidade ?? []"
              :key="ri"
              class="text-xs text-ds-primary-accent"
            >
              {{ ref.sexo }}: {{ ref.valorMin }}–{{ ref.valorMax }}
            </span>
            <StudioActionMenu v-if="removable || movable" :label="`Ações de ${field.descricao || field.nome || 'medida'}`" :disabled="disabled">
              <details><summary>Detalhes da medida</summary><pre class="max-h-48 overflow-auto whitespace-pre-wrap text-xs">{{ JSON.stringify(field, null, 2) }}</pre></details>
            <button v-if="removable&&field.id!=null" type="button" :disabled="disabled" :aria-label="`Remover ${field.descricao || field.nome || 'medida'}`" class="ml-auto rounded border border-ds-divider px-2 py-1 text-xs disabled:opacity-50" @click="emit('remove',String(field.id))">Remover</button>
            <details v-if="movable && field.id != null" class="w-full text-xs" @dragstart.stop.prevent>
              <summary>Mover {{ field.descricao || 'medida' }}</summary>
              <div class="my-2 flex flex-wrap gap-2">
                <button type="button" :disabled="disabled || fi === 0 || !section.id" class="rounded border border-ds-divider p-2"
                  @click="move(String(field.id), section.id, String(section.fields[fi - 1]?.id))">Subir</button>
                <button type="button" :disabled="disabled || fi === section.fields.length - 1 || !section.id" class="rounded border border-ds-divider p-2"
                  @click="move(String(field.id), section.id, section.fields[fi + 2]?.id == null ? null : String(section.fields[fi + 2]?.id))">Descer</button>
                <select v-model="destination[String(field.id)]" :disabled="disabled" :aria-label="`Destino de ${field.descricao || 'medida'}`" class="rounded border border-ds-divider bg-ds-surface p-2">
                  <option value="" disabled>Escolha a seção</option><option v-for="target in destinations" :key="target.id" :value="target.id">{{ target.title }}</option>
                </select>
                <button type="button" :disabled="disabled || !destination[String(field.id)]" class="rounded border border-ds-divider p-2"
                  @click="move(String(field.id), destination[String(field.id)] || null, null)">Mover para o fim</button>
              </div>
            </details>
            </StudioActionMenu>
          </li>
        </ul>
        <div v-if="movable && section.id" class="mt-2 rounded border border-dashed border-ds-divider p-3 text-sm"
          :class="{ 'studio-drop-end': dropTarget === section.id + ':end' }" @dragover="allowDrop($event, section.id)" @dragleave.self="dropTarget = null" @drop.stop.prevent="drop($event, section.id)">Solte aqui para mover ao fim de {{ section.title }}</div>
      </div>
    </div>

    <details v-if="!friendly" class="mt-4">
      <summary class="cursor-pointer text-xs text-ds-muted">Componentes preservados (somente leitura)</summary>
      <pre class="mt-2 max-h-48 overflow-auto whitespace-pre-wrap text-xs">{{ JSON.stringify(campos.filter(c => c.tipo !== 'numero' && c.tipo !== 'etiqueta' || c.coluna === -1), null, 2) }}</pre>
    </details>
    <details v-if="!friendly" class="mt-4">
      <summary class="cursor-pointer text-xs text-ds-muted">JSON bruto</summary>
      <pre class="mt-2 max-h-48 overflow-auto rounded-ds-btn bg-ds-surface p-2 text-xs text-ds-text-secondary">{{ JSON.stringify({ camposScript: campos }, null, 2) }}</pre>
    </details>
  </StudioDsCard>
</template>
