<template>
  <div>
    <DsPageHeader title="Complementos da variável" subtitle="Grupo, códigos universais, especialidades, anexos e estudos" icon="diagram-3" />
    <DsPageShell>
      <DsAlert v-if="error" variant="error">{{ error }}</DsAlert>
      <div v-else class="space-y-8">
        <section id="normalidades">
          <DsSectionTitle title="Normalidades" />
          <DsAlert v-if="!detalhes.normalidades.length" variant="info">Nenhuma normalidade cadastrada.</DsAlert>
          <DsTable v-else>
            <template #head><tr><th>Sexo</th><th>Faixa</th><th>Idade</th><th>Referência</th></tr></template>
            <tr v-for="n in detalhes.normalidades" :key="n.codNormalidade">
              <td>{{ n.sexo || '—' }}</td><td>{{ n.valorMin ?? '—' }} a {{ n.valorMax ?? '—' }}</td>
              <td>{{ n.idadeMin ?? '—' }} a {{ n.idadeMax ?? '—' }}</td>
              <td>{{ n.referencia?.titulo || '—' }}</td>
            </tr>
          </DsTable>
        </section>

        <section>
          <DsSectionTitle title="Grupo" />
          <div class="flex gap-2 items-end">
            <DsSelect v-model="grupo" label="Grupo da variável" class="flex-1"><option value="">Sem grupo</option><option v-for="g in grupos" :key="g.codGrupo" :value="String(g.codGrupo)">{{ g.nome }}</option></DsSelect>
            <DsButton v-if="auth.isAdmin" :loading="saving" @click="salvarGrupo">Salvar</DsButton>
          </div>
        </section>

        <section id="especialidades">
          <DsSectionTitle title="Classificações vinculadas" />
          <div class="max-h-60 overflow-y-auto border rounded-xl p-3 grid md:grid-cols-2 gap-2">
            <label v-for="c in classificacoesCatalogo" :key="c.codClassificacao" class="flex gap-2 text-sm">
              <input v-model="classificacoesSelecionadas" type="checkbox" :value="c.codClassificacao" />
              <span>{{ c.nome }} <small class="text-gray-500">({{ classificacaoGrupo(c.codGrupo) }})</small></span>
            </label>
          </div>
          <DsButton v-if="auth.isAdmin" class="mt-2" :loading="saving" @click="salvarClassificacoes">Salvar classificações</DsButton>
        </section>

        <section id="anexos">
          <DsSectionTitle title="Códigos universais (DICOM)" />
          <DsSearchInput v-model="codigoBusca" placeholder="Filtrar códigos" @enter="loadCodigos" />
          <div class="max-h-72 overflow-y-auto border rounded-xl p-3 grid md:grid-cols-2 gap-2">
            <label v-for="c in codigos" :key="c.codUniversal" class="flex gap-2 text-sm"><input v-model="codigosSelecionados" type="checkbox" :value="c.codUniversal" /><code>{{ c.codigo }}</code><span>{{ c.descricaoPtBr }}</span></label>
          </div>
          <DsButton v-if="auth.isAdmin" class="mt-2" :loading="saving" @click="salvarCodigos">Salvar vínculos</DsButton>
        </section>

        <section id="estudos">
          <DsSectionTitle title="Especialidades" />
          <div class="space-y-2 mb-3">
            <div v-for="e in especialidades.vinculadas" :key="e.codEspecialidade" class="flex justify-between border rounded-xl p-3">
              <span><strong>{{ e.nome }}</strong><small class="block text-gray-500">{{ e.descricao }}</small></span>
              <DsButton v-if="auth.isAdmin" variant="danger" size="sm" @click="removerEspecialidade(e.codEspecialidade)">Remover</DsButton>
            </div>
          </div>
          <div v-if="auth.isAdmin" class="grid md:grid-cols-3 gap-2 items-end">
            <DsSelect v-model="especialidadeNova" label="Nova especialidade"><option value="">Selecione</option><option v-for="e in especialidades.disponiveis" :key="e.codEspecialidade" :value="String(e.codEspecialidade)">{{ e.nome }}</option></DsSelect>
            <DsInput v-model="especialidadeDescricao" label="Descrição" />
            <DsButton :loading="saving" @click="salvarEspecialidade">Vincular</DsButton>
          </div>
        </section>

        <section>
          <DsSectionTitle title="Anexos" />
          <div v-for="a in anexos.anexos" :key="a.codAnexo" class="flex justify-between border-b py-2 text-sm">
            <a :href="a.link || a.caminho" target="_blank" rel="noopener">{{ a.nome || a.descricao || a.tipoAnexo }}</a>
            <DsButton v-if="auth.isAdmin" variant="danger" size="sm" @click="removerAnexo(a.codAnexo)">Excluir</DsButton>
          </div>
          <div v-if="auth.isAdmin" class="grid md:grid-cols-2 gap-3 mt-4">
            <DsSelect v-model="anexo.tipoAnexo" label="Tipo"><option value="URL">URL</option><option value="ARQUIVO">Arquivo</option></DsSelect>
            <DsInput v-model="anexo.nome" label="Nome" />
            <DsInput v-model="anexo.descricao" label="Descrição" />
            <DsInput v-if="anexo.tipoAnexo === 'URL'" v-model="anexo.link" label="URL" />
            <div v-else><label class="block text-sm font-medium mb-1">Arquivo</label><input type="file" @change="onFile" /></div>
            <DsSelect v-model="anexo.codFormula" label="Fórmula"><option value="">Nenhuma</option><option v-for="f in anexos.formulas" :key="f.codFormula" :value="String(f.codFormula)">{{ f.formula }}</option></DsSelect>
            <DsSelect v-model="anexo.codReferencia" label="Referência"><option value="">Nenhuma</option><option v-for="r in anexos.referencias" :key="r.codReferencia" :value="String(r.codReferencia)">{{ r.titulo }} {{ r.ano }}</option></DsSelect>
          </div>
          <DsButton v-if="auth.isAdmin" class="mt-3" :loading="saving" @click="salvarAnexo">Adicionar anexo</DsButton>
        </section>

        <section>
          <DsSectionTitle title="Estudos" />
          <pre class="bg-gray-50 rounded-xl p-3 text-xs overflow-auto">{{ JSON.stringify(estudos, null, 2) }}</pre>
        </section>
        <DsButton variant="secondary" to="/variaveis">Voltar</DsButton>
      </div>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { AnexoContexto, Classificacao, ClassificacaoGrupo, CodigoUniversal, Especialidade, GrupoVariavelDto, VariavelDetalhesCompletosDto } from '~/composables/useVariaveisApi'
definePageMeta({ layout: 'default' })
const route = useRoute(); const api = useVariaveisApi(); const auth = useAuthStore(); const swal = useSwal()
const id = Number(route.params.id); const saving = ref(false); const error = ref(''); const grupo = ref(''); const grupos = ref<GrupoVariavelDto[]>([])
const classificacoesCatalogo = ref<Classificacao[]>([]); const gruposClassificacao = ref<ClassificacaoGrupo[]>([]); const classificacoesSelecionadas = ref<number[]>([])
const codigoBusca = ref(''); const codigos = ref<CodigoUniversal[]>([]); const codigosSelecionados = ref<number[]>([])
const especialidades = ref<{ vinculadas: Especialidade[]; disponiveis: Especialidade[] }>({ vinculadas: [], disponiveis: [] }); const especialidadeNova = ref(''); const especialidadeDescricao = ref('')
const anexos = ref<AnexoContexto>({ anexos: [], formulas: [], referencias: [] }); const estudos = ref<Array<Record<string, unknown>>>([])
const detalhes = ref<VariavelDetalhesCompletosDto>({ normalidades: [], equacoes: [] })
const anexo = reactive({ tipoAnexo: 'URL', nome: '', descricao: '', link: '', codFormula: '', codReferencia: '', arquivo: null as File | null })
async function loadCodigos() { const r = await api.listCodigosUniversais(codigoBusca.value); codigos.value = r.data || [] }
async function load() {
  error.value = ''
  try {
    const settled = await Promise.allSettled([
      api.getVariavelEdicao(id),
      api.listGrupos(),
      api.getCodigosVinculados(id),
      api.getEspecialidades(id),
      api.getAnexos(id),
      api.getEstudos(id),
      api.getClassificacoes(),
      api.getVariavelClassificacoes(id),
      api.getDetalhesCompletos(id)
    ])
    const value = <T>(i: number) =>
      settled[i].status === 'fulfilled' ? (settled[i] as PromiseFulfilledResult<{ data: T }>).value.data : null
    const failures = settled
      .map((r, i) => (r.status === 'rejected' ? (r.reason instanceof Error ? r.reason.message : `Falha na carga #${i + 1}`) : null))
      .filter(Boolean) as string[]

    const edicao = value<{ codGrupo?: number | null }>(0)
    if (edicao) grupo.value = edicao.codGrupo ? String(edicao.codGrupo) : ''
    grupos.value = value<GrupoVariavelDto[]>(1) || []
    especialidades.value = value<{ vinculadas: Especialidade[]; disponiveis: Especialidade[] }>(3) || { vinculadas: [], disponiveis: [] }
    anexos.value = value<AnexoContexto>(4) || { anexos: [], formulas: [], referencias: [] }
    estudos.value = value<Array<Record<string, unknown>>>(5) || []
    const cats = value<{ grupos: ClassificacaoGrupo[]; classificacoes: Classificacao[] }>(6)
    classificacoesCatalogo.value = cats?.classificacoes || []
    gruposClassificacao.value = cats?.grupos || []
    classificacoesSelecionadas.value = (value<Classificacao[]>(7) || []).map((c) => c.codClassificacao)
    detalhes.value = value<VariavelDetalhesCompletosDto>(8) || { normalidades: [], equacoes: [] }

    await loadCodigos()
    const linked = value<Array<{ codigo: string }>>(2) || []
    const linkedCodes = new Set(linked.map((x) => x.codigo))
    codigosSelecionados.value = codigos.value.filter((x) => linkedCodes.has(x.codigo)).map((x) => x.codUniversal)

    if (failures.length && !edicao && !detalhes.value.normalidades.length) {
      error.value = failures[0]
    } else if (failures.length) {
      await swal.toast(`Alguns dados não carregaram: ${failures[0]}`, 'warning')
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Erro ao carregar complementos.'
  }
}
async function run(fn: () => Promise<unknown>, message: string) { saving.value = true; try { await fn(); await swal.toast(message); await load() } catch (e) { await swal.toast(e instanceof Error ? e.message : 'Erro na operação', 'error') } finally { saving.value = false } }
const salvarGrupo = () => run(() => api.alterarGrupo(id, grupo.value ? Number(grupo.value) : null), 'Grupo atualizado.')
const salvarClassificacoes = () => run(() => api.setVariavelClassificacoes(id, classificacoesSelecionadas.value), 'Classificações atualizadas.')
const classificacaoGrupo = (codGrupo: number) => gruposClassificacao.value.find(g => g.codGrupo === codGrupo)?.nome || codGrupo
const salvarCodigos = () => run(() => api.setCodigosUniversais(id, codigosSelecionados.value), 'Códigos atualizados.')
const salvarEspecialidade = () => especialidadeNova.value && run(() => api.setEspecialidade(id, { codEspecialidade: Number(especialidadeNova.value), descricao: especialidadeDescricao.value }), 'Especialidade vinculada.')
const removerEspecialidade = (code: number) => run(() => api.removeEspecialidade(id, code), 'Especialidade removida.')
function onFile(e: Event) { anexo.arquivo = (e.target as HTMLInputElement).files?.[0] || null }
const salvarAnexo = () => run(() => { const f = new FormData(); Object.entries(anexo).forEach(([k, v]) => { if (v !== '' && v !== null) f.append(k, v instanceof File ? v : String(v)) }); return api.createAnexo(id, f) }, 'Anexo adicionado.')
const removerAnexo = (code: number) => run(() => api.deleteAnexo(id, code), 'Anexo removido.')
onMounted(load)
</script>
