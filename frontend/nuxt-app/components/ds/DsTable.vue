<template>
  <div class="overflow-x-auto rounded-2xl border border-gray-200 bg-white">
    <table class="w-full text-sm text-left">
      <thead class="bg-ds-surface text-ds-text">
        <slot name="head" />
      </thead>
      <tbody class="divide-y divide-gray-100">
        <slot />
      </tbody>
    </table>
  </div>
</template>

<script setup lang="ts"></script>

<style scoped>
:deep(thead th) {
  padding: 0.75rem 1rem;
  font-weight: 600;
  font-size: 0.875rem;
}
:deep(tbody td) {
  padding: 0.75rem 1rem;
}
:deep(tbody tr:hover) {
  background-color: #f9fafb;
}
</style>
