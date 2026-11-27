import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'
import {stripTypeScriptTypes} from 'node:module'
import test from 'node:test'
import {computed,ref} from 'vue'

const source = await readFile(new URL('../components/studio/VoiceModelPreview.vue', import.meta.url), 'utf8')
const code = stripTypeScriptTypes(source.match(/<script setup lang="ts">([\s\S]*?)<\/script>/)[1].replace(/^import .*$/gm, ''))
function fixture() {
  const props = {movable:true,disabled:false,dragContext:'session-a',campos:[
    {id:1,tipo:'etiqueta',nome:'A',descricao:'Paciente',coluna:1},
    {id:2,tipo:'numero',nome:'PESO',etiquetaPai:'A',ordem:'2'},
    {id:3,tipo:'numero',nome:'ALTURA',etiquetaPai:'A',ordem:'1'}
  ]}
  const events=[]
  const setup=new Function('defineProps','defineEmits','ref','computed','onMounted','onBeforeUnmount',`${code};return {startDrag,drop,move,grouped}`)
  const view=setup(()=>props,()=>(...event)=>events.push(event),ref,computed,()=>{},()=>{})
  const data=new Map()
  const event={dataTransfer:{setData:(key,value)=>data.set(key,value),getData:key=>data.get(key)},preventDefault(){}}
  return {...view,props,events,event,data}
}
test('drop sends the source identity and target anchor without changing fields',()=>{
  const f=fixture();f.startDrag(f.event,f.props.campos[1]);f.drop(f.event,'other','3')
  assert.deepEqual(f.events,[['move',{sourceId:'2',sectionId:'other',beforeItemId:'3'}]])
  assert.equal(f.props.campos[1].etiquetaPai,'A')
})
test('foreign-session drag and disabled editing cannot dispatch movements',()=>{
  const f=fixture();f.startDrag(f.event,f.props.campos[1]);f.props.dragContext='another';f.drop(f.event,'1')
  assert.equal(f.events.length,0)
  f.props.disabled=true;f.move('2','1',null)
  assert.equal(f.events.length,0)
})
test('drop on itself is ignored, and empty destination appends',()=>{
  const f=fixture();f.move('2','1','2');assert.equal(f.events.length,0)
  f.move('2','empty',null);assert.equal(f.events[0][1].beforeItemId,null)
})
test('preview follows numeric or textual order after moving without reordering the raw JSON array',()=>{
  const f=fixture();assert.deepEqual(f.grouped.value[0].fields.map(field=>field.id),[3,2])
  assert.deepEqual(f.props.campos.map(field=>field.id),[1,2,3])
})
