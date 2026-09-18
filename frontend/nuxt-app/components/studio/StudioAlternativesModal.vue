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

const { theme } = useStudioTheme()
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
      class="studio-shell studio-alternatives-modal"
      :data-theme="theme"
      @click.self="skip"
    >
      <div class="studio-alternatives-modal__panel" role="dialog" aria-modal="true" aria-labelledby="studio-alternatives-title">
        <header class="studio-alternatives-modal__header">
          <h2 id="studio-alternatives-title" class="studio-alternatives-modal__title">
            Cadastrar variáveis alternativas
          </h2>
          <p class="studio-alternatives-modal__subtitle">
            Medidas sem correspondência no banco. Opcionalmente, vincule cada uma como alternativa de uma variável existente.
            Você pode cancelar e seguir sem cadastrar.
          </p>
        </header>

        <div class="studio-alternatives-modal__body">
          <div
            v-for="measure in measures"
            :key="measure.id"
            class="studio-alternatives-modal__item"
          >
            <div class="studio-alternatives-modal__item-head">
              <strong class="studio-alternatives-modal__item-label">{{ measure.label }}</strong>
              <span class="studio-alternatives-modal__item-meta">
                {{ measure.originalText || 'Sem trecho original' }}
                <template v-if="measure.unit"> · {{ measure.unit }}</template>
              </span>
            </div>
            <label class="studio-alternatives-modal__field-label">Variável principal (opcional)</label>
            <select
              class="studio-alternatives-modal__select"
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

        <footer class="studio-alternatives-modal__footer">
          <span class="studio-alternatives-modal__count">{{ selectedCount }} selecionada(s)</span>
          <div class="studio-alternatives-modal__actions">
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
