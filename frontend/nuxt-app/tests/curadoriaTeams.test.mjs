import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'

const read = relative => readFile(new URL(relative, import.meta.url), 'utf8')
const imported = source => import(`data:text/javascript;base64,${Buffer.from(stripTypeScriptTypes(source)).toString('base64')}`)
const { createCuradoriaTeamsController, CURADORIA_TAB_CONFIG } = await imported(await read('../utils/curadoriaTeams.ts'))
const { isTeamsCuradoriaPath } = await imported(await read('../utils/curadoriaPublicRoutes.ts'))
const context = () => ({ app: { host: { name: 'TeamsModern' }, theme: 'default' }, page: { frameContext: 'settings' }, chat: { id: 'fixture-chat' } })
const deferred = () => { let resolve; const promise = new Promise(done => { resolve = done }); return { promise, resolve } }

function fixture(options = {}) {
  const calls = [], views = []
  let handler, onTheme
  const sdk = {
    app: {
      initialize: async () => { calls.push('initialize'); if (options.initError) throw new Error('private SDK failure and secret material') },
      getContext: async () => options.context || context(),
      registerOnThemeChangeHandler(callback) { onTheme = callback }
    },
    pages: { config: {
      isSupported: () => options.supported !== false,
      setValidityState(value) { calls.push(['valid', value]) },
      registerOnSaveHandler(callback) { handler = callback; calls.push('register') },
      async setConfig(value) { calls.push(['config', value]); if (options.saveError) throw new Error('private host failure'); await options.pending?.promise }
    } }
  }
  const controller = createCuradoriaTeamsController(options.kind || 'configuration', () => options.load || Promise.resolve(sdk), state => views.push(state), options.timeout || 500)
  const event = () => ({ success: 0, failures: [], notifySuccess() { this.success++; calls.push('success') }, notifyFailure(message) { this.failures.push(message); calls.push('failure') } })
  return { calls, views, controller, event, get handler() { return handler }, get onTheme() { return onTheme }, get latest() { return views.at(-1) } }
}
async function finishUntil(predicate) {
  for (let i = 0; i < 100 && !predicate(); i++) await new Promise(done => setTimeout(done, 5))
  assert.ok(predicate(), 'async condition did not complete')
}

test('initializes the SDK before configuration, requires explicit confirmation and saves only fixed HTTPS destinations', async () => {
  const f = fixture(); await f.controller.start()
  assert.equal(f.calls[0], 'initialize'); assert.equal(f.latest.mode, 'ready')
  assert.deepEqual(f.calls.filter(c => c[0] === 'valid'), [['valid', false]])
  const invalid = f.event(); f.handler(invalid)
  assert.equal(invalid.failures.length, 1); assert.equal(invalid.success, 0)
  f.controller.setConfirmed(true)
  assert.deepEqual(f.calls.at(-1), ['valid', true])
  f.controller.setConfirmed(false)
  assert.deepEqual(f.calls.at(-1), ['valid', false])
  const unchecked = f.event(); f.handler(unchecked)
  assert.equal(unchecked.success, 0); assert.equal(unchecked.failures.length, 1)
  f.controller.setConfirmed(true)
  const saved = f.event(); f.handler(saved)
  await finishUntil(() => saved.success === 1)
  assert.deepEqual(f.calls.find(c => c[0] === 'config')[1], CURADORIA_TAB_CONFIG)
  for (const key of ['contentUrl', 'websiteUrl']) {
    const url = new URL(CURADORIA_TAB_CONFIG[key]); assert.equal(url.protocol, 'https:')
    assert.equal(url.hostname, 'conteudo.medware.com.br'); assert.equal(url.search, '')
  }
  assert.equal(f.latest.mode, 'saved'); assert.ok(f.latest.message.includes('não ativa a coleta'))
  f.controller.dispose()
})

test('success is never notified before setConfig resolves and a concurrent save cannot duplicate the operation', async () => {
  const pending = deferred(); const f = fixture({ pending }); await f.controller.start(); f.controller.setConfirmed(true)
  const first = f.event(), second = f.event(); f.handler(first); f.handler(second)
  assert.equal(first.success, 0); assert.equal(f.latest.mode, 'saving'); assert.equal(second.failures.length, 1)
  assert.equal(f.calls.filter(c => c[0] === 'config').length, 1)
  pending.resolve(); await finishUntil(() => first.success === 1); f.controller.dispose()
})

test('save failure is sanitized, notifies failure only and lets the host offer a deliberate retry', async () => {
  const f = fixture({ saveError: true }); await f.controller.start(); f.controller.setConfirmed(true)
  const event = f.event(); f.handler(event); await finishUntil(() => event.failures.length === 1)
  assert.equal(event.success, 0); assert.equal(f.latest.mode, 'error')
  assert.deepEqual(f.calls.at(-2), ['valid', true])
  assert.ok(!JSON.stringify(f.views).includes('private')); f.controller.dispose()
})

test('initialization errors, unsupported capabilities/hosts/frames and concrete incompatible scopes never enable saving', async () => {
  const contexts = [
    { ...context(), app: { ...context().app, host: { name: 'Office' } } },
    { ...context(), app: { ...context().app, host: { name: '' } } },
    { ...context(), page: { frameContext: 'content' } },
    { ...context(), page: { frameContext: 'sidePanel' } },
    { ...context(), page: {} },
    ...[undefined, { id: 'fixture-chat' }].flatMap(chat => [
      { ...context(), chat, team: { internalId: 'private-team' } },
      { ...context(), chat, team: { groupId: 'private-group' } },
      { ...context(), chat, channel: { id: 'private-channel' } },
      { ...context(), chat, meeting: { id: 'private-meeting' } }
    ])
  ]
  for (const options of [{ initError: true }, { supported: false }, ...contexts.map(context => ({ context }))]) {
    const f = fixture(options); await f.controller.start(); f.controller.setConfirmed(true)
    assert.ok(['browser', 'unsupported'].includes(f.latest.mode))
    assert.equal(f.latest.canConfigure, false); assert.equal(f.handler, undefined)
    assert.ok(!f.calls.some(c => Array.isArray(c) && c[0] === 'valid' && c[1] === true))
    assert.ok(!JSON.stringify(f.views).includes('private SDK')); f.controller.dispose()
  }
})

test('settings can configure before chat ID exists and ignore empty optional scope objects, without auto-confirming', async () => {
  for (const host of ['Teams', 'TeamsModern']) {
    for (const chat of [undefined, {}, { id: '' }, { id: 'fixture-chat' }]) {
      const data = { ...context(), app: { ...context().app, host: { name: host } }, chat, team: {}, channel: { id: '' }, meeting: { id: ' ' } }
      const f = fixture({ context: data }); await f.controller.start()
      assert.equal(f.latest.mode, 'ready'); assert.equal(f.latest.canConfigure, true)
      assert.equal(f.latest.confirmed, false); assert.equal(typeof f.handler, 'function')
      assert.deepEqual(f.calls.filter(c => c[0] === 'valid'), [['valid', false]])
      f.controller.setConfirmed(true); assert.deepEqual(f.calls.at(-1), ['valid', true])
      f.controller.setConfirmed(false); assert.deepEqual(f.calls.at(-1), ['valid', false])
      f.controller.setConfirmed(true)
      const saved = f.event(); f.handler(saved); await finishUntil(() => saved.success === 1)
      assert.equal(f.latest.mode, 'saved'); f.controller.dispose()
    }
  }
})

test('diagnostic contains only allowlisted host/frame and identifier presence, including unsupported scope', async () => {
  const f = fixture({ context: { ...context(), chat: undefined, meeting: { id: 'private-meeting' } } })
  await f.controller.start()
  assert.equal(f.latest.mode, 'unsupported')
  assert.deepEqual(f.latest.diagnostic, {
    host: 'TeamsModern', frame: 'settings', configurationSupported: true,
    chatIdPresent: false, teamIdPresent: false, channelIdPresent: false, meetingIdPresent: true
  })
  assert.ok(!JSON.stringify(f.views).includes('private-meeting')); f.controller.dispose()
  const g = fixture({ context: { ...context(), app: { ...context().app, host: { name: 'private-host' } }, page: { frameContext: 'private-frame' } } })
  await g.controller.start()
  assert.equal(g.latest.diagnostic.host, 'outro'); assert.equal(g.latest.diagnostic.frame, 'outro')
  assert.ok(!JSON.stringify(g.views).includes('private-')); g.controller.dispose()
})

test('content without a chat ID or with incompatible identifiers is informational only, never configures', async () => {
  for (const extra of [{ chat: undefined }, { meeting: { id: 'fixture-meeting' } }, { team: { internalId: 'fixture-team' } }, { channel: { id: 'fixture-channel' } }]) {
    const f = fixture({ kind: 'content', context: { ...context(), ...extra, page: { frameContext: 'content' } } })
    await f.controller.start()
    assert.equal(f.latest.mode, 'unsupported'); assert.equal(f.latest.canConfigure, false)
    assert.equal(f.latest.diagnostic, null); assert.equal(f.handler, undefined)
    assert.deepEqual(f.calls, ['initialize']); f.controller.dispose()
  }
})

test('initialization and save timeouts are bounded and late completions cannot produce false success', async () => {
  const load = deferred(); const f = fixture({ load: load.promise, timeout: 15 }); await f.controller.start()
  assert.equal(f.latest.mode, 'browser'); assert.equal(f.handler, undefined)
  f.controller.dispose()
  const pending = deferred(); const g = fixture({ pending, timeout: 15 }); await g.controller.start(); g.controller.setConfirmed(true)
  const event = g.event(); g.handler(event); await finishUntil(() => event.failures.length === 1)
  pending.resolve(); await new Promise(done => setTimeout(done, 20))
  assert.equal(event.success, 0); assert.equal(event.failures.length, 1); g.controller.dispose()
})

test('closing a page invalidates pending work, disables configuration and ignores theme callbacks', async () => {
  const pending = deferred(); const f = fixture({ pending }); await f.controller.start(); f.controller.setConfirmed(true)
  const event = f.event(); f.handler(event); f.controller.dispose()
  const count = f.views.length; f.onTheme('contrast'); pending.resolve()
  await finishUntil(() => event.failures.length === 1)
  assert.equal(event.success, 0); assert.equal(f.views.length, count)
  assert.deepEqual(f.calls.filter(c => c[0] === 'valid').at(-1), ['valid', false])
})

test('informational tab adapts only a safe theme and never accesses configuration APIs', async () => {
  const f = fixture({ kind: 'content', context: { ...context(), page: { frameContext: 'content' } } })
  await f.controller.start(); assert.equal(f.latest.mode, 'ready'); assert.equal(f.latest.canConfigure, false)
  for (const value of ['dark', 'contrast', 'default']) { f.onTheme(value); assert.equal(f.latest.theme, value) }
  f.onTheme('url(https://untrusted.invalid)'); assert.equal(f.latest.theme, 'default')
  assert.ok(!f.calls.some(c => Array.isArray(c) && ['valid', 'config'].includes(c[0])))
  f.controller.dispose()
})

test('context identifiers are neither exposed as view state nor copied into tab configuration', async () => {
  const data = { ...context(), chat: { id: 'private-chat-id' }, user: { id: 'private-user-id', tenant: { id: 'private-tenant-id' } } }
  const f = fixture({ context: data }); await f.controller.start(); f.controller.setConfirmed(true)
  const event = f.event(); f.handler(event); await finishUntil(() => event.success === 1)
  const visible = JSON.stringify({ views: f.views, calls: f.calls })
  for (const id of ['private-chat-id', 'private-user-id', 'private-tenant-id']) assert.ok(!visible.includes(id))
  f.controller.dispose()
})

test('HTTP framing policy is restricted to the two exact Teams paths and official host families', async () => {
  const source = await read('../server/middleware/curadoriaTeamsHeaders.ts')
  const code = stripTypeScriptTypes(source).replace(/^import .+ from .+\r?\n/gm, '').replace('export default ', 'return ')
  const handler = new Function('defineEventHandler', 'getRequestURL', 'removeResponseHeader', 'setResponseHeader', 'isTeamsCuradoriaPath', code)(
    callback => callback, event => new URL(event.url), (e, name) => delete e.headers[name], (e, key, value) => { e.headers[key] = value }, isTeamsCuradoriaPath)
  for (const path of ['/curadoria/teams/configurar', '/curadoria/teams/aba']) {
    const event = { url: `https://conteudo.medware.com.br${path}?untrusted=value`, headers: { 'X-Frame-Options': 'DENY' } }; handler(event)
    assert.equal(event.headers['X-Frame-Options'], undefined)
    const csp = event.headers['Content-Security-Policy']
    assert.ok(csp.startsWith("frame-ancestors 'self' "))
    for (const origin of ['https://teams.microsoft.com', 'https://*.teams.microsoft.com', 'https://*.cloud.microsoft', 'https://*.microsoft365.com', 'https://*.office.com']) assert.ok(csp.includes(origin))
    assert.ok(!csp.split(' ').includes('*')); assert.ok(!csp.includes('untrusted')); assert.equal(event.headers['Cache-Control'], 'no-store')
  }
  for (const path of ['/curadoria/privacidade', '/curadoria/termos', '/curadoria/teams', '/curadoria/teams/aba/admin', '/api/web', '/biblioteca']) {
    const event = { url: `https://conteudo.medware.com.br${path}`, headers: { 'X-Frame-Options': 'DENY' } }; handler(event)
    assert.deepEqual(event.headers, { 'X-Frame-Options': 'DENY' })
  }
})
