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
      <div v-if="$slots.actions || showUserInfo" class="flex items-center gap-3">
        <slot name="actions" />
        <div v-if="showUserInfo" class="flex items-center gap-3 text-right">
          <div
            class="h-10 w-10 rounded-full bg-gradient-to-br from-blue-500 to-indigo-500 text-white text-sm font-semibold flex items-center justify-center shadow-md ring-2 ring-white/80"
            :title="userName"
          >
            {{ initials }}
          </div>
          <div>
            <p class="text-sm text-gray-600" :class="showClock ? 'mb-1' : 'mb-0'">
              Bem-vindo(a), <strong class="text-ds-text">{{ userName }}</strong>
            </p>
            <span
              v-if="showClock"
              class="inline-flex items-center gap-1 text-xs font-medium px-3 py-1 rounded-full bg-white/60 backdrop-blur-sm text-gray-700 border border-white/70"
            >
              <i class="bi bi-clock" />
              {{ now }}
            </span>
          </div>
        </div>
      </div>
    </div>
    <hr class="mt-4 border-white/50" />
  </header>
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    title: string
    subtitle?: string
    icon?: string
    showUserInfo?: boolean
    showClock?: boolean
  }>(),
  { showUserInfo: true, showClock: true }
)

const auth = useAuthStore()
const { now } = useClock()
const userName = computed(() => auth.user?.nome || 'Usuário')
const initials = computed(() => {
  const parts = userName.value.trim().split(/\s+/).filter(Boolean)
  if (parts.length >= 2) return `${parts[0][0]}${parts[1][0]}`.toUpperCase()
  return userName.value.slice(0, 2).toUpperCase() || 'US'
})
</script>
