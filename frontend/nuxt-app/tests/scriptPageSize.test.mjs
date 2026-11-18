import assert from 'node:assert/strict'
import test from 'node:test'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import { parse, babelParse, compileScript, compileTemplate } from '@vue/compiler-sfc'

test('Script listing sends selected page size and preserves filters', async () => {
  const source = await readFile(new URL('../pages/scripts/index.vue', import.meta.url), 'utf8')
  const { descriptor } = parse(source)
  compileScript(descriptor, { id: 'scripts' })
  assert.deepEqual(compileTemplate({ source: descriptor.template.content, filename: 'scripts.vue', id: 'scripts' }).errors, [])
  const script = descriptor.scriptSetup.content
  const ast = babelParse(script, { sourceType: 'module', plugins: ['typescript'] })
  const handler = ast.program.body.find(n => n.type === 'FunctionDeclaration' && n.id.name === 'load')
  const calls = []
  const box = value => ({ value })
  const deps = {
    hasParams: box(true), loading: box(false), errorMsg: box(''), page: box(4), pageSize: box('10'),
    sistema: box('Laudos UX'), pacote: box('1'), filtros: { nome: 'Anamnese', aprovado: '1', ativo: '1' },
    items: box([]), publications: box({}), auth: { isAdmin: false }, selectedScriptIds: box([]),
    totalPages: box(0), totalItems: box(0), pacoteNome: box(''), syncSelectAll() {},
    scriptsApi: { listScripts: async args => { calls.push(args); return { data: [{ codScriptLaudo: 1, nomePacote: 'Consulta' }], totalPages: 2, totalItems: 36 } } }
  }
  const load = new Function(...Object.keys(deps), `return ${stripTypeScriptTypes(script.slice(handler.start, handler.end))}`)(...Object.values(deps))
  for (const size of ['10', '20', '30', '40', '50']) {
    deps.pageSize.value = size
    await load(1)
    assert.equal(calls.at(-1).pageSize, Number(size))
    assert.equal(calls.at(-1).nome, 'Anamnese')
    assert.equal(calls.at(-1).pacote, '1')
    assert.equal(deps.page.value, 1)
  }
  assert.match(script, /watch\(pageSize, \(\) => load\(1\)\)/)
  assert.match(script, /remainingTotal \/ Number\(pageSize.value\)/)
})
