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
          <NuxtLink
            to="/"
            class="inline-flex h-9 w-9 items-center justify-center rounded-ds-btn border border-transparent bg-ds-surface-elevated transition-colors hover:border-ds-primary-accent"
            title="Voltar ao sistema de conteúdos"
            aria-label="Voltar ao sistema de conteúdos"
          >
            <svg viewBox="0 0 64 64" class="h-7 w-7" aria-hidden="true">
              <defs>
                <linearGradient id="mdw-studio-logo-bg" x1="8" y1="4" x2="56" y2="60" gradientUnits="userSpaceOnUse">
                  <stop stop-color="#60a5fa" />
                  <stop offset="0.55" stop-color="#2563eb" />
                  <stop offset="1" stop-color="#7c3aed" />
                </linearGradient>
                <linearGradient id="mdw-studio-logo-shine" x1="16" y1="6" x2="48" y2="32" gradientUnits="userSpaceOnUse">
                  <stop stop-color="#ffffff" stop-opacity="0.55" />
                  <stop offset="1" stop-color="#ffffff" stop-opacity="0" />
                </linearGradient>
              </defs>
              <rect x="4" y="6" width="56" height="52" rx="16" fill="url(#mdw-studio-logo-bg)" />
              <rect x="4" y="6" width="56" height="52" rx="16" fill="url(#mdw-studio-logo-shine)" />
              <rect x="5.5" y="7.5" width="53" height="49" rx="14.5" fill="none" stroke="white" stroke-opacity="0.45" />
              <text
                x="32"
                y="38"
                text-anchor="middle"
                fill="white"
                font-family="Manrope, Inter, sans-serif"
                font-size="15"
                font-weight="700"
                letter-spacing="0.5"
              >MDW</text>
            </svg>
          </NuxtLink>
        </div>
      </div>
    </nav>
    <main>
      <slot />
    </main>
  </div>
</template>
