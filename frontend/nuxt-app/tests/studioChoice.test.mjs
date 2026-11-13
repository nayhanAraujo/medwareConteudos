import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'
import { ref, computed, watch, nextTick } from 'vue'

const source = await readFile(new URL('../composables/useStudioChoice.ts', import.meta.url), 'utf8')
const code = stripTypeScriptTypes(source.replace(/^import .*$/gm, '').replace('export function', 'function'))
const prompt = {draftId:'draft',revision:4,token:'signed-question',sectionId:'section-a',candidates:[{codVariavel:1,nome:'Peso'},{codVariavel:2,nome:'Peso alternativo'}]}
function fixture() {
  const draft = ref({id:'draft',revision:4,document:{secoes:[{id:'section-a'},{id:'section-b'}]}})
  const selectedSection = ref('section-a'), calls=[]
  let fail=false, offline=false
  const api = {
    async post(path,body) {calls.push({path,body:JSON.parse(JSON.stringify(body))}); if(fail)throw new Error('Network error'); return {...draft.value,revision:5}},
    async get() {if(offline)throw new Error('Offline');return {...draft.value,revision:5}}
  }
  const deps={ref,computed,watch}
  const create=new Function(...Object.keys(deps),`${code}; return useStudioChoice;`)(...Object.values(deps))
  const state=create({draft,selectedSection,api,accept(value){draft.value=value}})
  state.receiveChoice(structuredClone(prompt))
  return {state,draft,selectedSection,calls,fail:()=>{fail=true},recover:()=>{fail=false},offline:()=>{offline=true}}
}
test('polls at the same revision and newer revisions never silently close the suggestions',async()=>{
  const f=fixture(); f.draft.value=JSON.parse(JSON.stringify(f.draft.value));await nextTick()
  assert.equal(f.state.choices.value.token,prompt.token);assert.equal(f.state.choiceStale.value,false)
  f.draft.value={...f.draft.value,revision:5};await nextTick()
  assert.equal(f.state.choices.value.token,prompt.token);assert.equal(f.state.choiceStale.value,true)
  await f.state.resolveChoice(1);assert.equal(f.calls.length,0)
})
test('suggestions do not dispatch a measure until the user explicitly chooses one',async()=>{
  const f=fixture();await nextTick();assert.equal(f.calls.length,0)
  f.selectedSection.value='section-b' // Unrelated context changes cannot silently change the modal destination.
  await f.state.resolveChoice(2)
  assert.equal(f.calls.length,1);assert.equal(f.calls[0].body.codVariavel,2);assert.equal(f.calls[0].body.sectionId,'section-a')
  assert.equal(f.state.choices.value,null);assert.equal(f.draft.value.revision,5)
})
test('a failed selection stays visible and retries exactly the same operation even after a poll',async()=>{
  const f=fixture();f.fail();await f.state.resolveChoice(1)
  assert.ok(f.state.choices.value);assert.match(f.state.choiceError.value,/Network error/)
  f.draft.value={...f.draft.value,revision:5};await nextTick();f.recover();await f.state.resolveChoice(1)
  assert.deepEqual(f.calls[0],f.calls[1]);assert.equal(f.state.choices.value,null)
})
test('late searches cannot replace the question already being reviewed',()=>{
  const f=fixture();f.state.receiveChoice({...prompt,token:'another-question'})
  assert.equal(f.state.choices.value.token,prompt.token)
})
test('explicit cancellation notifies the backend without adding a measure',async()=>{
  const f=fixture();await f.state.cancelChoice()
  assert.equal(f.calls[0].path,'/api/voice/rascunhos/draft/escolhas/cancelar');assert.deepEqual(f.calls[0].body,{token:prompt.token})
  assert.equal(f.state.choices.value,null);assert.equal(f.draft.value.revision,4)
})
test('a lost selection reply cannot be dismissed without checking the server state',async()=>{
  const f=fixture();f.fail();await f.state.resolveChoice(1);f.recover();f.offline();await f.state.cancelChoice()
  assert.ok(f.state.choices.value);assert.match(f.state.choiceError.value,/Offline/);assert.equal(f.calls.length,1)
})
test('switching drafts closes old questions and rejects foreign draft events',async()=>{
  const f=fixture();f.draft.value={...f.draft.value,id:'another-draft'};await nextTick()
  assert.equal(f.state.choices.value,null);f.state.receiveChoice(prompt);assert.equal(f.state.choices.value,null)
})
test('double clicks send one operation',async()=>{
  const f=fixture();await Promise.all([f.state.resolveChoice(1),f.state.resolveChoice(1)])
  assert.equal(f.calls.length,1)
})
