<script setup lang="ts">
import type { VariableBankOption } from '~/components/studio/StudioVariableCombobox.vue'

export interface UnmatchedMeasureForAlternative {
  id: string
  label: string
  originalText?: string | null
  unit?: string | null
}

const props = defineProps<{
  open: boolean
  measures: UnmatchedMeasureForAlternative[]
  bankOptions: VariableBankOption[]
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  skip: []
  confirm: [items: Array<{ measureId: string; label: string; codVariavel: number }>]
}>()

const selections = ref<Record<string, number | null>>({})
const saving = ref(false)

watch(
  () => [props.open, props.measures] as const,
  ([isOpen]) => {
    if (!isOpen) return
    const next: Record<string, number | null> = {}
    for (const m of props.measures) next[m.id] = null
    selections.value = next
  },
  { immediate: true }
)

const selectedCount = computed(() =>
  Object.values(selections.value).filter((v): v is number => typeof v === 'number' && v > 0).length
)

function close() {
  emit('update:open', false)
}

function skip() {
  emit('skip')
  close()
}

function confirm() {
  const items = props.measures
    .map(m => {
      const cod = selections.value[m.id]
      if (!cod) return null
      return { measureId: m.id, label: m.label, codVariavel: cod }
    })
    .filter((x): x is { measureId: string; label: string; codVariavel: number } => x != null)

  emit('confirm', items)
  close()
}
</script>

<template>
  <Teleport to="body">
    <div
      v-if="open"
      class="fixed inset-0 z-[120] flex items-center justify-center bg-black/55 p-4"
      @click.self="skip"
    >
      <div class="studio-card max-h-[min(90vh,40rem)] w-full max-w-2xl overflow-hidden shadow-2xl">
        <header class="border-b border-ds-border px-4 py-3">
          <h2 class="text-base font-semibold text-ds-text">Cadastrar variáveis alternativas</h2>
          <p class="mt-1 text-sm text-ds-muted">
            Medidas sem correspondência no banco. Opcionalmente, vincule cada uma como alternativa de uma variável existente.
            Você pode cancelar e seguir sem cadastrar.
          </p>
        </header>

        <div class="max-h-[24rem] space-y-3 overflow-y-auto px-4 py-3">
          <div
            v-for="measure in measures"
            :key="measure.id"
            class="rounded-ds-sm border border-ds-border bg-ds-surface-elevated p-3"
          >
            <div class="mb-2">
              <strong class="block text-sm text-ds-text">{{ measure.label }}</strong>
              <span class="text-xs text-ds-muted">
                {{ measure.originalText || 'Sem trecho original' }}
                <template v-if="measure.unit"> · {{ measure.unit }}</template>
              </span>
            </div>
            <label class="block text-xs text-ds-muted mb-1">Variável principal (opcional)</label>
            <select
              class="h-10 w-full rounded-ds-sm border border-ds-field-border bg-ds-surface px-2 text-sm text-ds-text"
              :value="selections[measure.id] ?? ''"
              @change="selections[measure.id] = Number(($event.target as HTMLSelectElement).value) || null"
            >
              <option value="">Não cadastrar</option>
              <option
                v-for="variable in bankOptions"
                :key="variable.codvariavel"
                :value="variable.codvariavel"
              >
                {{ variable.nome }} ({{ variable.sigla || variable.variavel }})
              </option>
            </select>
          </div>
        </div>

        <footer class="flex flex-wrap items-center justify-between gap-2 border-t border-ds-border px-4 py-3">
          <span class="text-xs text-ds-muted">{{ selectedCount }} selecionada(s)</span>
          <div class="flex flex-wrap gap-2">
            <StudioDsButton variant="ghost" :disabled="saving" @click="skip">
              Cancelar / pular
            </StudioDsButton>
            <StudioDsButton variant="primary" :disabled="saving" @click="confirm">
              {{ selectedCount > 0 ? 'Cadastrar e continuar' : 'Continuar sem cadastrar' }}
            </StudioDsButton>
          </div>
        </footer>
      </div>
    </div>
  </Teleport>
</template>
