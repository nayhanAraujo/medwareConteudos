import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'
import { computed, ref, watch } from 'vue'

const source = await readFile(new URL('../composables/useStudioDraft.ts', import.meta.url), 'utf8')
const code = stripTypeScriptTypes(source.replace(/^import .*$/gm, '').replace('export function', 'function').replaceAll('import.meta.client', 'false'))
const document = {nome: 'Laudo', secoes: []}
function fixture() {
  const calls = []
  let server = {id:'draft', revision:0, archived:false, document, updatedAt:''}
  let failWrite = false, offline = false, commandFound = false, blocked = false
  const api = {
    async get(path) {
      if (offline) throw new Error('offline')
      if (path.includes('/comandos/')) { if (!commandFound) throw new Error('not found'); return {} }
      if (path.endsWith('/historico')) return []
      if (path.endsWith('/validacao')) return {revision:server.revision, metadataHash:'current', problems:[]}
      return server
    },
    async post(path, request) {
      calls.push({path, request})
      if (blocked) throw Object.assign(new Error('Não é possível remover Peso.'),{code:'measure_in_use',details:{removed:[{nome:'Peso'}],dependents:[{measure:{nome:'Superfície Corporal'}}]}})
      if (failWrite) throw new Error('Connection failed')
      server = {...server, revision:server.revision + 1, document:{...document, nome:'Updated'}}
      return server
    }
  }
  const deps = {ref, computed, watch, useApi:()=>api, useAuthStore:()=>({user:{codusuario:1}}), onMounted(){}, onBeforeUnmount(){}}
  const create = new Function(...Object.keys(deps), `${code}; return useStudioDraft;`)(...Object.values(deps))
  const state = create(); state.accept(server)
  return {state, calls, block:()=>{blocked=true}, failWrite:()=>{failWrite=true}, offline:()=>{offline=true}, committed:()=>{commandFound=true; server={...server,revision:1}}, advance:()=>{server={...server,revision:5}}}
}

test('commands carry revision and idempotency key, and only server success updates document', async () => {
  const f=fixture()
  await f.state.send('rename',{name:'Updated'})
  assert.equal(f.calls[0].request.expectedRevision,0)
  assert.match(f.calls[0].request.commandId,/^[0-9a-f-]{36}$/)
  assert.equal(f.state.draft.value.revision,1)
  assert.equal(f.state.pending.value,null)
})

test('failed command remains pending and does not change the confirmed draft', async () => {
  const f=fixture(); f.failWrite()
  await f.state.send('rename',{name:'Updated'})
  assert.equal(f.state.draft.value.revision,0)
  assert.ok(f.state.pending.value)
  await f.state.send('createSection',{name:'Another',column:1})
  assert.equal(f.calls.length,1)
})

test('reconnect discovers a committed command without writing it again', async () => {
  const f=fixture(); f.failWrite(); await f.state.send('rename',{name:'Updated'}); f.committed()
  await f.state.reconcile(true)
  assert.equal(f.calls.length,1)
  assert.equal(f.state.pending.value,null)
  assert.equal(f.state.draft.value.revision,1)
})

test('failed reconciliation cannot silently discard a command', async () => {
  const f=fixture(); f.failWrite(); await f.state.send('rename',{name:'Updated'}); f.offline()
  await f.state.discardPending()
  assert.ok(f.state.pending.value)
})

test('a conflicting revision prevents automatic replay', async () => {
  const f=fixture(); f.failWrite(); await f.state.send('rename',{name:'Updated'}); f.advance()
  await f.state.reconcile(true)
  assert.equal(f.calls.length,1)
  assert.equal(f.state.draft.value.revision,5)
  assert.ok(f.state.pending.value)
})

test('late voice event cannot overwrite a more recent manual revision', () => {
  const f=fixture()
  f.state.accept({...f.state.draft.value,revision:5})
  f.state.accept({...f.state.draft.value,revision:4})
  assert.equal(f.state.draft.value.revision,5)
})

test('preview is stale after a document or metadata revision change', async () => {
  const f=fixture()
  f.state.preview.value={content:'TXT',format:'txt',revision:0,metadataHash:'old'}
  await f.state.refreshDetails()
  assert.equal(f.state.outdated.value,true)
})


test('dependency rejection preserves revision, clears pending command and shows a business notice',async()=>{
  const f=fixture();f.block();await f.state.send('deleteItem',{itemId:'peso'})
  assert.equal(f.state.draft.value.revision,0)
  assert.equal(f.state.pending.value,null)
  assert.equal(f.state.blocked.value.removed[0].nome,'Peso')
  assert.equal(f.state.error.value,'')
  assert.equal(f.state.sync.value,'Rascunho preservado')
})
