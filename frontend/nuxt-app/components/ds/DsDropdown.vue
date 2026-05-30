<template>
  <div ref="root" class="relative inline-block">
    <button
      type="button"
      class="inline-flex items-center gap-2"
      :class="triggerClass"
      @click.stop="toggle"
    >
      <slot name="trigger" />
    </button>
    <Transition name="ds-dropdown">
      <div
        v-if="open"
        class="absolute z-40 mt-2 min-w-[12rem] bg-white rounded-2xl border border-gray-200 shadow-ds-card py-1 overflow-hidden"
        :class="alignClass"
      >
        <slot :close="close" />
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    triggerClass?: string
    align?: 'left' | 'right'
  }>(),
  { align: 'right' }
)

const open = ref(false)
const root = ref<HTMLElement | null>(null)

const alignClass = computed(() => (props.align === 'right' ? 'right-0' : 'left-0'))

function toggle() {
  open.value = !open.value
}

function close() {
  open.value = false
}

function onClickOutside(e: MouseEvent) {
  if (root.value && !root.value.contains(e.target as Node)) close()
}

onMounted(() => document.addEventListener('click', onClickOutside))
onUnmounted(() => document.removeEventListener('click', onClickOutside))
</script>

<style scoped>
.ds-dropdown-enter-active,
.ds-dropdown-leave-active {
  transition: opacity 0.15s ease, transform 0.15s ease;
}
.ds-dropdown-enter-from,
.ds-dropdown-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}
</style>
