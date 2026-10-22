import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import { createPinia, setActivePinia } from 'pinia'
import test from 'node:test'

const source = await readFile(new URL('../stores/auditoria.ts', import.meta.url), 'utf8')
const code = stripTypeScriptTypes(source).replace(/from ['"]pinia['"]/g, `from ${JSON.stringify(import.meta.resolve('pinia'))}`)
const { useAuditoriaStore } = await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`)
const pending = () => { let resolve, reject; const promise = new Promise((yes, no) => { resolve = yes; reject = no }); return { promise, resolve, reject } }
const setup = () => { setActivePinia(createPinia()); return useAuditoriaStore() }

test('older responses and failures never replace a newer filter result', async () => {
  const store = setup(), first = pending(), second = pending()
  let count = 0
  const api = { get: () => (++count === 1 ? first.promise : second.promise) }
  const old = store.load(api, { module: 'usuarios' })
  const latest = store.load(api, { module: 'scripts' })
  second.resolve({ data: [{ id: '9223372036854775807' }], nextCursor: 'next' })
  await latest
  first.reject(new Error('Old request failed'))
  await old
  assert.equal(store.rows[0].id, '9223372036854775807')
  assert.equal(store.nextCursor, 'next')
  assert.equal(store.error, '')
  assert.equal(store.loading, false)
})

test('closing a detail invalidates in-flight responses', async () => {
  const store = setup(), request = pending()
  const loading = store.openDetail({ get: () => request.promise }, '9223372036854775807')
  store.closeDetail()
  request.resolve({ data: { id: '9223372036854775807', changes: [] } })
  await loading
  assert.equal(store.detailId, null)
  assert.equal(store.detail, null)
  assert.equal(store.detailLoading, false)
})

test('clearing the workspace removes protected data and ignores pending work', async () => {
  const store = setup(), list = pending(), catalog = pending()
  const loading = store.load({ get: () => list.promise }, {})
  const catalogLoading = store.loadCatalog({ get: () => catalog.promise })
  store.clear()
  list.resolve({ data: [{ id: '1', actorName: 'Private' }], nextCursor: 'private' })
  catalog.resolve({ data: { modules: ['private'], actions: [], results: [] } })
  await Promise.all([loading, catalogLoading])
  assert.deepEqual(store.rows, [])
  assert.deepEqual(store.catalog.modules, [])
  assert.equal(store.nextCursor, null)
})

test('request filters and opaque cursor are encoded and failures do not retain old data', async () => {
  const store = setup()
  let path = ''
  await store.load({ get: async p => { path = p; return { data: [{ id: '1' }], nextCursor: 'next' } } }, { entityId: 'a&b', correlationId: 'request code' }, 'opaque+/=')
  const params = new URL(path, 'https://example.test').searchParams
  assert.equal(params.get('entityId'), 'a&b')
  assert.equal(params.get('cursor'), 'opaque+/=')
  assert.equal(params.get('correlationId'), 'request code')
  assert.equal(params.get('take'), '50')
  await store.load({ get: async () => { throw new Error('Forbidden') } }, {})
  assert.deepEqual(store.rows, [])
  assert.equal(store.nextCursor, null)
  assert.equal(store.error, 'Forbidden')
})
