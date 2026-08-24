<template>
  <Teleport to="body">
    <Transition name="ds-modal">
      <div
        v-if="modelValue"
        class="fixed inset-0 z-50 flex items-center justify-center p-4"
        role="dialog"
        aria-modal="true"
        :aria-labelledby="titleId"
      >
        <div class="absolute inset-0 bg-black/40 backdrop-blur-sm" @click="onBackdrop" />
        <div
          class="relative w-full bg-white rounded-3xl shadow-ds border border-gray-100 max-h-[90vh] overflow-hidden flex flex-col animate-slide-up"
          :class="sizeClass"
        >
          <div v-if="title || $slots.header" class="flex items-center justify-between px-6 py-4 border-b border-gray-100">
            <h3 :id="titleId" class="text-lg font-semibold font-manrope text-ds-text">
              <slot name="header">{{ title }}</slot>
            </h3>
            <button
              type="button"
              class="p-2 rounded-full border-0 bg-transparent shadow-none hover:bg-gray-100 text-gray-500 transition-colors outline-none focus-visible:ring-2 focus-visible:ring-gray-300"
              aria-label="Fechar"
              @click="close"
            >
              <i class="bi bi-x-lg" />
            </button>
          </div>
          <div class="px-6 py-4 overflow-y-auto flex-1">
            <slot />
          </div>
          <div v-if="$slots.footer" class="px-6 py-4 border-t border-gray-100 flex justify-end gap-2">
            <slot name="footer" />
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    modelValue: boolean
    title?: string
    size?: 'sm' | 'md' | 'lg' | 'xl' | '2xl'
    closeOnBackdrop?: boolean
  }>(),
  { size: 'md', closeOnBackdrop: true }
)

const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

const titleId = `ds-modal-title-${Math.random().toString(36).slice(2, 9)}`

const sizeClass = computed(() => {
  const map = { sm: 'max-w-md', md: 'max-w-lg', lg: 'max-w-2xl', xl: 'max-w-4xl', '2xl': 'max-w-6xl' }
  return map[props.size]
})

function close() {
  emit('update:modelValue', false)
}

function onBackdrop() {
  if (props.closeOnBackdrop) close()
}

watch(
  () => props.modelValue,
  (open) => {
    document.body.style.overflow = open ? 'hidden' : ''
  }
)

onUnmounted(() => {
  document.body.style.overflow = ''
})
</script>

<style scoped>
.ds-modal-enter-active,
.ds-modal-leave-active {
  transition: opacity 0.2s ease;
}
.ds-modal-enter-from,
.ds-modal-leave-to {
  opacity: 0;
}
</style>
