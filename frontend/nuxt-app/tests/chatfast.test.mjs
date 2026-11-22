import test from 'node:test'
import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { isPublicChatfastPath } from '../utils/chatfastPublicRoutes.ts'
import { endpoint, publish } from '../../../tools/production/chatfast/publish.mjs'
import { literal, parseSource } from '../../../tools/production/chatfast/prepare-demo.mjs'

test('only the two exact dashboard routes are public', () => {
  for (const path of ['/automacoes/chatfast/diario', '/automacoes/chatfast/semanal/']) assert.equal(isPublicChatfastPath(path), true)
  for (const path of ['/automacoes', '/automacoes/chatfast/gestao', '/automacoes/chatfast/diario/private', '/automacoes/chatfast/diario%2fgestao', '/AUTOMACOES/chatfast/diario']) assert.equal(isPublicChatfastPath(path), false)
})
test('public pages use only the whitelist API, without auth or raw HTML', async () => {
  const source = await readFile(new URL('../components/Automacoes/ChatfastDashboard.vue', import.meta.url), 'utf8')
  assert.ok(source.includes('/api/publico/chatfast/v1/relatorios/'))
  for (const forbidden of ['v-html', 'useApi()', 'useAuthStore()', 'Authorization:', 'X-Chatfast-Key', '/api/web/']) assert.equal(source.includes(forbidden), false, forbidden)
  assert.ok(source.includes("credentials: 'omit'")); assert.ok(source.includes('Não informado pelo produtor'))
})
test('adapter is dry-run by default and does not even invoke the network', async () => {
  const result = await publish({ schemaVersion: '1.0', batchId: 'test', type: 'diario', metrics: {} }, { base: 'https://example.invalid/api-dotnet', fetchImpl: () => { throw new Error('Network forbidden') } })
  assert.equal(result.dryRun, true)
  assert.equal(endpoint('https://example.invalid/api-dotnet', 'relatorios').pathname, '/api-dotnet/api/integracoes/chatfast/v1/relatorios')
  assert.throws(() => endpoint('http://example.invalid', 'relatorios', true))
  assert.throws(() => endpoint('https://key:secret@example.invalid', 'relatorios'))
})
test('adapter retries identical bytes and refuses altered-key conflicts without exposing body', async () => {
  const bodies = []; let calls = 0
  const args = { base: 'https://example.invalid', key: 'a'.repeat(64), send: true, pause: async () => {}, fetchImpl: async (_, init) => { bodies.push(init.body); calls++; return calls === 1 ? new Response(null, { status: 503 }) : Response.json({ success: true, duplicate: true }) } }
  const payload = { schemaVersion: '1.0', batchId: 'test', type: 'diario', metrics: {} }
  assert.equal((await publish(payload, args)).duplicate, true); assert.equal(bodies[0], bodies[1])
  await assert.rejects(publish(payload, { ...args, fetchImpl: async () => new Response('CONFIDENTIAL', { status: 409 }) }), e => e.message.includes('409') && !e.message.includes('CONFIDENTIAL'))
})
test('TSX extraction never executes expressions, imports, callbacks or code', () => {
  globalThis.chatfastSideEffect = false
  const source = parseSource('import x from "dangerous"; const DATA = [{name:"synthetic", value:1}]; const BAD = (globalThis.chatfastSideEffect = true); const X = [{x:2}].map(() => { throw new Error("must not run") });')
  assert.equal(source.constant('DATA')[0].value, 1)
  assert.throws(() => source.constant('BAD'))
  assert.deepEqual(source.constant('X', true), [{x:2}]); assert.equal(globalThis.chatfastSideEffect, false)
  assert.throws(() => literal({type:'ObjectExpression',properties:[{type:'ObjectProperty',key:{name:'__proto__'},value:{type:'StringLiteral',value:'unsafe'}}]}))
})
test('extended public presentation has no raw labels or private endpoints', async () => {
  const source = await readFile(new URL('../components/Automacoes/ChatfastSummary.vue', import.meta.url), 'utf8')
  for (const bad of ['v-html','details.', 'Authorization', '/api/web/']) assert.equal(source.includes(bad), false)
  assert.ok(source.includes('chatfastStatistics[stat.code]'))
  const admin = await readFile(new URL('../components/Automacoes/ChatfastPrivateReport.vue', import.meta.url), 'utf8')
  assert.equal(admin.includes('v-html'), false); assert.ok(admin.includes('não compartilhar'))
  const management = await readFile(new URL('../pages/automacoes/chatfast/gestao.vue', import.meta.url), 'utf8')
  assert.ok(management.includes('compactWorkspace: true'))
  assert.equal(management.includes('<aside'), false, 'Compact layout targets aside elements as the sidebar; detail panels must not use aside.')
})
