import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'
import { ref } from 'vue'

const source = await readFile(new URL('../composables/useRealtimeVoice.ts', import.meta.url), 'utf8')
const code = stripTypeScriptTypes(source.replace(/^import .*$/gm, '').replace('export function', 'function'))

function fixture(options) {
  const events = new Map(), calls = []
  const store = { sessionId: 'draft-1', camposScriptJson: '{"camposScript":[]}',
    updateCamposScript(json) { this.camposScriptJson = json }, addHistory() {} }
  const hub = {
    on(name, handler) { events.set(name, handler) }, onclose() {}, async start() {}, async stop() {},
    async invoke(name, value) {
      calls.push({ name, value })
      if (name === 'StartSession') events.get('SessionReady')({ sessionId: 'draft-1', revision: 0, camposScriptJson: store.camposScriptJson })
    }
  }
  const deps = {
    ref, useRuntimeConfig: () => ({ public: { apiBase: '/api-dotnet' } }), useAuthStore: () => ({ token: 'test-token' }),
    useVoiceSessionStore: () => store, onBeforeUnmount() {}, HttpTransportType: { WebSockets: 1 }, LogLevel: { None: 6 },
    HubConnectionBuilder: class { withUrl() { return this } configureLogging() { return this } build() { return hub } }
  }
  const create = new Function(...Object.keys(deps), `${code}; return useRealtimeVoice;`)(...Object.values(deps))
  return { client: create(options), events, calls, store }
}

test('choice events synchronize their draft snapshot before opening suggestions', async () => {
  const received=[]
  const f=fixture({onDraft:value=>received.push(['draft',value.revision]),onChoice:value=>received.push(['choice',value.revision])})
  await f.client.start()
  f.events.get('ChoiceRequested')({sessionId:'draft-1',revision:3,draft:{id:'draft',revision:3},token:'question'})
  assert.deepEqual(received,[['draft',3],['choice',3]])
})

test('old or foreign draft events cannot replace a newer draft', async () => {
  const f = fixture(); await f.client.start()
  const newer = '{"camposScript":[{"descricao":"Átrio esquerdo"}]}'
  f.events.get('DraftUpdated')({ sessionId: 'draft-1', revision: 2, camposScriptJson: newer })
  f.events.get('DraftUpdated')({ sessionId: 'draft-1', revision: 1, camposScriptJson: '{"camposScript":[]}' })
  f.events.get('DraftUpdated')({ sessionId: 'another-user', revision: 3, camposScriptJson: '{"camposScript":[]}' })
  assert.equal(f.store.camposScriptJson, newer)
  await f.client.stop()
})

test('save proposal never confirms itself and is invalidated by a new revision', async () => {
  const f = fixture(); await f.client.start(12)
  f.events.get('SaveProposed')({ sessionId: 'draft-1', revision: 0, proposalId: 'p1', modeloId: 12, nome: 'Laudo' })
  assert.equal(f.client.proposal.value.proposalId, 'p1')
  assert.equal(f.calls.filter(c => c.name === 'ConfirmSave').length, 0)
  f.events.get('DraftUpdated')({ sessionId: 'draft-1', revision: 1, camposScriptJson: '{"camposScript":[]}' })
  assert.equal(f.client.proposal.value, null)
  await f.client.confirmSave()
  assert.equal(f.calls.filter(c => c.name === 'ConfirmSave').length, 0)
  await f.client.stop()
})

test('explicit confirmation sends the exact proposal and revision', async () => {
  const f = fixture(); await f.client.start(12)
  f.events.get('SaveProposed')({ sessionId: 'draft-1', revision: 0, proposalId: 'p1', modeloId: 12, nome: 'Laudo' })
  await f.client.confirmSave()
  assert.deepEqual(f.calls.at(-1), { name: 'ConfirmSave', value: { sessionId: 'draft-1', revision: 0, proposalId: 'p1' } })
  await f.client.stop()
})
