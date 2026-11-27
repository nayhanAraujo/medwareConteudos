import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'
import {stripTypeScriptTypes} from 'node:module'
import test from 'node:test'
import {ref} from 'vue'

const source = await readFile(new URL('../utils/voiceChange.ts', import.meta.url), 'utf8')
const describe = new Function(`${stripTypeScriptTypes(source).replaceAll('export ', '')};return describeVoiceChange`)()
const section = (id, name, items) => ({id,name,items:items.map(name=>({id:name,name}))})
test('movement within a section produces readable position and highlight',()=>{
  const before = [section('a','Paciente',['Altura','Peso','Superfície Corporal'])]
  const after = [section('a','Paciente',['Altura','Superfície Corporal','Peso'])]
  const change=describe(before,after)
  assert.equal(change.ids.length,1)
  assert.match(change.message,/Paciente/)
  assert.doesNotMatch(change.message,/VR_/)
})
test('movement between sections identifies the destination and does not announce removal',()=>{
  const result=describe([section('a','Paciente',['Peso']),section('b','Dados',[])],[section('a','Paciente',[]),section('b','Dados',['Peso'])])
  assert.deepEqual(result.ids,['Peso'])
  assert.equal(result.message,'Peso agora está no início em Dados.')
})
test('an identical document from polling does not imply an added item',()=>{
  const value=[section('a','Paciente',['Peso'])]
  assert.deepEqual(describe(value,value).ids,[])
})
test('feedback highlight expires and a newer change replaces the previous timer',async()=>{
  const src=await readFile(new URL('../composables/useVoiceFeedback.ts',import.meta.url),'utf8')
  let next=0,cleanup
  const timers=new Map()
  const create=new Function('ref','setTimeout','clearTimeout','onBeforeUnmount',`${stripTypeScriptTypes(src).replace('export function','function')};return useVoiceFeedback()`)
  const state=create(ref,fn=>{timers.set(++next,fn);return next},id=>timers.delete(id),fn=>{cleanup=fn})
  state.show(['a'],'Primeira alteração')
  state.show(['b'],'Segunda alteração')
  assert.equal(timers.size,1)
  timers.get(next)()
  assert.deepEqual(state.highlighted.value,[])
  assert.equal(state.message.value,'Segunda alteração')
  cleanup()
  assert.equal(timers.size,0)
})
