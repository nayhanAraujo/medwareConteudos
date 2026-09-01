<script setup lang="ts">
import type { VariableMatchCandidate } from '~/types/conversion'

export interface VariableBankOption {
  codvariavel: number
  nome?: string
  sigla?: string
  variavel?: string
  nomes_clinicos?: string[]
}

const props = withDefaults(defineProps<{
  modelValue: number | null
  candidates?: VariableMatchCandidate[]
  bankOptions?: VariableBankOption[]
  placeholder?: string
  ariaLabel?: string
}>(), {
  candidates: () => [],
  bankOptions: () => [],
  placeholder: 'Selecionar variável...'
})

const emit = defineEmits<{
  'update:modelValue': [value: number | null]
}>()

const open = ref(false)
const search = ref('')
const rootRef = ref<HTMLElement | null>(null)
const searchInputRef = ref<HTMLInputElement | null>(null)

const buildHaystack = (parts: Array<string | undefined | null>) =>
  parts.filter(Boolean).join(' ').toLocaleLowerCase()

const matchesSearch = (parts: Array<string | undefined | null>) => {
  const term = search.value.trim().toLocaleLowerCase()
  if (!term) return true
  return buildHaystack(parts).includes(term)
}

const filteredCandidates = computed(() => {
  const rows = props.candidates.filter(c =>
    matchesSearch([c.nome, c.sigla, c.variavel])
  )
  if (!props.modelValue || rows.some(c => c.codVariavel === props.modelValue)) return rows
  const selected = props.candidates.find(c => c.codVariavel === props.modelValue)
  return selected ? [selected, ...rows] : rows
})

const filteredBankOptions = computed(() => {
  const rows = props.bankOptions.filter(v =>
    matchesSearch([v.nome, v.sigla, v.variavel, ...(v.nomes_clinicos ?? [])])
  )
  if (!props.modelValue || rows.some(v => v.codvariavel === props.modelValue)) return rows
  const selected = props.bankOptions.find(v => v.codvariavel === props.modelValue)
  return selected ? [selected, ...rows] : rows
})

const hasResults = computed(() =>
  filteredCandidates.value.length > 0 || filteredBankOptions.value.length > 0
)

const selectedLabel = computed(() => {
  if (!props.modelValue) return props.placeholder
  const candidate = props.candidates.find(c => c.codVariavel === props.modelValue)
  if (candidate) return `${candidate.nome} (${candidate.sigla})`
  const variable = props.bankOptions.find(v => v.codvariavel === props.modelValue)
  if (variable) return `${variable.nome} (${variable.sigla || variable.variavel})`
  return `Variável #${props.modelValue}`
})

function closeDropdown() {
  open.value = false
  search.value = ''
}

function toggleDropdown() {
  open.value = !open.value
  if (open.value) {
    search.value = ''
    nextTick(() => searchInputRef.value?.focus())
  }
}

function selectValue(codVariavel: number) {
  emit('update:modelValue', codVariavel)
  closeDropdown()
}

function clearSelection(event: MouseEvent) {
  event.stopPropagation()
  emit('update:modelValue', null)
  closeDropdown()
}

function onDocumentClick(event: MouseEvent) {
  if (!open.value) return
  const target = event.target as Node
  if (rootRef.value && !rootRef.value.contains(target)) closeDropdown()
}

onMounted(() => document.addEventListener('click', onDocumentClick))
onUnmounted(() => document.removeEventListener('click', onDocumentClick))
</script>

<template>
  <div ref="rootRef" class="relative w-full min-w-[14rem]">
    <div
      class="flex h-10 w-full items-center rounded-ds-sm border border-ds-field-border bg-ds-surface-elevated text-sm text-ds-text transition-colors hover:border-ds-primary-accent/60"
      :class="open ? 'border-ds-primary-accent' : ''"
    >
      <button
        type="button"
        class="min-w-0 flex-1 truncate px-3 text-left"
        :class="!modelValue ? 'text-ds-muted' : ''"
        :aria-label="ariaLabel || placeholder"
        :aria-expanded="open"
        @click.stop="toggleDropdown"
      >
        {{ selectedLabel }}
      </button>
      <button
        v-if="modelValue"
        type="button"
        class="shrink-0 px-2 text-ds-muted hover:text-ds-text"
        aria-label="Limpar seleção"
        @click.stop="clearSelection"
      >
        <i class="bi bi-x-lg text-xs" />
      </button>
      <button
        type="button"
        class="shrink-0 px-2 text-ds-muted"
        aria-label="Abrir lista"
        @click.stop="toggleDropdown"
      >
        <i class="bi text-xs" :class="open ? 'bi-chevron-up' : 'bi-chevron-down'" />
      </button>
    </div>

    <div
      v-if="open"
      class="absolute z-30 mt-1 w-full overflow-hidden rounded-ds-sm border border-ds-field-border bg-ds-surface-elevated shadow-lg"
      @click.stop
    >
      <div class="sticky top-0 z-10 border-b border-ds-border bg-ds-surface-elevated p-2">
        <div class="relative">
          <span class="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-2.5 text-ds-muted">
            <i class="bi bi-search text-xs" />
          </span>
          <input
            ref="searchInputRef"
            v-model="search"
            type="text"
            class="h-9 w-full rounded-ds-sm border border-ds-field-border bg-ds-surface pl-8 pr-8 text-sm text-ds-text outline-none focus:border-ds-primary-accent"
            placeholder="Buscar por nome, sigla ou nome clínico..."
            autocomplete="off"
            @keydown.esc.prevent="closeDropdown"
          >
          <button
            v-if="search"
            type="button"
            class="absolute inset-y-0 right-0 flex items-center pr-2 text-ds-muted hover:text-ds-text"
            aria-label="Limpar busca"
            @click="search = ''"
          >
            <i class="bi bi-x-lg text-xs" />
          </button>
        </div>
      </div>

      <div class="max-h-56 overflow-y-auto py-1">
        <p v-if="!hasResults" class="px-3 py-4 text-center text-xs text-ds-muted">
          Nenhuma variável encontrada
        </p>

        <template v-if="filteredCandidates.length">
          <p class="px-3 pb-1 pt-2 text-[11px] font-semibold uppercase tracking-wide text-ds-muted">
            Sugestões
          </p>
          <button
            v-for="candidate in filteredCandidates"
            :key="`s-${candidate.codVariavel}`"
            type="button"
            class="flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-sm transition-colors hover:bg-ds-surface"
            :class="modelValue === candidate.codVariavel ? 'bg-ds-surface text-ds-primary-accent' : 'text-ds-text'"
            @click="selectValue(candidate.codVariavel)"
          >
            <span class="truncate">{{ candidate.nome }} ({{ candidate.sigla }})</span>
            <span class="shrink-0 text-xs text-ds-muted">{{ candidate.score }}%</span>
          </button>
        </template>

        <template v-if="filteredBankOptions.length">
          <p class="px-3 pb-1 pt-2 text-[11px] font-semibold uppercase tracking-wide text-ds-muted">
            Banco de variáveis
          </p>
          <button
            v-for="variable in filteredBankOptions"
            :key="`b-${variable.codvariavel}`"
            type="button"
            class="w-full truncate px-3 py-2 text-left text-sm transition-colors hover:bg-ds-surface"
            :class="modelValue === variable.codvariavel ? 'bg-ds-surface text-ds-primary-accent' : 'text-ds-text'"
            @click="selectValue(variable.codvariavel)"
          >
            {{ variable.nome }} ({{ variable.sigla || variable.variavel }})
          </button>
        </template>
      </div>
    </div>
  </div>
</template>
