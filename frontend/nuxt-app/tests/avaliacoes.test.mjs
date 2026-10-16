import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'
import { compileScript, parse } from '@vue/compiler-sfc'
import { computed, reactive, ref } from 'vue'

test('Central handles permissions, filters, empty results, failures and management actions', async () => {
  const source = await readFile(new URL('../pages/avaliacoes/index.vue', import.meta.url), 'utf8')
  const { descriptor } = parse(source)
  const compiled = compileScript(descriptor, { id: 'avaliacoes-test' })
  const code = stripTypeScriptTypes(compiled.content).replace(/from ['"]vue['"]/g, `from ${JSON.stringify(import.meta.resolve('vue'))}`)
  const { default: component } = await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`)
  const rights = reactive({ visualizar: false, acompanhar: false, moderar: false })
  const calls = []
  let failure = false, mounted
  const row = { id: 7, situacao: 'nova', visivel: true }
  const globals = {
    computed, reactive, ref, definePageMeta() {}, onMounted: callback => { mounted = callback },
    useAuthStore: () => ({ can: (_, action) => rights[action] }),
    useAvaliacoesApi: () => ({
      list: async query => { calls.push(['list', query]); if (failure) throw new Error('Sem conexão'); return { data: [], page: 1, totalPages: 0, totalItems: 0 } },
      summary: async () => ({ data: { quantidade: 0, mediaGeral: null, distribuicaoNotas: {} } }),
      detail: async id => ({ data: { ...row, id }, historico: [] }),
      follow: async (...args) => calls.push(['follow', ...args]),
      note: async (...args) => calls.push(['note', ...args]),
      visibility: async (...args) => calls.push(['visibility', ...args])
    })
  }
  const originals = Object.fromEntries(Object.keys(globals).map(key => [key, globalThis[key]]))
  Object.assign(globalThis, globals)
  try {
    const page = component.setup({}, { expose() {} })
    await mounted()
    assert.equal(calls.length, 0)
    rights.visualizar = true
    page.filters.versao = '0'; page.filters.visivel = 'false'; page.filters.clinica = 'Clínica Coração'
    await page.load()
    const query = new URLSearchParams(calls[0][1])
    assert.equal(query.get('versao'), '0'); assert.equal(query.get('visivel'), 'false'); assert.equal(query.get('clinica'), 'Clínica Coração')
    assert.equal(page.rows.value.length, 0); assert.equal(page.summary.value.quantidade, 0); assert.equal(page.loading.value, false)
    failure = true
    await page.load()
    assert.equal(page.error.value, 'Sem conexão'); assert.equal(page.loading.value, false)
    failure = false
    await page.openDetail(7)
    calls.length = 0
    await page.change('state')
    assert.deepEqual(calls, [])
    rights.acompanhar = true
    page.state.value = 'concluida'
    await page.change('state')
    assert.deepEqual(calls[0], ['follow', 7, 'concluida'])
    calls.length = 0
    page.note.value = 'Anotação privada'
    await page.change('note')
    assert.deepEqual(calls[0], ['note', 7, 'Anotação privada'])
    calls.length = 0
    await page.change('visibility')
    assert.deepEqual(calls, [])
    rights.moderar = true
    page.reason.value = 'Revisar comentário'
    await page.change('visibility')
    assert.deepEqual(calls[0], ['visibility', 7, false, 'Revisar comentário'])
    assert.equal(page.busy.value, false)
    assert.deepEqual(page.changes({ antes: '{"NotaGeral":4}', depois: '{"NotaGeral":2}' }), ['Nota geral: 4 → 2'])
    page.closeDetail()
    assert.equal(page.detailId.value, null)
  } finally {
    for (const key of Object.keys(globals)) {
      if (originals[key] === undefined) delete globalThis[key]
      else globalThis[key] = originals[key]
    }
  }
})
