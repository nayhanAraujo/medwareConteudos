<template>
  <div>
    <DsPageHeader
      title="Padrões de Normalidade por Cliente"
      subtitle="Snapshots customizados por cliente — não alteram o catálogo global"
      icon="people"
    />
    <DsPageShell>
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <div class="lg:col-span-4">
          <div class="rounded-2xl border border-gray-200 bg-white p-4 mb-4">
            <DsSelect v-model="codClienteSel" label="Cliente">
              <option value="">Selecione o cliente</option>
              <option v-for="c in clientes" :key="c.codCliente" :value="String(c.codCliente)">
                #{{ c.codCliente }} · {{ c.nome }}
              </option>
            </DsSelect>
          </div>

          <template v-if="codClienteSel">
            <div class="flex justify-between items-center mb-2">
              <h3 class="font-semibold text-sm text-ds-text mb-0">Padrões</h3>
              <DsButton v-if="auth.isAdmin" size="sm" variant="success" icon="plus-lg" @click="abrirNovoPadrao">
                Novo
              </DsButton>
            </div>
            <div class="rounded-2xl border border-gray-200 bg-white p-2 max-h-[55vh] overflow-y-auto">
              <button
                v-for="p in painel?.padroes || []"
                :key="p.codPadrao"
                type="button"
                class="w-full text-left rounded-xl px-3 py-2 transition border mb-2"
                :class="selectedPadraoId === p.codPadrao ? 'bg-blue-50 border-blue-200' : 'bg-white border-gray-200 hover:bg-gray-50'"
                @click="selectPadrao(p.codPadrao)"
              >
                <div class="flex items-center gap-2 mb-1">
                  <p class="font-semibold text-sm mb-0 flex-1">{{ p.nome }}</p>
                  <DsBadge v-if="p.padraoVigente === 1" variant="success">Vigente</DsBadge>
                  <DsBadge v-if="p.ativo !== 1" variant="dark">Inativo</DsBadge>
                </div>
                <p class="text-xs text-gray-500 mb-1"><code>{{ p.codigo }}</code></p>
                <p class="text-[11px] text-gray-500">
                  {{ p.referenciaTitulo ? `Origem: ${p.referenciaTitulo}` : 'Customizado' }}
                  · {{ p.totalFaixas }} faixa(s)
                </p>
              </button>
              <p v-if="!painel?.padroes?.length && !loading" class="text-sm text-gray-500 px-2 py-4 text-center">
                Nenhum padrão cadastrado para este cliente.
              </p>
            </div>
          </template>
        </div>

        <div class="lg:col-span-8">
          <DsAlert v-if="errorMsg" variant="error" class="mb-4">{{ errorMsg }}</DsAlert>
          <div v-if="loading" class="text-center py-8 text-gray-500">Carregando...</div>

          <template v-else-if="painel?.padraoSelecionado">
            <div class="rounded-2xl border border-gray-200 bg-white p-4 mb-4">
              <div class="flex flex-wrap justify-between items-start gap-2">
                <div>
                  <p class="font-semibold text-ds-text mb-1">{{ painel.padraoSelecionado.nome }}</p>
                  <p class="text-sm text-gray-600 mb-1">Código API: <code>{{ painel.padraoSelecionado.codigo }}</code></p>
                  <p class="text-sm text-gray-600">
                    Origem:
                    {{ painel.padraoSelecionado.referenciaTitulo || '100% customizado (sem referência)' }}
                  </p>
                </div>
                <div v-if="auth.isAdmin" class="flex flex-wrap gap-2">
                  <DsSelect v-model="importReferencia" input-class="min-w-[200px]">
                    <option value="">Importar snapshot de...</option>
                    <option v-for="r in painel.referencias || []" :key="r.codigo" :value="String(r.codigo)">
                      #{{ r.codigo }} · {{ r.titulo }}
                    </option>
                  </DsSelect>
                  <DsButton variant="secondary" size="sm" icon="box-arrow-in-down" @click="importarReferencia">
                    Importar
                  </DsButton>
                  <DsButton
                    v-if="painel.padraoSelecionado.padraoVigente !== 1"
                    variant="secondary"
                    size="sm"
                    icon="star"
                    @click="definirVigente"
                  >
                    Definir vigente
                  </DsButton>
                  <DsButton variant="danger" size="sm" icon="trash" @click="excluirPadrao">Excluir</DsButton>
                </div>
              </div>
            </div>

            <div class="rounded-2xl border border-gray-200 bg-white p-4 mb-4">
              <DsInput
                v-model="variavelBusca"
                label="Buscar variável"
                placeholder="Nome, sigla ou código"
                @enter="load()"
              />
              <div class="flex justify-end mt-2">
                <DsButton variant="secondary" size="sm" icon="search" @click="load()">Filtrar</DsButton>
              </div>
            </div>

            <div class="rounded-2xl border border-gray-200 bg-white overflow-hidden">
              <table class="w-full text-sm">
                <thead class="bg-gray-50 border-b border-gray-200">
                  <tr>
                    <th class="px-3 py-2 text-left">Variável</th>
                    <th class="px-3 py-2 text-center">Faixas</th>
                    <th class="px-3 py-2 text-left">Comentário</th>
                    <th class="px-3 py-2 text-center w-28">Ações</th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="v in painel.variaveis || []"
                    :key="v.codVariavel"
                    class="border-b border-gray-100 hover:bg-gray-50"
                  >
                    <td class="px-3 py-2">
                      <p class="font-medium mb-0">{{ v.nomeVariavel }}</p>
                      <p class="text-xs text-gray-500 mb-0"><code>{{ v.variavel }}</code></p>
                    </td>
                    <td class="px-3 py-2 text-center">{{ v.totalFaixas }}</td>
                    <td class="px-3 py-2 text-gray-600 truncate max-w-[200px]">{{ v.comentarioTexto || '—' }}</td>
                    <td class="px-3 py-2 text-center">
                      <DsButton size="sm" variant="secondary" icon="sliders" @click="abrirFaixas(v)">
                        Editar
                      </DsButton>
                    </td>
                  </tr>
                </tbody>
              </table>
              <p v-if="!(painel.variaveis?.length)" class="text-sm text-gray-500 p-4 text-center mb-0">
                Nenhuma variável neste padrão. Importe de uma referência ou cadastre faixas manualmente.
              </p>
            </div>
          </template>

          <DsAlert v-else-if="codClienteSel && !loading" variant="info">
            Selecione um padrão ou crie um novo para este cliente.
          </DsAlert>
          <DsAlert v-else-if="!codClienteSel" variant="info">
            Selecione um cliente para gerenciar os padrões de normalidade.
          </DsAlert>
        </div>
      </div>
    </DsPageShell>

    <DsModal v-model="faixasModalOpen" :title="`Faixas — ${modalVariavelNome}`" size="2xl">
      <div class="overflow-x-auto">
        <table class="w-full text-sm min-w-[640px]">
          <thead>
            <tr class="border-b border-gray-200">
              <th class="px-2 py-2 text-left">Sexo</th>
              <th class="px-2 py-2 text-left">Min</th>
              <th class="px-2 py-2 text-left">Max</th>
              <th class="px-2 py-2 text-left">Idade min</th>
              <th class="px-2 py-2 text-left">Idade max</th>
              <th class="px-2 py-2 text-left">Página</th>
              <th class="px-2 py-2 text-left">Classificação</th>
              <th class="px-2 py-2" />
            </tr>
          </thead>
          <tbody>
            <tr v-for="n in modalFaixas" :key="n.codFaixa" class="border-b border-gray-100">
              <td class="px-2 py-2">{{ n.sexo }}</td>
              <td class="px-2 py-2"><input v-model.number="n.valorMin" type="number" class="w-20 px-2 py-1 border rounded" step="any" /></td>
              <td class="px-2 py-2"><input v-model.number="n.valorMax" type="number" class="w-20 px-2 py-1 border rounded" step="any" /></td>
              <td class="px-2 py-2"><input v-model.number="n.idadeMin" type="number" class="w-20 px-2 py-1 border rounded" /></td>
              <td class="px-2 py-2"><input v-model.number="n.idadeMax" type="number" class="w-20 px-2 py-1 border rounded" /></td>
              <td class="px-2 py-2"><input v-model.number="n.pagina" type="number" class="w-20 px-2 py-1 border rounded" step="any" /></td>
              <td class="px-2 py-2 text-gray-500">{{ n.classificacao || '—' }}</td>
              <td class="px-2 py-2 whitespace-nowrap">
                <DsButton size="sm" variant="secondary" icon="check2" @click="salvarFaixa(n)">Salvar</DsButton>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="mt-4">
        <label class="block text-sm font-medium text-gray-700 mb-1">Comentário de normalidade (texto livre)</label>
        <input
          v-model="modalComentario"
          type="text"
          maxlength="500"
          class="w-full px-3 py-2 border border-gray-200 rounded-lg"
          placeholder="Ex.: ≤ 5"
        />
      </div>
      <DsAlert v-if="!modalFaixas.length" variant="info">Nenhuma faixa para esta variável.</DsAlert>
      <template #footer>
        <DsButton variant="secondary" @click="salvarComentarioModal">Salvar comentário</DsButton>
        <DsButton variant="secondary" @click="faixasModalOpen = false">Fechar</DsButton>
      </template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
import type { PadroesClientePainel } from '~/composables/useVariaveisApi'

definePageMeta({ layout: 'default' })

const auth = useAuthStore()
const variaveisApi = useVariaveisApi()
const paineisApi = usePaineisApi()
const swal = useSwal()

const clientes = ref<{ codCliente: number; nome: string }[]>([])
const codClienteSel = ref('')
const selectedPadraoId = ref<number | null>(null)
const variavelBusca = ref('')
const importReferencia = ref('')
const loading = ref(false)
const errorMsg = ref('')
const painel = ref<PadroesClientePainel | null>(null)

const faixasModalOpen = ref(false)
const modalVariavelNome = ref('')
const modalCodVariavel = ref<number | null>(null)
const modalComentario = ref('')
const modalFaixas = ref<
  Array<{
    codFaixa: number
    sexo?: string | null
    valorMin?: number | null
    valorMax?: number | null
    idadeMin?: number | null
    idadeMax?: number | null
    pagina?: number | null
    classificacao?: string | null
  }>
>([])

async function loadClientes() {
  try {
    const res = await paineisApi.listClientes()
    clientes.value = (res.data || []).map((c) => ({
      codCliente: c.codCliente ?? (c as { CodCliente?: number }).CodCliente ?? 0,
      nome: (c.nome ?? (c as { Nome?: string }).Nome ?? '').trim()
    })).filter(c => c.codCliente > 0)
  } catch {
    clientes.value = []
  }
}

async function load() {
  if (!codClienteSel.value) return
  loading.value = true
  errorMsg.value = ''
  try {
    const res = await variaveisApi.getPadroesCliente({
      codCliente: Number(codClienteSel.value),
      codPadrao: selectedPadraoId.value ?? undefined,
      variavelBusca: variavelBusca.value || undefined
    })
    painel.value = res.data
    if (!selectedPadraoId.value && painel.value?.padraoSelecionado?.codPadrao) {
      selectedPadraoId.value = painel.value.padraoSelecionado.codPadrao
    }
  } catch (err) {
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar padrões.'
  } finally {
    loading.value = false
  }
}

function onClienteChange() {
  selectedPadraoId.value = null
  painel.value = null
  load()
}

function selectPadrao(codPadrao: number) {
  selectedPadraoId.value = codPadrao
  load()
}

async function abrirNovoPadrao() {
  if (!import.meta.client) return
  const { default: Swal } = await import('sweetalert2')
  const result = await Swal.fire({
    title: 'Novo padrão',
    html: `
      <div class="text-start space-y-2">
        <label class="block text-sm">Nome<input id="swal-nome" class="swal2-input w-full" placeholder="Eco adulto — ASE 2015" /></label>
        <label class="block text-sm">Código API<input id="swal-codigo" class="swal2-input w-full" placeholder="ECO_ADULTO" /></label>
        <label class="block text-sm"><input id="swal-vigente" type="checkbox" /> Definir como vigente</label>
      </div>`,
    showCancelButton: true,
    confirmButtonText: 'Criar',
    preConfirm: () => {
      const nome = (document.getElementById('swal-nome') as HTMLInputElement)?.value?.trim()
      const codigo = (document.getElementById('swal-codigo') as HTMLInputElement)?.value?.trim()
      const vigente = (document.getElementById('swal-vigente') as HTMLInputElement)?.checked
      if (!nome || !codigo) {
        Swal.showValidationMessage('Nome e código são obrigatórios.')
        return false
      }
      return { nome, codigo, vigente }
    }
  })
  if (!result.isConfirmed || !result.value) return
  try {
    const created = await variaveisApi.createPadraoCliente({
      codCliente: Number(codClienteSel.value),
      nome: result.value.nome,
      codigo: result.value.codigo,
      padraoVigente: result.value.vigente
    })
    selectedPadraoId.value = created.codPadrao
    await swal.toast('Padrão criado.')
    await load()
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao criar padrão.', 'error')
  }
}

async function importarReferencia() {
  if (!selectedPadraoId.value || !importReferencia.value) {
    await swal.toast('Selecione uma referência para importar.', 'warning')
    return
  }
  const confirm = await swal.confirm(
    'Importar snapshot',
    'Isso substituirá todas as faixas e comentários deste padrão pelos dados da referência.'
  )
  if (!confirm?.isConfirmed) return
  try {
    const res = await variaveisApi.importarReferenciaPadrao(selectedPadraoId.value, Number(importReferencia.value))
    await swal.toast(res.data?.message || 'Snapshot importado.')
    await load()
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao importar.', 'error')
  }
}

async function definirVigente() {
  if (!selectedPadraoId.value) return
  try {
    await variaveisApi.setPadraoVigente(selectedPadraoId.value)
    await swal.toast('Padrão definido como vigente.')
    await load()
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao definir vigente.', 'error')
  }
}

async function excluirPadrao() {
  if (!selectedPadraoId.value) return
  const confirm = await swal.confirm('Excluir padrão', 'Todas as faixas e comentários serão removidos.')
  if (!confirm?.isConfirmed) return
  try {
    await variaveisApi.deletePadraoCliente(selectedPadraoId.value)
    selectedPadraoId.value = null
    await swal.toast('Padrão excluído.')
    await load()
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao excluir.', 'error')
  }
}

function abrirFaixas(v: { codVariavel: number; nomeVariavel: string; comentarioTexto?: string | null }) {
  const regs = painel.value?.faixasPorVariavel?.[String(v.codVariavel)] || []
  modalFaixas.value = regs.map(n => ({ ...n }))
  modalVariavelNome.value = v.nomeVariavel
  modalCodVariavel.value = v.codVariavel
  modalComentario.value = v.comentarioTexto || ''
  faixasModalOpen.value = true
}

async function salvarFaixa(n: {
  codFaixa: number
  valorMin?: number | null
  valorMax?: number | null
  idadeMin?: number | null
  idadeMax?: number | null
  pagina?: number | null
}) {
  try {
    await variaveisApi.atualizarFaixaPadrao(n.codFaixa, {
      valorMin: n.valorMin ?? null,
      valorMax: n.valorMax ?? null,
      idadeMin: n.idadeMin ?? null,
      idadeMax: n.idadeMax ?? null,
      pagina: n.pagina ?? null
    })
    await swal.toast('Faixa atualizada.')
    await load()
    if (modalCodVariavel.value) {
      const v = painel.value?.variaveis?.find(x => x.codVariavel === modalCodVariavel.value)
      if (v) abrirFaixas(v)
    }
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao salvar faixa.', 'error')
  }
}

async function salvarComentarioModal() {
  if (!selectedPadraoId.value || !modalCodVariavel.value) return
  try {
    await variaveisApi.salvarComentarioPadrao(selectedPadraoId.value, {
      codVariavel: modalCodVariavel.value,
      texto: modalComentario.value
    })
    await swal.toast('Comentário salvo.')
    await load()
    if (modalCodVariavel.value) {
      const v = painel.value?.variaveis?.find(x => x.codVariavel === modalCodVariavel.value)
      if (v) abrirFaixas({ ...v, comentarioTexto: modalComentario.value })
    }
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao salvar comentário.', 'error')
  }
}

onMounted(async () => {
  await loadClientes()
})

watch(codClienteSel, () => {
  onClienteChange()
})
</script>
