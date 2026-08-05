<script setup lang="ts">
const emit = defineEmits<{
  select: [file: File]
}>()

const isDragging = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)

const onDragOver = (e: DragEvent) => {
  e.preventDefault()
  isDragging.value = true
}

const onDragLeave = () => {
  isDragging.value = false
}

const onDrop = (e: DragEvent) => {
  e.preventDefault()
  isDragging.value = false
  const file = e.dataTransfer?.files[0]
  if (file && file.type.startsWith('image/')) {
    emit('select', file)
  }
}

const onFileChange = (e: Event) => {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (file) emit('select', file)
}

const openPicker = () => fileInput.value?.click()
</script>

<template>
  <div
    class="flex cursor-pointer flex-col items-center justify-center rounded-ds border-2 border-dashed p-10 transition-colors"
    :class="isDragging ? 'border-ds-primary bg-ds-primary-light' : 'bg-ds-surface hover:border-ds-primary-accent'"
    @dragover="onDragOver"
    @dragleave="onDragLeave"
    @drop="onDrop"
    @click="openPicker"
  >
    <input
      ref="fileInput"
      type="file"
      accept="image/*"
      class="hidden"
      @change="onFileChange"
    >
    <i class="bi bi-cloud-arrow-up mb-3 text-4xl text-ds-primary-accent" />
    <p class="font-medium text-ds-text">Arraste uma imagem ou clique para selecionar</p>
    <p class="mt-1 text-sm text-ds-muted">PNG, JPG, WEBP — máx. 10MB</p>
  </div>
</template>
