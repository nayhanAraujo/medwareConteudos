<template>
  <div v-if="slides.length" class="rounded-2xl border border-gray-200 bg-ds-surface overflow-hidden">
    <div class="relative min-h-[280px] flex items-center justify-center p-4 bg-white/60">
      <img
        :key="current"
        :src="slides[current].src"
        :alt="slides[current].alt"
        class="max-h-[420px] w-full object-contain"
      />
      <button
        v-if="slides.length > 1"
        type="button"
        class="absolute left-2 top-1/2 -translate-y-1/2 w-9 h-9 rounded-full bg-white/90 border border-gray-200 shadow-sm flex items-center justify-center text-gray-700 hover:bg-white"
        aria-label="Imagem anterior"
        @click="prev"
      >
        <i class="bi bi-chevron-left" />
      </button>
      <button
        v-if="slides.length > 1"
        type="button"
        class="absolute right-2 top-1/2 -translate-y-1/2 w-9 h-9 rounded-full bg-white/90 border border-gray-200 shadow-sm flex items-center justify-center text-gray-700 hover:bg-white"
        aria-label="Próxima imagem"
        @click="next"
      >
        <i class="bi bi-chevron-right" />
      </button>
    </div>
    <p v-if="slides[current].caption" class="text-center text-xs text-gray-600 px-4 pt-2">
      {{ slides[current].caption }}
    </p>
    <div v-if="slides.length > 1" class="flex justify-center gap-2 py-3">
      <button
        v-for="(_, index) in slides"
        :key="index"
        type="button"
        class="w-2 h-2 rounded-full transition-colors"
        :class="index === current ? 'bg-gray-800' : 'bg-gray-300 hover:bg-gray-400'"
        :aria-label="`Ir para imagem ${index + 1}`"
        @click="current = index"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
export interface DsCarouselSlide {
  src: string
  alt?: string
  caption?: string
}

const props = defineProps<{ slides: DsCarouselSlide[] }>()

const current = ref(0)

watch(
  () => props.slides,
  () => {
    current.value = 0
  },
  { deep: true }
)

function prev() {
  if (props.slides.length <= 1) return
  current.value = (current.value - 1 + props.slides.length) % props.slides.length
}

function next() {
  if (props.slides.length <= 1) return
  current.value = (current.value + 1) % props.slides.length
}
</script>
