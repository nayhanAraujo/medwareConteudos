import assert from 'node:assert/strict'
import test from 'node:test'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import { ref, computed, watch, effectScope } from 'vue'

test('Global search debounces, filters the system and active status, paginates and ignores stale responses', async () => {
  const source = await readFile(new URL('../composables/useGlobalScriptSearch.ts', import.meta.url), 'utf8')
  const code = stripTypeScriptTypes(source).replace('export function', 'function')
  const calls = []
  const pending = []
  let cleanup
  let scheduled
  const create = new Function('ref', 'computed', 'watch', 'onBeforeUnmount', 'useScriptsApi', 'setTimeout', 'clearTimeout', `${code}; return useGlobalScriptSearch`)(
    ref, computed, watch, callback => { cleanup = callback },
    () => ({ listScripts: args => { calls.push(args); return new Promise((resolve, reject) => pending.push({ resolve, reject })) } }),
    callback => { scheduled = callback; return 1 }, () => { scheduled = undefined }
  )
  const scope = effectScope()
  const sistema = ref('Laudos UX')
  const state = scope.run(() => create(sistema))
  const flush = async () => { await Promise.resolve(); await Promise.resolve() }
  const result = name => ({ data: [{ nome: name }], totalPages: 2, totalItems: 11 })
  try {
    state.query.value = 'ab'
    assert.equal(scheduled, undefined)
    state.query.value = 'anam'
    state.query.value = 'anamnese'
    scheduled()
    assert.equal(calls.length, 1)
    assert.equal(calls[0].nome, 'anamnese')
    assert.equal(calls[0].sistema, 'Laudos UX')
    assert.equal(calls[0].ativo, 1)
    assert.equal('pacote' in calls[0], false)
    state.query.value = 'MOD-000001'
    pending[0].resolve(result('Antigo'))
    await flush()
    assert.deepEqual(state.items.value, [])
    scheduled()
    pending[1].resolve(result('Atual'))
    await flush()
    assert.equal(state.items.value[0].nome, 'Atual')
    assert.equal(state.totalItems.value, 11)
    const next = state.search(2)
    assert.equal(calls.at(-1).page, 2)
    pending.at(-1).resolve(result('Pagina 2'))
    await next
    state.includeInactive.value = true
    scheduled()
    assert.equal(calls.at(-1).ativo, undefined)
    assert.equal(state.page.value, 1)
    pending.at(-1).reject(new Error('Falha de rede'))
    await flush()
    assert.equal(state.error.value, 'Falha de rede')
    assert.equal(state.loading.value, false)
    const retry = state.search()
    pending.at(-1).resolve(result('Recuperado'))
    await retry
    assert.equal(state.error.value, '')
    sistema.value = 'Laudos Flex'
    scheduled()
    assert.equal(calls.at(-1).sistema, 'Laudos Flex')
    state.query.value = ''
    pending.at(-1).resolve(result('Obsoleto'))
    await flush()
    assert.deepEqual(state.items.value, [])
    assert.equal(state.loading.value, false)
    assert.equal(state.ready.value, false)
  } finally { cleanup(); scope.stop() }
})
