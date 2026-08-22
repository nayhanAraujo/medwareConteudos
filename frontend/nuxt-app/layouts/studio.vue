<script setup lang="ts">
const { isDark, toggleTheme, initTheme } = useStudioTheme()

onMounted(() => {
  initTheme()
})
</script>

<template>
  <div class="studio-shell min-h-screen bg-ds-page antialiased" data-theme="dark">
    <nav class="studio-nav">
      <div class="mx-auto flex max-w-7xl items-center justify-between px-4 py-3 sm:px-6">
        <NuxtLink to="/studio" class="flex items-center gap-2 font-semibold text-ds-text">
          <i class="bi bi-file-earmark-code text-ds-primary-accent" />
          Conversor LaudosUX
        </NuxtLink>
        <div class="flex items-center gap-4 text-sm">
          <NuxtLink to="/studio" class="text-ds-text-secondary hover:text-ds-primary-accent">Início</NuxtLink>
          <NuxtLink to="/studio/converter" class="text-ds-text-secondary hover:text-ds-primary-accent">Converter</NuxtLink>
          <NuxtLink to="/studio/modelos" class="text-ds-text-secondary hover:text-ds-primary-accent">Modelos</NuxtLink>
          <NuxtLink to="/studio/voz" class="text-ds-text-secondary hover:text-ds-primary-accent">Modo voz</NuxtLink>
          <NuxtLink to="/studio/editor" class="text-ds-text-secondary hover:text-ds-primary-accent">Editor</NuxtLink>
          <button
            type="button"
            class="inline-flex h-9 w-9 items-center justify-center rounded-ds-btn border bg-ds-surface-elevated text-ds-text-secondary transition-colors hover:border-ds-primary-accent hover:text-ds-primary-accent"
            :aria-label="isDark ? 'Ativar tema claro' : 'Ativar tema escuro'"
            :title="isDark ? 'Tema claro' : 'Tema escuro'"
            @click="toggleTheme"
          >
            <i :class="isDark ? 'bi bi-sun' : 'bi bi-moon'" class="text-base" />
          </button>
        </div>
      </div>
    </nav>
    <main>
      <slot />
    </main>
  </div>
</template>
