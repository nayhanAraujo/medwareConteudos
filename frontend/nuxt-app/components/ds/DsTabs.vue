<template>
  <div class="mb-6">
    <div class="flex flex-wrap gap-2 bg-white/60 backdrop-blur-sm rounded-2xl p-2 border border-gray-200">
      <button
        v-for="tab in tabs"
        :key="tab.id"
        type="button"
        class="flex-1 min-w-[8rem] px-4 py-2.5 rounded-xl text-sm font-medium transition-all flex items-center justify-center gap-2"
        :class="
          modelValue === tab.id
            ? 'bg-black text-white shadow-sm'
            : 'text-gray-600 hover:bg-gray-100 hover:text-ds-text'
        "
        @click="$emit('update:modelValue', tab.id)"
      >
        <i v-if="tab.icon" :class="`bi bi-${tab.icon}`" />
        {{ tab.label }}
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
export interface DsTabItem {
  id: string
  label: string
  icon?: string
}

defineProps<{
  modelValue: string
  tabs: DsTabItem[]
}>()

defineEmits<{ 'update:modelValue': [id: string] }>()
</script>
