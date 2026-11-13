import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import { once } from 'node:events'
import { createServer } from 'node:http'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { createApp, defineEventHandler, toNodeListener, toWebRequest, sendWebResponse } from 'h3'

const read = relative => readFile(new URL(relative, import.meta.url), 'utf8')
const imported = source => import(`data:text/javascript;base64,${Buffer.from(stripTypeScriptTypes(source)).toString('base64')}`)
const { handleCuradoriaPilot } = await imported(await read('../server/utils/curadoriaPilot.ts'))
const { verifyCuradoriaPilotChat } = await imported(await read('../utils/curadoriaPilot.ts'))
const digest = value => createHash('sha256').update(value, 'utf8').digest('hex')
const chat = 'fixture-chat-with-unicode-á', hash = digest(chat), pin = `sha256:${hash}`
const request = (body = { chatSha256: hash }, init = {}) => new Request('http://localhost/api/curadoria/teams/piloto', {
  method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body), ...init
})

test('private pin allows only the exact digest, never returns it and fails closed without valid configuration', async () => {
  for (const [configured, candidate, status, allowed] of [
    [pin, hash, 200, true], [pin, digest('another-chat'), 200, false],
    ['', hash, 503, false], [undefined, hash, 503, false], ['sha256:*', hash, 503, false],
    [hash, hash, 503, false], [{ pin }, hash, 503, false]
  ]) {
    const response = await handleCuradoriaPilot(request({ chatSha256: candidate }), configured)
    assert.equal(response.status, status); assert.equal(response.headers.get('cache-control'), 'no-store')
    const body = await response.text(); assert.deepEqual(JSON.parse(body), { allowed })
    assert.ok(!body.includes(hash)); assert.ok(!body.includes(chat)); assert.equal(response.headers.get('set-cookie'), null)
  }
})

test('endpoint rejects raw IDs, extra fields, invalid JSON/types, query switches, wrong methods/media types and oversized/chunked bodies', async () => {
  for (const body of [null, [], 'test', {}, { chatId: chat }, { chatSha256: hash, allowed: true }, { chatSha256: hash.toUpperCase() }, { chatSha256: 'a'.repeat(63) }, { chatSha256: { value: hash } }]) {
    assert.equal((await handleCuradoriaPilot(request(body), pin)).status, 400)
  }
  assert.equal((await handleCuradoriaPilot(request({}, { body: '{broken' }), pin)).status, 400)
  assert.equal((await handleCuradoriaPilot(new Request('http://localhost/api/curadoria/teams/piloto?allowed=true', { method: 'POST' }), pin)).status, 400)
  assert.equal((await handleCuradoriaPilot(new Request('http://localhost/api/curadoria/teams/piloto'), pin)).status, 405)
  assert.equal((await handleCuradoriaPilot(request({}, { headers: { 'content-type': 'text/plain' } }), pin)).status, 415)
  assert.equal((await handleCuradoriaPilot(request({}, { body: 'x'.repeat(257) }), pin)).status, 413)
  assert.equal((await handleCuradoriaPilot(request({}, { headers: { 'content-type': 'application/json', 'content-length': '999' } }), pin)).status, 413)
  const stream = new ReadableStream({ start(c) { c.enqueue(new Uint8Array(200)); c.enqueue(new Uint8Array(100)); c.close() } })
  assert.equal((await handleCuradoriaPilot(request({}, { body: stream, duplex: 'half' }), pin)).status, 413)
})

test('browser sends only an in-memory SHA-256 to a fixed no-store endpoint, without cookies, identifier URLs or credentials', async t => {
  const calls = []
  t.mock.method(globalThis, 'fetch', async (url, init) => {
    calls.push({ url, init })
    return handleCuradoriaPilot(new Request(`http://localhost${url}`, init), pin)
  })
  assert.equal(await verifyCuradoriaPilotChat(chat), true)
  assert.equal(await verifyCuradoriaPilotChat('another-chat'), false)
  assert.equal(calls.length, 2)
  const { url, init } = calls[0]
  assert.equal(url, '/api/curadoria/teams/piloto'); assert.equal(init.method, 'POST')
  assert.equal(init.credentials, 'omit'); assert.equal(init.cache, 'no-store'); assert.equal(init.redirect, 'error')
  assert.equal(init.referrerPolicy, 'no-referrer'); assert.deepEqual(JSON.parse(init.body), { chatSha256: hash })
  assert.ok(!JSON.stringify(calls).includes(chat))
  const before = calls.length
  for (const value of ['', ' ', null, 'x'.repeat(2049)]) assert.equal(await verifyCuradoriaPilotChat(value), false)
  assert.equal(calls.length, before)
})

test('browser fails closed on unavailable server, invalid responses, redirects, exceptions and cancellation', async t => {
  for (const result of [new Response('{}', { status: 503 }), new Response('invalid'), Response.json({ allowed: 'true' }), Response.json({ allowed: false })]) {
    t.mock.method(globalThis, 'fetch', async () => result)
    assert.equal(await verifyCuradoriaPilotChat(chat), false)
    t.mock.restoreAll()
  }
  t.mock.method(globalThis, 'fetch', async () => { throw new Error('private transport failure') })
  assert.equal(await verifyCuradoriaPilotChat(chat), false)
})

test('hanging request bodies and browser requests are bounded and cancelled', async t => {
  t.mock.timers.enable({ apis: ['setTimeout'] })
  let cancelled = false
  const stream = new ReadableStream({ cancel() { cancelled = true } })
  const pending = handleCuradoriaPilot(request({}, { body: stream, duplex: 'half' }), pin)
  t.mock.timers.tick(5000)
  assert.equal((await pending).status, 400); assert.equal(cancelled, true)
  t.mock.timers.reset()
  const nativeTimer = globalThis.setTimeout
  t.mock.method(globalThis, 'setTimeout', callback => nativeTimer(callback, 30))
  let aborted = false
  t.mock.method(globalThis, 'fetch', async (_, init) => new Promise((_, reject) => {
    init.signal.addEventListener('abort', () => { aborted = true; reject(new Error('abort')) }, { once: true })
  }))
  assert.equal(await verifyCuradoriaPilotChat(chat), false); assert.equal(aborted, true)
})

test('real HTTP h3 adapter preserves validation, generic responses and no-store without exposing the private config', async () => {
  const app = createApp()
  app.use('/api/curadoria/teams/piloto', defineEventHandler(async event => sendWebResponse(event, await handleCuradoriaPilot(toWebRequest(event), pin))))
  const server = createServer(toNodeListener(app)); server.listen(0, '127.0.0.1'); await once(server, 'listening')
  try {
    const root = `http://127.0.0.1:${server.address().port}/api/curadoria/teams/piloto`
    const options = { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ chatSha256: hash }) }
    const response = await fetch(root, options)
    assert.equal(response.status, 200); assert.equal(response.headers.get('cache-control'), 'no-store')
    assert.deepEqual(await response.json(), { allowed: true })
    assert.equal((await fetch(root + '?allow=true', options)).status, 400)
    assert.equal((await fetch(root, { ...options, body: JSON.stringify({ chatId: chat }) })).status, 400)
    assert.equal((await fetch(root, { ...options, body: 'x'.repeat(300) })).status, 413)
    assert.equal((await fetch(root)).status, 405)
  } finally { server.closeAllConnections(); await new Promise(done => server.close(done)) }
})

test('Nuxt keeps pin private, client wiring is fixed and fingerprint helper never signs in or changes services', async () => {
  const config = await read('../nuxt.config.ts')
  assert.ok(config.includes("curadoriaPilotChatSha256: ''"))
  assert.ok(!config.slice(config.indexOf('public:')).includes('curadoriaPilotChatSha256'))
  assert.ok((await read('../composables/useCuradoriaTeams.ts')).includes('8000, verifyCuradoriaPilotChat'))
  const route = await read('../server/api/curadoria/teams/piloto.post.ts')
  assert.ok(route.includes('useRuntimeConfig(event).curadoriaPilotChatSha256'))
  const helper = await read('../../../tools/production/Get-CuradoriaPilotFingerprint.ps1')
  assert.ok(helper.includes(".ChatType -ne 'group'")); assert.ok(helper.includes('$chats.Count -ne 1'))
  assert.ok(helper.includes('[Text.Encoding]::UTF8.GetBytes($chatIdentifier)'))
  assert.ok(!/Connect-MgGraph|Install-Module|Set-Service|Restart-Service|Set-ItemProperty|Write-Output.*\.Id/.test(helper))
  const proxy = await read('../../../tools/production/templates/public.web.config')
  const pilotRule = proxy.match(/<rule name="Curadoria pilot check"[\s\S]*?<\/rule>/)?.[0]
  assert.ok(pilotRule?.includes('^api/curadoria/teams/piloto$'))
  assert.ok(pilotRule?.includes('http://127.0.0.1:3000/{R:0}'))
  assert.ok(proxy.indexOf(pilotRule) < proxy.indexOf('name="API and Swagger"'))
})

test('PowerShell helper accepts only a unique group in an existing permitted session', { skip: process.platform !== 'win32' }, () => {
  const result = spawnSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-File', fileURLToPath(new URL('../../../tools/production/tests/Test-CuradoriaPilotFingerprint.ps1', import.meta.url))], { encoding: 'utf8', windowsHide: true, timeout: 15000 })
  assert.equal(result.status, 0, result.stdout + result.stderr)
  assert.ok(result.stdout.includes('PASS'))
})
