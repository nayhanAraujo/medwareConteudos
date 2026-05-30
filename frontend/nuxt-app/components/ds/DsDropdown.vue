<template>
  <div ref="root" class="relative inline-block">
    <button
      type="button"
      class="ds-dropdown-trigger inline-flex items-center gap-2 border-0 bg-transparent p-0 shadow-none focus:outline-none focus-visible:ring-2 focus-visible:ring-black/10"
      :class="triggerClass"
      @click.stop="toggle"
    >
      <slot name="trigger" />
    </button>
    <Teleport to="body">
      <Transition name="ds-dropdown">
        <div
          v-if="open"
          ref="menuRef"
          class="ds-dropdown-menu fixed z-[200] min-w-[13rem] max-w-[18rem] bg-white rounded-2xl border border-gray-200 shadow-ds-card py-1.5 px-1 overflow-hidden"
          :style="menuStyle"
        >
          <slot :close="close" />
        </div>
      </Transition>
    </Teleport>
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
const menuRef = ref<HTMLElement | null>(null)
const menuStyle = ref<Record<string, string>>({ top: '0px', left: '0px' })

async function updatePosition() {
  await nextTick()
  if (!root.value) return
  const rect = root.value.getBoundingClientRect()
  const gap = 8
  const menuHeight = menuRef.value?.offsetHeight ?? 280
  const menuWidth = menuRef.value?.offsetWidth ?? 208
  const spaceBelow = window.innerHeight - rect.bottom
  const openUp = spaceBelow < menuHeight + gap && rect.top > menuHeight + gap

  let top = openUp ? rect.top - gap : rect.bottom + gap
  let transform = openUp ? 'translateY(-100%)' : ''

  let left = props.align === 'right' ? rect.right : rect.left
  if (props.align === 'right') {
    transform = `${transform} translateX(-100%)`.trim()
  }

  if (left - (props.align === 'right' ? menuWidth : 0) < 8) {
    left = props.align === 'right' ? menuWidth + 8 : 8
    transform = openUp ? 'translateY(-100%)' : ''
  } else if (props.align !== 'right' && left + menuWidth > window.innerWidth - 8) {
    left = window.innerWidth - menuWidth - 8
  }

  menuStyle.value = {
    top: `${top}px`,
    left: `${left}px`,
    transform: transform || 'none'
  }
}

async function toggle() {
  if (!open.value) {
    open.value = true
    await updatePosition()
    await updatePosition()
    return
  }
  open.value = false
}

function close() {
  open.value = false
}

function onClickOutside(e: MouseEvent) {
  const target = e.target as Node
  if (root.value?.contains(target) || menuRef.value?.contains(target)) return
  close()
}

function onScrollOrResize() {
  if (open.value) updatePosition()
}

onMounted(() => {
  document.addEventListener('click', onClickOutside)
  window.addEventListener('scroll', onScrollOrResize, true)
  window.addEventListener('resize', onScrollOrResize)
})

onUnmounted(() => {
  document.removeEventListener('click', onClickOutside)
  window.removeEventListener('scroll', onScrollOrResize, true)
  window.removeEventListener('resize', onScrollOrResize)
})
</script>

<style>
.ds-dropdown-menu .ds-dropdown-item {
  border: none;
  background: transparent;
  box-shadow: none;
  outline: none;
  text-decoration: none;
  color: inherit;
  cursor: pointer;
  border-radius: 0.5rem;
  transition: background-color 0.15s ease, color 0.15s ease;
}

.ds-dropdown-menu .ds-dropdown-item:hover:not(:disabled):not(.ds-dropdown-item--disabled) {
  background-color: rgb(243 244 246);
}

.ds-dropdown-menu .ds-dropdown-item.ds-dropdown-item--danger:hover:not(:disabled):not(.ds-dropdown-item--disabled) {
  background-color: rgb(255 241 242);
  color: rgb(225 29 72);
}

.ds-dropdown-menu .ds-dropdown-item:focus,
.ds-dropdown-menu .ds-dropdown-item:focus-visible {
  outline: none;
  box-shadow: none;
}

.ds-dropdown-menu .ds-dropdown-item:focus-visible:not(:disabled):not(.ds-dropdown-item--disabled) {
  background-color: rgb(243 244 246);
}

.ds-dropdown-menu .ds-dropdown-item.ds-dropdown-item--danger:focus-visible:not(:disabled):not(.ds-dropdown-item--disabled) {
  background-color: rgb(255 241 242);
}

.ds-dropdown-menu .ds-dropdown-item:disabled,
.ds-dropdown-menu .ds-dropdown-item.ds-dropdown-item--disabled {
  cursor: not-allowed;
  opacity: 0.55;
}
</style>

<style scoped>
.ds-dropdown-enter-active,
.ds-dropdown-leave-active {
  transition: opacity 0.15s ease;
}

.ds-dropdown-enter-from,
.ds-dropdown-leave-to {
  opacity: 0;
}
</style>
