import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'
import { computed, ref } from 'vue'
import { createPinia, setActivePinia } from 'pinia'

const source = await readFile(new URL('../stores/permissionsCenter.ts', import.meta.url), 'utf8')
const code = stripTypeScriptTypes(source).replace(/from ['"]pinia['"]/g, `from ${JSON.stringify(import.meta.resolve('pinia'))}`)
const { usePermissionsCenterStore } = await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`)
const catalog = ['relatorios.visualizar', 'relatorios.criar', 'relatorios.excluir', 'scripts.visualizar'].map(chave => {
  const [dominio, acao] = chave.split('.')
  return { chave, dominio, acao, descricao: `Permissão ${acao}`, status: -1 }
})
function deferred() { let resolve, reject; const promise = new Promise((res, rej) => { resolve = res; reject = rej }); return { promise, resolve, reject } }

test('Permission drafts, inheritance, module batches and request ordering', async t => {
  const originals = Object.fromEntries(['ref', 'computed', 'useApi', 'usePermissoesApi'].map(key => [key, globalThis[key]]))
  let api, calls, failSave
  function setup() {
    calls = []; failSave = false
    api = {
      catalogo: async () => ({ data: catalog }), perfis: async () => ({ data: ['comum', 'admin', 'revisor'] }),
      perfil: async perfil => ({ data: { perfil, permissoes: perfil === 'comum' ? ['relatorios.visualizar'] : ['scripts.visualizar'] } }),
      usuario: async id => ({ data: { codUsuario: id, perfil: id === 99 ? 'admin' : 'comum', excecoes: [{ chave: 'relatorios.visualizar', modo: 'NEGAR' }], efetivas: [] } }),
      salvarPerfil: async (...args) => { calls.push(['profile', ...args]); if (failSave) throw new Error('Falha ao salvar') },
      salvarUsuario: async (...args) => { calls.push(['user', ...args]); if (failSave) throw new Error('Falha ao salvar') }
    }
    Object.assign(globalThis, { ref, computed, usePermissoesApi: () => api, useApi: () => ({ get: async () => ({ data: [] }) }) })
    setActivePinia(createPinia())
    return usePermissionsCenterStore()
  }
  try {
    await t.test('Tracks changed keys, clears reverted edits and restores saved profile', async () => {
      const store = setup(); await store.load()
      assert.equal(store.ready, true); assert.equal(store.dirtyCount, 0)
      store.toggleProfilePermission('relatorios.criar', true)
      assert.equal(store.dirtyCount, 1); assert.equal(store.effectiveHas('relatorios.criar'), true)
      store.toggleProfilePermission('relatorios.criar', false)
      assert.equal(store.dirtyCount, 0)
      store.toggleProfilePermission('relatorios.visualizar', false)
      store.discard(); assert.equal(store.profileHas('relatorios.visualizar'), true)
      assert.equal(store.dirtyCount, 0)
    })
    await t.test('Uses profile inheritance instead of loaded effective permissions', async () => {
      const store = setup(); await store.load(); await store.selectContext('usuario', undefined, 5)
      assert.equal(store.effectiveHas('relatorios.visualizar'), false)
      assert.equal(store.resultLabel('relatorios.visualizar'), 'Negado por exceção')
      store.setUserOverride('relatorios.visualizar', '')
      assert.equal(store.effectiveHas('relatorios.visualizar'), true)
      assert.equal(store.resultLabel('relatorios.visualizar'), 'Permitido pelo perfil')
      assert.equal(store.dirtyCount, 1)
      store.setUserOverride('relatorios.criar', 'NEGAR')
      assert.equal(store.effectiveHas('relatorios.criar'), false)
      assert.equal(store.dirtyCount, 2, 'An explicit denial differs from inheritance even with the same effective result')
      store.setUserOverride('relatorios.visualizar', 'NEGAR'); store.setUserOverride('relatorios.criar', '')
      assert.equal(store.dirtyCount, 0)
      store.setUserOverride('relatorios.criar', 'PERMITIR')
      assert.equal(store.effectiveHas('relatorios.criar'), true)
      store.discard(); assert.equal(store.userOverride('relatorios.criar'), '')
    })
    await t.test('Batch operations affect the complete selected module only', async () => {
      const store = setup(); await store.load()
      const reports = store.modules.find(item => item.domain === 'relatorios')
      store.applyModule(reports, 'permitir')
      assert.equal(store.enabledCount(reports), 3); assert.equal(store.effectiveHas('scripts.visualizar'), false)
      assert.equal(store.dirtyCount, 2)
      await store.selectContext('usuario', undefined, 5)
      store.applyModule(reports, 'permitir'); assert.equal(store.enabledCount(reports), 3)
      assert.equal(store.userOverride('scripts.visualizar'), '')
      store.applyModule(reports, 'herdar')
      assert.equal(store.enabledCount(reports), 1); assert.deepEqual(store.userOverrides, [])
      store.applyModule(reports, 'negar'); assert.equal(store.enabledCount(reports), 0)
    })
    await t.test('Successful save advances the baseline; failed save preserves drafts', async () => {
      const store = setup(); await store.load()
      store.toggleProfilePermission('relatorios.criar', true); failSave = true
      await assert.rejects(store.save(), /Falha/)
      assert.equal(store.dirtyCount, 1); assert.equal(store.saving, false)
      failSave = false; await store.save(); assert.equal(store.dirtyCount, 0)
      store.toggleProfilePermission('relatorios.criar', false); store.discard()
      assert.equal(store.effectiveHas('relatorios.criar'), true)
      await store.selectContext('usuario', undefined, 5)
      store.setUserOverride('relatorios.criar', 'PERMITIR'); await store.save()
      assert.equal(store.dirtyCount, 0); assert.equal(calls.at(-1)[0], 'user')
    })
    await t.test('Administrator remains read-only with total effective access', async () => {
      const store = setup(); await store.load(); await store.selectContext('perfil', 'admin')
      assert.equal(store.canMutate, false); assert.equal(store.effectiveHas('relatorios.criar'), true)
      store.toggleProfilePermission('relatorios.criar', false); assert.equal(store.dirtyCount, 0)
      await store.selectContext('usuario', undefined, 99)
      store.setUserOverride('relatorios.criar', 'NEGAR')
      assert.equal(store.effectiveHas('relatorios.criar'), true); assert.equal(store.dirtyCount, 0)
      await store.save(); assert.equal(calls.length, 0)
    })
    await t.test('Out-of-order loads and stale errors never replace the active context', async () => {
      const store = setup(); await store.load()
      const slow = deferred(); api.perfil = profile => profile === 'revisor' ? slow.promise : Promise.resolve({ data: { permissoes: ['relatorios.visualizar'] } })
      const first = store.selectContext('perfil', 'revisor')
      assert.equal(store.canMutate, false)
      await store.selectContext('perfil', 'comum')
      slow.resolve({ data: { permissoes: ['scripts.visualizar'] } }); await first
      assert.equal(store.selectedProfile, 'comum'); assert.equal(store.effectiveHas('scripts.visualizar'), false)
      const stale = deferred(); api.usuario = () => stale.promise
      const pending = store.selectContext('usuario', undefined, 5)
      await store.selectContext('perfil', 'comum'); stale.reject(new Error('Antigo')); await pending
      assert.equal(store.error, ''); assert.equal(store.loading, false)
    })
    await t.test('Failed inherited profile load keeps editing disabled and supports retry', async () => {
      const store = setup(); await store.load(); api.perfil = async () => { throw new Error('Sem conexão') }
      await store.selectContext('usuario', undefined, 5)
      assert.equal(store.ready, false); assert.equal(store.canMutate, false); assert.equal(store.error, 'Sem conexão')
      api.perfil = async () => ({ data: { permissoes: [] } }); await store.selectContext('usuario')
      assert.equal(store.ready, true); assert.equal(store.error, '')
    })
  } finally { Object.assign(globalThis, originals) }
})
