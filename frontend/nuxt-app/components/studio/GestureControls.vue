<script setup lang="ts">
import { createPinch, pinchRatio, resolveMeasureMove, trackingGapMs, type GestureHand, type GesturePoint, type MeasureMove } from '~/utils/studioGesture'

const props = defineProps<{
  model: HTMLElement | null
  revision: string | number | null
  suspended?: string
}>()
const emit = defineEmits<{ move: [move: MeasureMove] }>()
const base = useRuntimeConfig().app.baseURL
const video = ref<HTMLVideoElement | null>(null)
const state = ref<'off' | 'starting' | 'ready'>('off')
const error = ref('')
const desktop = ref(false)
const cursor = ref<GesturePoint | null>(null)
const outline = ref<DOMRect | null>(null)
const line = ref<{ x: number; y: number; width: number } | null>(null)
const grabbed = ref<string | null>(null)
const pinch = createPinch()
const zone = '(min-width: 1024px)'

let screen: MediaQueryList
let worker: Worker | null = null
let stream: MediaStream | null = null
let animation = 0
let startTimeout: ReturnType<typeof setTimeout> | undefined
let generation = 0
let framePending = false
let lastVideoTime = -1
let lastResult = 0
let dragRevision: typeof props.revision
let dragSections: { id: string; items: string[] }[] = []
let smooth: GesturePoint | null = null

function reset() {
  pinch.reset()
  cursor.value = null
  smooth = null
  outline.value = null
  line.value = null
  grabbed.value = null
  dragSections = []
}

function stop() {
  generation++
  clearTimeout(startTimeout)
  cancelAnimationFrame(animation)
  worker?.terminate()
  worker = null
  stream?.getTracks().forEach(track => track.stop())
  stream = null
  if (video.value) video.value.srcObject = null
  framePending = false
  lastVideoTime = -1
  lastResult = 0
  state.value = 'off'
  reset()
}

function fail(message: string) {
  stop()
  error.value = message
}

function blocked() {
  return !props.model || !!props.suspended || Array.from(
    document.querySelectorAll<HTMLElement>('dialog[open], [aria-modal="true"], .swal2-popup')
  ).some(element => element.getClientRects().length && getComputedStyle(element).visibility === 'visible')
}

function measureOrder() {
  const sections = new Map<string, string[]>()
  for (const node of props.model?.querySelectorAll<HTMLElement>('[data-gesture-section]') ?? []) {
    const { gestureSection: sectionId, gestureItem: itemId } = node.dataset
    if (!sectionId) continue
    if (!sections.has(sectionId)) sections.set(sectionId, [])
    if (itemId) sections.get(sectionId)!.push(itemId)
  }
  return Array.from(sections, ([id, items]) => ({ id, items }))
}

function targetAt(point: GesturePoint) {
  const target = document.elementFromPoint(point.x, point.y)?.closest<HTMLElement>('[data-gesture-section]')
  if (!target || !props.model?.contains(target)) return null
  const { gestureSection: sectionId, gestureItem: itemId, gestureNext } = target.dataset
  if (!sectionId) return null
  const rect = target.getBoundingClientRect()
  const after = !itemId || point.y >= rect.top + rect.height / 2
  return { sectionId, itemId, beforeItemId: after ? gestureNext || null : itemId ?? null, rect, after }
}

function updateHand(hand: GestureHand | null, timestamp: number, aspect: number) {
  const index = hand?.[8]
  const ratio = hand && pinchRatio(hand, aspect)
  const action = pinch.sample(ratio, timestamp)
  if (!index || action === 'lost') {
    reset()
    return
  }

  // A smaller active area in the image lets the cursor reach the viewport edges comfortably.
  const map = (value: number, size: number) => Math.max(0, Math.min(size - 1, (value - 0.1) / 0.8 * size))
  const x = map(1 - index.x, window.innerWidth)
  const y = map(index.y, window.innerHeight)
  smooth = smooth ? { x: smooth.x + (x - smooth.x) * 0.42, y: smooth.y + (y - smooth.y) * 0.42 } : { x, y }
  cursor.value = smooth

  const target = targetAt(smooth)
  if (action === 'grab' && target?.itemId) {
    grabbed.value = target.itemId
    dragRevision = props.revision
    dragSections = measureOrder()
  }

  const sourceId = grabbed.value
  outline.value = !sourceId && target?.itemId ? target.rect : null
  const move = sourceId && target ? { sourceId, sectionId: target.sectionId, beforeItemId: target.beforeItemId } : null
  const valid = move && resolveMeasureMove(dragSections, move) !== null
  line.value = valid && target
    ? { x: target.rect.left, y: target.after ? target.rect.bottom : target.rect.top, width: target.rect.width }
    : null
  if (action === 'release') {
    grabbed.value = null
    dragSections = []
    line.value = null
    if (move && valid && dragRevision === props.revision && !blocked()) emit('move', move)
  }
}

function render() {
  animation = requestAnimationFrame(render)
  const now = performance.now()
  if (blocked()) {
    reset()
    return
  }
  if (lastResult && now - lastResult > trackingGapMs) reset()
  if (grabbed.value && cursor.value) {
    const y = cursor.value.y
    const edge = 64
    if (y < edge) window.scrollBy(0, -Math.ceil((edge - y) / 6))
    else if (y > window.innerHeight - edge) window.scrollBy(0, Math.ceil((y - window.innerHeight + edge) / 6))
  }
  const currentVideo = video.value
  const currentWorker = worker
  if (!currentWorker || !currentVideo || currentVideo.readyState < 2 || framePending || currentVideo.currentTime === lastVideoTime) return

  lastVideoTime = currentVideo.currentTime
  framePending = true
  const currentGeneration = generation
  void createImageBitmap(currentVideo).then(frame => {
    if (generation !== currentGeneration || blocked()) {
      frame.close()
      if (generation === currentGeneration) framePending = false
      return
    }
    try {
      currentWorker.postMessage({ type: 'frame', timestamp: now, frame }, [frame])
    } catch {
      frame.close()
      fail('Não foi possível analisar a imagem da câmera.')
    }
  }).catch(() => {
    if (generation === currentGeneration) fail('Não foi possível capturar a imagem da câmera.')
  })
}

type WorkerReply = { type: 'ready' | 'error' } | { type: 'frame'; hand: GestureHand | null; timestamp: number; aspect: number }

async function start() {
  if (state.value !== 'off' || blocked()) return
  error.value = ''
  if (!navigator.mediaDevices?.getUserMedia) {
    error.value = 'A câmera precisa de uma página HTTPS ou de localhost.'
    return
  }
  if (!window.Worker || !window.OffscreenCanvas || !window.createImageBitmap) {
    error.value = 'Este navegador não oferece o processamento necessário para gestos.'
    return
  }

  const currentGeneration = ++generation
  state.value = 'starting'
  try {
    const camera = await navigator.mediaDevices.getUserMedia({
      video: { facingMode: 'user', width: { ideal: 640 }, height: { ideal: 480 }, frameRate: { ideal: 24 } },
      audio: false
    })
    if (currentGeneration !== generation) {
      camera.getTracks().forEach(track => track.stop())
      return
    }
    stream = camera
    const preview = video.value!
    camera.getVideoTracks().forEach(track => track.addEventListener('ended', () => {
      if (generation === currentGeneration) fail('A câmera foi desconectada.')
    }, { once: true }))
    startTimeout = setTimeout(() => fail('O reconhecimento de mãos demorou para iniciar. Tente novamente.'), 20000)
    preview.srcObject = camera
    await preview.play()
    if (currentGeneration !== generation) return

    const asset = (path: string) => new URL(base + path, location.origin).href
    worker = new Worker(asset('studio-gestures/worker.js'))
    worker.onerror = () => fail('Não foi possível carregar o reconhecimento de mãos.')
    worker.onmessage = ({ data }: MessageEvent<WorkerReply>) => {
      if (generation !== currentGeneration) return
      if (data.type === 'ready') {
        clearTimeout(startTimeout)
        state.value = 'ready'
        render()
      } else if (data.type === 'frame') {
        framePending = false
        lastResult = performance.now()
        if (lastResult - data.timestamp <= trackingGapMs && !blocked()) updateHand(data.hand, data.timestamp, data.aspect)
        else reset()
      } else {
        fail('Não foi possível iniciar ou continuar o reconhecimento de mãos.')
      }
    }
    worker.postMessage({ type: 'start', runtime: asset('studio-gesture-runtime/0.10.32'), model: asset('studio-gestures/hand_landmarker.task') })
  } catch (cause) {
    if (currentGeneration !== generation) return
    const name = cause instanceof Error ? cause.name : ''
    if (name === 'NotAllowedError') fail('A câmera está bloqueada. Confira a permissão do site no navegador.')
    else if (name === 'NotFoundError') fail('Nenhuma câmera foi encontrada.')
    else if (name === 'NotReadableError') fail('Não foi possível acessar a câmera. Confira se ela está em uso.')
    else fail('Não foi possível iniciar a câmera ou o reconhecimento de mãos.')
  }
}

function resize() {
  desktop.value = screen.matches
  if (!desktop.value) stop()
}
function visibility() {
  if (document.hidden) stop()
}
function escape(event: KeyboardEvent) {
  if (event.key === 'Escape') reset()
}

watch(() => [props.revision, props.suspended, props.model], reset, { flush: 'sync' })
onMounted(() => {
  screen = matchMedia(zone)
  resize()
  screen.addEventListener('change', resize)
  document.addEventListener('visibilitychange', visibility)
  document.addEventListener('keydown', escape, true)
  document.addEventListener('pointerdown', reset, true)
  document.addEventListener('dragstart', reset, true)
})
onBeforeUnmount(() => {
  stop()
  screen?.removeEventListener('change', resize)
  document.removeEventListener('visibilitychange', visibility)
  document.removeEventListener('keydown', escape, true)
  document.removeEventListener('pointerdown', reset, true)
  document.removeEventListener('dragstart', reset, true)
})
</script>

<template>
  <section v-if="desktop" class="space-y-2 rounded-xl border border-ds-divider p-4 text-sm" aria-label="Editar com gestos">
    <h2 class="font-semibold">Editar com gestos</h2>
    <button type="button" class="rounded border border-ds-divider px-3 py-2 disabled:opacity-50"
      :disabled="state === 'off' && !!suspended" @click="state === 'off' ? start() : stop()">
      {{ state === 'off' ? 'Ativar câmera' : 'Desativar câmera' }}
    </button>
    <video v-show="state === 'ready'" ref="video" class="aspect-[4/3] w-32 -scale-x-100 rounded bg-black object-contain" muted playsinline aria-hidden="true" />
    <p v-if="suspended" role="status">{{ suspended }}</p>
    <p v-else-if="state === 'starting'" role="status">Aguardando câmera e reconhecimento de mãos…</p>
    <p v-else-if="state === 'ready'" role="status">Câmera ativa. Aponte, faça a pinça para pegar e abra os dedos para soltar.</p>
    <p v-if="error" role="alert" class="text-ds-danger">{{ error }}</p>
  </section>
  <Teleport to="body">
    <div v-if="cursor" aria-hidden="true" class="pointer-events-none fixed inset-0 z-40 overflow-hidden text-ds-primary-accent">
      <div v-if="outline" class="absolute rounded outline-2 outline-current"
        :style="{ left: outline.left + 'px', top: outline.top + 'px', width: outline.width + 'px', height: outline.height + 'px' }" />
      <div v-if="line" class="absolute h-1 -translate-y-1/2 rounded bg-current shadow"
        :style="{ left: line.x + 'px', top: line.y + 'px', width: line.width + 'px' }" />
      <div class="absolute size-4 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-current shadow"
        :class="{ 'bg-current': grabbed }" :style="{ left: cursor.x + 'px', top: cursor.y + 'px' }" />
    </div>
  </Teleport>
</template>
