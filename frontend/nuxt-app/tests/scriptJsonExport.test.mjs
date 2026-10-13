import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'
import { compileScript, parse } from '@vue/compiler-sfc'
import { renderToString } from '@vue/server-renderer'
import { computed, createSSRApp, h, ref } from 'vue'

test('Exportar JSON appears for UX scripts with JSON, with or without versions', async () => {
  const source = await readFile(new URL('../components/scripts/ScriptActionsDropdown.vue', import.meta.url), 'utf8')
  const { descriptor } = parse(source)
  const compiled = compileScript(descriptor, { id: 'script-actions-test', inlineTemplate: true })
  const code = stripTypeScriptTypes(compiled.content).replace(/from ['"]vue['"]/g, `from ${JSON.stringify(import.meta.resolve('vue'))}`)
  const { default: component } = await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`)
  const dependencies = { computed, ref, useAuthStore: () => ({ can: () => false }), useScriptsApi: () => ({}), useSwal: () => ({}) }
  const originals = Object.fromEntries(Object.keys(dependencies).map(key => [key, globalThis[key]]))
  Object.assign(globalThis, dependencies)
  try {
    for (const [sistema, temArquivoJson, ultimaVersao, expected] of [
      ['Laudos UX', true, undefined, true],
      ['Laudos UX', true, 'V2', true],
      ['Laudos UX', false, 'V2', false],
      ['Laudos Flex', true, undefined, false]
    ]) {
      const app = createSSRApp(component, { item: { sistema, temArquivoJson, ultimaVersao, codScriptLaudo: 1, nome: 'Teste', mrdList: [] } })
      app.component('DsDropdown', { setup: (_, { slots }) => () => h('div', slots.default?.({ close() {} })) })
      app.component('DsDropdownItem', { setup: (_, { slots }) => () => h('button', slots.default?.()) })
      app.component('DsDropdownDivider', { render: () => h('hr') })
      const html = await renderToString(app)
      assert.equal(html.includes('Exportar JSON'), expected, `${sistema}, JSON=${temArquivoJson}, versao=${ultimaVersao}`)
    }
  } finally {
    for (const key of Object.keys(dependencies)) {
      if (originals[key] === undefined) delete globalThis[key]
      else globalThis[key] = originals[key]
    }
  }
})

test('Individual package selects extension and batch creates one POST and download', async () => {
  const source = await readFile(new URL('../composables/useScriptsApi.ts', import.meta.url), 'utf8')
  const code = stripTypeScriptTypes(source)
  const { useScriptsApi } = await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`)
  const originalApi = globalThis.useApi
  const originalDocument = globalThis.document
  const calls = []
  const downloads = []
  let mime = 'application/json'
  let failure = false
  globalThis.useApi = () => ({ getBlob: async (path, options) => {
    calls.push({ path, options })
    if (failure) throw new Error('Falha ao exportar')
    return new Blob(['conteudo'], { type: mime })
  } })
  globalThis.document = { createElement: () => ({ click() { downloads.push(this.download) } }) }
  try {
    const api = useScriptsApi()
    for (const name of ['RECEITA ESPECIAL', 'RECEITA ESPECIAL.mrd', 'RECEITA ESPECIAL.json']) {
      await api.download('/api/web/scripts/1/exportar-mrd', name)
      assert.equal(downloads.pop(), 'RECEITA ESPECIAL.json')
    }
    await api.downloadScriptPackage(1, 'Modelo')
    assert.deepEqual(downloads, ['Modelo.json'])
    mime = 'application/zip'
    await api.downloadScriptPackage(2, 'Modelo')
    assert.deepEqual(downloads, ['Modelo.json', 'Modelo.zip'])
    calls.length = 0
    downloads.length = 0
    await api.downloadScriptBatch([1, 2])
    assert.equal(calls.length, 1)
    assert.equal(calls[0].path, '/api/web/scripts/exportar-lote')
    assert.equal(calls[0].options.method, 'POST')
    assert.deepEqual(JSON.parse(calls[0].options.body), { ids: [1, 2] })
    assert.deepEqual(downloads, ['modelos.zip'])
    failure = true
    await assert.rejects(api.downloadScriptBatch([1, 2]), /Falha ao exportar/)
    assert.equal(downloads.length, 1)
  } finally {
    if (originalApi === undefined) delete globalThis.useApi
    else globalThis.useApi = originalApi
    if (originalDocument === undefined) delete globalThis.document
    else globalThis.document = originalDocument
  }
})
