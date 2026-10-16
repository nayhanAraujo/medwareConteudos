import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'
import { babelParse, parse } from '@vue/compiler-sfc'

test('PDF deletion confirms, checks permissions and uses the attachment source', async () => {
  const source = await readFile(new URL('../pages/scripts/index.vue', import.meta.url), 'utf8')
  const { descriptor } = parse(source)
  const script = descriptor.scriptSetup.content
  const ast = babelParse(script, { sourceType: 'module', plugins: ['typescript'] })
  const handler = ast.program.body.find(node => node.type === 'FunctionDeclaration' && node.id.name === 'excluirPdf')
  const code = stripTypeScriptTypes(script.slice(handler.start, handler.end))
  const base = { codArquivo: 10, codVersao: null, nomeArquivo: 'Impressao.pdf' }
  const version = { ...base, codVersao: 21 }
  const item = { pdfsDisplay: [base, version] }
  const calls = []
  let confirmed = false
  let fail = false
  const dependencies = {
    canEditScripts: { value: true }, canDeleteScripts: { value: true },
    deletingPdf: { value: null }, detailItem: { value: item }, page: { value: 2 },
    swal: {
      confirm: async () => ({ isConfirmed: confirmed }), toast: async () => {},
      error: async (_, message) => calls.push(['error', message])
    },
    scriptsApi: {
      deleteScriptAttachment: async id => { if (fail) throw new Error('API falhou'); calls.push(['script', id]) },
      excluirVersaoAnexo: async id => calls.push(['version', id])
    },
    load: async page => calls.push(['reload', page])
  }
  const remove = new Function(...Object.keys(dependencies), `return ${code}`)(...Object.values(dependencies))
  await remove(base)
  assert.deepEqual(calls, [])
  assert.equal(item.pdfsDisplay.length, 2)
  confirmed = true
  fail = true
  await remove(base)
  assert.deepEqual(calls.pop(), ['error', 'API falhou'])
  assert.equal(item.pdfsDisplay.length, 2)
  assert.equal(dependencies.deletingPdf.value, null)
  fail = false
  await remove(base)
  assert.deepEqual(calls.splice(0), [['script', 10], ['reload', 2]])
  assert.deepEqual(item.pdfsDisplay, [version])
  dependencies.canDeleteScripts.value = false
  await remove(version)
  assert.deepEqual(calls, [])
  assert.equal(item.pdfsDisplay.length, 1)
  dependencies.canDeleteScripts.value = true
  await remove(version)
  assert.deepEqual(calls, [['version', 10], ['reload', 2]])
  assert.equal(item.pdfsDisplay.length, 0)
})
