import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'
import { babelParse, parse, compileScript, compileTemplate } from '@vue/compiler-sfc'

test('Version deletion respects permission, active status, confirmation, failures and refresh', async () => {
  const source = await readFile(new URL('../pages/scripts/[id]/versoes/index.vue', import.meta.url), 'utf8')
  const { descriptor } = parse(source)
  const script = descriptor.scriptSetup.content
  const ast = babelParse(script, { sourceType: 'module', plugins: ['typescript'] })
  const handler = ast.program.body.find(node => node.type === 'FunctionDeclaration' && node.id.name === 'excluir')
  const code = stripTypeScriptTypes(script.slice(handler.start, handler.end))
  const calls = []
  let confirmed = false, fail = false
  const deps = {
    canDelete: { value: false }, loading: { value: false }, deleting: { value: null },
    swal: { confirm: async () => ({ isConfirmed: confirmed }), toast: async (...args) => calls.push(['toast', ...args]) },
    scriptsApi: { excluirVersao: async id => { if (fail) throw Error('Versão vinculada'); calls.push(['delete', id]) } },
    carregar: async () => calls.push(['refresh'])
  }
  const remove = new Function(...Object.keys(deps), `return ${code}`)(...Object.values(deps))
  const version = { codVersao: 21, numeroVersao: 'V2', ativo: 'F' }
  await remove(version)
  assert.deepEqual(calls, [])
  deps.canDelete.value = true
  await remove({ ...version, ativo: 'T' })
  await remove(version)
  assert.deepEqual(calls, [])
  assert.equal(deps.deleting.value, null)
  confirmed = true
  await remove(version)
  assert.deepEqual(calls.map(x => x[0]), ['delete', 'toast', 'refresh'])
  assert.equal(calls[0][1], 21)
  calls.length = 0
  fail = true
  await remove(version)
  assert.deepEqual(calls, [['toast', 'Versão vinculada', 'error']])
  assert.equal(deps.deleting.value, null)
  deps.deleting.value = 22
  calls.length = 0
  await remove(version)
  assert.deepEqual(calls, [])
})

test('Version list and detail scripts and templates compile', async () => {
  for (const file of ['index.vue', '[codversao]/index.vue']) {
    const filename = `../pages/scripts/[id]/versoes/${file}`
    const source = await readFile(new URL(filename, import.meta.url), 'utf8')
    const { descriptor, errors } = parse(source)
    assert.deepEqual(errors, [])
    compileScript(descriptor, { id: file })
    const compiled = compileTemplate({ source: descriptor.template.content, filename, id: file })
    assert.deepEqual(compiled.errors, [])
  }
})
