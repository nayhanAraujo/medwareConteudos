<template>
  <NuxtLink
    v-if="to"
    :to="to"
    class="ds-dropdown-item flex w-full items-center gap-2 px-3 py-2 text-sm text-ds-text"
    :class="itemClass"
    @click="$emit('click', $event)"
  >
    <slot />
  </NuxtLink>
  <a
    v-else-if="href"
    :href="href"
    class="ds-dropdown-item flex w-full items-center gap-2 px-3 py-2 text-sm text-ds-text"
    :class="itemClass"
    target="_blank"
    rel="noopener noreferrer"
    @click="$emit('click', $event)"
  >
    <slot />
  </a>
  <button
    v-else
    type="button"
    :disabled="disabled"
    class="ds-dropdown-item flex w-full items-center gap-2 px-3 py-2 text-sm text-ds-text"
    :class="itemClass"
    @click="$emit('click', $event)"
  >
    <slot />
  </button>
</template>

<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    to?: string
    href?: string
    disabled?: boolean
    danger?: boolean
  }>(),
  { disabled: false, danger: false }
)

defineEmits<{ click: [e: MouseEvent] }>()

const itemClass = computed(() => ({
  'ds-dropdown-item--disabled': props.disabled,
  'ds-dropdown-item--danger text-rose-600': props.danger && !props.disabled
}))
</script>
