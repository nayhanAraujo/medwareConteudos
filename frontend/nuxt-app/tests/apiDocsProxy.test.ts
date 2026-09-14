import assert from 'node:assert/strict'
import { test } from 'node:test'
import { createServer } from 'node:http'
import { once } from 'node:events'
import { createApp, createRouter, defineEventHandler, toNodeListener } from 'h3'
import { handleDocsHttp } from '../server/utils/docsHttp.ts'
import {
  DocsProxyError, executeDocsOperation, getDocsConfig, getDocsContext, getDocsOpenApi,
  parseExecutionForm, readLimitedResponse, requireDocsAdmin, safeFilename,
  type DocsExecutionInput, type DocsRuntimeConfig,
} from '../server/utils/docsProxy.ts'
import { DOCS_LIMITS } from '../utils/docsPolicy.ts'
import type { OpenApiDocument } from '../types/apiDocs.ts'

const runtime: DocsRuntimeConfig = {
  docsApiBase: 'http://current.test:5080', docsSandboxBase: 'http://sandbox.test:5081',
  docsPublicBase: 'https://api.example.com', docsSandboxPublicBase: 'https://sandbox.example.com',
  docsSandboxInstanceId: 'sandbox-fixed', docsSupportUrl: 'https://support.example.com',
}
const admin = { webAuthorization: 'Bearer admin-test-token', authorization: 'Bearer partner-test-token' }
const validContext = { environment: 'Homologacao', instanceId: 'sandbox-fixed', allowWrites: true }
const input = (patch: Partial<DocsExecutionInput> = {}): DocsExecutionInput => ({ operationId: 'getItem', parameters: { path: { id: '1' } }, files: [], confirmedWrite: false, ...patch })
function spec(path = '/apiconteudos/v1/items/{id}', method = 'get', operation: Record<string, unknown> = {}): OpenApiDocument {
  return { openapi: '3.0.1', servers: [{ url: 'https://DO-NOT-FETCH.invalid' }], paths: { [path]: { [method]: { operationId: 'getItem', parameters: [{ name: 'id', in: 'path', required: true }, { name: 'q', in: 'query' }], responses: {}, ...operation } } } }
}
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'content-type': 'application/json' } })
function mock(options: { document?: OpenApiDocument; context?: unknown; role?: string; meStatus?: number; result?: () => Response; specStatus?: number } = {}) {
  const calls: { url: string; init: RequestInit }[] = []
  const fetch = (async (url: string | URL | Request, init: RequestInit = {}) => {
    calls.push({ url: String(url), init })
    const path = new URL(String(url)).pathname
    if (path === '/api/web/auth/me') return json({ success: true, user: { role: options.role ?? 'admin' } }, options.meStatus ?? 200)
    if (path.endsWith('/swagger.json')) return json(options.document ?? spec(), options.specStatus ?? 200)
    if (path === '/api/documentacao/contexto') return json(options.context ?? validContext)
    return options.result?.() ?? json({ success: true })
  }) as typeof globalThis.fetch
  return { calls, fetch }
}
function hasCode(code: string, status?: number) {
  return (error: unknown) => error instanceof DocsProxyError && error.code === code && (status === undefined || error.statusCode === status)
}

test('config público não vaza pin nem aceita bases com userinfo/query', () => {
  const config = getDocsConfig(runtime)
  assert.deepEqual(config.environments.map(item => Object.keys(item)), [['id', 'label', 'publicBaseUrl'], ['id', 'label', 'publicBaseUrl']])
  assert.ok(!JSON.stringify(config).includes('sandbox-fixed'))
  assert.throws(() => getDocsConfig({ docsApiBase: 'http://user:secret@host' }), hasCode('DOCS_CONFIG_INVALID', 503))
  assert.throws(() => getDocsConfig({ docsApiBase: 'http://host?redirect=https://evil.test' }), hasCode('DOCS_CONFIG_INVALID', 503))
})
test('admin gate ignora credencial de teste e exige role real da API atual', async () => {
  const backend = mock()
  await assert.rejects(requireDocsAdmin(runtime, { authorization: 'Bearer admin-test-token' }, backend), hasCode('DOCS_ADMIN_AUTH_REQUIRED', 401))
  assert.equal(backend.calls.length, 0)
  for (const role of ['user', 'administrador', '', 'admin, user']) await assert.rejects(requireDocsAdmin(runtime, admin, mock({ role })), hasCode('DOCS_ADMIN_REQUIRED', 403))
  await assert.rejects(requireDocsAdmin(runtime, admin, mock({ meStatus: 401 })), hasCode('DOCS_UPSTREAM_UNAUTHORIZED', 401))
  await requireDocsAdmin(runtime, admin, backend)
  assert.equal(backend.calls[0]?.url, 'http://current.test:5080/api/web/auth/me')
  assert.equal(new Headers(backend.calls[0]?.init.headers).get('authorization'), 'Bearer admin-test-token')
})
test('spec interna/web é protegida inclusive em homologação; parceiros é público', async () => {
  const backend = mock()
  await getDocsOpenApi(runtime, 'atual', 'parceiros', {}, backend)
  assert.equal(backend.calls.length, 1)
  assert.equal(backend.calls[0]?.url, 'http://current.test:5080/swagger/apiconteudos/swagger.json')
  for (const definition of ['interna', 'web'] as const) {
    await assert.rejects(getDocsOpenApi(runtime, 'homologacao', definition, {}, mock()), hasCode('DOCS_ADMIN_AUTH_REQUIRED'))
    const target = mock()
    await getDocsOpenApi(runtime, 'homologacao', definition, { ...admin, authorization: 'Bearer sandbox-admin-token' }, target)
    assert.equal(target.calls[0]?.url, 'http://current.test:5080/api/web/auth/me')
    assert.equal(new Headers(target.calls[1]?.init.headers).get('authorization'), 'Bearer sandbox-admin-token')
    assert.ok(target.calls[1]?.url.startsWith('http://sandbox.test:5081/swagger/'))
  }
})
test('contexto publica allowWrites efetivo e falha fechado para pin/origem/ambiente incorretos', async () => {
  assert.equal((await getDocsContext(runtime, 'homologacao', mock())).allowWrites, true)
  assert.equal((await getDocsContext(runtime, 'atual', mock())).allowWrites, false)
  for (const config of [{ ...runtime, docsSandboxInstanceId: '' }, { ...runtime, docsSandboxInstanceId: 'wrong' }, { ...runtime, docsSandboxBase: runtime.docsApiBase }]) assert.equal((await getDocsContext(config, 'homologacao', mock())).allowWrites, false)
  for (const context of [{ ...validContext, allowWrites: 'true' }, { ...validContext, environment: 'Production' }, { ...validContext, instanceId: 'other' }]) assert.equal((await getDocsContext(runtime, 'homologacao', mock({ context }))).allowWrites, false)
  await assert.rejects(getDocsContext(runtime, 'homologacao', mock({ context: {} })), hasCode('DOCS_CONTEXT_INVALID'))
})
test('produção permite leituras sem encaminhar gate ao destino e preserva status da API', async () => {
  const backend = mock({ result: () => json({ error: 'not found' }, 404) })
  const response = await executeDocsOperation(runtime, 'atual', 'parceiros', input(), admin, backend)
  assert.equal(response.status, 404)
  assert.equal(response.url, 'https://api.example.com/apiconteudos/v1/items/1')
  assert.deepEqual(JSON.parse(response.body), { error: 'not found' })
  const request = backend.calls.at(-1)!
  assert.equal(new Headers(request.init.headers).get('authorization'), 'Bearer partner-test-token')
  assert.equal(new Headers(request.init.headers).has('x-docs-web-authorization'), false)
  assert.equal(request.init.redirect, 'manual')
  assert.equal(request.init.credentials, 'omit')
  assert.ok(request.init.signal instanceof AbortSignal)
  assert.ok(!backend.calls.some(call => call.url.includes('DO-NOT-FETCH')))
})
test('execute interno faz admin gate mesmo com credencial de teste válida', async () => {
  const backend = mock({ document: spec('/api/v1/items/{id}') })
  await assert.rejects(executeDocsOperation(runtime, 'atual', 'interna', input(), { authorization: 'Bearer test' }, backend), hasCode('DOCS_ADMIN_AUTH_REQUIRED'))
  assert.equal(backend.calls.length, 0)
  await executeDocsOperation(runtime, 'atual', 'interna', input(), admin, backend)
  assert.equal(backend.calls[0]?.url, 'http://current.test:5080/api/web/auth/me')
})
test('writes bloqueadas em produção e exigem confirmação/pin/contexto em homologação', async () => {
  const document = spec('/apiconteudos/v1/items/{id}', 'delete')
  const production = mock({ document })
  await assert.rejects(executeDocsOperation(runtime, 'atual', 'parceiros', input({ confirmedWrite: true }), {}, production), hasCode('DOCS_OPERATION_FORBIDDEN', 403))
  assert.equal(production.calls.length, 1)
  const unconfirmed = mock({ document })
  await assert.rejects(executeDocsOperation(runtime, 'homologacao', 'parceiros', input(), {}, unconfirmed), hasCode('DOCS_WRITE_CONFIRMATION_REQUIRED', 400))
  assert.equal(unconfirmed.calls.length, 1)
  for (const context of [{ ...validContext, allowWrites: false }, { ...validContext, environment: 'Production' }, { ...validContext, instanceId: 'wrong' }]) {
    const backend = mock({ document, context })
    await assert.rejects(executeDocsOperation(runtime, 'homologacao', 'parceiros', input({ confirmedWrite: true }), {}, backend), hasCode('DOCS_SANDBOX_NOT_VERIFIED', 403))
    assert.equal(backend.calls.length, 2)
  }
  const verified = mock({ document })
  await executeDocsOperation(runtime, 'homologacao', 'parceiros', input({ confirmedWrite: true }), {}, verified)
  assert.equal(verified.calls.at(-1)?.init.method, 'DELETE')
  assert.equal(verified.calls.at(-2)?.url, 'http://sandbox.test:5081/api/documentacao/contexto')
})
test('token é a exceção POST exata e ainda valida JSON', async () => {
  const backend = mock({ document: spec('/apiconteudos/v1/token', 'post', { parameters: [], requestBody: { required: true, content: { 'application/json': { schema: { type: 'object' } } } } }) })
  await executeDocsOperation(runtime, 'atual', 'parceiros', input({ parameters: {}, body: '{"senha":"password-test"}', contentType: 'application/json' }), {}, backend)
  assert.equal(backend.calls.length, 2)
  assert.equal(backend.calls[1]?.init.method, 'POST')
  assert.equal(backend.calls[1]?.init.body, '{"senha":"password-test"}')
  await assert.rejects(executeDocsOperation(runtime, 'atual', 'parceiros', input({ parameters: {}, body: '{bad}' }), {}, backend), hasCode('DOCS_BODY_INVALID_JSON', 400))
})
test('allowlist rejeita IDs desconhecidos/duplicados, namespaces errados e marcação unsafe', async () => {
  await assert.rejects(executeDocsOperation(runtime, 'atual', 'parceiros', input({ operationId: 'https://evil.invalid' }), {}, mock()), hasCode('DOCS_OPERATION_UNKNOWN'))
  await assert.rejects(executeDocsOperation(runtime, 'atual', 'parceiros', input(), {}, mock({ document: spec('/api/web/users/{id}') })), hasCode('DOCS_OPERATION_FORBIDDEN'))
  const duplicate: OpenApiDocument = { ...spec(), paths: { ...spec().paths, ...spec('/apiconteudos/v1/other/{id}').paths } }
  await assert.rejects(executeDocsOperation(runtime, 'atual', 'parceiros', input(), {}, mock({ document: duplicate })), hasCode('DOCS_OPERATION_UNKNOWN'))
  const backend = mock({ document: spec('/apiconteudos/v1/items/{id}', 'get', { 'x-docs-safe-try': false }) })
  await assert.rejects(executeDocsOperation(runtime, 'atual', 'parceiros', input(), {}, backend), hasCode('DOCS_OPERATION_FORBIDDEN'))
  assert.equal(backend.calls.length, 1)
  await assert.rejects(getDocsOpenApi(runtime, 'https://evil.test' as any, 'parceiros', {}, mock()), hasCode('DOCS_ENVIRONMENT_UNKNOWN', 404))
  await assert.rejects(getDocsOpenApi(runtime, 'atual', '__proto__' as any, {}, mock()), hasCode('DOCS_DEFINITION_UNKNOWN', 404))
})
test('paths/query/headers não escapam da operação e credenciais não entram em parâmetros', async () => {
  for (const id of ['..', '%2f', '%252f', '../web', 'a?x=1', 'a#z', 'a\\b']) await assert.rejects(executeDocsOperation(runtime, 'atual', 'parceiros', input({ parameters: { path: { id } } }), {}, mock()), hasCode('DOCS_PATH_INVALID', 400))
  for (const name of ['Authorization', 'Cookie', 'X-Docs-Web-Authorization']) await assert.rejects(executeDocsOperation(runtime, 'atual', 'parceiros', input({ parameters: { path: { id: '1' }, headers: { [name]: 'secret' } } }), {}, mock()), hasCode('DOCS_PARAMETER_FORBIDDEN'))
  for (const name of ['Host', 'X-Forwarded-Host', 'Content-Type', 'X-Http-Method-Override']) {
    const backend = mock({ document: spec('/apiconteudos/v1/items/{id}', 'get', { parameters: [{ name: 'id', in: 'path' }, { name, in: 'header' }] }) })
    await assert.rejects(executeDocsOperation(runtime, 'atual', 'parceiros', input({ parameters: { path: { id: '1' }, headers: { [name]: 'evil.invalid' } } }), {}, backend), hasCode('DOCS_HEADER_FORBIDDEN'))
  }
  await assert.rejects(executeDocsOperation(runtime, 'atual', 'parceiros', input({ parameters: { path: { id: '1' }, query: { token: 'secret' } } }), {}, mock()), hasCode('DOCS_PARAMETER_FORBIDDEN'))
  const backend = mock()
  await executeDocsOperation(runtime, 'atual', 'parceiros', input({ parameters: { path: { id: '1' }, query: { q: 'text&admin=true#x' } } }), {}, backend)
  const url = new URL(backend.calls.at(-1)!.url)
  assert.deepEqual([...url.searchParams], [['q', 'text&admin=true#x']])
})
test('multipart valida nomes, JSON e campos duplicados; uploads são reconstruídos pelo OAS', async () => {
  const form = new FormData()
  form.append('operationId', 'getItem')
  form.append('parameters', '{"path":{"id":"1"}}')
  form.append('body', '{"title":"exemplo"}')
  form.append('ContentType', 'multipart/form-data')
  form.append('confirmedWrite', 'true')
  form.append('file:arquivo', new Blob(['sample']), '../../sample.txt')
  const parsed = await parseExecutionForm(form)
  assert.equal(parsed.files[0]?.filename, 'sample.txt')
  const backend = mock({ document: spec('/apiconteudos/v1/items/{id}', 'post', { requestBody: { required: true, content: { 'multipart/form-data': { schema: { type: 'object', required: ['title', 'arquivo'], properties: { title: { type: 'string' }, arquivo: { type: 'string', format: 'binary' } } } } } } }) })
  await executeDocsOperation(runtime, 'homologacao', 'parceiros', parsed, {}, backend)
  const request = backend.calls.at(-1)!
  assert.ok(request.init.body instanceof FormData)
  assert.equal((request.init.body as FormData).get('title'), 'exemplo')
  assert.equal(await ((request.init.body as FormData).get('arquivo') as File).text(), 'sample')
  assert.equal(new Headers(request.init.headers).has('content-type'), false)
  form.append('operationId', 'duplicate')
  await assert.rejects(parseExecutionForm(form), hasCode('DOCS_FORM_INVALID'))
  const invalid = new FormData(); invalid.set('operationId', 'x'); invalid.set('parameters', '{bad}')
  await assert.rejects(parseExecutionForm(invalid), hasCode('DOCS_PARAMETERS_INVALID'))
})
test('binário e texto preservam corpo; cabeçalhos sensíveis/redirect não são expostos', async () => {
  const binary = await executeDocsOperation(runtime, 'atual', 'parceiros', input(), {}, mock({ result: () => new Response(new Uint8Array([0, 255, 128, 42]), { headers: { 'content-type': 'application/pdf', 'content-disposition': "attachment; filename*=UTF-8''..%2Fexemplo.pdf", 'set-cookie': 'secret=token', Authorization: 'Bearer secret' } }) }))
  assert.deepEqual(binary.binary, { base64: 'AP+AKg==', filename: 'exemplo.pdf', contentType: 'application/pdf' })
  assert.equal(binary.body, '')
  assert.equal(binary.headers['set-cookie'], undefined)
  assert.equal(binary.headers.authorization, undefined)
  const text = await executeDocsOperation(runtime, 'atual', 'parceiros', input(), {}, mock({ result: () => new Response('ação', { headers: { 'content-type': 'text/plain; charset=utf-8' } }) }))
  assert.equal(text.body, 'ação')
  assert.equal(text.binary, undefined)
  await assert.rejects(executeDocsOperation(runtime, 'atual', 'parceiros', input(), {}, mock({ result: () => new Response(null, { status: 302, headers: { location: 'https://outside.invalid' } }) })), hasCode('DOCS_UPSTREAM_REDIRECT', 502))
  assert.equal(safeFilename('CON.txt'), 'resposta.bin')
})
test('limites de streaming e timeout valem também para o corpo após receber headers', async () => {
  await assert.rejects(readLimitedResponse(new Response('too long'), 3), hasCode('DOCS_RESPONSE_TOO_LARGE', 502))
  await assert.rejects(readLimitedResponse(new Response('x', { headers: { 'content-length': String(DOCS_LIMITS.responseBytes + 1) } }), DOCS_LIMITS.responseBytes), hasCode('DOCS_RESPONSE_TOO_LARGE', 502))
  const hangingFetch = (async (_url: unknown, init: RequestInit) => new Response(new ReadableStream({ start(controller) { init.signal?.addEventListener('abort', () => controller.error(new Error('aborted'))) } }))) as typeof fetch
  await assert.rejects(getDocsOpenApi(runtime, 'atual', 'parceiros', {}, { fetch: hangingFetch, timeoutMs: 20 }), hasCode('DOCS_UPSTREAM_TIMEOUT', 504))
  const offline = (async () => { throw new Error('secret URL header details') }) as typeof fetch
  await assert.rejects(getDocsOpenApi(runtime, 'atual', 'parceiros', {}, { fetch: offline }), error => hasCode('DOCS_UPSTREAM_UNAVAILABLE', 502)(error) && !String(error).includes('secret URL'))
})

test('HTTP real: multipart, JSON de erro separado, no-store, cookies falsos e limites', async t => {
  const backend = createServer((request, response) => {
    response.setHeader('content-type', 'application/json')
    if (request.url?.endsWith('/swagger.json')) response.end(JSON.stringify(spec()))
    else response.end('{"ok":true}')
  }).listen(0, '127.0.0.1')
  await once(backend, 'listening')
  const backendPort = (backend.address() as any).port
  const config = { ...runtime, docsApiBase: `http://127.0.0.1:${backendPort}` }
  const app = createApp()
  const router = createRouter()
  router.get('/api/documentacao/config', defineEventHandler(event => handleDocsHttp(event, 'config', config)))
  router.get('/api/documentacao/:environment/:definition/openapi', defineEventHandler(event => handleDocsHttp(event, 'openapi', config)))
  router.post('/api/documentacao/:environment/:definition/executar', defineEventHandler(event => handleDocsHttp(event, 'executar', config)))
  app.use(router)
  const portal = createServer(toNodeListener(app)).listen(0, '127.0.0.1')
  await once(portal, 'listening')
  t.after(() => { portal.closeAllConnections(); portal.close(); backend.closeAllConnections(); backend.close() })
  const root = `http://127.0.0.1:${(portal.address() as any).port}/api/documentacao`
  const form = new FormData(); form.set('operationId', 'getItem'); form.set('parameters', '{"path":{"id":"1"}}')
  const ok = await fetch(`${root}/atual/parceiros/executar`, { method: 'POST', body: form })
  assert.equal(ok.status, 200)
  assert.equal(ok.headers.get('cache-control'), 'no-store, private')
  assert.equal((await ok.json()).body, '{"ok":true}')
  const forged = await fetch(`${root}/atual/web/openapi`, { headers: { Cookie: 'mdw_user={"role":"admin"}; mdw_token=fake' } })
  assert.equal(forged.status, 401)
  const error = await forged.json()
  assert.equal(error.error.code, 'DOCS_ADMIN_AUTH_REQUIRED')
  assert.equal(error.statusCode, 401)
  assert.ok(!JSON.stringify(error).includes('stack'))
  const wrongType = await fetch(`${root}/atual/parceiros/executar`, { method: 'POST', body: '{}' })
  assert.equal(wrongType.status, 415)
  const invalidForm = await fetch(`${root}/atual/parceiros/executar`, { method: 'POST', body: 'not multipart', headers: { 'Content-Type': 'multipart/form-data; boundary=x' } })
  assert.equal(invalidForm.status, 400)
  const tooLarge = new FormData(); tooLarge.set('operationId', 'getItem'); tooLarge.set('file:upload', new Blob([new Uint8Array(DOCS_LIMITS.requestBytes + 1)]))
  const oversized = await fetch(`${root}/atual/parceiros/executar`, { method: 'POST', body: tooLarge })
  assert.equal(oversized.status, 413)
  const crossSite = await fetch(`${root}/atual/parceiros/executar`, { method: 'POST', body: form, headers: { 'Sec-Fetch-Site': 'cross-site' } })
  assert.equal(crossSite.status, 403)
})
