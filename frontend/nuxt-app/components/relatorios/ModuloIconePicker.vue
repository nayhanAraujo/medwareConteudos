<template>
  <fieldset class="space-y-3">
    <legend class="text-sm font-medium">Ícone do módulo</legend>
    <div class="flex items-center gap-3 flex-wrap">
      <span class="preview"><img v-if="modelValue" :src="modelValue" alt="Ícone selecionado" width="64" height="64"><i v-else class="bi bi-folder2-open" /></span>
      <DsButton variant="secondary" icon="upload" :disabled="busy" @click="input?.click()">Enviar imagem</DsButton>
      <DsButton v-if="modelValue" variant="secondary" icon="trash" :disabled="busy" @click="emit('update:modelValue', null)">Remover</DsButton>
      <input ref="input" type="file" class="hidden" accept="image/png,image/jpeg,image/webp" aria-label="Enviar ícone do módulo" @change="upload">
    </div>
    <div class="gallery">
      <button v-for="item in gallery" :key="item.icon" type="button" :title="item.label" :disabled="busy" @click="choose(item)">
        <i :class="'bi bi-' + item.icon" :style="{ color: item.color }" aria-hidden="true" /><span>{{ item.label }}</span>
      </button>
    </div>
    <DsAlert v-if="error" variant="error">{{ error }}</DsAlert>
  </fieldset>
</template>
<script setup lang="ts">
defineProps<{ modelValue: string | null }>()
const emit = defineEmits<{ 'update:modelValue': [value: string | null]; busy: [value: boolean] }>()
const input = ref<HTMLInputElement>(), busy = ref(false), error = ref('')
let active = true
onBeforeUnmount(() => { active = false })
const gallery = [
  { icon: 'calendar-check', label: 'Agenda', color: '#2563eb' },
  { icon: 'wallet2', label: 'Caixa', color: '#16936c' },
  { icon: 'journal-bookmark', label: 'Biblioteca', color: '#8b5bc7' },
  { icon: 'receipt-cutoff', label: 'Faturamento', color: '#147d59' },
  { icon: 'person-vcard', label: 'Profissionais', color: '#d84672' },
  { icon: 'graph-up-arrow', label: 'Financeiro', color: '#16854d' },
  { icon: 'box-seam', label: 'Estoque', color: '#d58913' },
  { icon: 'file-earmark-ruled', label: 'Fiscal', color: '#337da1' },
  { icon: 'people', label: 'Pacientes', color: '#0c9caa' },
  { icon: 'file-earmark-medical', label: 'Laudos', color: '#cf4e64' },
  { icon: 'heart-pulse', label: 'Exames', color: '#e54975' },
  { icon: 'folder2-open', label: 'Geral', color: '#64748b' },
]
function setBusy(value: boolean) { if (active) { busy.value = value; emit('busy', value) } }
async function choose(item: typeof gallery[number]) {
  setBusy(true); error.value = ''
  const probe = document.createElement('i'); probe.className = 'bi bi-' + item.icon
  probe.style.position = 'absolute'; probe.style.visibility = 'hidden'; document.body.append(probe)
  try {
    const style = getComputedStyle(probe, '::before')
    const glyph = style.content.replace(/^["']|["']$/g, '')
    if (!glyph || glyph === 'none') throw new Error()
    await document.fonts.load('80px bootstrap-icons', glyph)
    if (!document.fonts.check('80px bootstrap-icons', glyph)) throw new Error()
    const canvas = document.createElement('canvas'); canvas.width = canvas.height = 128
    const ctx = canvas.getContext('2d')!
    ctx.font = '80px bootstrap-icons'; ctx.fillStyle = item.color; ctx.textAlign = 'center'; ctx.textBaseline = 'middle'
    ctx.fillText(glyph, 64, 64)
    if (active) emit('update:modelValue', canvas.toDataURL('image/png'))
  } catch { error.value = 'Não foi possível carregar o ícone. Tente novamente ou envie uma imagem.' }
  finally { probe.remove(); setBusy(false) }
}
async function upload(event: Event) {
  const el = event.target as HTMLInputElement, file = el.files?.[0]; el.value = ''
  if (!file) return
  error.value = ''
  if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type) || file.size > 1024 * 1024) {
    error.value = 'Selecione uma imagem PNG, JPEG ou WebP de até 1 MB.'; return
  }
  setBusy(true)
  const url = URL.createObjectURL(file)
  try {
    const img = new Image(); img.src = url; await img.decode()
    const canvas = document.createElement('canvas'); canvas.width = canvas.height = 128
    const scale = Math.min(128 / img.naturalWidth, 128 / img.naturalHeight)
    const w = img.naturalWidth * scale, h = img.naturalHeight * scale
    canvas.getContext('2d')!.drawImage(img, (128 - w) / 2, (128 - h) / 2, w, h)
    if (active) emit('update:modelValue', canvas.toDataURL('image/png'))
  } catch { error.value = 'Não foi possível ler a imagem.' }
  finally { URL.revokeObjectURL(url); setBusy(false) }
}
</script>
<style scoped>
.preview { width: 72px; height: 72px; display: inline-flex; align-items: center; justify-content: center; background: #f4f5f7; border-radius: 8px; font-size: 30px; }
.gallery { display: grid; grid-template-columns: repeat(auto-fit, minmax(88px, 1fr)); gap: 8px; }
.gallery button { min-height: 88px; border: 1px solid #d1d5db; border-radius: 8px; background: white; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 6px; font-size: 11px; }
.gallery i { font-size: 30px; }
.gallery button:focus-visible { outline: 2px solid #2563eb; outline-offset: 2px; }
</style>
