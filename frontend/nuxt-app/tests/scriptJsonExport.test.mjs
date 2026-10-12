import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'
import { compileScript, parse } from '@vue/compiler-sfc'
import { renderToString } from '@vue/server-renderer'
import { computed, createSSRApp, h } from 'vue'

test('Exportar JSON appears for UX scripts with JSON, with or without versions', async () => {
  const source = await readFile(new URL('../components/scripts/ScriptActionsDropdown.vue', import.meta.url), 'utf8')
  const { descriptor } = parse(source)
  const compiled = compileScript(descriptor, { id: 'script-actions-test', inlineTemplate: true })
  const code = stripTypeScriptTypes(compiled.content).replace(/from ['"]vue['"]/g, `from ${JSON.stringify(import.meta.resolve('vue'))}`)
  const { default: component } = await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`)
  const dependencies = { computed, useAuthStore: () => ({ can: () => false }), useScriptsApi: () => ({}), useSwal: () => ({}) }
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
