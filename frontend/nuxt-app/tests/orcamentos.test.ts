import test from 'node:test'
import assert from 'node:assert/strict'
import { newQuoteDraft, newQuoteItem, quoteCommands, quotePayload, quoteApproved, quoteEditable, quoteMoney } from '../utils/orcamentos.ts'
import { quoteSteps, initialQuoteStep, quoteStageActions, quoteStepBlock, type QuoteStepState } from '../utils/orcamentoSteps.ts'
import {quoteCanEmail,pendingQuoteEmailAuthorization} from '../utils/orcamentoEmail.ts'
import {canImportAdminClient,adminClientImportPayload,type AdminClientPreviewData} from '../utils/adminClients.ts'

test('Admin client import requires a live official preview and never posts editable identity or pricing',()=>{
 const now=Date.UTC(2026,9,26),preview:AdminClientPreviewData={resolution:{state:'encontrado_admin',message:'Revisar',canImport:true,localId:null,candidate:{codigoAdmin:13687,name:'Clínica',cnpj:'11222333000181',active:true}},consultationId:'11111111-1111-4111-8111-111111111111',expiresAtUtc:'2026-10-26T00:15:00Z'}
 assert.equal(canImportAdminClient(preview,now),true)
 assert.equal(canImportAdminClient({...preview,expiresAtUtc:'2026-10-25T23:59:59Z'},now),false)
 assert.equal(canImportAdminClient({...preview,resolution:{...preview.resolution,state:'conflito'}},now),false)
 assert.equal(canImportAdminClient({...preview,resolution:{...preview.resolution,candidate:{...preview.resolution.candidate!,active:false}}},now),false)
 assert.throws(()=>adminClientImportPayload(preview,'Revisado',false,now))
 assert.deepEqual(adminClientImportPayload(preview,'  Identidade conferida  ',true,now),{consultationId:preview.consultationId,confirm:true,reason:'Identidade conferida'})
})

test('imported email must be confirmed before advancing and only newest unused authorization is pending',()=>{
 const state:QuoteStepState={...readyNavigation,issues:[{severity:'impedimento',step:1,message:'Confirme a origem'}]}
 assert.equal(quoteStepBlock(1,2,state)?.step,1)
 const a={action:'email_autorizado',revision:1,reference:'email:a'}
 assert.equal(pendingQuoteEmailAuthorization([a],1),a)
 assert.equal(pendingQuoteEmailAuthorization([{action:'email_revogado',revision:1,reference:a.reference},a],1),undefined)
 assert.equal(pendingQuoteEmailAuthorization([{action:'email_iniciado',revision:1,reference:a.reference},a],1),undefined)
 assert.equal(pendingQuoteEmailAuthorization([a],2),undefined)
 const b={...a,reference:'email:b'}
 assert.equal(pendingQuoteEmailAuthorization([b,a],1),b)
})

test('email is available only for authorized current approved proposals before acceptance',()=>{
 assert.equal(quoteCanEmail('aprovado',true,false,true),true)
 assert.equal(quoteCanEmail('enviado',true,false,true),true)
 for(const status of ['rascunho','analise','em_aprovacao','devolvido','aceito','pago','encaminhado','cancelado','recusado'])assert.equal(quoteCanEmail(status,true,false,true),false)
 assert.equal(quoteCanEmail('aprovado',false,false,true),false)
 assert.equal(quoteCanEmail('aprovado',true,true,true),false)
 assert.equal(quoteCanEmail('aprovado',true,false,false),false)
})

const readyNavigation:QuoteStepState={status:'rascunho',dirty:false,hasScope:true,hasTitle:true,itemCount:1,calculationComplete:true,issues:[],analysisLoading:false,analysisDirty:false,processing:false,awaitingClassification:false,awaitingApplication:false}
test('forward steps require saved data but returning is always allowed',()=>{
 assert.match(quoteStepBlock(1,2,{...readyNavigation,dirty:true})!.reason,/Salve/)
 assert.equal(quoteStepBlock(3,2,{...readyNavigation,dirty:true}),null)
 assert.equal(quoteStepBlock(2,3,{...readyNavigation,dirty:true,editStep:3}),null)
 assert.ok(quoteStepBlock(2,4,{...readyNavigation,dirty:true,editStep:3}))
 assert.equal(quoteStepBlock(1,4,{...readyNavigation,hasScope:false})!.step,1)
 assert.equal(quoteStepBlock(1,4,{...readyNavigation,hasTitle:false})!.step,1)
})
test('AI remains optional but outstanding work cannot be skipped',()=>{
 assert.equal(quoteStepBlock(2,3,readyNavigation),null)
 for(const key of ['analysisLoading','analysisDirty','processing','awaitingClassification','awaitingApplication'] as const)assert.equal(quoteStepBlock(2,3,{...readyNavigation,[key]:true})!.step,2)
 assert.equal(quoteStepBlock(1,4,{...readyNavigation,processing:true})!.step,2)
})
test('saved blockers prevent review and approval is required before proposal',()=>{
 const issue={severity:'impedimento',step:3,message:'Preço-hora pendente'}
 assert.equal(quoteStepBlock(3,4,{...readyNavigation,issues:[issue]})!.step,3)
 assert.equal(quoteStepBlock(3,4,{...readyNavigation,issues:[{...issue,severity:'aviso'}]}),null)
 assert.equal(quoteStepBlock(4,5,{...readyNavigation,status:'em_aprovacao'})!.step,4)
 assert.equal(quoteStepBlock(4,5,{...readyNavigation,status:'aprovado'}),null)
 assert.equal(quoteStepBlock(1,5,{...readyNavigation,itemCount:0,calculationComplete:false})!.step,3)
})
import {needsGenericConfirmation} from '../utils/orcamentoTechnical.ts'

test('generic confirmation reflects all contexts and never treats inactive rules as applicable',()=>{
 const front={id:'frente-1',category:'laudos',technologyId:8,profileId:'eco',serviceIds:['layout'],description:'Ajustar layout'}
 const rule={id:'impressao',name:'Impressão',active:true,category:'laudos',technologyId:null,profileId:null,serviceIds:['layout'],instructions:'Validar impressão',checks:[]}
 const catalog={profiles:[],services:[],rules:[rule]}
 assert.equal(needsGenericConfirmation([front],catalog),false)
 assert.equal(needsGenericConfirmation([front],{...catalog,rules:[{...rule,active:false}]}),true)
 assert.equal(needsGenericConfirmation([front,{...front,id:'frente-2',serviceIds:['remover-campos']}],catalog),true)
})

test('five presentation steps never grant actions or change the business workflow', () => {
  assert.equal(quoteSteps.length,5)
  assert.equal(initialQuoteStep('rascunho'),1);assert.equal(initialQuoteStep('em_aprovacao'),4);assert.equal(initialQuoteStep('pago'),5)
  const authorActions=quoteCommands('em_aprovacao',()=>true,true)
  assert.ok(!quoteStageActions(4,authorActions).includes('aprovar'))
  assert.deepEqual(quoteStageActions(3,['aprovar','enviar']),[])
  assert.deepEqual(quoteStageActions(5,['aceitar','confirmar_pagamento','cancelar']),['aceitar','confirmar_pagamento'])
  assert.deepEqual(quoteStageActions(4,[]),[])
})

test('drafts have no fabricated price, deadline, validity or payment terms and do not share items', () => {
  const a = newQuoteDraft(), b = newQuoteDraft()
  assert.equal(a.validUntil, null); assert.equal(a.paymentTerms, ''); assert.equal(a.deadlineValidated, false)
  assert.equal(a.items[0]!.manualValue, null); assert.equal(a.items[0]!.unitPrice, null)
  a.items[0]!.description = 'Changed'; assert.equal(b.items[0]!.description, '')
  assert.equal(quoteMoney(null), 'Pendente'); assert.notEqual(quoteMoney(0), 'Pendente')
})
test('approval is hidden from commercial editors even if all permissions are granted', () => {
  assert.ok(!quoteCommands('em_aprovacao', () => true, true).includes('aprovar'))
  assert.ok(quoteCommands('em_aprovacao', () => true, false).includes('aprovar'))
  assert.ok(!quoteCommands('em_aprovacao', () => false, false).includes('aprovar'))
  assert.deepEqual(quoteCommands('em_aprovacao', () => true, false, true), [])
})
test('commercial milestones cannot skip payment or acceptance in the UI', () => {
  const can = (p: string) => p === 'registrar_marcos'
  assert.deepEqual(quoteCommands('enviado', can, false), ['aceitar', 'recusar'])
  assert.deepEqual(quoteCommands('aceito', can, false), ['confirmar_pagamento'])
  assert.deepEqual(quoteCommands('pago', can, false), ['encaminhar'])
  assert.equal(quoteEditable('aprovado'), false); assert.equal(quoteApproved('aprovado'), true)
})
test('form serialization preserves null versus zero and does not mutate the local draft', () => {
  const draft = newQuoteDraft(), item = newQuoteItem()
  Object.assign(item, { unitPrice: '', manualValue: '300.50', discountPercent: '', discountValue: 0 })
  draft.items = [item]
  const payload = quotePayload(draft)
  assert.equal(payload.items[0]!.unitPrice, null); assert.equal(payload.items[0]!.manualValue, 300.5)
  assert.equal(payload.items[0]!.discountPercent, null); assert.equal(payload.items[0]!.discountValue, 0)
  assert.equal(draft.items[0]!.manualValue, '300.50')
})
