<script setup lang="ts">
import type { CampoScript } from '~/types/voice'

const props = defineProps<{
  campos: CampoScript[]
}>()

const grouped = computed(() => {
  const sections: Array<{ title: string; fields: CampoScript[] }> = []
  let current: { title: string; fields: CampoScript[] } | null = null

  for (const campo of props.campos) {
    if (campo.tipo === 'etiqueta') {
      current = { title: campo.descricao ?? campo.nome ?? 'Seção', fields: [] }
      sections.push(current)
      continue
    }
    if (!current) {
      current = { title: 'Campos', fields: [] }
      sections.push(current)
    }
    current.fields.push(campo)
  }

  return sections
})
</script>

<template>
  <StudioDsCard title="Modelo (camposScript)">
    <div v-if="!campos.length" class="text-sm text-ds-muted">
      Modelo vazio. Use o microfone para adicionar campos.
    </div>
    <div v-else class="space-y-4">
      <div v-for="(section, si) in grouped" :key="si" class="rounded-ds-btn border border-ds-divider p-3">
        <p class="font-semibold text-ds-text">{{ section.title }}</p>
        <ul class="mt-2 space-y-1">
          <li
            v-for="(field, fi) in section.fields"
            :key="`${field.nome}-${fi}`"
            class="flex flex-wrap items-center gap-2 text-sm text-ds-text-secondary"
          >
            <span class="text-ds-text">{{ field.descricao }}</span>
            <span v-if="field.nome" class="rounded bg-ds-surface-elevated px-1.5 py-0.5 text-xs font-mono">{{ field.nome }}</span>
            <span v-if="field.tipo" class="text-xs text-ds-muted">({{ field.tipo }})</span>
            <span v-if="field.medida" class="text-xs">{{ field.medida }}</span>
            <span
              v-for="(ref, ri) in field.referenciaNormalidade ?? []"
              :key="ri"
              class="text-xs text-ds-primary-accent"
            >
              {{ ref.sexo }}: {{ ref.valorMin }}–{{ ref.valorMax }}
            </span>
          </li>
        </ul>
      </div>
    </div>

    <details class="mt-4">
      <summary class="cursor-pointer text-xs text-ds-muted">JSON bruto</summary>
      <pre class="mt-2 max-h-48 overflow-auto rounded-ds-btn bg-ds-surface p-2 text-xs text-ds-text-secondary">{{ JSON.stringify({ camposScript: campos }, null, 2) }}</pre>
    </details>
  </StudioDsCard>
</template>
