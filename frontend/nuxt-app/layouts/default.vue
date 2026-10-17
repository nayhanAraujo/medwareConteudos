<template>
  <div class="flex h-screen w-full" :class="{ 'permissions-layout': route.meta.permissionsWorkspace }">
    <LayoutAppSidebar />
    <main class="app-main-glass flex-1 overflow-y-auto font-sans" :class="route.meta.permissionsWorkspace ? 'permissions-main' : 'p-5'">
      <slot />
    </main>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
</script>

<style scoped>
.permissions-layout :deep(.permissions-main) { min-width: 0; padding: 48px; }
@media (max-width: 1023px) {
  .permissions-layout :deep(.permissions-main) { padding: 12px; }
  .permissions-layout :deep(aside) { width: 56px; }
  .permissions-layout :deep(aside > div:first-child) { padding: 12px 4px; }
  .permissions-layout :deep(aside svg) { width: 36px; height: 36px; }
  .permissions-layout :deep(aside a) { justify-content: center; padding: 14px 4px; }
  .permissions-layout :deep(aside a i) { margin: 0; }
  .permissions-layout :deep(aside span) { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
}
</style>
