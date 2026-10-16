<template>
  <div class="flex h-screen w-full">
    <div class="hidden md:flex"><LayoutAppSidebar /></div>
    <main class="app-main-glass flex-1 min-w-0 p-4 md:p-5 overflow-y-auto font-sans">
      <NuxtLink to="/conteudos" class="mb-4 inline-flex items-center gap-2 text-sm text-blue-700 md:hidden">
        <i class="bi bi-arrow-left" /> Conteúdos
      </NuxtLink>
      <slot />
    </main>
  </div>
</template>
