<template>
  <section class="mb-8 min-w-0" aria-label="Busca global de modelos">
    <h2 class="text-base font-semibold mb-3">Buscar modelo</h2>
    <div class="flex flex-wrap items-center gap-x-5 gap-y-3 mb-4">
      <DsSearchInput v-model="query" placeholder="Buscar modelo por nome ou código" wrapper-class="!mb-0 flex-1 min-w-0 basis-64" @enter="search()" />
      <label class="flex items-center gap-2 text-sm shrink-0">
        <input v-model="includeInactive" type="checkbox" /> Incluir inativos
      </label>
    </div>
    <div v-if="ready" aria-live="polite" :aria-busy="loading">
      <p v-if="loading" class="text-sm py-4 text-gray-500">Buscando modelos...</p>
      <DsAlert v-else-if="error" variant="error">
        {{ error }}
        <DsButton size="sm" variant="secondary" icon="arrow-clockwise" @click="search()">Tentar novamente</DsButton>
      </DsAlert>
      <DsEmptyState v-else-if="!items.length" title="Nenhum modelo encontrado" icon="search" />
      <template v-else>
        <p class="text-sm text-gray-500 mb-2">{{ totalItems }} modelo(s) encontrado(s)</p>
        <ul class="divide-y divide-gray-200">
          <li v-for="item in items" :key="item.codScriptLaudo" class="flex flex-wrap items-center justify-between gap-3 py-4 min-w-0">
            <div class="min-w-0 flex-1 basis-56">
              <p class="font-mono text-xs text-gray-500 break-all">{{ item.codUniversal || item.codScriptLaudo }}</p>
              <h3 class="text-sm font-semibold break-words">{{ item.nome }}</h3>
              <p class="text-sm text-gray-600 break-words">{{ item.nomePacote || 'Sem pacote' }}</p>
              <div class="flex flex-wrap gap-2 mt-1">
                <DsBadge :variant="item.ativo ? 'success' : 'neutral'">{{ item.ativo ? 'Ativo' : 'Inativo' }}</DsBadge>
                <DsBadge :variant="item.aprovado ? 'success' : 'warning'">{{ item.aprovado ? 'Aprovado' : 'Não aprovado' }}</DsBadge>
                <span v-if="item.ultimaVersao" class="text-xs">{{ item.ultimaVersao }}</span>
              </div>
            </div>
            <div class="flex flex-wrap items-center gap-2">
              <DsButton variant="secondary" size="sm" icon="info-circle-fill" @click="detail = item">Detalhes</DsButton>
              <ScriptsScriptActionsDropdown :item="item" @transfer="openTransfer(item)" />
            </div>
          </li>
        </ul>
        <div v-if="totalPages > 1" class="flex flex-wrap items-center justify-center gap-3 mt-4">
          <DsButton size="sm" variant="secondary" icon="chevron-left" :disabled="page <= 1 || loading" @click="search(page - 1)">Anterior</DsButton>
          <span class="text-sm">{{ page }} / {{ totalPages }}</span>
          <DsButton size="sm" variant="secondary" icon="chevron-right" :disabled="page >= totalPages || loading" @click="search(page + 1)">Próxima</DsButton>
        </div>
      </template>
    </div>
    <DsModal :model-value="!!detail" :title="detail?.nome || 'Detalhes do modelo'" size="lg" @update:model-value="!$event && (detail = null)">
      <div v-if="detail" class="space-y-3 text-sm break-words">
        <p><strong>Código universal:</strong> {{ detail.codUniversal || '—' }}</p>
        <p><strong>Pacote:</strong> {{ detail.nomePacote || 'Sem pacote' }}</p>
        <p><strong>Sistema:</strong> {{ detail.sistema }}</p>
        <p><strong>Versão:</strong> {{ detail.ultimaVersao || 'Sem versão' }}</p>
        <p><strong>Descrição:</strong> {{ detail.descricao || '—' }}</p>
        <p><strong>Status:</strong> {{ detail.ativo ? 'Ativo' : 'Inativo' }}</p>
        <p><strong>Aprovado:</strong> {{ detail.aprovado ? 'Sim' : 'Não' }}</p>
        <p v-if="detail.aprovado"><strong>Ambiente:</strong> {{ detail.ambienteAprovacao || 'Ambiente não informado' }}</p>
        <ScriptsScriptActionsDropdown :item="detail" @transfer="openTransfer" />
        <DsButton v-if="detail.codPacote" variant="secondary" size="sm" icon="box-arrow-up-right" :to="{ path: '/scripts', query: { sistema, pacote: String(detail.codPacote), nome: detail.codUniversal || detail.nome } }">Abrir no pacote</DsButton>
      </div>
    </DsModal>
    <DsModal :model-value="!!transferItem" title="Transferir para outro pacote" @update:model-value="!transferring && !$event && (transferItem = null)">
      <p class="text-sm mb-3 break-words">{{ transferItem?.nome }}</p>
      <DsSelect v-model="destination" label="Pacote de destino" :disabled="transferring">
        <option value="">Selecione</option>
        <option v-for="p in destinations" :key="p.codPacote" :value="String(p.codPacote)">{{ p.nome }}</option>
      </DsSelect>
      <template #footer>
        <DsButton variant="secondary" :disabled="transferring" @click="transferItem = null">Cancelar</DsButton>
        <DsButton icon="box-arrow-right" :loading="transferring" :disabled="!destination || transferring" @click="transfer">Transferir</DsButton>
      </template>
    </DsModal>
  </section>
</template>

<script setup lang="ts">
import type { ScriptListItem, PacoteDto } from '~/composables/useScriptsApi'
const props = defineProps<{ sistema: string; pacotes: PacoteDto[] }>()
const route = useRoute()
const router = useRouter()
const api = useScriptsApi()
const swal = useSwal()
const { query, includeInactive, items, loading, error, page, totalPages, totalItems, ready, search } = useGlobalScriptSearch(toRef(props, 'sistema'))
onMounted(() => {
  query.value = String(route.query.modelo || '')
  includeInactive.value = route.query.inativos === '1'
})
watch([query, includeInactive], () => {
  void router.replace({ query: { ...route.query, modelo: query.value || undefined, inativos: includeInactive.value ? '1' : undefined } })
})
const detail = ref<ScriptListItem | null>(null)
const transferItem = ref<ScriptListItem | null>(null)
const destination = ref('')
const transferring = ref(false)
const destinations = computed(() => props.pacotes.filter(p => p.codPacote !== transferItem.value?.codPacote))
function openTransfer(item: ScriptListItem) {
  detail.value = null
  if (!item.codPacote) { void swal.error('Transferência indisponível', 'O modelo não possui pacote de origem.'); return }
  destination.value = ''
  transferItem.value = item
}
async function transfer() {
  if (!transferItem.value?.codPacote || !destination.value || transferring.value) return
  transferring.value = true
  try {
    const body = { pacoteOrigem: transferItem.value.codPacote, pacoteDestino: Number(destination.value), codigosScripts: [transferItem.value.codScriptLaudo] }
    const validation = await api.validatePackageTransfer(body)
    const warning = !validation.data.destinationMapped || validation.data.specificMappings.length
      ? ' Existem regras de publicação no Assistente que precisam ser conferidas.' : ''
    const confirmation = await swal.confirm('Transferir modelo?', `O modelo será movido para o pacote selecionado.${warning}`)
    if (!confirmation.isConfirmed) return
    await api.transferPackage(body)
    transferItem.value = null
    await search(page.value)
    await swal.toast('Modelo transferido!')
  } catch (cause) {
    await swal.error('Erro ao transferir modelo', cause instanceof Error ? cause.message : 'Tente novamente.')
  } finally { transferring.value = false }
}
</script>
