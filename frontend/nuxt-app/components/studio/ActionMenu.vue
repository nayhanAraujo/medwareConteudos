<script setup lang="ts">
const props = defineProps<{ label: string; disabled?: boolean }>()
const menu = ref<HTMLDetailsElement | null>(null)
function close() { if (menu.value) menu.value.open = false }
watch(() => props.disabled, disabled => { if (disabled) close() })
function outside(event: Event) { if (!menu.value?.contains(event.target as Node)) close() }
function escape(event: KeyboardEvent) {
  if (event.key === 'Escape' && menu.value?.open) {
    event.stopPropagation()
    close()
    menu.value.querySelector('summary')?.focus()
  }
}
function action(event: Event) {
  if ((event.target as HTMLElement).closest('button')) queueMicrotask(close)
}
onMounted(() => document.addEventListener('pointerdown', outside))
onBeforeUnmount(() => document.removeEventListener('pointerdown', outside))
</script>

<template>
  <details ref="menu" class="studio-action-menu" @keydown="escape" @click.stop @click.capture="action" @dragstart.stop.prevent>
    <summary :aria-label="label" :aria-disabled="disabled" @click="disabled && $event.preventDefault()">⋯</summary>
    <div class="studio-action-options"><slot /></div>
  </details>
</template>

<style>
.studio-action-menu { position: relative; flex: 0 0 auto; }
.studio-action-menu > summary { list-style: none; cursor: pointer; font-size: 1.4rem; line-height: 1; padding: .45rem .65rem; border-radius: .4rem; }
.studio-action-menu > summary::-webkit-details-marker { display: none; }
.studio-action-menu > summary[aria-disabled=true] { opacity: .4; cursor: default; }
.studio-action-options { position: absolute; right: 0; top: 100%; z-index: 30; width: min(21rem, 75vw); padding: .6rem; border: 1px solid var(--color-ds-divider); border-radius: .65rem; background: var(--color-ds-surface-elevated); box-shadow: 0 8px 24px #0003; }
.studio-action-options > button { display: block; width: 100%; text-align: left; margin: 0; padding: .6rem; border: 0; background: transparent; color: inherit; }
.studio-action-options > button:hover { background: var(--color-ds-surface); }
.studio-drop-before { box-shadow: 0 -3px 0 0 var(--color-ds-primary-accent); }
.studio-drop-end { border-color: var(--color-ds-primary-accent) !important; background: var(--color-ds-surface-elevated); box-shadow: inset 0 3px 0 var(--color-ds-primary-accent); }
.studio-selected-section { border-color: var(--color-ds-primary-accent) !important; box-shadow: 0 0 0 1px var(--color-ds-primary-accent); }
</style>
