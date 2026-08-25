<template>
  <div>
    <DsPageHeader title="Fórmulas" subtitle="Fórmulas, variáveis vinculadas e equações por linguagem" icon="calculator">
      <template #actions>
        <DsButton variant="success" size="sm" icon="plus-lg" @click="openCreate">Nova fórmula</DsButton>
      </template>
    </DsPageHeader>
    <DsPageShell>
      <div class="grid grid-cols-1 md:grid-cols-4 gap-3 mb-5 items-end">
        <DsInput v-model="filters.sigla" label="Sigla" @enter="load(1)" />
        <DsInput v-model="filters.nome" label="Nome" @enter="load(1)" />
        <DsInput v-model="filters.formula" label="Fórmula" @enter="load(1)" />
        <div class="flex gap-2 justify-end">
          <DsButton variant="secondary" size="sm" icon="eraser" @click="clearFilters">Limpar</DsButton>
          <DsButton size="sm" icon="search" @click="load(1)">Filtrar</DsButton>
        </div>
      </div>

      <div v-if="loading" class="text-center py-10 text-gray-500">Carregando fórmulas...</div>
      <DsAlert v-else-if="errorMessage" variant="error">{{ errorMessage }}</DsAlert>
      <DsAlert v-else-if="!items.length" variant="info">
        Nenhuma fórmula encontrada. Use o botão "Nova fórmula" no topo da página para cadastrar a primeira.
      </DsAlert>
      <DsTable v-else>
        <template #head><tr><th>Nome</th><th>Variáveis</th><th>Fórmula</th><th>Validação</th><th>Casas</th><th class="text-right">Ações</th></tr></template>
        <tr v-for="item in items" :key="item.codFormula">
          <td><strong>{{ item.nome }}</strong><br><small class="text-gray-500">{{ item.descricao || '-' }}</small></td>
          <td>{{ item.variaveis }}<br><code class="text-xs">{{ item.siglas }}</code></td>
          <td><code class="text-xs whitespace-pre-wrap">{{ item.formula }}</code></td>
          <td>
            <span
              v-if="listItemValidation(item).pending"
              class="inline-flex items-center gap-1 text-gray-500"
              :title="listItemValidationTitle(item)"
            >
              <i class="bi bi-hourglass-split" />
              <span class="text-xs font-medium">Verificando</span>
            </span>
            <span
              v-else-if="listItemValidation(item).hasErrors"
              class="inline-flex items-center gap-1 text-orange-700"
              :title="listItemValidationTitle(item)"
            >
              <i class="bi bi-exclamation-triangle-fill" />
              <span class="text-xs font-medium">Pendências</span>
            </span>
            <span v-else class="inline-flex items-center gap-1 text-green-700" title="Todas as variáveis usadas na fórmula estão vinculadas.">
              <i class="bi bi-check-circle-fill" />
              <span class="text-xs font-medium">OK</span>
            </span>
          </td>
          <td>{{ item.casasDecimais }}</td>
          <td><div class="flex justify-end gap-2 whitespace-nowrap">
            <DsButton size="sm" variant="ghost" icon="eye" @click="openView(item.codFormula)">Detalhes</DsButton>
            <DsButton size="sm" variant="secondary" icon="pencil" @click="openEdit(item.codFormula)">Editar</DsButton>
            <DsButton size="sm" variant="danger" icon="trash" @click="remove(item)">Excluir</DsButton>
          </div></td>
        </tr>
      </DsTable>

      <div v-if="totalPages > 1" class="flex justify-center items-center gap-2 mt-6">
        <DsButton size="sm" variant="secondary" :disabled="page <= 1" @click="load(page - 1)">Anterior</DsButton>
        <span class="text-sm text-gray-500">Página {{ page }} de {{ totalPages }}</span>
        <DsButton size="sm" variant="secondary" :disabled="page >= totalPages" @click="load(page + 1)">Próxima</DsButton>
      </div>
    </DsPageShell>

    <DsModal v-model="editorOpen" :title="editingId ? 'Editar fórmula' : 'Nova fórmula'" size="xl">
      <form class="space-y-5" @submit.prevent="save">
        <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
          <DsInput v-model="form.nome" label="Nome" hint="Se vazio, usa a sigla da primeira variável." />
          <DsInput v-model="form.descricao" label="Descrição" />
          <DsInput v-model="form.casasDecimais" label="Casas decimais" type="number" required />
        </div>
        <DsTextarea v-model="form.formula" label="Fórmula" :rows="4" required />
        <DsAlert v-if="formulaValidation.hasErrors" variant="warning" title="Variáveis pendentes">
          <p v-if="formulaValidation.unselected.length" class="mb-1">
            Selecione em "Variáveis vinculadas": {{ formulaValidation.unselected.join(', ') }}.
          </p>
          <p v-if="formulaValidation.unknown.length" class="mb-0">
            Não encontradas no cadastro de variáveis: {{ formulaValidation.unknown.join(', ') }}.
          </p>
        </DsAlert>

        <div>
          <h3 class="font-semibold mb-2">Variáveis vinculadas</h3>
          <DsSearchInput v-model="variableSearch" placeholder="Pesquisar variável..." class="mb-2" />
          <div class="max-h-56 overflow-y-auto border border-gray-200 rounded-2xl p-3 grid grid-cols-1 md:grid-cols-2 gap-2">
            <label v-for="variable in filteredVariables" :key="variable.codVariavel" class="flex gap-2 text-sm items-start">
              <input v-model="form.variavelIds" type="checkbox" :value="variable.codVariavel" class="mt-1">
              <span><strong>{{ variable.nome }}</strong> <code>{{ variable.sigla }}</code><br><small class="text-gray-500">{{ variable.formula || 'Sem fórmula vinculada' }}</small></span>
            </label>
          </div>
        </div>

        <div>
          <div class="flex items-center justify-between mb-2">
            <h3 class="font-semibold">Equações por linguagem/referência</h3>
            <DsButton size="sm" variant="secondary" icon="plus" @click="addEquation">Adicionar equação</DsButton>
          </div>
          <div v-for="(equation, index) in form.equacoes" :key="index" class="border border-gray-200 rounded-2xl p-3 mb-3">
            <div class="grid grid-cols-1 md:grid-cols-3 gap-3 mb-3">
              <DsSelect v-model="equation.codLinguagem" label="Linguagem" required>
                <option value="">Selecione</option><option v-for="language in meta.linguagens" :key="language.codLinguagem" :value="language.codLinguagem">{{ language.nome }}</option>
              </DsSelect>
              <DsSelect v-model="equation.codReferencia" label="Referência">
                <option value="">Sem referência</option><option v-for="reference in meta.referencias" :key="reference.codReferencia" :value="reference.codReferencia">{{ reference.titulo }} {{ reference.ano ? `(${reference.ano})` : '' }}</option>
              </DsSelect>
              <DsInput v-model="equation.nomeFuncao" label="Nome da função" />
            </div>
            <DsTextarea v-model="equation.equacao" label="Equação" :rows="3" required />
            <DsAlert v-if="equationValidation(index).hasErrors" variant="warning" title="Variáveis pendentes" class="mt-3">
              <p v-if="equationValidation(index).unselected.length" class="mb-1">
                Selecione em "Variáveis vinculadas": {{ equationValidation(index).unselected.join(', ') }}.
              </p>
              <p v-if="equationValidation(index).unknown.length" class="mb-0">
                Não encontradas no cadastro de variáveis: {{ equationValidation(index).unknown.join(', ') }}.
              </p>
            </DsAlert>
            <div class="flex justify-end mt-2"><DsButton size="sm" variant="danger" icon="trash" @click="form.equacoes.splice(index, 1)">Remover</DsButton></div>
          </div>
        </div>
      </form>
      <template #footer>
        <DsButton variant="secondary" @click="editorOpen = false">Cancelar</DsButton>
        <DsButton :loading="saving" :disabled="formulaReferencesValidation.hasErrors" @click="save">Salvar</DsButton>
      </template>
    </DsModal>

    <DsModal v-model="viewOpen" title="Detalhes da fórmula" size="lg">
      <div v-if="detail" class="space-y-3 text-sm">
        <p><strong>{{ detail.nome }}</strong> — {{ detail.descricao || 'Sem descrição' }}</p>
        <pre class="bg-gray-100 rounded-xl p-3 overflow-x-auto"><code>{{ detail.formula }}</code></pre>
        <p><strong>CODVARIAVEL:</strong> {{ detail.codVariavel ?? '-' }}</p>
        <p><strong>Casas decimais:</strong> {{ detail.casasDecimais }}</p>
        <h3 class="font-semibold">Equações</h3>
        <DsAlert v-if="!detail.equacoes.length" variant="info">Nenhuma equação cadastrada.</DsAlert>
        <div v-for="(equation, index) in detail.equacoes" :key="index" class="border-b border-gray-100 pb-2">
          <strong>{{ equation.linguagem }}</strong> <span class="text-gray-500">{{ equation.referencia || 'Sem referência' }}</span>
          <pre class="whitespace-pre-wrap"><code>{{ equation.equacao }}</code></pre>
        </div>
      </div>
      <template #footer><DsButton variant="secondary" @click="viewOpen = false">Fechar</DsButton></template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

interface FormulaListItem { codFormula: number; codVariavel?: number | null; nome: string; formula: string; descricao?: string; casasDecimais: number; variaveis: string; siglas: string; variavelIds?: string }
interface VariableOption { codVariavel: number; nome: string; sigla: string; codigo?: string; abreviacao?: string; formula?: string; normalidade?: string }
interface LanguageOption { codLinguagem: number; nome: string }
interface ReferenceOption { codReferencia: number; titulo: string; ano?: number; autores?: string }
interface Equation { codLinguagem: number | string; codReferencia: number | string; equacao: string; nomeFuncao: string }
interface FormulaDetail { codFormula: number; codVariavel?: number | null; nome: string; formula: string; descricao?: string; casasDecimais: number; variavelIds: number[]; equacoes: Array<Equation & { linguagem: string; referencia?: string }> }

const api = useApi()
const auth = useAuthStore()
const swal = useSwal()
const loading = ref(false)
const saving = ref(false)
const errorMessage = ref('')
const items = ref<FormulaListItem[]>([])
const page = ref(1)
const totalPages = ref(0)
const filters = reactive({ sigla: '', nome: '', formula: '' })
const meta = reactive<{ variaveis: VariableOption[]; linguagens: LanguageOption[]; referencias: ReferenceOption[] }>({ variaveis: [], linguagens: [], referencias: [] })
const editorOpen = ref(false)
const viewOpen = ref(false)
const editingId = ref<number | null>(null)
const detail = ref<FormulaDetail | null>(null)
const variableSearch = ref('')
const form = reactive({ nome: '', descricao: '', formula: '', casasDecimais: 2, codVariavel: null as number | null, variavelIds: [] as number[], equacoes: [] as Equation[] })
const reservedFormulaTokens = new Set([
  'if', 'else', 'return', 'true', 'false', 'null', 'undefined', 'new', 'var', 'let', 'const',
  'function', 'typeof', 'void', 'this', 'Math', 'Number', 'String', 'Boolean', 'parseFloat',
  'parseInt', 'toFixed', 'pow', 'round', 'abs', 'min', 'max', 'floor', 'ceil', 'sqrt'
].map(token => token.toUpperCase()))

const filteredVariables = computed(() => {
  const term = variableSearch.value.trim().toLocaleLowerCase()
  return !term ? meta.variaveis : meta.variaveis.filter(v => `${v.nome} ${v.sigla}`.toLocaleLowerCase().includes(term))
})
const variableTokens = computed(() => {
  const tokens = new Map<string, VariableOption>()
  meta.variaveis.forEach(variable => {
    variableAliasTokens(variable).forEach(token => tokens.set(token, variable))
  })
  return tokens
})
const selectedVariableTokens = computed(() => {
  const selected = new Set(form.variavelIds.map(Number))
  return new Set(
    meta.variaveis
      .filter(variable => selected.has(variable.codVariavel))
      .flatMap(variableAliasTokens)
      .filter(Boolean)
  )
})
const ownerVariableTokens = computed(() => {
  const owner = meta.variaveis.find(variable => variable.codVariavel === form.codVariavel)
  return new Set(owner ? variableAliasTokens(owner) : [])
})
const formulaReferencesValidation = computed(() => validateFormulaReferences([
  form.formula,
  ...form.equacoes.map(equation => equation.equacao)
]))
const formulaValidation = computed(() => validateFormulaReferences([form.formula]))

async function load(target = 1) {
  loading.value = true; errorMessage.value = ''; page.value = target
  const query = new URLSearchParams({ page: String(target), pageSize: '10' })
  Object.entries(filters).forEach(([key, value]) => value && query.set(key, value))
  try {
    const response = await api.get<{ data: FormulaListItem[]; totalPages: number }>(`/api/web/formulas?${query}`)
    items.value = response.data || []; totalPages.value = response.totalPages || 0
  } catch (error) { errorMessage.value = message(error) } finally { loading.value = false }
}

async function ensureMeta() {
  if (meta.variaveis.length) return
  const response = await api.get<{ data: typeof meta }>('/api/web/formulas/meta')
  Object.assign(meta, response.data)
}

function resetForm() {
  editingId.value = null; variableSearch.value = ''
  Object.assign(form, { nome: '', descricao: '', formula: '', casasDecimais: 2, codVariavel: null, variavelIds: [], equacoes: [] })
}

async function openCreate() {
  try {
    await ensureMeta(); resetForm(); editorOpen.value = true
  } catch (error) {
    await swal.toast(message(error), 'error')
  }
}
async function openEdit(id: number) {
  try {
    await ensureMeta(); resetForm()
    const response = await api.get<{ data: FormulaDetail }>(`/api/web/formulas/${id}`)
    const data = response.data; editingId.value = id
    Object.assign(form, { nome: data.nome, descricao: data.descricao || '', formula: data.formula, casasDecimais: data.casasDecimais, codVariavel: data.codVariavel ?? null, variavelIds: data.variavelIds.filter(id => id !== data.codVariavel), equacoes: data.equacoes.map(e => ({ codLinguagem: e.codLinguagem, codReferencia: e.codReferencia || '', equacao: e.equacao, nomeFuncao: e.nomeFuncao || '' })) })
    editorOpen.value = true
  } catch (error) {
    await swal.toast(message(error), 'error')
  }
}
async function openView(id: number) {
  try {
    detail.value = (await api.get<{ data: FormulaDetail }>(`/api/web/formulas/${id}`)).data; viewOpen.value = true
  } catch (error) {
    await swal.toast(message(error), 'error')
  }
}
function addEquation() { form.equacoes.push({ codLinguagem: '', codReferencia: '', equacao: '', nomeFuncao: '' }) }

async function save() {
  if (!form.formula.trim() || !form.variavelIds.length) { await swal.warning('Dados incompletos', 'Informe a fórmula e selecione ao menos uma variável.'); return }
  if (formulaReferencesValidation.value.hasErrors) {
    await swal.warning('Variáveis pendentes', 'Todas as variáveis usadas na fórmula e nas equações precisam estar selecionadas em Variáveis vinculadas.')
    return
  }
  saving.value = true
  const variavelIds = form.variavelIds.map(Number)
  const payload = { nome: form.nome || null, descricao: form.descricao || null, formula: form.formula, casasDecimais: Number(form.casasDecimais), variavelIds, codVariavel: form.codVariavel ?? variavelIds[0] ?? null, equacoes: form.equacoes.map(e => ({ codLinguagem: Number(e.codLinguagem), codReferencia: e.codReferencia ? Number(e.codReferencia) : null, equacao: e.equacao, nomeFuncao: e.nomeFuncao || null })) }
  try {
    if (editingId.value) await api.put(`/api/web/formulas/${editingId.value}`, payload); else await api.post('/api/web/formulas', payload)
    editorOpen.value = false; await swal.toast('Fórmula salva com sucesso.'); await load(page.value)
  } catch (error) { await swal.toast(message(error), 'error') } finally { saving.value = false }
}

async function remove(item: FormulaListItem) {
  const confirmation = await swal.confirm('Excluir fórmula', `Deseja excluir "${item.nome}"?`)
  if (!confirmation?.isConfirmed) return
  try { await api.del(`/api/web/formulas/${item.codFormula}`); await swal.toast('Fórmula excluída.'); await load(page.value) } catch (error) { await swal.toast(message(error), 'error') }
}
function clearFilters() { Object.assign(filters, { sigla: '', nome: '', formula: '' }); load(1) }
function message(error: unknown) { return error instanceof Error ? error.message : 'Erro inesperado.' }
function equationValidation(index: number) { return validateFormulaReferences([form.equacoes[index]?.equacao || '']) }
function listItemValidation(item: FormulaListItem) {
  if (!meta.variaveis.length) return { unselected: [], unknown: [], hasErrors: false, pending: true }
  return validateFormulaReferencesForTokens([item.formula], linkedTokensFromItem(item), tokensForVariableId(item.codVariavel))
}
function listItemValidationTitle(item: FormulaListItem) {
  const validation = listItemValidation(item)
  if (validation.pending) return 'Aguardando carregamento do catálogo de variáveis.'
  const messages = []
  if (validation.unselected.length) messages.push(`Não vinculadas: ${validation.unselected.join(', ')}`)
  if (validation.unknown.length) messages.push(`Não cadastradas: ${validation.unknown.join(', ')}`)
  return messages.join(' | ')
}
function validateFormulaReferences(expressions: string[]) {
  return validateFormulaReferencesForTokens(expressions, selectedVariableTokens.value, ownerVariableTokens.value)
}
function validateFormulaReferencesForTokens(expressions: string[], selectedTokens: Set<string>, ignoredTokens = new Set<string>()) {
  const tokens = extractFormulaTokens(expressions)
  const known = variableTokens.value
  const unselected = tokens
    .filter(token => !ignoredTokens.has(token))
    .filter(token => !selectedTokens.has(token))
    .map(formatFormulaToken)
  const unknown = tokens
    .filter(token => !ignoredTokens.has(token))
    .filter(token => !known.has(token))
    .map(formatFormulaToken)
  return {
    unselected,
    unknown,
    hasErrors: unselected.length > 0 || unknown.length > 0,
    pending: false
  }
}
function extractFormulaTokens(expressions: string[]) {
  const found = new Set<string>()
  const knownTokens = [...variableTokens.value.keys()].filter(token => token.length >= 2 && !reservedFormulaTokens.has(token))
  expressions.forEach(expression => {
    const code = expression || ''
    for (const match of code.matchAll(/<<\s*VR_([A-Za-z0-9_]+)\s*>>/gi)) addFormulaToken(found, match[1])
    for (const match of code.matchAll(/(?:document\.)?getElementById\(\s*['"]VR_([A-Za-z0-9_]+)['"]\s*\)(?:\s*\.value)?/gi)) addFormulaToken(found, match[1])
    for (const match of code.matchAll(/(?<!<)\bVR_([A-Za-z0-9_]+)\b(?!>)/gi)) addFormulaToken(found, match[1])
    knownTokens.forEach(token => {
      const pattern = new RegExp(`(?<!<<)(?<![\\w])(?:VR_)?${escapeRegExp(token)}(?![\\w])(?!>>)`, 'gi')
      if (pattern.test(code)) addFormulaToken(found, token)
    })
  })
  return [...found].sort()
}
function addFormulaToken(tokens: Set<string>, value: string) {
  const token = normalizeFormulaToken(value)
  if (token && !reservedFormulaTokens.has(token)) tokens.add(token)
}
function normalizeFormulaToken(value?: string) {
  return (value || '').trim().replace(/^VR_/i, '').replace(/[^A-Za-z0-9_]+/g, '_').replace(/^_+|_+$/g, '').toUpperCase()
}
function variableAliasTokens(variable: VariableOption) {
  return [variable.codigo, variable.sigla, variable.abreviacao]
    .map(value => normalizeFormulaToken(value))
    .filter(Boolean)
}
function tokensForVariableId(codVariavel?: number | null) {
  const variable = meta.variaveis.find(item => item.codVariavel === codVariavel)
  return new Set(variable ? variableAliasTokens(variable) : [])
}
function linkedTokensFromItem(item: FormulaListItem) {
  const ids = parseListItemVariableIds(item.variavelIds)
  if (ids.length) {
    return new Set(
      meta.variaveis
        .filter(variable => ids.includes(variable.codVariavel))
        .flatMap(variableAliasTokens)
        .filter(Boolean)
    )
  }

  return linkedTokensFromSiglas(item.siglas)
}
function parseListItemVariableIds(value?: string) {
  return (value || '')
    .split(',')
    .map(id => Number(id.trim()))
    .filter(id => Number.isFinite(id) && id > 0)
}
function linkedTokensFromSiglas(siglas?: string) {
  return new Set(
    (siglas || '')
      .split(',')
      .map(sigla => normalizeFormulaToken(sigla))
      .filter(Boolean)
  )
}
function formatFormulaToken(token: string) { return `VR_${token}` }
function escapeRegExp(value: string) { return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') }
onMounted(async () => {
  await Promise.all([
    load(1),
    ensureMeta().catch(() => undefined)
  ])
})
</script>
