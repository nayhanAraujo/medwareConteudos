<template>
  <div>
    <label :id="labelId" class="mb-1.5 block text-sm font-medium text-gray-900">Conteúdo</label>
    <div class="rounded-lg border border-gray-300 bg-white text-gray-900">
      <div role="toolbar" aria-label="Formatação da frase" class="flex flex-wrap items-center gap-1 border-b border-gray-200 p-2">
        <button v-for="tool in tools" :key="tool.command" type="button" :title="tool.label" :aria-label="tool.label" :disabled="!ready" class="h-8 w-8 rounded border-0 bg-transparent hover:bg-gray-100" @mousedown.prevent @click="command(tool.command)"><i :class="`bi bi-${tool.icon}`" /></button>
        <select aria-label="Fonte" :disabled="!ready" class="h-8 max-w-40 border border-gray-200 text-sm" @change="command('fontName', ($event.target as HTMLSelectElement).value)">
          <option v-for="font in ['Arial', 'Calibri', 'Tahoma', 'Times New Roman', 'Trebuchet MS', 'Verdana']" :key="font">{{ font }}</option>
        </select>
        <input type="color" title="Cor do texto" aria-label="Cor do texto" :disabled="!ready" class="h-8 w-8" @input="command('foreColor', ($event.target as HTMLInputElement).value)">
      </div>
      <div ref="editor" role="textbox" aria-multiline="true" :aria-labelledby="labelId" :contenteditable="ready" class="phrase-rtf min-h-52 max-h-96 overflow-auto p-4 outline-none" @input="update" @paste="paste" />
    </div>
    <DsAlert v-if="error" variant="error" class="mt-2">{{ error }}</DsAlert>
    <p v-else-if="!ready" class="mt-2 text-sm text-gray-500">Carregando conteúdo...</p>
  </div>
</template>

<script setup lang="ts">
import DOMPurify from 'dompurify'
import { phraseHtmlToRtf } from '~/utils/phraseRtf'
const props = defineProps<{ modelValue: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: string]; ready: [value: boolean] }>()
const editor = ref<HTMLElement>()
const labelId = useId()
const ready = ref(false)
const error = ref('')
let emitted = ''
let generation = 0
const tools = [
  { command: 'bold', icon: 'type-bold', label: 'Negrito' },
  { command: 'italic', icon: 'type-italic', label: 'Itálico' },
  { command: 'underline', icon: 'type-underline', label: 'Sublinhado' },
  { command: 'justifyLeft', icon: 'text-left', label: 'Alinhar à esquerda' },
  { command: 'justifyCenter', icon: 'text-center', label: 'Centralizar' },
  { command: 'justifyRight', icon: 'text-right', label: 'Alinhar à direita' },
  { command: 'undo', icon: 'arrow-counterclockwise', label: 'Desfazer' },
  { command: 'redo', icon: 'arrow-clockwise', label: 'Refazer' }
]
function setReady(value: boolean) { ready.value = value; emit('ready', value) }
async function load() {
  const current = ++generation
  setReady(false); error.value = ''
  try {
    const value = props.modelValue
    if (!editor.value) return
    if (/^\s*\{\\rtf/i.test(value)) {
      const { RTFJS } = await import('rtf.js')
      RTFJS.loggingEnabled(false)
      // Firebird ISO8859_1 exposes the original ANSI bytes as code points.
      const ansi = value.replace(/[^\x00-\xff]/g, ch => `\\u${ch.charCodeAt(0) > 32767 ? ch.charCodeAt(0) - 65536 : ch.charCodeAt(0)}?`)
      const bytes = Uint8Array.from(ansi, ch => ch.charCodeAt(0))
      const doc = new RTFJS.Document(bytes.buffer)
      const elements = await doc.render()
      if (current !== generation || !editor.value) return
      const container = document.createElement('div')
      container.append(...elements)
      editor.value.innerHTML = DOMPurify.sanitize(container.innerHTML)
    } else {
      editor.value.textContent = value
    }
    if (editor.value.querySelector('img,table,svg,object') || /\\(?:pict|object|trowd)\b/.test(value)) throw new Error('Este RTF contém imagens ou tabelas e não pode ser editado neste editor.')
    setReady(true)
  } catch (reason) {
    if (current === generation) error.value = reason instanceof Error ? reason.message : 'Não foi possível abrir o RTF.'
  }
}
function update() {
  if (!editor.value || !ready.value) return
  try { emitted = phraseHtmlToRtf(editor.value); emit('update:modelValue', emitted) }
  catch (reason) { error.value = String(reason); setReady(false) }
}
function command(name: string, value?: string) {
  editor.value?.focus()
  document.execCommand('styleWithCSS', false, 'true')
  document.execCommand(name, false, value)
  update()
}
function paste(event: ClipboardEvent) {
  event.preventDefault()
  const html = event.clipboardData?.getData('text/html')
  if (html) document.execCommand('insertHTML', false, DOMPurify.sanitize(html, { FORBID_TAGS: ['img', 'table', 'svg', 'object'] }))
  else document.execCommand('insertText', false, event.clipboardData?.getData('text/plain') || '')
  update()
}
onMounted(load)
watch(() => props.modelValue, value => { if (value !== emitted) load() })
onBeforeUnmount(() => { generation++; emit('ready', false) })
</script>

<style scoped>
.phrase-rtf { white-space: pre-wrap; overflow-wrap: anywhere; font-family: Arial, sans-serif; font-size: 14px; }
.phrase-rtf :deep(p) { margin: 0; }
</style>
