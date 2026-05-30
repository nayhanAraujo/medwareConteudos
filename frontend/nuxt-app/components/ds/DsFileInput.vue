<template>
  <div>
    <label v-if="label" :for="inputId" class="block text-sm font-medium text-ds-text mb-1.5">
      {{ label }}
    </label>

    <div
      class="relative rounded-2xl border bg-white transition-all"
      :class="zoneClass"
      @dragover.prevent="onDragOver"
      @dragleave.prevent="onDragLeave"
      @drop.prevent="onDrop"
    >
      <input
        :id="inputId"
        ref="inputRef"
        type="file"
        class="sr-only"
        :accept="accept"
        :multiple="multiple"
        :required="required && !hasFiles"
        :disabled="disabled"
        @change="onNativeChange"
      />

      <button
        type="button"
        class="ds-file-input-trigger flex w-full flex-col items-center justify-center gap-2 border-0 bg-transparent px-4 py-5 text-center shadow-none outline-none appearance-none focus:outline-none focus-visible:ring-2 focus-visible:ring-black/10 rounded-2xl"
        :class="disabled ? 'cursor-not-allowed' : 'cursor-pointer'"
        :disabled="disabled"
        @click="openPicker"
      >
        <span
          class="inline-flex h-11 w-11 items-center justify-center rounded-xl transition-colors"
          :class="dragActive ? 'bg-sky-100 text-sky-600' : 'bg-gray-100 text-gray-500 group-hover:bg-gray-200/80'"
        >
          <i :class="`bi bi-${icon} text-xl`" />
        </span>
        <span class="text-sm font-medium text-ds-text">
          {{ hasFiles ? changeLabel : chooseLabel }}
        </span>
        <span class="text-xs text-gray-500">{{ acceptHint }}</span>
      </button>
    </div>

    <ul v-if="displayFiles.length" class="mt-2 space-y-1.5">
      <li
        v-for="file in displayFiles"
        :key="file.key"
        class="flex items-center gap-2 rounded-xl border border-gray-100 bg-gray-50 px-3 py-2 text-xs text-gray-700"
      >
        <i :class="fileIcon(file.name)" class="shrink-0 text-gray-400" />
        <span class="min-w-0 flex-1 truncate">{{ file.name }}</span>
        <span v-if="file.size" class="shrink-0 text-gray-400">{{ file.size }}</span>
      </li>
    </ul>

    <p v-if="hint" class="text-xs text-gray-500 mt-1.5">{{ hint }}</p>
  </div>
</template>

<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    label?: string
    hint?: string
    accept?: string
    multiple?: boolean
    required?: boolean
    disabled?: boolean
    icon?: string
    chooseLabel?: string
    changeLabel?: string
    id?: string
  }>(),
  {
    multiple: false,
    required: false,
    disabled: false,
    icon: 'cloud-arrow-up',
    chooseLabel: 'Clique para escolher ou arraste aqui',
    changeLabel: 'Clique para alterar o arquivo'
  }
)

const emit = defineEmits<{ change: [files: FileList | null] }>()

const inputRef = ref<HTMLInputElement | null>(null)
const dragActive = ref(false)
const selectedFiles = ref<File[]>([])

const inputId = computed(() => props.id || `ds-file-${Math.random().toString(36).slice(2, 9)}`)

const hasFiles = computed(() => selectedFiles.value.length > 0)

const zoneClass = computed(() => {
  if (props.disabled) return 'border-gray-200 opacity-60 pointer-events-none'
  if (dragActive.value) return 'border-sky-300 bg-sky-50/40 border-dashed'
  if (hasFiles.value) return 'border-gray-300 border-solid hover:border-gray-400'
  return 'border-gray-200 border-dashed hover:border-gray-300 hover:bg-gray-50/50 group'
})

const acceptHint = computed(() => {
  if (props.accept?.includes('image')) return 'Imagens (PNG, JPG, etc.)'
  if (props.accept === '.pdf') return 'Arquivos PDF'
  if (props.accept === '.json') return 'Arquivo JSON'
  if (props.accept === '.dll') return 'Biblioteca DLL'
  if (props.multiple) return 'Um ou mais arquivos'
  return 'Um arquivo por vez'
})

const displayFiles = computed(() =>
  selectedFiles.value.map((file, index) => ({
    key: `${file.name}-${file.size}-${index}`,
    name: file.name,
    size: formatSize(file.size)
  }))
)

function formatSize(bytes: number) {
  if (!bytes) return ''
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function fileIcon(name: string) {
  const lower = name.toLowerCase()
  if (/\.(png|jpe?g|gif|webp|svg)$/.test(lower)) return 'bi bi-file-earmark-image'
  if (lower.endsWith('.pdf')) return 'bi bi-file-earmark-pdf'
  if (lower.endsWith('.json')) return 'bi bi-filetype-json'
  if (lower.endsWith('.dll')) return 'bi bi-file-earmark-code'
  return 'bi bi-file-earmark'
}

function setFiles(files: FileList | null) {
  if (!files?.length) {
    selectedFiles.value = []
    emit('change', null)
    return
  }

  const list = props.multiple ? Array.from(files) : [files[0]]
  selectedFiles.value = list
  emit('change', files)
}

function openPicker() {
  inputRef.value?.click()
}

function onNativeChange(e: Event) {
  const input = e.target as HTMLInputElement
  setFiles(input.files)
}

function onDragOver() {
  if (props.disabled) return
  dragActive.value = true
}

function onDragLeave() {
  dragActive.value = false
}

function onDrop(e: DragEvent) {
  dragActive.value = false
  if (props.disabled) return

  const dropped = e.dataTransfer?.files
  if (!dropped?.length) return

  if (props.multiple) {
    setFiles(dropped)
    return
  }

  const dt = new DataTransfer()
  dt.items.add(dropped[0])
  setFiles(dt.files)
  if (inputRef.value) inputRef.value.files = dt.files
}
</script>

<style scoped>
.ds-file-input-trigger:disabled {
  cursor: not-allowed;
}
</style>
