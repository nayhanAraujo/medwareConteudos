<template>
  <NuxtLink
    v-if="to"
    :to="to"
    :class="[baseClass, sizeClass, variantClass, block ? 'w-full' : '', 'disabled:opacity-50 disabled:pointer-events-none']"
  >
    <i v-if="icon" :class="`bi bi-${icon}`" />
    <slot />
  </NuxtLink>
  <a
    v-else-if="href"
    :href="href"
    :class="[baseClass, sizeClass, variantClass, block ? 'w-full' : '']"
  >
    <i v-if="icon" :class="`bi bi-${icon}`" />
    <slot />
  </a>
  <button
    v-else
    :type="type"
    :disabled="disabled || loading"
    :class="[baseClass, sizeClass, variantClass, block ? 'w-full' : '', 'disabled:opacity-50 disabled:pointer-events-none']"
    @click="$emit('click', $event)"
  >
    <i v-if="icon" :class="`bi bi-${icon}`" />
    <slot />
  </button>
</template>

<script setup lang="ts">
const baseClass =
  'inline-flex items-center justify-center gap-2 font-medium transition-all appearance-none outline-none focus:outline-none focus-visible:ring-2 focus-visible:ring-black/10'

const props = withDefaults(
  defineProps<{
    variant?: 'primary' | 'secondary' | 'ghost' | 'success' | 'danger'
    size?: 'sm' | 'md' | 'lg'
    type?: 'button' | 'submit' | 'reset'
    icon?: string
    disabled?: boolean
    loading?: boolean
    block?: boolean
    to?: string | Record<string, unknown> | object
    href?: string
  }>(),
  { variant: 'primary', size: 'md', type: 'button' }
)

defineEmits<{ click: [e: MouseEvent] }>()

const variantClass = computed(() => {
  const map = {
    primary:
      'border-0 text-white bg-black rounded-full hover:bg-gray-900 hover:shadow-lg shadow-[0_2.8px_2.2px_rgba(0,0,0,0.034),0_6.7px_5.3px_rgba(0,0,0,0.048)]',
    secondary: 'bg-white hover:bg-gray-100 text-black border border-gray-200 rounded-full hover:shadow-md',
    ghost: 'border-0 bg-transparent hover:bg-gray-100 text-gray-700 rounded-full',
    success: 'border-0 bg-green-600 hover:bg-green-700 text-white rounded-full',
    danger: 'border-0 bg-rose-600 hover:bg-rose-700 text-white rounded-full'
  }
  return map[props.variant]
})

const sizeClass = computed(() => {
  const map = { sm: 'text-sm px-4 py-2', md: 'text-sm px-6 py-2.5', lg: 'text-base px-8 py-4' }
  return map[props.size]
})
</script>