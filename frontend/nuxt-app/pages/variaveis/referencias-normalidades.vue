<template>
  <div>
    <DsPageHeader
      title="Normalidades por Referência"
      subtitle="Vincule normalidades às referências e mantenha o banco consistente"
      icon="journal-medical"
    />
    <DsPageShell>
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <div class="lg:col-span-4">
          <div class="rounded-2xl border border-gray-200 bg-white p-4 mb-4">
            <DsInput v-model="referenciaBusca" label="Buscar referência" placeholder="Título, código ou descrição" @enter="load()" />
            <div class="flex justify-end mt-2">
              <DsButton variant="secondary" size="sm" icon="search" @click="load()">Filtrar</DsButton>
            </div>
          </div>
          <div class="rounded-2xl border border-gray-200 bg-white p-2 max-h-[65vh] overflow-y-auto">
            <button
              v-for="r in painel?.referencias || []"
              :key="r.codigo"
              type="button"
              class="w-full text-left rounded-xl px-3 py-2 transition border mb-2"
              :class="selectedRefId === r.codigo ? 'bg-blue-50 border-blue-200' : 'bg-white border-gray-200 hover:bg-gray-50'"
              @click="selectReferencia(r.codigo)"
            >
              <p class="font-semibold text-sm mb-1">#{{ r.codigo }} · {{ r.titulo }}</p>
              <p class="text-xs text-gray-500 mb-1">{{ r.autores || 'Sem autores' }}</p>
              <div class="text-[11px] text-gray-500">
                <span class="mr-2">{{ r.ano || 's/ano' }}</span>
                <span class="mr-2">Vars: {{ r.totalVariaveis }}</span>
                <span>Norms: {{ r.totalNormalidades }}</span>
              </div>
            </button>
          </div>
          <DsAlert variant="warning" class="mt-4">
            Normalidades sem referência: <strong>{{ painel?.totalSemReferencia || 0 }}</strong>
          </DsAlert>
        </div>

        <div class="lg:col-span-8">
          <DsAlert v-if="errorMsg" variant="error" class="mb-4">{{ errorMsg }}</DsAlert>
          <template v-if="painel?.referenciaSelecionada">
            <div class="rounded-2xl border border-gray-200 bg-white p-4 mb-4">
              <p class="font-semibold text-ds-text mb-1">
                #{{ painel.referenciaSelecionada.codigo }} · {{ painel.referenciaSelecionada.titulo }}
              </p>
              <p class="text-sm text-gray-600 mb-1">Ano: {{ painel.referenciaSelecionada.ano || 's/ano' }}</p>
              <p class="text-sm text-gray-600 mb-1">Autores: {{ painel.referenciaSelecionada.autores || 'Sem autores' }}</p>
              <p class="text-sm text-gray-600">{{ painel.referenciaSelecionada.descricao || 'Sem descrição' }}</p>
              <div v-if="painel.anexosReferencia?.length" class="mt-3 text-sm">
                <p class="font-medium mb-1">Anexos:</p>
                <ul class="space-y-1">
                  <li v-for="(a, i) in painel.anexosReferencia" :key="`${i}-${a.caminho || ''}`">
                    <a v-if="a.caminho" :href="a.caminho" target="_blank" rel="noopener" class="text-blue-600 hover:underline">
                      {{ a.descricao || a.tipo || 'Anexo' }}
                    </a>
                    <span v-else>{{ a.descricao || a.tipo || 'Anexo' }}</span>
                  </li>
                </ul>
              </div>
            </div>

            <div class="rounded-2xl border border-gray-200 bg-white p-4 mb-4">
              <div class="flex flex-wrap justify-between items-center gap-2 mb-3">
                <h3 class="font-semibold text-ds-text mb-0">Variáveis vinculadas</h3>
                <div class="flex gap-2 items-center">
                  <DsSelect v-model="importOrigem" input-class="min-w-[220px]">
                    <option value="">Importar de outra referência</option>
                    <option v-for="r in painel.outrasReferencias || []" :key="r.codigo" :value="String(r.codigo)">
                      #{{ r.codigo }} · {{ r.titulo }}
                    </option>
                  </DsSelect>
                  <DsButton variant="secondary" size="sm" icon="box-arrow-in-down" @click="importar">Importar</DsButton>
                </div>
              </div>
              <DsTable v-if="painel.variaveisVinculadas?.length">
                <template #head>
                  <tr>
                    <th>Variável</th>
                    <th>Total Normalidades</th>
                    <th />
                  </tr>
                </template>
                <tr v-for="v in painel.variaveisVinculadas" :key="v.codVariavel">
                  <td>
                    <p class="font-semibold mb-0">{{ v.nomeVariavel }}</p>
                    <small class="text-gray-500">{{ v.variavel }} · {{ v.sigla || 's/sigla' }}</small>
                  </td>
                  <td>{{ v.totalNormalidades }}</td>
                  <td>
                    <div class="flex justify-end">
                      <DsButton size="sm" variant="secondary" icon="eye" @click="abrirNormalidades(v.codVariavel, v.nomeVariavel)">
                        Ver normalidades
                      </DsButton>
                    </div>
                  </td>
                </tr>
              </DsTable>
              <DsAlert v-else variant="info">Ainda não existem variáveis vinculadas a esta referência.</DsAlert>
            </div>

            <div class="rounded-2xl border border-gray-200 bg-white p-4">
              <div class="flex justify-between items-center mb-3">
                <h3 class="font-semibold text-ds-text mb-0">Vincular novas normalidades</h3>
                <span class="text-xs text-gray-500">
                  Mostrando {{ painel.normalidadesDisponiveis?.length || 0 }} de até {{ painel.limiteDisponiveis || 0 }}
                </span>
              </div>
              <div class="grid grid-cols-1 md:grid-cols-12 gap-3 mb-4">
                <div class="md:col-span-9">
                  <DsInput v-model="variavelBusca" label="Filtrar normalidades disponíveis" placeholder="Nome, variável ou sigla" @enter="load()" />
                </div>
                <div class="md:col-span-3 flex items-end justify-end">
                  <DsButton variant="secondary" size="sm" icon="search" @click="load()">Aplicar filtro</DsButton>
                </div>
              </div>
              <div class="max-h-[45vh] overflow-y-auto border border-gray-100 rounded-xl">
                <table class="w-full text-sm">
                  <thead class="bg-gray-50 sticky top-0">
                    <tr>
                      <th class="text-left px-3 py-2">Sel</th>
                      <th class="text-left px-3 py-2">Variável</th>
                      <th class="text-left px-3 py-2">Faixa</th>
                      <th class="text-left px-3 py-2">Página</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="n in painel.normalidadesDisponiveis || []" :key="n.codNormalidade" class="border-t border-gray-100">
                      <td class="px-3 py-2">
                        <input type="checkbox" class="rounded" :checked="selectedNorms.has(n.codNormalidade)" @change="toggleNorm(n.codNormalidade)" />
                      </td>
                      <td class="px-3 py-2">
                        <p class="font-medium mb-0">{{ n.nomeVariavel }}</p>
                        <small class="text-gray-500">{{ n.variavel }} · {{ n.sigla || 's/sigla' }} · {{ n.sexo || '-' }}</small>
                      </td>
                      <td class="px-3 py-2">
                        {{ n.valorMin ?? '-' }} — {{ n.valorMax ?? '-' }}<br />
                        <small class="text-gray-500">Idade: {{ n.idadeMin ?? '-' }} a {{ n.idadeMax ?? '-' }}</small>
                      </td>
                      <td class="px-3 py-2 w-28">
                        <input
                          v-model="paginaByNorm[n.codNormalidade]"
                          type="number"
                          min="0"
                          class="w-full px-2 py-1 border border-gray-200 rounded-lg"
                          placeholder="Pg."
                        />
                      </td>
                    </tr>
                    <tr v-if="!(painel.normalidadesDisponiveis || []).length">
                      <td colspan="4" class="px-3 py-4 text-center text-gray-500">Nenhuma normalidade disponível para vincular.</td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <div class="flex justify-end mt-3">
                <DsButton icon="link-45deg" :loading="saving" :disabled="saving || !selectedNorms.size" @click="vincularSelecionadas">
                  Vincular selecionadas
                </DsButton>
              </div>
            </div>
          </template>
          <DsAlert v-else variant="info">Selecione uma referência para visualizar detalhes e vínculos.</DsAlert>
        </div>
      </div>
    </DsPageShell>

    <DsModal v-model="normalidadesModalOpen" :title="`Normalidades: ${modalVariavelNome}`" size="xl">
      <div v-if="modalNormalidades.length" class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead class="bg-gray-50">
            <tr>
              <th class="text-left px-2 py-2">Sexo</th>
              <th class="text-left px-2 py-2">Valor Min</th>
              <th class="text-left px-2 py-2">Valor Max</th>
              <th class="text-left px-2 py-2">Idade Min</th>
              <th class="text-left px-2 py-2">Idade Max</th>
              <th class="text-left px-2 py-2">Página</th>
              <th class="text-left px-2 py-2">Classificação</th>
              <th class="text-left px-2 py-2"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="n in modalNormalidades" :key="n.codNormalidade" class="border-t border-gray-100">
              <td class="px-2 py-2">{{ n.sexo || '-' }}</td>
              <td class="px-2 py-2"><input v-model.number="n.valorMin" type="number" step="any" class="w-24 px-2 py-1 border rounded" /></td>
              <td class="px-2 py-2"><input v-model.number="n.valorMax" type="number" step="any" class="w-24 px-2 py-1 border rounded" /></td>
              <td class="px-2 py-2"><input v-model.number="n.idadeMin" type="number" class="w-20 px-2 py-1 border rounded" /></td>
              <td class="px-2 py-2"><input v-model.number="n.idadeMax" type="number" class="w-20 px-2 py-1 border rounded" /></td>
              <td class="px-2 py-2"><input v-model.number="n.pagina" type="number" class="w-20 px-2 py-1 border rounded" /></td>
              <td class="px-2 py-2 text-gray-500">{{ n.classificacao || '-' }}</td>
              <td class="px-2 py-2">
                <div class="flex gap-1">
                  <DsButton size="sm" variant="secondary" icon="check2" @click="salvarNormalidade(n)">Salvar</DsButton>
                  <DsButton size="sm" variant="danger" icon="x-circle" @click="desvincularNormalidade(n.codNormalidade)">Desvincular</DsButton>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <DsAlert v-else variant="info">Nenhuma normalidade encontrada para esta variável.</DsAlert>
      <template #footer>
        <DsButton variant="secondary" @click="normalidadesModalOpen = false">Fechar</DsButton>
      </template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

const variaveisApi = useVariaveisApi()
const swal = useSwal()

const loading = ref(false)
const saving = ref(false)
const errorMsg = ref('')

const referenciaBusca = ref('')
const variavelBusca = ref('')
const selectedRefId = ref<number | null>(null)
const painel = ref<import('~/composables/useVariaveisApi').ReferenciasNormalidadesPainel | null>(null)

const selectedNorms = ref<Set<number>>(new Set())
const paginaByNorm = reactive<Record<number, string>>({})
const importOrigem = ref('')

const normalidadesModalOpen = ref(false)
const modalVariavelNome = ref('')
const modalNormalidades = ref<
  Array<{
    codNormalidade: number
    sexo?: string | null
    valorMin?: number | null
    valorMax?: number | null
    idadeMin?: number | null
    idadeMax?: number | null
    pagina?: number | null
    classificacao?: string | null
  }>
>([])

async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    const res = await variaveisApi.getReferenciasNormalidades({
      referenciaId: selectedRefId.value ?? undefined,
      referenciaBusca: referenciaBusca.value || undefined,
      variavelBusca: variavelBusca.value || undefined,
      limite: 50
    })
    painel.value = res.data
    if (!selectedRefId.value && painel.value?.referenciaSelecionada?.codigo) {
      selectedRefId.value = painel.value.referenciaSelecionada.codigo
    }
    selectedNorms.value = new Set()
    Object.keys(paginaByNorm).forEach((k) => delete paginaByNorm[Number(k)])
  } catch (err) {
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar painel de referências.'
  } finally {
    loading.value = false
  }
}

function selectReferencia(codReferencia: number) {
  selectedRefId.value = codReferencia
  load()
}

function toggleNorm(codNormalidade: number) {
  if (selectedNorms.value.has(codNormalidade)) selectedNorms.value.delete(codNormalidade)
  else selectedNorms.value.add(codNormalidade)
  selectedNorms.value = new Set(selectedNorms.value)
}

async function vincularSelecionadas() {
  if (!selectedRefId.value) return
  saving.value = true
  try {
    await variaveisApi.vincularNormalidadesReferencia({
      codReferencia: selectedRefId.value,
      normalidades: Array.from(selectedNorms.value).map((cod) => ({
        codNormalidade: cod,
        pagina: paginaByNorm[cod] ? Number(paginaByNorm[cod]) : null
      }))
    })
    await swal.toast('Normalidades vinculadas com sucesso.')
    await load()
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao vincular normalidades', 'error')
  } finally {
    saving.value = false
  }
}

function abrirNormalidades(codVariavel: number, nomeVariavel: string) {
  const regs = painel.value?.normalidadesPorVariavel?.[String(codVariavel)] || []
  modalNormalidades.value = regs.map((n) => ({ ...n }))
  modalVariavelNome.value = nomeVariavel
  normalidadesModalOpen.value = true
}

async function salvarNormalidade(n: {
  codNormalidade: number
  valorMin?: number | null
  valorMax?: number | null
  idadeMin?: number | null
  idadeMax?: number | null
  pagina?: number | null
}) {
  try {
    await variaveisApi.atualizarNormalidadeReferencia({
      codNormalidade: n.codNormalidade,
      valorMin: n.valorMin ?? null,
      valorMax: n.valorMax ?? null,
      idadeMin: n.idadeMin ?? null,
      idadeMax: n.idadeMax ?? null,
      pagina: n.pagina ?? null
    })
    await swal.toast('Normalidade atualizada.')
    await load()
    abrirNormalidades(
      Number(
        Object.keys(painel.value?.normalidadesPorVariavel || {}).find((k) =>
          (painel.value?.normalidadesPorVariavel?.[k] || []).some((x) => x.codNormalidade === n.codNormalidade)
        ) || 0
      ),
      modalVariavelNome.value
    )
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao atualizar normalidade', 'error')
  }
}

async function desvincularNormalidade(codNormalidade: number) {
  const confirm = await swal.confirm('Desvincular normalidade', 'Deseja remover o vínculo desta normalidade com a referência?')
  if (!confirm?.isConfirmed) return
  try {
    await variaveisApi.desvincularNormalidadeReferencia({
      codNormalidade,
      codReferencia: selectedRefId.value || undefined
    })
    await swal.toast('Normalidade desvinculada.')
    await load()
    normalidadesModalOpen.value = false
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao desvincular normalidade', 'error')
  }
}

async function importar() {
  if (!selectedRefId.value || !importOrigem.value) {
    await swal.toast('Selecione a referência de origem para importação.', 'warning')
    return
  }
  const confirm = await swal.confirm('Importar normalidades', 'Deseja importar normalidades da referência selecionada?')
  if (!confirm?.isConfirmed) return
  try {
    const res = await variaveisApi.importarNormalidadesReferencia({
      codReferenciaDestino: selectedRefId.value,
      codReferenciaOrigem: Number(importOrigem.value)
    })
    await swal.toast(res.data.message || 'Importação concluída.')
    importOrigem.value = ''
    await load()
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao importar normalidades', 'error')
  }
}

onMounted(load)
</script>
