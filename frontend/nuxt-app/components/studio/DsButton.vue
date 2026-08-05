<script setup lang="ts">
type Variant = 'primary' | 'secondary' | 'ghost' | 'danger' | 'success'

const props = withDefaults(defineProps<{
  variant?: Variant
  icon?: string
  loading?: boolean
  disabled?: boolean
  type?: 'button' | 'submit' | 'reset'
}>(), {
  variant: 'primary',
  loading: false,
  disabled: false,
  type: 'button'
})

/** Variantes alinhadas ao guia Medware (escuro) / DS hu169yr (claro via tokens). */
const variantClasses: Record<Variant, string> = {
  primary: 'bg-ds-primary text-white hover:bg-ds-primary-hover active:bg-ds-primary-pressed border border-transparent',
  secondary: 'bg-ds-surface text-ds-text border hover:bg-ds-surface-elevated',
  ghost: 'bg-transparent text-ds-muted border border-transparent hover:text-ds-text hover:bg-ds-surface-elevated',
  danger: 'bg-ds-danger text-white border border-transparent hover:bg-ds-danger-hover',
  success: 'bg-ds-success text-white border border-transparent hover:bg-ds-success-hover'
}
</script>

<template>
  <button
    :type="type"
    :disabled="disabled || loading"
    class="inline-flex h-10 min-h-10 cursor-pointer items-center justify-center gap-2 rounded-ds-btn px-4 text-sm font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-50"
    :class="variantClasses[variant]"
  >
    <i v-if="loading" class="bi bi-arrow-repeat animate-spin" />
    <i v-else-if="icon" :class="['bi', icon]" />
    <slot />
  </button>
</template>
