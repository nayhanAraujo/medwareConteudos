import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'
import { ref } from 'vue'

const source = await readFile(new URL('../components/studio/JsonImport.vue', import.meta.url), 'utf8')
const script = source.match(/<script setup lang="ts">([\s\S]*?)<\/script>/)[1]
const code = stripTypeScriptTypes(script.replace(/^import .*$/gm, ''))
const review = {id:'review-1', document:{nome:'Modelo original',secoes:[],importedJson:{components:[]}}, associations:[],warnings:[],elementCount:52}
function fixture() {
  const events = [], requests = []
  let failure = false
  const props = {destination:'conventional',disabled:false}
  const deps = {
    ref, defineProps:()=>props, defineEmits:()=> (...args)=>events.push(args), nextTick:async()=>{}, document:{activeElement:null},
    useApi:()=>({postForm:async()=>structuredClone(review),post:async(path,body)=>{requests.push({path,body:structuredClone(body)}); if(failure)throw new Error('Connection interrupted'); return {session:{id:'new-session',isImported:true}}}})
  }
  const component = new Function(...Object.keys(deps), `${code};return {upload,confirmImport,close,review,name,error,associations};`)(...Object.values(deps))
  const file = new File(['{"camposScript":[]}'],'Modelo.json',{type:'application/json'})
  return {...component,events,requests,props,fail:()=>{failure=true},recover:()=>{failure=false},select:()=>component.upload({target:{files:[file],value:'file'}})}
}
test('analysis and cancel never replace the current work',async()=>{
  const f=fixture(); await f.select()
  assert.equal(f.review.value.elementCount,52);assert.equal(f.events.length,0)
  f.close();assert.equal(f.review.value,null);assert.equal(f.events.length,0);assert.equal(f.requests.length,0)
})
test('confirmation opens only the returned new session',async()=>{
  const f=fixture();await f.select();await f.confirmImport()
  assert.equal(f.requests[0].body.destination,'conventional')
  assert.match(f.requests[0].body.commandId,/^[0-9a-f-]{36}$/)
  assert.equal(f.events[0][0],'confirmed');assert.equal(f.events[0][1].session.id,'new-session')
  assert.equal(f.review.value,null)
})
test('uncertain confirmation retries the exact same identity and payload',async()=>{
  const f=fixture();await f.select();f.fail();await f.confirmImport()
  assert.equal(f.events.length,0);assert.ok(f.review.value)
  f.name.value='Edited after connection failure';f.associations.value={another:42};f.recover();await f.confirmImport()
  assert.deepEqual(f.requests[0],f.requests[1]);assert.equal(f.events.length,1)
})
test('disabled import cannot submit a confirmation',async()=>{
  const f=fixture();await f.select();f.props.disabled=true;await f.confirmImport()
  assert.equal(f.requests.length,0);assert.equal(f.events.length,0)
})
test('invalid file is rejected before contacting the API',async()=>{
  const f=fixture();await f.upload({target:{files:[new File(['x'],'modelo.txt')],value:''}})
  assert.equal(f.review.value,null);assert.match(f.error.value,/JSON/);assert.equal(f.events.length,0)
})
