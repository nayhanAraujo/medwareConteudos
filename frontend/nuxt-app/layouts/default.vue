<template>
  <div class="flex h-screen w-full">
    <LayoutAppSidebar />
    <main class="flex-1 p-5 overflow-y-auto bg-ds-page font-sans">
      <slot />
    </main>
  </div>
</template>
