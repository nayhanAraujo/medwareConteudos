<template>
  <div>
    <DsPageHeader
      :title="relatorio ? `Configurações — ${relatorio.nome}` : 'Configurações do relatório'"
      subtitle="Validações, filtros e índice de colunas"
      icon="sliders"
    >
      <template #actions><DsButton variant="secondary" icon="arrow-left" to="/relatorios?view=lista">Voltar</DsButton></template>
    </DsPageHeader>

    <DsPageShell>
      <DsAlert v-if="error" variant="error" class="mb-4">{{ error }}</DsAlert>
      <div class="mb-5 flex flex-wrap gap-2">
        <DsButton size="sm" :variant="tab === 'validacoes' ? 'primary' : 'secondary'" @click="tab = 'validacoes'">Validações</DsButton>
        <DsButton size="sm" :variant="tab === 'filtros' ? 'primary' : 'secondary'" @click="tab = 'filtros'">Filtros</DsButton>
        <DsButton size="sm" :variant="tab === 'colunas' ? 'primary' : 'secondary'" @click="tab = 'colunas'">Colunas</DsButton>
      </div>

      <div v-if="loading" class="py-10 text-center text-gray-500">Carregando...</div>

      <section v-else-if="tab === 'validacoes'" class="space-y-4">
        <div class="flex justify-between gap-3">
          <div><h2 class="text-lg font-semibold">Histórico de validações</h2><p class="text-sm text-gray-500">{{ validacoes.length }} registro(s)</p></div>
          <DsButton v-if="auth.isAdmin && validacoesDisponiveis" variant="success" icon="shield-check" @click="validacaoModal = true">Nova validação</DsButton>
        </div>
        <DsAlert v-if="!validacoesDisponiveis" variant="warning">A tabela RELATORIO_VALIDACOES ainda não está disponível.</DsAlert>
        <DsTable v-else>
          <template #head><tr><th>Status</th><th>Método</th><th>Validador</th><th>Data</th><th>Próxima</th><th>Observações</th></tr></template>
          <tr v-for="item in validacoes" :key="item.codvalidacao">
            <td>{{ statusLabel(item.status_validacao) }}</td><td>{{ item.metodo_validacao }}</td><td>{{ item.validador || 'N/A' }}</td>
            <td>{{ formatDate(item.dthrvalidacao) }}</td><td>{{ formatDate(item.dthrproxima_validacao) }}</td><td>{{ item.observacoes || '—' }}</td>
          </tr>
          <tr v-if="!validacoes.length"><td colspan="6" class="text-center text-gray-500">Nenhuma validação cadastrada.</td></tr>
        </DsTable>
      </section>

      <section v-else-if="tab === 'filtros'" class="space-y-4">
        <div class="flex justify-between gap-3">
          <div><h2 class="text-lg font-semibold">Filtros do sistema</h2><p class="text-sm text-gray-500">Catálogo compartilhado pelos relatórios</p></div>
          <DsButton v-if="auth.isAdmin && filtrosDisponiveis" variant="success" icon="plus" @click="openFiltro()">Novo filtro</DsButton>
        </div>
        <DsAlert v-if="!filtrosDisponiveis" variant="warning">A tabela RELATORIO_FILTROS ainda não está disponível.</DsAlert>
        <DsTable v-else>
          <template #head><tr><th>Nome</th><th>Tipo</th><th>Descrição</th><th>Status</th><th v-if="auth.isAdmin" /></tr></template>
          <tr v-for="item in filtros" :key="item.codfiltro">
            <td>{{ item.nome }}</td><td>{{ item.tipo }}</td><td>{{ item.descricao }}</td><td>{{ item.ativo === 'S' ? 'Ativo' : 'Inativo' }}</td>
            <td v-if="auth.isAdmin"><div class="flex justify-end gap-2"><DsButton size="sm" variant="secondary" @click="openFiltro(item)">Editar</DsButton><DsButton size="sm" variant="danger" @click="removeFiltro(item)">Excluir</DsButton></div></td>
          </tr>
          <tr v-if="!filtros.length"><td :colspan="auth.isAdmin ? 5 : 4" class="text-center text-gray-500">Nenhum filtro cadastrado.</td></tr>
        </DsTable>
      </section>

      <section v-else class="space-y-5">
        <div class="grid gap-3 md:grid-cols-4">
          <div class="rounded-2xl border p-4"><p class="text-sm text-gray-500">Relatórios XML</p><strong class="text-2xl">{{ statusColunas.totalRelatoriosXml ?? 0 }}</strong></div>
          <div class="rounded-2xl border p-4"><p class="text-sm text-gray-500">Com colunas</p><strong class="text-2xl">{{ statusColunas.relatoriosComColunas ?? 0 }}</strong></div>
          <div class="rounded-2xl border p-4"><p class="text-sm text-gray-500">Colunas indexadas</p><strong class="text-2xl">{{ statusColunas.totalColunasIndexadas ?? 0 }}</strong></div>
          <div class="rounded-2xl border p-4"><p class="text-sm text-gray-500">Sem colunas</p><strong class="text-2xl">{{ statusColunas.relatoriosSemColunas ?? 0 }}</strong></div>
        </div>
        <DsAlert v-if="statusColunas.tabelaExiste === false" variant="warning">{{ statusColunas.message }}</DsAlert>
        <div class="flex flex-col gap-3 md:flex-row md:items-end">
          <div class="flex-1"><DsInput v-model="buscaColunas" label="Pesquisar relatórios por colunas" placeholder="Ex.: paciente, data, resultado" @enter="buscarColunas" /></div>
          <DsButton variant="secondary" icon="search" :loading="buscando" @click="buscarColunas">Pesquisar</DsButton>
          <DsButton v-if="auth.isAdmin && statusColunas.tabelaExiste" icon="arrow-repeat" :loading="reindexando" @click="reindexar">Reindexar todos</DsButton>
        </div>
        <div v-if="resultadoBusca !== null">
          <h2 class="mb-2 text-lg font-semibold">Resultado da pesquisa ({{ resultadoBusca.length }})</h2>
          <DsTable><template #head><tr><th>Relatório</th><th>Módulo</th><th>Formato</th><th>Colunas encontradas</th></tr></template>
            <tr v-for="item in resultadoBusca" :key="item.codRelatorio"><td>{{ item.nome }}</td><td>{{ item.modulo || '—' }}</td><td>{{ item.formato }}</td><td>{{ item.colunasEncontradas.map((c: any) => c.nome).join(', ') }}</td></tr>
            <tr v-if="!resultadoBusca.length"><td colspan="4" class="text-center text-gray-500">Nenhum relatório contém todas as colunas informadas.</td></tr>
          </DsTable>
        </div>
        <div>
          <h2 class="mb-2 text-lg font-semibold">Colunas deste relatório ({{ colunas.length }})</h2>
          <DsTable><template #head><tr><th>Posição</th><th>Nome</th><th>Tipo</th><th>Indexada em</th></tr></template>
            <tr v-for="item in colunas" :key="`${item.posicao_coluna}-${item.nome_coluna}`"><td>{{ item.posicao_coluna }}</td><td>{{ item.nome_coluna }}</td><td>{{ item.tipo_coluna || 'TEXTO' }}</td><td>{{ formatDate(item.dthrcriacao) }}</td></tr>
            <tr v-if="!colunas.length"><td colspan="4" class="text-center text-gray-500">Nenhuma coluna indexada.</td></tr>
          </DsTable>
        </div>
      </section>
    </DsPageShell>

    <DsModal v-model="validacaoModal" title="Nova validação" size="lg">
      <div class="space-y-3"><DsSelect v-model="validacaoForm.statusValidacao" label="Status"><option value="A">Aprovado</option><option value="R">Rejeitado</option><option value="P">Pendente</option></DsSelect><DsInput v-model="validacaoForm.metodoValidacao" label="Método" /><DsTextarea v-model="validacaoForm.criteriosValidacao" label="Critérios" /><DsTextarea v-model="validacaoForm.observacoes" label="Observações" /><DsInput v-model="validacaoForm.dthrProximaValidacao" type="datetime-local" label="Próxima validação" /></div>
      <template #footer><DsButton variant="secondary" @click="validacaoModal = false">Cancelar</DsButton><DsButton :loading="saving" @click="saveValidacao">Salvar</DsButton></template>
    </DsModal>

    <DsModal v-model="filtroModal" :title="filtroForm.codfiltro ? 'Editar filtro' : 'Novo filtro'" size="xl">
      <div class="space-y-3"><DsInput v-model="filtroForm.nome" label="Nome" /><DsTextarea v-model="filtroForm.descricao" label="Descrição" /><div class="grid gap-3 md:grid-cols-2"><DsSelect v-model="filtroForm.tipo" label="Tipo"><option value="COMUM">Comum</option><option value="MULTISELECAO">Multiseleção</option><option value="PARAMETRO">Parâmetro</option></DsSelect><DsSelect v-model="filtroForm.ativo" label="Status"><option value="S">Ativo</option><option value="N">Inativo</option></DsSelect></div><DsTextarea v-if="['COMUM', 'MULTISELECAO'].includes(filtroForm.tipo)" v-model="filtroForm.sql_filtro" label="SQL do filtro" /><DsTextarea v-model="filtroForm.sql_query" label="SQL da consulta" /></div>
      <template #footer><DsButton variant="secondary" @click="filtroModal = false">Cancelar</DsButton><DsButton :loading="saving" @click="saveFiltro">Salvar</DsButton></template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })
const route = useRoute(); const api = useRelatoriosApi(); const auth = useAuthStore(); const swal = useSwal()
const id = computed(() => Number(route.params.id)); const tab = ref<'validacoes' | 'filtros' | 'colunas'>('validacoes')
const loading = ref(true); const saving = ref(false); const buscando = ref(false); const reindexando = ref(false); const error = ref('')
const relatorio = ref<any>(null); const validacoes = ref<any[]>([]); const filtros = ref<any[]>([]); const colunas = ref<any[]>([]); const statusColunas = ref<any>({})
const validacoesDisponiveis = ref(true); const filtrosDisponiveis = ref(true); const buscaColunas = ref(''); const resultadoBusca = ref<any[] | null>(null)
const validacaoModal = ref(false); const filtroModal = ref(false)
const validacaoForm = reactive<any>({ statusValidacao: 'P', metodoValidacao: '', criteriosValidacao: '', observacoes: '', dthrProximaValidacao: '' })
const filtroForm = reactive<any>({ codfiltro: null, nome: '', descricao: '', tipo: 'COMUM', sql_filtro: '', sql_query: '', ativo: 'S' })

async function load() { loading.value = true; error.value = ''; try { const [detail, vals, fils, cols, status] = await Promise.all([api.getRelatorio(id.value), api.listValidacoes(id.value), api.listFiltros(), api.listColunas(id.value), api.getStatusColunas()]); relatorio.value = detail.data; validacoes.value = vals.data.data || []; validacoesDisponiveis.value = vals.data.tabelaExiste; filtros.value = fils.data.data || []; filtrosDisponiveis.value = fils.data.tabelaExiste; colunas.value = cols.data.data || []; statusColunas.value = status.data || {} } catch (e) { error.value = e instanceof Error ? e.message : 'Erro ao carregar configurações.' } finally { loading.value = false } }
function statusLabel(value: string) { return ({ A: 'Aprovado', R: 'Rejeitado', P: 'Pendente' } as Record<string, string>)[value] || value }
function formatDate(value?: string | null) { if (!value) return '—'; const d = new Date(value); return Number.isNaN(d.getTime()) ? value : d.toLocaleString('pt-BR') }
async function saveValidacao() { if (!validacaoForm.metodoValidacao.trim()) return swal.toast('Informe o método de validação.', 'error'); saving.value = true; try { await api.createValidacao(id.value, { ...validacaoForm, dthrProximaValidacao: validacaoForm.dthrProximaValidacao || null }); validacaoModal.value = false; Object.assign(validacaoForm, { statusValidacao: 'P', metodoValidacao: '', criteriosValidacao: '', observacoes: '', dthrProximaValidacao: '' }); await load(); await swal.toast('Validação cadastrada.') } catch (e) { await swal.toast(e instanceof Error ? e.message : 'Erro ao cadastrar validação.', 'error') } finally { saving.value = false } }
function openFiltro(item?: any) { Object.assign(filtroForm, item || { codfiltro: null, nome: '', descricao: '', tipo: 'COMUM', sql_filtro: '', sql_query: '', ativo: 'S' }); filtroModal.value = true }
async function saveFiltro() { saving.value = true; try { const body = { nome: filtroForm.nome.trim(), descricao: filtroForm.descricao.trim(), tipo: filtroForm.tipo, sqlFiltro: filtroForm.sql_filtro || null, sqlQuery: filtroForm.sql_query.trim(), ativo: filtroForm.ativo }; filtroForm.codfiltro ? await api.updateFiltro(filtroForm.codfiltro, body as any) : await api.createFiltro(body as any); filtroModal.value = false; await load(); await swal.toast('Filtro salvo.') } catch (e) { await swal.toast(e instanceof Error ? e.message : 'Erro ao salvar filtro.', 'error') } finally { saving.value = false } }
async function removeFiltro(item: any) { const answer = await swal.confirm('Excluir filtro', `Excluir “${item.nome}”?`); if (!answer?.isConfirmed) return; try { await api.deleteFiltro(item.codfiltro); await load(); await swal.toast('Filtro excluído.') } catch (e) { await swal.toast(e instanceof Error ? e.message : 'Erro ao excluir filtro.', 'error') } }
async function buscarColunas() { if (!buscaColunas.value.trim()) return swal.toast('Informe ao menos uma coluna.', 'error'); buscando.value = true; try { const response = await api.searchColunas(buscaColunas.value); resultadoBusca.value = response.data.data || [] } catch (e) { await swal.toast(e instanceof Error ? e.message : 'Erro na pesquisa.', 'error') } finally { buscando.value = false } }
async function reindexar() { const answer = await swal.confirm('Reindexar colunas', 'Reprocessar todos os relatórios XML ativos?'); if (!answer?.isConfirmed) return; reindexando.value = true; try { const response = await api.reindexarColunas(); await load(); await swal.toast(response.data.message || 'Reindexação concluída.') } catch (e) { await swal.toast(e instanceof Error ? e.message : 'Erro na reindexação.', 'error') } finally { reindexando.value = false } }
onMounted(load)
</script>
