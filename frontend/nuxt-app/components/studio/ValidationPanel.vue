<script setup lang="ts">
const props = defineProps<{
  validation: {
    isValid: boolean
    errors: string[]
    warnings: string[]
  } | null
}>()
</script>

<template>
  <div v-if="validation" class="space-y-3">
    <div
      class="flex items-center gap-2 rounded-ds-sm px-3 py-2 text-sm font-medium"
      :class="validation.isValid
        ? 'bg-ds-success/15 text-ds-success'
        : 'bg-ds-danger/15 text-ds-danger'"
    >
      <i :class="validation.isValid ? 'bi bi-check-circle' : 'bi bi-x-circle'" />
      {{ validation.isValid ? 'HTML válido para LaudosUX' : 'HTML com erros de validação' }}
    </div>

    <ul v-if="validation.errors.length" class="space-y-1">
      <li
        v-for="(error, i) in validation.errors"
        :key="`e-${i}`"
        class="flex items-start gap-2 text-sm text-ds-danger"
      >
        <i class="bi bi-exclamation-triangle mt-0.5" />
        {{ error }}
      </li>
    </ul>

    <ul v-if="validation.warnings.length" class="space-y-1">
      <li
        v-for="(warning, i) in validation.warnings"
        :key="`w-${i}`"
        class="flex items-start gap-2 text-sm text-ds-warning"
      >
        <i class="bi bi-info-circle mt-0.5" />
        {{ warning }}
      </li>
    </ul>
  </div>
</template>
