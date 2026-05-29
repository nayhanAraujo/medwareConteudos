<template>
  <header class="page-header mb-4">
    <div class="d-flex justify-content-between align-items-center flex-wrap gap-3">
      <div>
        <h1 class="page-title mb-1">
          <i v-if="icon" :class="`bi bi-${icon} icon-gradient me-2`" />
          {{ title }}
        </h1>
        <p v-if="subtitle" class="text-muted mb-0">{{ subtitle }}</p>
      </div>
      <div class="text-end">
        <p class="mb-1 text-muted small">
          Bem-vindo(a), <strong>{{ userName }}</strong>
        </p>
        <span class="badge bg-secondary">
          <i class="bi bi-clock me-1" />
          {{ now }}
        </span>
      </div>
    </div>
    <hr class="page-header-divider mt-3" />
  </header>
</template>

<script setup lang="ts">
defineProps<{
  title: string
  subtitle?: string
  icon?: string
}>()

const auth = useAuthStore()
const { now } = useClock()

const userName = computed(() => auth.user?.nome || 'Usuário')
</script>
