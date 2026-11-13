import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'
import {stripTypeScriptTypes} from 'node:module'
import test from 'node:test'
import {ref} from 'vue'
const source=await readFile(new URL('../composables/useApi.ts',import.meta.url),'utf8')
const code=stripTypeScriptTypes(source.replace('export function','function').replaceAll('import.meta.client','false'))
const details={removed:[{nome:'Peso'}],dependents:[{measure:{nome:'Superfície Corporal'}}]}
const deps={useRuntimeConfig:()=>({apiServerBase:''}),useAuthStore:()=>({token:'test'}),useRoute:()=>({}),useSwal:()=>({}),useState:()=>ref(false),fetch:async()=>({ok:false,status:409,json:async()=>({code:'measure_in_use',message:'Não é possível remover Peso.',details})})}
const api=new Function(...Object.keys(deps),`${code};return useApi()`)(...Object.values(deps))
for(const [name,call] of [['JSON',()=>api.post('/test',{})],['multipart',()=>api.postForm('/test',new FormData())],['download',()=>api.getBlob('/test')]])
  test(`${name} preserves structured business conflict`,async()=>{
    await assert.rejects(call,e=>e.status===409&&e.code==='measure_in_use'&&e.details===details&&e.message.includes('Peso'))
  })
