<template>
  <div>
    <DsPageHeader :title="titulo" icon="arrow-left-right" />
    <DsPageShell>
      <div class="flex flex-wrap justify-between gap-2 mb-5">
        <DsButton variant="secondary" size="sm" icon="arrow-left" :to="`/scripts/${scriptId}/versoes`">
          Voltar para versões
        </DsButton>
      </div>

      <DsAlert v-if="erro" variant="error" class="mb-4">{{ erro }}</DsAlert>

      <section class="rounded-xl border border-gray-200 bg-white p-4 mb-6">
        <div class="grid grid-cols-1 md:grid-cols-[1fr_auto_1fr_auto] gap-3 items-end">
          <DsSelect v-model="versao1Id" label="Versão de origem" :disabled="carregando">
            <option value="">Selecione...</option>
            <option v-for="versao in versoes" :key="versao.codVersao" :value="String(versao.codVersao)">
              {{ rotuloVersao(versao) }}
            </option>
          </DsSelect>
          <i class="bi bi-arrow-left-right text-xl text-gray-400 pb-2 hidden md:block" />
          <DsSelect v-model="versao2Id" label="Versão de destino" :disabled="carregando">
            <option value="">Selecione...</option>
            <option v-for="versao in versoes" :key="versao.codVersao" :value="String(versao.codVersao)">
              {{ rotuloVersao(versao) }}
            </option>
          </DsSelect>
          <DsButton :disabled="!podeComparar || carregando" icon="arrow-left-right" @click="comparar">
            {{ carregando ? 'Carregando...' : 'Comparar' }}
          </DsButton>
        </div>
        <p v-if="versoes.length < 2 && !carregando" class="text-sm text-amber-700 mt-3">
          O script precisa possuir ao menos duas versões para ser comparado.
        </p>
      </section>

      <template v-if="resultado">
        <div class="grid grid-cols-1 md:grid-cols-3 gap-3 mb-6">
          <div class="summary-card">
            <span class="summary-label">Intervalo</span>
            <strong>{{ resultado.diasEntreVersoes == null ? 'Não calculável' : `${resultado.diasEntreVersoes} dia(s)` }}</strong>
          </div>
          <div class="summary-card">
            <span class="summary-label">Responsáveis</span>
            <strong>{{ resultado.mesmoResponsavel ? 'Mesmo responsável' : 'Responsáveis diferentes' }}</strong>
          </div>
          <div class="summary-card">
            <span class="summary-label">Aprovação</span>
            <strong>{{ resumoAprovacao }}</strong>
          </div>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-2 gap-4 mb-6">
          <article v-for="(versao, indice) in [resultado.versao1, resultado.versao2]" :key="versao.codVersao" class="version-card">
            <div class="flex flex-wrap items-center justify-between gap-2 mb-3">
              <h2 class="text-lg font-semibold text-ds-text">{{ versao.numeroVersao }}</h2>
              <div class="flex gap-1">
                <DsBadge :variant="versao.ativo ? 'success' : 'default'">{{ versao.ativo ? 'Ativa' : 'Inativa' }}</DsBadge>
                <DsBadge :variant="versao.aprovado ? 'primary' : 'warning'">{{ versao.aprovado ? 'Aprovada' : 'Pendente' }}</DsBadge>
              </div>
            </div>
            <dl class="grid grid-cols-2 gap-3 text-sm">
              <div><dt class="meta-label">Data</dt><dd>{{ formatarData(versao.dataCriacao) }}</dd></div>
              <div><dt class="meta-label">Responsável</dt><dd>{{ versao.usuarioResponsavel || '—' }}</dd></div>
              <div class="col-span-2"><dt class="meta-label">Aprovada por</dt><dd>{{ versao.aprovadoPor || '—' }}</dd></div>
            </dl>
            <DsButton variant="ghost" size="sm" class="mt-3" :to="`/scripts/${scriptId}/versoes/${versao.codVersao}`">
              Detalhes da versão {{ indice + 1 }}
            </DsButton>
          </article>
        </div>

        <section v-for="campo in camposDiff" :key="campo.titulo" class="diff-section">
          <div class="diff-title">
            <h3>{{ campo.titulo }}</h3>
            <DsBadge :variant="campo.igual ? 'success' : 'warning'">{{ campo.igual ? 'Sem alterações' : 'Alterado' }}</DsBadge>
          </div>
          <div class="diff-head">
            <span>{{ resultado.versao1.numeroVersao }}</span>
            <span>{{ resultado.versao2.numeroVersao }}</span>
          </div>
          <div class="diff-body">
            <div v-for="(linha, index) in campo.linhas" :key="index" class="diff-row">
              <pre :class="['diff-line', linha.tipo === 'removida' && 'diff-removed', linha.tipo === 'igual' && 'diff-equal']">{{ linha.esquerda ?? '' }}</pre>
              <pre :class="['diff-line', linha.tipo === 'adicionada' && 'diff-added', linha.tipo === 'igual' && 'diff-equal']">{{ linha.direita ?? '' }}</pre>
            </div>
          </div>
        </section>
      </template>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type {
  ScriptVersaoComparacaoItem,
  ScriptsComparacaoResultado
} from '~/composables/useScriptsComparacaoApi'

definePageMeta({ layout: 'default' })

type LinhaDiff = { esquerda: string | null; direita: string | null; tipo: 'igual' | 'removida' | 'adicionada' }

const route = useRoute()
const api = useScriptsComparacaoApi()
const scriptId = Number(route.params.id)
const titulo = ref('Comparar versões')
const versoes = ref<ScriptVersaoComparacaoItem[]>([])
const versao1Id = ref('')
const versao2Id = ref('')
const resultado = ref<ScriptsComparacaoResultado | null>(null)
const carregando = ref(false)
const erro = ref('')

const podeComparar = computed(() =>
  Number(versao1Id.value) > 0 && Number(versao2Id.value) > 0 && versao1Id.value !== versao2Id.value
)

const resumoAprovacao = computed(() => {
  if (!resultado.value) return ''
  if (!resultado.value.mesmoStatusAprovacao) return 'Status diferentes'
  return resultado.value.versao1.aprovado ? 'Ambas aprovadas' : 'Ambas pendentes'
})

const camposDiff = computed(() => {
  if (!resultado.value) return []
  const pares = [
    ['Descrição geral', resultado.value.versao1.descricaoAlteracoes, resultado.value.versao2.descricaoAlteracoes],
    ['Alterações na interface', resultado.value.versao1.alteracoesInterface, resultado.value.versao2.alteracoesInterface],
    ['Alterações no código', resultado.value.versao1.alteracoesCodigo, resultado.value.versao2.alteracoesCodigo]
  ] as const
  return pares.map(([tituloCampo, esquerda, direita]) => ({
    titulo: tituloCampo,
    igual: normalizar(esquerda) === normalizar(direita),
    linhas: gerarDiff(esquerda, direita)
  }))
})

function normalizar(texto?: string) {
  return (texto || '').replace(/\r\n/g, '\n').trim()
}

function linhas(texto?: string) {
  const valor = normalizar(texto)
  return valor ? valor.split('\n') : ['(não informado)']
}

function gerarDiff(texto1?: string, texto2?: string): LinhaDiff[] {
  const a = linhas(texto1)
  const b = linhas(texto2)
  const tabela = Array.from({ length: a.length + 1 }, () => Array<number>(b.length + 1).fill(0))
  for (let i = a.length - 1; i >= 0; i--) {
    for (let j = b.length - 1; j >= 0; j--) {
      tabela[i]![j] = a[i] === b[j]
        ? tabela[i + 1]![j + 1]! + 1
        : Math.max(tabela[i + 1]![j]!, tabela[i]![j + 1]!)
    }
  }

  const resultadoDiff: LinhaDiff[] = []
  let i = 0
  let j = 0
  while (i < a.length || j < b.length) {
    if (i < a.length && j < b.length && a[i] === b[j]) {
      resultadoDiff.push({ esquerda: a[i]!, direita: b[j]!, tipo: 'igual' })
      i++
      j++
    } else if (j < b.length && (i === a.length || tabela[i]![j + 1]! >= tabela[i + 1]![j]!)) {
      resultadoDiff.push({ esquerda: null, direita: b[j]!, tipo: 'adicionada' })
      j++
    } else {
      resultadoDiff.push({ esquerda: a[i]!, direita: null, tipo: 'removida' })
      i++
    }
  }
  return resultadoDiff
}

function rotuloVersao(versao: ScriptVersaoComparacaoItem) {
  return `${versao.numeroVersao} — ${formatarData(versao.dataCriacao)}`
}

function formatarData(valor?: string) {
  if (!valor) return 'Data não disponível'
  const data = new Date(valor)
  return Number.isNaN(data.getTime()) ? 'Data não disponível' : data.toLocaleString('pt-BR')
}

async function carregar() {
  if (!Number.isInteger(scriptId) || scriptId <= 0) {
    erro.value = 'Identificador do script inválido.'
    return
  }
  carregando.value = true
  erro.value = ''
  try {
    const response = await api.listar(scriptId)
    titulo.value = `Comparar versões — ${response.data.script.nome}`
    versoes.value = response.data.versoes
    if (versoes.value.length >= 2) {
      versao1Id.value = String(versoes.value[1]!.codVersao)
      versao2Id.value = String(versoes.value[0]!.codVersao)
    }
  } catch (e: unknown) {
    erro.value = e instanceof Error ? e.message : 'Não foi possível carregar as versões.'
  } finally {
    carregando.value = false
  }
}

async function comparar() {
  if (!podeComparar.value) return
  carregando.value = true
  erro.value = ''
  try {
    const response = await api.comparar(scriptId, Number(versao1Id.value), Number(versao2Id.value))
    resultado.value = response.data
  } catch (e: unknown) {
    resultado.value = null
    erro.value = e instanceof Error ? e.message : 'Não foi possível comparar as versões.'
  } finally {
    carregando.value = false
  }
}

onMounted(carregar)
</script>

<style scoped>
.summary-card, .version-card, .diff-section {
  padding: 1rem;
  border: 1px solid #e5e7eb;
  border-radius: .75rem;
  background: #fff;
}
.summary-card { display: flex; flex-direction: column; gap: .25rem; }
.summary-label, .meta-label {
  color: #6b7280;
  font-size: .75rem;
  font-weight: 500;
  letter-spacing: .025em;
  text-transform: uppercase;
}
.diff-section { padding: 0; margin-bottom: 1rem; overflow: hidden; }
.diff-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: .75rem;
  padding: .75rem 1rem;
  border-bottom: 1px solid #e5e7eb;
}
.diff-title h3 { color: var(--color-ds-text, #111827); font-weight: 600; }
.diff-head, .diff-row { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); }
.diff-head { color: #f3f4f6; background: #1f2937; font-size: .75rem; font-weight: 600; }
.diff-head span { padding: .5rem 1rem; }
.diff-head span + span, .diff-line + .diff-line { border-left: 1px solid #d1d5db; }
.diff-body { overflow-x: auto; }
.diff-line {
  min-height: 2.25rem;
  padding: .5rem 1rem;
  margin: 0;
  color: #374151;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: .75rem;
  overflow-wrap: anywhere;
  white-space: pre-wrap;
}
.diff-row + .diff-row { border-top: 1px solid #f3f4f6; }
.diff-equal { background: #fff; }
.diff-removed { color: #991b1b; background: #fef2f2; }
.diff-added { color: #166534; background: #f0fdf4; }
</style>
