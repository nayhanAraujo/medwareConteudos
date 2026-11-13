import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'
import {stripTypeScriptTypes} from 'node:module'
import test from 'node:test'
const source=await readFile(new URL('../utils/studioDependencies.ts',import.meta.url),'utf8')
const code=stripTypeScriptTypes(source).replaceAll('export ','')
const analyze=new Function(`${code};return analyzeStudioRemoval`)()
const node=(key,nome,dependencies=[])=>({key,itemId:key,nome,dependencies,sectionName:'Dados do paciente',preserved:false})
const graph=[node('peso','Peso'),node('altura','Altura'),node('sc','Superfície Corporal',['peso','altura']),node('index','Volume indexado',['sc'])]
test('removal notice names direct and indirect dependents across sections',()=>{
  const block=analyze(graph,new Set(['altura','sc','index']),'deleteItem')
  assert.equal(block.removed[0].nome,'Peso')
  assert.equal(block.dependents.find(d=>d.measure.key==='sc').indirect,false)
  assert.equal(block.dependents.find(d=>d.measure.key==='index').indirect,true)
})
test('deleting all consumers together is valid',()=>assert.equal(analyze(graph,new Set(),'deleteSection'),null))
test('removing calculated measure without remaining consumers is valid',()=>assert.equal(analyze(graph,new Set(['peso','altura']),'clearSection'),null))
test('missing references do not block unrelated operations',()=>assert.equal(analyze(graph.filter(n=>n.key!=='peso'),new Set(['altura','sc']),'deleteItem'),null))
test('preserved helper reference is explained',()=>{
  const block=analyze([graph[0],{...node('helper','Função auxiliar',['peso']),preserved:true}],new Set(['helper']),'deleteItem')
  assert.equal(block.dependents[0].measure.preserved,true)
})
test('cycle in an imported dependency graph terminates',()=>{
  const cycle=[node('a','A',['b']),node('b','B',['a']),node('unused','Livre')]
  assert.equal(analyze(cycle,new Set(['a','b']),'deleteItem'),null)
})
