<template>
  <div
    class="relative min-h-screen flex items-center justify-center font-sans p-4"
    :class="backgroundImage ? '' : 'bg-ds-page'"
  >
    <div
      v-if="backgroundImage"
      class="fixed inset-0 -z-20 bg-cover bg-center"
      :style="{ backgroundImage: `url(${backgroundImage})` }"
      aria-hidden="true"
    />
    <div
      v-if="backgroundImage"
      class="fixed inset-0 -z-10"
      :class="overlayClass"
      aria-hidden="true"
    />
    <div
      class="relative z-0 w-full bg-white/50 backdrop-blur-md rounded-3xl shadow-ds border border-white/60 p-8"
      :class="maxWidthClass"
    >
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    size?: 'sm' | 'md' | 'lg'
    backgroundImage?: string
    overlayClass?: string
  }>(),
  { size: 'md', overlayClass: 'bg-ds-page/75' }
)

const maxWidthClass = computed(() => {
  const map = { sm: 'max-w-sm', md: 'max-w-md', lg: 'max-w-2xl' }
  return map[props.size]
})
</script>
