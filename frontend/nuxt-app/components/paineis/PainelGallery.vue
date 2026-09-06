<template>
  <DsModal :model-value="!!painel" size="2xl" :title="title" @update:model-value="emit('close')">
    <div ref="galleryBody" tabindex="0" aria-label="Imagens do painel" @keydown.left.prevent="move(-1)" @keydown.right.prevent="move(1)" @keydown.esc="emit('close')">
      <p class="mb-3 text-sm text-gray-500">Use as setas para navegar pelas imagens da última versão.</p>
      <p v-if="loading" class="py-16 text-center" role="status">Carregando imagens...</p>
      <DsAlert v-else-if="error" variant="error">{{ error }}</DsAlert>
      <DsAlert v-else-if="!images.length" variant="info">Esta versão ainda não possui imagens anexadas.</DsAlert>
      <template v-else>
        <div class="flex h-[40vh] min-h-32 items-center justify-center rounded-xl bg-slate-100">
          <img v-if="current?.url" :src="current.url" :alt="current.name" class="h-full w-full object-contain" />
          <p v-else class="p-6 text-center" role="status">{{ current?.failed ? 'Não foi possível carregar esta imagem.' : 'Carregando imagem...' }}</p>
        </div>
        <div class="my-4 grid grid-cols-2 items-center gap-3 sm:grid-cols-[auto_minmax(0,1fr)_auto]">
          <DsButton variant="secondary" icon="chevron-left" :disabled="images.length < 2" @click="move(-1)">Anterior</DsButton>
          <p class="order-first col-span-2 min-w-0 text-center text-sm sm:order-none sm:col-span-1" aria-live="polite">{{ index + 1 }} / {{ images.length }}<span class="block truncate text-gray-500">{{ current?.name }}</span></p>
          <DsButton variant="secondary" icon="chevron-right" :disabled="images.length < 2" @click="move(1)">Próxima</DsButton>
        </div>
        <div class="flex flex-wrap justify-center gap-2">
          <button v-for="(img, i) in images" :key="img.id" type="button" class="rounded-lg border px-3 py-2 text-sm" :class="i === index ? 'bg-slate-800 text-white' : 'bg-white'" :aria-label="`Ver imagem ${i + 1}`" :aria-current="i === index ? 'true' : undefined" @click="index = i">{{ i + 1 }}</button>
        </div>
      </template>
    </div>
    <template #footer><DsButton variant="secondary" @click="emit('close')">Fechar</DsButton></template>
  </DsModal>
</template>

<script setup lang="ts">
import type { PainelItem } from '~/composables/usePaineisApi'
const props = defineProps<{ painel: PainelItem | null }>()
const emit = defineEmits<{ close: [] }>()
const api = usePaineisVersoesApi()
const files = useApi()
const images = ref<{ id: number; name: string; url?: string; failed?: boolean }[]>([])
const index = ref(0)
const loading = ref(false)
const error = ref('')
const version = ref('')
const galleryBody = ref<HTMLElement | null>(null)
let opener: HTMLElement | null = null
let generation = 0
const current = computed(() => images.value[index.value])
const title = computed(() => `#${props.painel?.codpainel} · ${props.painel?.nome || ''}${version.value ? ` · Versão ${version.value}` : ''}`)
function clear() {
  generation++
  images.value.forEach(img => { if (img.url) URL.revokeObjectURL(img.url) })
  images.value = []
}
function move(delta: number) {
  if (images.value.length) index.value = (index.value + delta + images.value.length) % images.value.length
}
watch(() => props.painel, async painel => {
  clear()
  index.value = 0
  error.value = ''
  version.value = ''
  if (!painel) { opener?.focus(); return }
  opener = document.activeElement as HTMLElement | null
  const request = generation
  loading.value = true
  await nextTick()
  galleryBody.value?.focus()
  try {
    const list = await api.list(painel.codpainel)
    if (request !== generation) return
    const latest = list.data[0]
    if (!latest) { error.value = 'Este painel ainda não possui versões.'; return }
    version.value = latest.numeroVersao
    const detail = await api.get(painel.codpainel, latest.codVersaoPainel)
    if (request !== generation) return
    images.value = detail.data.imagens.map(img => ({ id: img.codImagemPainel, name: img.nomeArquivo || 'Imagem do painel' }))
    loading.value = false
    await Promise.all(images.value.map(async img => {
      try {
        const blob = await files.getBlob(`/api/web/paineis/${painel.codpainel}/versoes/${latest.codVersaoPainel}/download/IMAGEM?imagemId=${img.id}`)
        if (request === generation) img.url = URL.createObjectURL(blob)
      } catch { if (request === generation) img.failed = true }
    }))
  } catch (err) {
    if (request === generation) error.value = err instanceof Error ? err.message : 'Erro ao carregar imagens.'
  } finally { if (request === generation) loading.value = false }
})
onBeforeUnmount(clear)
</script>
