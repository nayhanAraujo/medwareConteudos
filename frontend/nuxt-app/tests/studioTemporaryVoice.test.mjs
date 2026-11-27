import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'
import {stripTypeScriptTypes} from 'node:module'
import test from 'node:test'
import {ref,computed} from 'vue'
const source=await readFile(new URL('../components/studio/TemporaryVoice.vue',import.meta.url),'utf8')
const code=stripTypeScriptTypes(source.match(/<script setup lang="ts">([\s\S]*?)<\/script>/)[1].replace(/^import .*$/gm,''))
function fixture(){
  const calls=[],events=[];let failure=false
  const store={sessionId:'work',revision:3,documentHash:'hash',camposScript:{camposScript:[]},applySession(value){this.revision=value.revision}}
  const api={async post(path,payload){calls.push({path,payload:JSON.parse(JSON.stringify(payload))});if(failure)throw new Error('Rede indisponível');store.revision=4;return {revision:4}},async get(){return {data:[]}}}
  const deps={ref,computed,useVoiceFeedback:()=>({highlighted:ref([]),message:ref(''),clear(){},show(){}}),watch:()=>{},defineProps:()=>({realtimeAvailable:true,busy:false}),defineEmits:()=>(...event)=>events.push(event),useVoiceSessionStore:()=>store,useApi:()=>api,useVoiceApi:()=>({async getSession(){return {revision:store.revision,documentHash:'hash',dependencies:[{sourceId:'2',itemId:'uuid-peso'}]}}}),onMounted(){},defineExpose(){}}
  const component=new Function(...Object.keys(deps),`${code};return {receiveChoice,includeChoice,cancelChoice,choice,choiceSection,choiceRequest,choiceStale,choiceError,pane,moveMeasure,manual,pendingManual}`)(...Object.values(deps))
  const prompt={sessionId:'work',token:'signed-question',revision:3,candidates:[{codVariavel:1,nome:'Peso'}],sections:[{id:'section',nome:'Paciente'}]}
  return {...component,calls,events,store,prompt,fail(){failure=true},recover(){failure=false}}
}
test('temporary movement retries the same command and maps imported raw ID to its canonical item',async()=>{
  const f=fixture();f.fail();await f.moveMeasure({sourceId:'2',sectionId:'other',beforeItemId:'3'})
  assert.equal(f.pendingManual.value.operation,'move')
  assert.deepEqual(f.calls[0].payload.itemIds,['uuid-peso'])
  f.recover();await f.manual(null)
  assert.deepEqual(f.calls[0].payload,f.calls[1].payload)
  assert.equal(f.pendingManual.value,null)
})
test('temporary suggestions wait for explicit selection and do not open microphone',()=>{
  const f=fixture();assert.equal(f.calls.length,0);f.receiveChoice(f.prompt)
  assert.equal(f.choice.value.token,'signed-question');assert.equal(f.calls.length,0)
})
test('later suggestions cannot replace the question under review',()=>{
  const f=fixture();f.receiveChoice(f.prompt);f.receiveChoice({...f.prompt,token:'other'})
  assert.equal(f.choice.value.token,'signed-question')
})
test('failed temporary inclusion retains exact operation for idempotent retry',async()=>{
  const f=fixture();f.receiveChoice(f.prompt);f.fail();await f.includeChoice(1)
  assert.ok(f.choice.value);assert.ok(f.choiceError.value);f.store.revision=4;f.recover();await f.includeChoice(1)
  assert.deepEqual(f.calls[0],f.calls[1]);assert.equal(f.choice.value,null)
})
test('stale temporary question cannot dispatch a fresh inclusion',async()=>{
  const f=fixture();f.receiveChoice(f.prompt);f.store.revision=4;await f.includeChoice(1)
  assert.equal(f.choiceStale.value,true);assert.equal(f.calls.length,0)
})
test('cancelling a temporary choice does not include a measure',async()=>{
  const f=fixture();f.receiveChoice(f.prompt);await f.cancelChoice()
  assert.match(f.calls[0].path,/escolhas\/cancelar$/);assert.equal(f.choice.value,null)
})
