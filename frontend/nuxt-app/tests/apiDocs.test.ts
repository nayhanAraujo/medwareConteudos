import assert from 'node:assert/strict'
import { test } from 'node:test'
import { spawnSync } from 'node:child_process'
import { buildCodeSamples, buildOperationUrl, exampleForSchema, flattenOperations, resolveSchema } from '../utils/apiDocs.ts'
import { isDeniedOperation, isTokenOperation, operationPolicy } from '../utils/docsPolicy.ts'
import type { DocsOperation, OpenApiDocument } from '../types/apiDocs.ts'

function operation(patch: Partial<DocsOperation> = {}): DocsOperation {
  return { id: 'getItem', method: 'get', path: '/apiconteudos/v1/items/{id}', tag: 'Items', summary: 'Consultar item', parameters: [{ name: 'id', in: 'path', required: true }], responses: {}, ...patch }
}
test('flattenOperations fornece o contrato UI e resolve herança, refs e security', () => {
  const spec: OpenApiDocument = {
    security: [{ PartnerJwt: [] }],
    components: {
      parameters: { Page: { name: 'page', in: 'query', schema: { type: 'integer' } } },
      requestBodies: { Input: { content: { 'application/json': { schema: { type: 'string' } } } } },
    },
    paths: {
      '/apiconteudos/v1/items': {
        parameters: [{ $ref: '#/components/parameters/Page' } as any],
        'x-docs-safe-try': false,
        get: { summary: 'Itens', tags: ['Itens'], parameters: [{ name: 'page', in: 'query', required: true }], responses: { '200': { description: 'OK' } } },
        post: { operationId: 'create', security: [], requestBody: { $ref: '#/components/requestBodies/Input' } },
      },
    },
  }
  const [get, post] = flattenOperations(spec)
  assert.equal(get?.id, 'GET /apiconteudos/v1/items')
  assert.equal(get?.method, 'get')
  assert.equal(get?.tag, 'Itens')
  assert.equal(get?.summary, 'Itens')
  assert.equal(get?.parameters.length, 1)
  assert.equal(get?.parameters[0]?.required, true)
  assert.deepEqual(get?.security, [{ PartnerJwt: [] }])
  assert.equal(get?.['x-docs-safe-try'], false)
  assert.equal(post?.id, 'create')
  assert.deepEqual(post?.security, [])
  assert.ok(post?.requestBody?.content?.['application/json'])
  assert.equal(post?.raw?.operationId, 'create')
})
test('resolveSchema suporta allOf e ponteiros escapados sem buscar refs externas ou protótipos', () => {
  const spec: OpenApiDocument = { components: { schemas: {
    'Base/X': { type: 'object', required: ['id'], properties: { id: { type: 'integer' } } },
    Item: { allOf: [{ $ref: '#/components/schemas/Base~1X' }, { properties: { nome: { type: 'string' } }, required: ['nome'] }] },
    Loop: { $ref: '#/components/schemas/Loop' },
  } } }
  const schema = resolveSchema(spec, { $ref: '#/components/schemas/Item' })
  assert.deepEqual(schema.required, ['id', 'nome'])
  assert.deepEqual(Object.keys(schema.properties || {}), ['id', 'nome'])
  assert.deepEqual(resolveSchema(spec, { $ref: '#/components/schemas/Loop' }), {})
  assert.deepEqual(resolveSchema(spec, { $ref: 'https://outside.invalid/spec' }), {})
  assert.deepEqual(resolveSchema(spec, { $ref: '#/constructor/prototype' }), {})
})
test('exampleForSchema termina ciclos, oculta segredos e respeita readOnly/oneOf/formats', () => {
  const spec: OpenApiDocument = { components: { schemas: { Node: { type: 'object', properties: { next: { $ref: '#/components/schemas/Node' } } } } } }
  assert.doesNotThrow(() => JSON.stringify(exampleForSchema(spec, { $ref: '#/components/schemas/Node' })))
  assert.deepEqual(exampleForSchema(spec, { type: 'object', properties: {
    id: { readOnly: true, type: 'integer' }, senha: { example: 'super-secret' }, arquivo: { type: 'string', format: 'binary' },
    opcao: { oneOf: [{ type: 'boolean' }] }, data: { type: 'string', format: 'date' },
  } }), { senha: 'SEU_SEGREDO_AQUI', arquivo: 'ARQUIVO_EXEMPLO', opcao: true, data: '2026-01-01' })
})
test('URL separa path e query, codifica Unicode e rejeita traversal/injeção/nested encoding', () => {
  const op = operation()
  assert.equal(buildOperationUrl('https://api.example.com/base', op, { path: { id: 'ação !' }, query: { busca: 'a&admin=true#x' } }), 'https://api.example.com/base/apiconteudos/v1/items/a%C3%A7%C3%A3o%20%21?busca=a%26admin%3Dtrue%23x')
  for (const id of ['..', '.', '../web', 'a/b', 'a\\b', '%2e%2e', '%252f', '?admin=true', '#fragment', '\n']) assert.throws(() => buildOperationUrl('https://api.example.com', op, { path: { id } }))
  for (const path of ['//evil.invalid/x', '/api/../admin', '/api/%2e%2e/admin', '/api/x?z=1', '/api/x#x', '/api/x\\y']) assert.throws(() => buildOperationUrl('https://api.example.com', operation({ path }), {}))
  assert.throws(() => buildOperationUrl('https://user:password@api.example.com', op, { path: { id: '1' } }))
})
test('serialização de query respeita arrays e deepObject', () => {
  const op = operation({ path: '/apiconteudos/v1/items', parameters: [
    { name: 'ids', in: 'query', explode: false }, { name: 'tags', in: 'query' }, { name: 'filter', in: 'query', style: 'deepObject' },
  ] })
  const url = new URL(buildOperationUrl('https://api.example.com', op, { query: { ids: [1, 2], tags: ['a', 'b'], filter: { nome: 'João & Ana' } } }))
  assert.equal(url.searchParams.get('ids'), '1,2')
  assert.deepEqual(url.searchParams.getAll('tags'), ['a', 'b'])
  assert.equal(url.searchParams.get('filter[nome]'), 'João & Ana')
})
test('política falha fechada e não amplia a exceção de token para paths arbitrários', () => {
  const write = operation({ method: 'post', path: '/api/web/items' })
  const context = { environment: 'Homologacao', allowWrites: true, instanceId: 'fixed-id' }
  assert.equal(operationPolicy(write, 'atual', context, 'fixed-id').allowed, false)
  assert.equal(operationPolicy(write, 'homologacao', context).allowed, false)
  assert.equal(operationPolicy(write, 'homologacao', context, 'other').allowed, false)
  assert.equal(operationPolicy(write, 'homologacao', context, 'fixed-id').requiresConfirmation, true)
  assert.equal(operationPolicy(write, 'homologacao', context, 'fixed-id').allowed, true)
  assert.equal(isTokenOperation({ method: 'post', path: '/apiconteudos/v1/token' }), true)
  assert.equal(isTokenOperation({ method: 'post', path: '/api/web/users/token' }), false)
  for (const path of ['/api/voice/sessions', '/api/conversions/health', '/api/web/assistente/publicacao', '/api/web/firebird-admin', '/api/web/agente', '/api/web/scripts/1/send-images-email', '/api/web/scripts/1/projeto-azure', '/api/web/scripts/aprovar/anything']) assert.equal(isDeniedOperation({ method: 'get', path }), true, path)
  assert.equal(isDeniedOperation({ ...write, 'x-docs-safe-try': false }), true)
})
test('amostras cURL/JS/Python escapam entradas e nunca copiam credenciais de autenticação', () => {
  const body = { title: 'D\'Ávila "texto"\n` ${globalThis.hacked = true}', senha: 'NAO-COPIAR', enabled: true, item: null }
  const samples = buildCodeSamples(operation({ method: 'post', security: [{ jwt: [] }] }), { path: { id: '1' }, headers: { Authorization: 'Bearer NAO-COPIAR', 'X-Docs-Web-Authorization': 'Bearer ADMIN-SECRETO' } }, body, 'https://api.example.com', 'application/json')
  for (const sample of Object.values(samples)) {
    assert.ok(!sample.includes('NAO-COPIAR'))
    assert.ok(!sample.includes('ADMIN-SECRETO'))
    assert.ok(sample.includes('SEU_TOKEN_AQUI'))
  }
  assert.ok(samples.curl.includes(`'"'"'`))
  const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor
  assert.doesNotThrow(() => new AsyncFunction(samples.javascript))
  const parsed = spawnSync('python', ['-X', 'utf8', '-c', 'import ast,sys; ast.parse(sys.stdin.read())'], { input: samples.python, encoding: 'utf8' })
  if (!parsed.error) assert.equal(parsed.status, 0, parsed.stderr)
})
test('amostras multipart e binárias usam placeholders e não fixam boundary', () => {
  const op = operation({ method: 'post', path: '/apiconteudos/v1/upload', parameters: [], requestBody: { content: { 'multipart/form-data': { schema: { type: 'object', properties: { file: { type: 'string', format: 'binary' }, nome: { type: 'string' } } } } } }, responses: { '200': { content: { 'application/pdf': { schema: { type: 'string', format: 'binary' } } } } } })
  const samples = buildCodeSamples(op, {}, { nome: '@nao-ler-arquivo', file: 'C:\\dados\\segredo.txt' }, 'https://api.example.com', 'multipart/form-data')
  assert.ok(samples.curl.includes('--form-string'))
  assert.ok(samples.curl.includes('--output resposta.bin'))
  for (const sample of Object.values(samples)) { assert.ok(!sample.includes('segredo.txt')); assert.ok(!sample.includes('Content-Type')) }
  assert.ok(samples.javascript.includes('new File('))
  assert.ok(samples.python.includes('ExitStack'))
  const parsed = spawnSync('python', ['-X', 'utf8', '-c', 'import ast,sys; ast.parse(sys.stdin.read())'], { input: samples.python, encoding: 'utf8' })
  if (!parsed.error) assert.equal(parsed.status, 0, parsed.stderr)
  const binary = buildCodeSamples(operation({ method: 'post' }), { path: { id: '1' } }, undefined, 'https://api.example.com', 'application/octet-stream')
  assert.ok(binary.curl.includes('--data-binary @./arquivo-exemplo.bin'))
})
