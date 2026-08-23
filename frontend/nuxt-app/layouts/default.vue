<template>
  <div class="flex h-screen w-full">
    <LayoutAppSidebar />
    <main class="app-main-glass flex-1 p-5 overflow-y-auto font-sans">
      <slot />
    </main>
  </div>
</template>
