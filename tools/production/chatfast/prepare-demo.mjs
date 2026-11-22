import { readFile, mkdir, writeFile, stat } from 'node:fs/promises'
import { createHash } from 'node:crypto'
import { createRequire } from 'node:module'
import { dirname, resolve, relative, isAbsolute } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const repo = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const require = createRequire(resolve(repo, 'frontend/nuxt-app/package.json'))
const { parse } = require('@babel/parser')

// AST-only extraction: imported modules, callbacks, JSX and functions NEVER execute.
export function literal(node) {
  if (!node) throw new Error('Literal ausente.')
  if (['TSAsExpression', 'TSTypeAssertion', 'ParenthesizedExpression'].includes(node.type)) return literal(node.expression)
  if (['StringLiteral', 'NumericLiteral', 'BooleanLiteral'].includes(node.type)) return node.value
  if (node.type === 'NullLiteral') return null
  if (node.type === 'ArrayExpression') return node.elements.map(literal)
  if (node.type === 'ObjectExpression') return Object.fromEntries(node.properties.map(p => {
    if (p.type !== 'ObjectProperty' || p.computed || p.shorthand) throw new Error('Objeto deve conter apenas propriedades literais.')
    const key = p.key.name ?? p.key.value
    if (['__proto__', 'constructor', 'prototype'].includes(key)) throw new Error('Chave proibida.')
    return [key, literal(p.value)]
  }))
  throw new Error(`Expressão não literal recusada: ${node.type}`)
}
export function parseSource(text) {
  const ast = parse(text, { sourceType: 'module', plugins: ['typescript', 'jsx'] })
  const declarations = new Map()
  for (const item of ast.program.body) if (item.type === 'VariableDeclaration')
    for (const d of item.declarations) if (d.id.type === 'Identifier') declarations.set(d.id.name, d.init)
  function constant(name, unwrapMap = false) {
    let node = declarations.get(name)
    // Only recover the literal receiver. Do NOT interpret/invoke .map(ut/ue).
    if (unwrapMap && node?.type === 'CallExpression' && node.callee.type === 'MemberExpression' && node.callee.property.name === 'map') node = node.callee.object
    return literal(node)
  }
  const metrics = new Map(), lists = []
  function visit(node) {
    if (!node || typeof node !== 'object') return
    if (node.type === 'JSXAttribute' && node.name.name === 'metrics' && node.value?.type === 'JSXExpressionContainer') {
      const list = literal(node.value.expression)
      for (const item of list) if (typeof item.label === 'string' && /^\d+(?:[.,]\d+)?(?: min)?$/.test(item.value)) metrics.set(item.label, Number(item.value.replace(' min', '').replace(',', '.')))
    }
    for (const [key, value] of Object.entries(node)) if (!['loc', 'start', 'end', 'extra'].includes(key)) {
      if (Array.isArray(value)) value.forEach(visit); else if (value && typeof value === 'object') visit(value)
    }
  }
  // Daily/weekly Metrics are literal, but tracker Metrics contain expressions; skip those attributes.
  function collect(node) { try { visit(node) } catch { /* handled by per-attribute collection below */ } }
  function walk(node) {
    if (!node || typeof node !== 'object') return
    if (node.type === 'JSXAttribute' && node.name.name === 'metrics') { collect(node); return }
    if (node.type === 'JSXOpeningElement' && node.name.name === 'NumberedList') {
      for (const attr of node.attributes) if (attr.name?.name === 'items' && attr.value?.type === 'JSXExpressionContainer')
        try { lists.push(literal(attr.value.expression)) } catch { /* named constants are read explicitly */ }
    }
    for (const [key, value] of Object.entries(node)) if (!['loc', 'extra'].includes(key)) {
      if (Array.isArray(value)) value.forEach(walk); else if (value && typeof value === 'object') walk(value)
    }
  }
  walk(ast)
  return { constant, metrics, lists }
}
const statValue = (code, value, previousValue = null) => ({ code, value, previousValue })
const base = (type, start, end, generatedAt, summary, details) => ({ schemaVersion: '1.1', batchId: `tsx-${type}-20261119-revisao-1`, type, periodStart: start, periodEnd: end, generatedAt,
  complete: true, metrics: { analyzed: null, waiting: null, deviations: null, critical: null, treated: null }, summary, details })
const table = (title, columns, rows) => ({ title, columns, rows: rows.map(r => r.map(v => v == null ? '—' : String(v))) })
export function convert(dailyText, weeklyText, trackerText, sources) {
  if (!dailyText.includes('Resumo de 19/11/2026') || !weeklyText.includes('Semana 16–19/11/2026')) throw new Error('Conversor restrito aos snapshots de 19/11 fornecidos; revise antes de usar outros períodos.')
  const d = parseSource(dailyText), w = parseSource(weeklyText), tracker = parseSource(trackerText).constant('DADOS')
  const required = (map, key) => { if (!map.has(key)) throw new Error(`Indicador não encontrado: ${key}`); return map.get(key) }
  const methods = ['businessHours08001800', 'criticalSelectionsNotUniqueCases', 'botAuthoritativeState']
  const limitations = ['snapshotNotLive', 'sourceFileNotRawAudit', 'noTreatmentEvidence']
  const dailyCases = d.constant('CASOS'), extra = d.constant('EXTRA')
  const daily = base('diario', '2026-11-19T08:00:00-03:00', '2026-11-19T17:10:00-03:00', '2026-11-19T17:15:00-03:00', {
    statistics: [statValue('analyzedConversations', required(d.metrics, 'Conversas analisadas')), statValue('longWaitOver60Snapshot', required(d.metrics, 'Esperas > 1h ~17h')), statValue('outOfPatternConversations', required(d.metrics, 'Casos fora do padrão')), statValue('criticalSelections', required(d.metrics, 'Atendimentos críticos'))],
    dailyVolume: [], trend: [], methodCodes: [...methods, 'longWaitSnapshot', 'dailyOutOfPatternWindows'], limitationCodes: [...limitations, 'audioTranscriptionPending', 'deviationsOnlyMonitoringWindows']
  }, {
    sources, recommendations: d.constant('REC_HOJE'), positiveExamples: d.constant('BONS'),
    tables: [table('Falhas por técnico', ['Técnicos', 'Equipes', 'Observação'], d.constant('FALHAS').map(r => [r.tecs.join(', '), r.eq.join(', '), r.txt])),
      table('Eventos de 19/11 por técnico', ['Técnico', 'Desvio', 'Crítico', 'Reincidência', 'Espera', 'Bom exemplo'], Object.entries(d.constant('HOJE_V19')).map(([name, values]) => [name, ...values]))],
    cases: dailyCases.map(c => ({ client: c.cliente, technicians: c.tec, team: c.equipe, status: extra[c.cliente]?.st ?? c.situacao ?? 'Não informado', description: c.oque, dates: '19/11/2026', occurrences: null, severity: c.grav })),
    tracker: { count: tracker.total_casos_arquivo, updatedAt: tracker.atualizado_em_txt, note: 'Snapshot da página de acompanhamento; ausência de registros não significa ausência de seleções críticas nos resumos.' }
  })
  // Definitions from the explicitly documented totals/method of the attached weekly TSX.
  if (!weeklyText.includes('151 desvios sem repetição') || !weeklyText.includes('n=354')) throw new Error('Fonte semanal mudou: revisar mapeamento antes de gerar.')
  const weekly = base('semanal', '2026-11-16T00:00:00-03:00', '2026-11-19T17:10:00-03:00', '2026-11-19T18:52:00-03:00', {
    statistics: [statValue('handledConversations', required(w.metrics, 'Conversas atendidas')), statValue('newConversations', 644), statValue('deduplicatedDeviations', 151), statValue('deviationsPer100', required(w.metrics, 'Desvios a cada 100'), 32.5), statValue('medianFirstReplyMinutes', required(w.metrics, '1ª resposta mediana'), 9.3), statValue('meanFirstReplyMinutes', 16.3), statValue('firstReplyMeasurements', 354), statValue('noReplyNewConversations', required(w.metrics, 'Sem nenhuma resposta'), 14), statValue('events', required(w.metrics, 'Eventos'), 465), statValue('deviationEvents', required(w.metrics, 'Desvios (eventos)'), 275), statValue('criticalSelections', required(w.metrics, 'Críticos')), statValue('recurrenceEvents', required(w.metrics, 'Reincidências'), 20), statValue('businessDays', 4, 5)],
    dailyVolume: [56,65,62,55].map((events,i) => ({ date: `2026-11-${16+i}`, events })),
    trend: w.constant('TREND_GERAL').map((deviationsPer100, i) => ({ start: ['2026-11-02','2026-11-09','2026-11-16'][i], end: ['2026-11-06','2026-11-13','2026-11-19'][i], deviationsPer100 })),
    methodCodes: [...methods, 'uniqueTechnicianConversation', 'newClientConversationFirstReply', 'globalConversationDeduplication'],
    limitationCodes: [...limitations, 'unequalBusinessDays', 'partialCollectionLastDay', 'missingDeploymentSummary', 'incompleteCriticalEventExtraction', 'recurrenceCoverageChanged', 'limitedFirstReplySample', 'audioTranscriptionPending', 'deviationsOnlyMonitoringWindows']
  }, {
    sources, recommendations: w.lists.flatMap(list => list.flatMap(item => typeof item === 'string' ? [item] : [item.text, ...(item.items ?? [])])).filter(x => typeof x === 'string'), positiveExamples: [],
    tables: [table('Indicadores por técnico', ['Técnico','Equipe','Conversas','Desvios deduplicados','Índice /100','1ª resposta mediana (min)','Média (min)','Medições','Sem resposta','Amostra <10','Poucas medições'], w.constant('SEMANA', true)),
      table('Equipes e metas', ['Equipe','Gestor','Técnicos','Conversas por equipe','Desvios','Índice /100','Mediana (min)','Média (min)','Medições','Meta índice','Meta tempo','Poucas medições'], w.constant('EQUIPES_SEMANA', true)),
      table('Bons exemplos', ['Técnico','Equipe','Bons exemplos','Anterior','Desvios'], w.constant('BONS')),
      table('Melhoraram', ['Técnico','Equipe','Índice anterior','Atual','Variação','Desvios','Reincidências','Bons'], w.constant('MELHORES')),
      table('Pioraram', ['Técnico','Equipe','Índice anterior','Atual','Variação','Desvios','Reincidências','Bons'], w.constant('PIORARAM'))],
    cases: w.constant('CASOS').map(c => ({ client: c.cliente, technicians: c.tecs.join(', '), team: c.eq, status: c.status, description: c.resumo, dates: c.datas, occurrences: c.vezes })), tracker: daily.details.tracker
  })
  return { diario: daily, semanal: weekly }
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const args = process.argv.slice(2), source = resolve(args[args.indexOf('--source') + 1] ?? ''), outputArg = args.indexOf('--output')
    if (!args.includes('--source') || outputArg < 0 || !args[outputArg + 1]) throw new Error('Use --source pasta-dos-TSX --output NOVA-pasta-privada-fora-do-repositorio.')
    const output = resolve(args[outputArg + 1]), rel = relative(repo, output)
    if (!isAbsolute(output) || !rel.startsWith('..') && !isAbsolute(rel)) throw new Error('Demonstração real deve ficar FORA do repositório e de pastas públicas.')
    if (/(?:^|[\\/])(public|static|wwwroot)(?:[\\/]|$)/i.test(output)) throw new Error('Pasta pública proibida.')
    const names = ['resumo_diario.tsx', 'relatorio_semanal.tsx', 'casos_criticos.tsx'], texts = [], sources = []
    for (const name of names) { const path = resolve(source, name); if ((await stat(path)).size > 2_000_000) throw new Error('TSX excede limite.'); const text = await readFile(path, 'utf8'); texts.push(text); sources.push({ file: name, sha256: createHash('sha256').update(text).digest('hex') }) }
    const reports = convert(...texts, sources)
    // Exclusive creation prevents replacing a previous local demonstration.
    await mkdir(output)
    for (const [name, report] of Object.entries(reports)) await writeFile(resolve(output, `${name}.json`), JSON.stringify(report, null, 2), { flag: 'wx' })
    console.log('Demonstração preparada em pasta privada. Nenhuma requisição HTTP ou gravação em banco foi executada.')
  } catch (e) { console.error(e.message); process.exitCode = 1 }
}
