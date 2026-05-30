<template>
  <nav aria-label="breadcrumb" class="mb-4">
    <ol class="flex flex-wrap items-center gap-2 text-sm text-gray-600">
      <li v-for="(item, index) in items" :key="index" class="flex items-center gap-2">
        <span v-if="index > 0" class="text-gray-400">/</span>
        <a
          v-if="item.href && !item.active"
          href="#"
          class="hover:text-ds-text transition-colors"
          @click.prevent="$emit('navigate', item)"
        >
          {{ item.label }}
        </a>
        <span v-else :class="item.active ? 'font-semibold text-ds-text' : ''">{{ item.label }}</span>
      </li>
    </ol>
  </nav>
</template>

<script setup lang="ts">
export interface DsBreadcrumbItem {
  label: string
  href?: boolean
  active?: boolean
}

defineProps<{ items: DsBreadcrumbItem[] }>()
defineEmits<{ navigate: [item: DsBreadcrumbItem] }>()
</script>
