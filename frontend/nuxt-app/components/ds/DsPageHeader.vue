<template>
  <header class="mb-6">
    <div class="flex flex-wrap justify-between items-center gap-3">
      <div>
        <h1 class="text-2xl font-semibold font-manrope text-ds-text mb-1 flex items-center gap-2">
          <i v-if="icon" :class="`bi bi-${icon} text-blue-600`" />
          {{ title }}
        </h1>
        <p v-if="subtitle" class="text-sm text-gray-600 mb-0">{{ subtitle }}</p>
      </div>
      <div v-if="$slots.actions || showUserInfo" class="flex items-center gap-3 text-right">
        <slot name="actions" />
        <div v-if="showUserInfo">
        <p class="mb-1 text-sm text-gray-600">
          Bem-vindo(a), <strong class="text-ds-text">{{ userName }}</strong>
        </p>
        <span class="inline-flex items-center gap-1 text-xs font-medium px-3 py-1 rounded-full bg-ds-surface text-gray-700">
          <i class="bi bi-clock" />
          {{ now }}
        </span>
        </div>
      </div>
    </div>
    <hr class="mt-4 border-gray-200/80" />
  </header>
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    title: string
    subtitle?: string
    icon?: string
    showUserInfo?: boolean
  }>(),
  { showUserInfo: true }
)

const auth = useAuthStore()
const { now } = useClock()
const userName = computed(() => auth.user?.nome || 'Usuário')
</script>
