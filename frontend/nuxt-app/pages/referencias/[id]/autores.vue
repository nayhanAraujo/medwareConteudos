<template>
  <div>
    <DsPageHeader title="Vincular Autores" subtitle="Associe autores à referência selecionada" icon="people" />
    <DsPageShell>
      <div class="flex flex-wrap justify-end gap-2 mb-4">
        <DsButton variant="secondary" icon="arrow-left" to="/referencias">Voltar</DsButton>
        <DsButton variant="secondary" icon="pencil-square" :to="`/referencias/${id}/editar`">Editar referência</DsButton>
      </div>

      <DsAlert v-if="errorMsg" variant="error" class="mb-4">{{ errorMsg }}</DsAlert>

      <div class="rounded-2xl border border-gray-200 bg-white p-4 mb-6">
        <h3 class="font-semibold text-ds-text mb-1">Referência #{{ referencia?.codReferencia }}</h3>
        <p class="text-sm text-gray-600 mb-0">{{ referencia?.titulo }} ({{ referencia?.ano || 's/ano' }})</p>
      </div>

      <div class="rounded-2xl border border-gray-200 bg-white p-4">
        <div class="flex justify-between items-center mb-4">
          <h4 class="font-semibold text-ds-text mb-0">Autores disponíveis</h4>
          <span class="text-xs text-gray-500">{{ selecionados.size }} selecionado(s)</span>
        </div>
        <div class="max-h-[55vh] overflow-y-auto space-y-2 pr-2">
          <label
            v-for="autor in autores"
            :key="autor.codAutor"
            class="flex items-center gap-3 p-3 rounded-xl border border-gray-200 hover:bg-gray-50 cursor-pointer"
          >
            <input
              type="checkbox"
              class="rounded"
              :checked="selecionados.has(autor.codAutor)"
              @change="toggleAutor(autor.codAutor)"
            />
            <div>
              <p class="font-medium text-sm mb-0">{{ autor.nome }}</p>
              <small class="text-gray-500">{{ autor.abreviacao || 'Sem abreviação' }}</small>
            </div>
          </label>
        </div>
      </div>

      <div class="flex justify-end mt-4">
        <DsButton icon="save" :loading="saving" :disabled="saving" @click="salvar">Salvar vínculo de autores</DsButton>
      </div>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default', middleware: ['admin'] })

const route = useRoute()
const id = computed(() => Number(route.params.id))
const referenciasApi = useReferenciasApi()
const swal = useSwal()

const referencia = ref<import('~/composables/useReferenciasApi').ReferenciaDetail | null>(null)
const autores = ref<import('~/composables/useReferenciasApi').AutorItem[]>([])
const selecionados = ref<Set<number>>(new Set())
const saving = ref(false)
const errorMsg = ref('')

async function loadAll() {
  try {
    errorMsg.value = ''
    const [refRes, autoresRes] = await Promise.all([
      referenciasApi.getReferencia(id.value),
      referenciasApi.listAutoresByReferencia(id.value)
    ])
    referencia.value = refRes.data
    autores.value = autoresRes.data || []
    selecionados.value = new Set((autores.value || []).filter((a) => a.selecionado).map((a) => a.codAutor))
  } catch (err) {
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar autores da referência.'
  }
}

function toggleAutor(codAutor: number) {
  if (selecionados.value.has(codAutor)) selecionados.value.delete(codAutor)
  else selecionados.value.add(codAutor)
  selecionados.value = new Set(selecionados.value)
}

async function salvar() {
  saving.value = true
  try {
    await referenciasApi.saveAutoresByReferencia(id.value, Array.from(selecionados.value))
    await swal.toast('Autores vinculados com sucesso.')
    await loadAll()
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao salvar autores', 'error')
  } finally {
    saving.value = false
  }
}

onMounted(loadAll)
</script>
