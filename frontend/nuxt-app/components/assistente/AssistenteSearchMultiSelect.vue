<template>
  <fieldset>
    <legend class="mb-1.5 text-sm font-medium text-ds-text">{{ label }}</legend>
    <DsInput v-model="term" :placeholder="searchPlaceholder" class="mb-2" @update:model-value="onSearch" />
    <p v-if="total >= 0" class="mb-2 text-xs text-gray-500">{{ total }} procedimento(s) cadastrado(s)</p>
    <div class="max-h-52 space-y-1 overflow-y-auto rounded-2xl border border-gray-200 p-3">
      <label v-for="option in options" :key="option.id" class="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1 hover:bg-gray-50">
        <input v-model="selected" type="checkbox" :value="option.id" :disabled="disabled" class="rounded border-gray-300">
        <span class="text-sm">{{ option.nome }}</span>
      </label>
      <p v-if="loading" class="text-sm text-gray-500">Carregando...</p>
      <p v-else-if="!options.length" class="text-sm text-gray-500">Nenhum procedimento encontrado.</p>
    </div>
    <p v-if="hint" class="mt-1 text-xs text-gray-500">{{ hint }}</p>
  </fieldset>
</template>

<script setup lang="ts">
import type { AssistenteOption } from '~/composables/useAssistenteApi'

const props = withDefaults(defineProps<{
  modelValue: number[]
  label: string
  hint?: string
  disabled?: boolean
  searchPlaceholder?: string
}>(), {
  searchPlaceholder: 'Buscar procedimento ou código TUSS...'
})

const emit = defineEmits<{ 'update:modelValue': [value: number[]] }>()
const api = useAssistenteApi()
const term = ref('')
const options = ref<AssistenteOption[]>([])
const total = ref(-1)
const loading = ref(false)
let timer: ReturnType<typeof setTimeout> | undefined

const selected = computed({
  get: () => props.modelValue,
  set: value => emit('update:modelValue', value)
})

async function fetchOptions(search = '') {
  loading.value = true
  try {
    const page = await api.list('procedimentos', 1, 50, search)
    total.value = page.total
    options.value = page.items.map(item => ({
      id: Number(item.id ?? item.codigo ?? item.codprocedimento ?? 0),
      nome: String(item.nome ?? item.descricao ?? item.descricao_proced ?? item.DESCRICAO_PROCED ?? `#${item.codigo}`)
    })).filter(item => item.id > 0)
  } finally {
    loading.value = false
  }
}

function onSearch() {
  clearTimeout(timer)
  timer = setTimeout(() => fetchOptions(term.value.trim()), 300)
}

onMounted(() => fetchOptions())
onUnmounted(() => clearTimeout(timer))
</script>
