import assert from 'node:assert/strict'
import test from 'node:test'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import { babelParse, parse } from '@vue/compiler-sfc'

test('Inactivation and approval revocation require permission, confirmation and refresh', async () => {
  const source = await readFile(new URL('../pages/scripts/[id]/versoes/index.vue', import.meta.url), 'utf8')
  const { descriptor } = parse(source)
  const script = descriptor.scriptSetup.content
  const ast = babelParse(script, { sourceType: 'module', plugins: ['typescript'] })
  const handler = ast.program.body.find(n => n.type === 'FunctionDeclaration' && n.id.name === 'alterarEstado')
  const code = stripTypeScriptTypes(script.slice(handler.start, handler.end))
  const calls = []
  let confirmed = false, fail = false
  const deps = {
    busy: { value: false }, canActivate: { value: false }, canApprove: { value: false }, changing: { value: false },
    swal: { confirm: async () => ({ isConfirmed: confirmed }), toast: async (...args) => calls.push(['toast', ...args]) },
    scriptsApi: {
      inativarVersao: async id => { if (fail) throw Error('Falha'); calls.push(['inativar', id]) },
      desaprovarVersao: async id => calls.push(['desaprovar', id])
    }, carregar: async () => calls.push(['refresh'])
  }
  const change = new Function(...Object.keys(deps), `return ${code}`)(...Object.values(deps))
  const item = { codVersao: 21, numeroVersao: 'V2' }
  await change(item, 'inativar')
  await change(item, 'desaprovar')
  assert.deepEqual(calls, [])
  deps.canActivate.value = true
  await change(item, 'inativar')
  assert.deepEqual(calls, [])
  assert.equal(deps.changing.value, false)
  confirmed = true
  await change(item, 'inativar')
  assert.deepEqual(calls.map(c => c[0]), ['inativar', 'toast', 'refresh'])
  calls.length = 0
  deps.canApprove.value = true
  await change(item, 'desaprovar')
  assert.deepEqual(calls.map(c => c[0]), ['desaprovar', 'toast', 'refresh'])
  calls.length = 0
  fail = true
  await change(item, 'inativar')
  assert.deepEqual(calls, [['toast', 'Falha', 'error']])
  assert.equal(deps.changing.value, false)
  calls.length = 0
  deps.busy.value = true
  await change(item, 'inativar')
  assert.deepEqual(calls, [])
})

test('API state actions use explicit non-toggle endpoints', async () => {
  const source = await readFile(new URL('../composables/useScriptsApi.ts', import.meta.url), 'utf8')
  const { useScriptsApi } = await import(`data:text/javascript;base64,${Buffer.from(stripTypeScriptTypes(source)).toString('base64')}`)
  const previous = globalThis.useApi
  const calls = []
  globalThis.useApi = () => ({ post: async path => calls.push(path) })
  try {
    await useScriptsApi().inativarVersao(21)
    await useScriptsApi().desaprovarVersao(21)
    assert.deepEqual(calls, ['/api/web/scripts/versoes/21/inativar', '/api/web/scripts/versoes/21/desaprovar'])
  } finally { globalThis.useApi = previous }
})
