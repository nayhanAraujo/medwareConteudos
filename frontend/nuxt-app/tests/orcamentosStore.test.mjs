import test from 'node:test'
import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import { createPinia, setActivePinia } from 'pinia'
const source = await readFile(new URL('../stores/orcamentos.ts', import.meta.url), 'utf8')
const code = stripTypeScriptTypes(source).replace(/from ['"]pinia['"]/g, `from ${JSON.stringify(import.meta.resolve('pinia'))}`)
const { useOrcamentosStore } = await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`)
const setup = () => { setActivePinia(createPinia()); return useOrcamentosStore() }
const pending = () => { let resolve, reject; const promise = new Promise((yes,no) => {resolve=yes;reject=no}); return {promise,resolve,reject} }
test('stale detail responses cannot replace the newer quote or repopulate after leaving', async () => {
  const store=setup(),old=pending(),latest=pending();let call=0;const api={get:()=>++call===1?old.promise:latest.promise}
  const a=store.load(api,'1'),b=store.load(api,'9223372036854775807')
  latest.resolve({data:{header:{id:'9223372036854775807'}}});await b;old.reject(new Error('obsolete'));await a
  assert.equal(store.detail.header.id,'9223372036854775807');assert.equal(store.error,'')
  const leaving=pending();const request=store.load({get:()=>leaving.promise},'3');store.clear();leaving.resolve({data:{header:{id:'3'}}});await request;assert.equal(store.detail,null)
})
test('conflicting save preserves the current data and exposes the server error', async () => {
  const store=setup();store.detail={header:{id:'1',version:'7'}}
  await assert.rejects(store.save({put:async()=>{throw new Error('Versão desatualizada')}},'1','6',{items:[]}))
  assert.equal(store.detail.header.version,'7');assert.equal(store.error,'Versão desatualizada');assert.equal(store.saving,false)
})
