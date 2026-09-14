import { Buffer } from 'node:buffer'
import type {
  DocsConfig, DocsContext, DocsDefinitionId, DocsEnvironmentId, DocsExecutionResult,
  DocsOperation, DocsParameters, OpenApiDocument,
} from '../../types/apiDocs.ts'
import { buildOperationUrl, flattenOperations, resolveSchema } from '../../utils/apiDocs.ts'
import {
  DOCS_DEFINITIONS, DOCS_LIMITS, isDefinitionPath, isDeniedOperation, isDocsDefinition, isDocsEnvironment,
  isSensitiveName, isWriteOperation, operationPolicy, requiresDocsAdmin,
} from '../../utils/docsPolicy.ts'

export interface DocsRuntimeConfig {
  docsApiBase?: unknown
  docsSandboxBase?: unknown
  docsPublicBase?: unknown
  docsSandboxPublicBase?: unknown
  docsSupportUrl?: unknown
  docsSandboxInstanceId?: unknown
}
export interface DocsCredentials { authorization?: string; webAuthorization?: string }
export interface DocsUpload { name: string; filename: string; contentType: string; data: Uint8Array }
export interface DocsExecutionInput {
  operationId: string
  parameters: DocsParameters
  body?: string
  files: DocsUpload[]
  confirmedWrite: boolean
  contentType?: string
}
export interface DocsProxyDependencies {
  fetch?: typeof globalThis.fetch
  /** Dependency injection for deterministic tests; never accepted from the client. */
  timeoutMs?: number
}
export class DocsProxyError extends Error {
  statusCode: number
  code: string
  constructor(statusCode: number, code: string, message: string) {
    super(message)
    this.name = 'DocsProxyError'
    this.statusCode = statusCode
    this.code = code
  }
}
const fail = (status: number, code: string, message: string): never => { throw new DocsProxyError(status, code, message) }
const isRecord = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)
const own = (value: object, key: string) => Object.prototype.hasOwnProperty.call(value, key)
const configText = (value: unknown, fallback = '') => typeof value === 'string' && value.trim() ? value.trim() : fallback

function configuredBase(value: unknown, fallback: string): string {
  try {
    const url = new URL(configText(value, fallback))
    if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.search || url.hash || /[%\\]/.test(url.pathname)) throw new Error()
    return url.href.replace(/\/$/, '')
  } catch { return fail(503, 'DOCS_CONFIG_INVALID', 'A base da API de documentação não está configurada corretamente.') }
}
function apiBase(config: DocsRuntimeConfig, environment: DocsEnvironmentId): string {
  return environment === 'atual' ? configuredBase(config.docsApiBase, 'http://localhost:5080') : configuredBase(config.docsSandboxBase, 'http://localhost:5081')
}
function publicBase(config: DocsRuntimeConfig, environment: DocsEnvironmentId): string {
  return configuredBase(environment === 'atual' ? config.docsPublicBase : config.docsSandboxPublicBase, apiBase(config, environment))
}
export function validateDocsRoute(environment: unknown, definition?: unknown): asserts environment is DocsEnvironmentId {
  if (!isDocsEnvironment(environment)) fail(404, 'DOCS_ENVIRONMENT_UNKNOWN', 'Ambiente de documentação não encontrado.')
  if (definition !== undefined && !isDocsDefinition(definition)) fail(404, 'DOCS_DEFINITION_UNKNOWN', 'Definição de documentação não encontrada.')
}
export function getDocsConfig(config: DocsRuntimeConfig): DocsConfig {
  const support = configText(config.docsSupportUrl)
  if (support) {
    try {
      const url = new URL(support)
      if (!['http:', 'https:', 'mailto:'].includes(url.protocol) || url.username || url.password) throw new Error()
    } catch { fail(503, 'DOCS_CONFIG_INVALID', 'O endereço de suporte não está configurado corretamente.') }
  }
  return { environments: [
    { id: 'atual', label: 'Ambiente atual', publicBaseUrl: publicBase(config, 'atual') },
    { id: 'homologacao', label: 'Homologação', publicBaseUrl: publicBase(config, 'homologacao') },
  ], supportUrl: support }
}
export function validateBearer(value: string | undefined, required = false): string | undefined {
  if (!value) {
    if (required) fail(401, 'DOCS_ADMIN_AUTH_REQUIRED', 'Informe a credencial web de administrador no cabeçalho dedicado.')
    return undefined
  }
  if (value.length > 8192 || !/^Bearer [A-Za-z0-9\-._~+/]+=*$/i.test(value)) fail(400, 'DOCS_AUTH_INVALID', 'A credencial deve usar o formato Bearer no cabeçalho.')
  return value
}

export async function readLimitedResponse(response: Response, maxBytes: number): Promise<Uint8Array> {
  const advertised = Number(response.headers.get('content-length'))
  if (Number.isFinite(advertised) && advertised > maxBytes) {
    await response.body?.cancel()
    return fail(502, 'DOCS_RESPONSE_TOO_LARGE', 'A resposta excedeu o limite permitido pelo portal.')
  }
  if (!response.body) return new Uint8Array()
  const reader = response.body.getReader()
  const chunks: Uint8Array[] = []
  let length = 0
  try {
    while (true) {
      const next = await reader.read()
      if (next.done) break
      length += next.value.byteLength
      if (length > maxBytes) {
        await reader.cancel()
        return fail(502, 'DOCS_RESPONSE_TOO_LARGE', 'A resposta excedeu o limite permitido pelo portal.')
      }
      chunks.push(next.value)
    }
  } finally { reader.releaseLock() }
  return Buffer.concat(chunks, length)
}

async function upstream(config: DocsProxyDependencies, url: string, init: RequestInit, maxBytes: number): Promise<{ response: Response; bytes: Uint8Array }> {
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), config.timeoutMs ?? DOCS_LIMITS.timeoutMs)
  let onAbort: () => void = () => {}
  const aborted = new Promise<never>((_resolve, reject) => {
    onAbort = () => reject(new DocsProxyError(504, 'DOCS_UPSTREAM_TIMEOUT', 'A API excedeu o tempo limite de resposta.'))
    controller.signal.addEventListener('abort', onAbort, { once: true })
  })
  try {
    return await Promise.race([aborted, (async () => {
      const response = await (config.fetch || globalThis.fetch)(url, { ...init, redirect: 'manual', signal: controller.signal, credentials: 'omit', cache: 'no-store' })
      if (response.status >= 300 && response.status < 400) {
        await response.body?.cancel()
        return fail(502, 'DOCS_UPSTREAM_REDIRECT', 'Redirecionamentos não são permitidos no proxy de documentação.')
      }
      return { response, bytes: await readLimitedResponse(response, maxBytes) }
    })()])
  } catch (error) {
    if (error instanceof DocsProxyError) throw error
    if (controller.signal.aborted) return fail(504, 'DOCS_UPSTREAM_TIMEOUT', 'A API excedeu o tempo limite de resposta.')
    return fail(502, 'DOCS_UPSTREAM_UNAVAILABLE', 'Não foi possível consultar a API selecionada.')
  } finally {
    clearTimeout(timeout)
    controller.signal.removeEventListener('abort', onAbort)
  }
}
function parseJson(bytes: Uint8Array): unknown {
  try { return JSON.parse(Buffer.from(bytes).toString('utf8')) } catch { return fail(502, 'DOCS_UPSTREAM_INVALID_JSON', 'A API retornou JSON inválido.') }
}
function metadataOk(response: Response): void {
  if (response.ok) return
  if (response.status === 401) fail(401, 'DOCS_UPSTREAM_UNAUTHORIZED', 'A API não aceitou a credencial informada.')
  if (response.status === 403) fail(403, 'DOCS_UPSTREAM_FORBIDDEN', 'A API recusou o acesso à documentação.')
  fail(502, 'DOCS_UPSTREAM_ERROR', 'Não foi possível carregar os metadados da API.')
}
export async function requireDocsAdmin(config: DocsRuntimeConfig, credentials: DocsCredentials, dependencies: DocsProxyDependencies = {}): Promise<void> {
  const authorization = validateBearer(credentials.webAuthorization, true)!
  const { response, bytes } = await upstream(dependencies, `${apiBase(config, 'atual')}/api/web/auth/me`, { headers: { Authorization: authorization, Accept: 'application/json' } }, DOCS_LIMITS.metadataBytes)
  metadataOk(response)
  const data = parseJson(bytes)
  // Only the backend's authenticated role counts. No cookie, JWT decoding, or user-supplied role.
  if (!isRecord(data) || data.success !== true || !isRecord(data.user) || typeof data.user.role !== 'string' || data.user.role.toLowerCase() !== 'admin') {
    fail(403, 'DOCS_ADMIN_REQUIRED', 'A documentação interna exige um administrador autenticado.')
  }
}
async function loadSpec(config: DocsRuntimeConfig, environment: DocsEnvironmentId, definition: DocsDefinitionId, credentials: DocsCredentials, dependencies: DocsProxyDependencies): Promise<OpenApiDocument> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (requiresDocsAdmin(definition)) headers.Authorization = validateBearer(
    environment === 'homologacao' ? credentials.authorization : credentials.webAuthorization, true)!
  const { response, bytes } = await upstream(dependencies, `${apiBase(config, environment)}/swagger/${DOCS_DEFINITIONS[definition].swagger}/swagger.json`, { headers }, DOCS_LIMITS.specBytes)
  metadataOk(response)
  const spec = parseJson(bytes)
  if (!isRecord(spec) || typeof spec.openapi !== 'string' || !spec.openapi.startsWith('3.') || !isRecord(spec.paths)) fail(502, 'DOCS_SPEC_INVALID', 'A API não retornou uma especificação OpenAPI 3 válida.')
  return spec as OpenApiDocument
}
export async function getDocsOpenApi(config: DocsRuntimeConfig, environment: DocsEnvironmentId, definition: DocsDefinitionId, credentials: DocsCredentials = {}, dependencies: DocsProxyDependencies = {}): Promise<OpenApiDocument> {
  validateDocsRoute(environment, definition)
  if (requiresDocsAdmin(definition)) await requireDocsAdmin(config, credentials, dependencies)
  return loadSpec(config, environment, definition, credentials, dependencies)
}
async function loadContext(config: DocsRuntimeConfig, environment: DocsEnvironmentId, dependencies: DocsProxyDependencies): Promise<DocsContext> {
  const { response, bytes } = await upstream(dependencies, `${apiBase(config, environment)}/api/documentacao/contexto`, { headers: { Accept: 'application/json' } }, DOCS_LIMITS.metadataBytes)
  metadataOk(response)
  const context = parseJson(bytes)
  if (!isRecord(context) || typeof context.environment !== 'string' || typeof context.instanceId !== 'string') fail(502, 'DOCS_CONTEXT_INVALID', 'A API não retornou um contexto de documentação válido.')
  return { environment: context.environment as string, instanceId: context.instanceId as string, allowWrites: context.allowWrites === true }
}
function sandboxPinned(config: DocsRuntimeConfig, context: DocsContext): boolean {
  const expected = configText(config.docsSandboxInstanceId)
  return !!expected && context.environment === 'Homologacao' && context.allowWrites === true && context.instanceId === expected
    && new URL(apiBase(config, 'atual')).origin !== new URL(apiBase(config, 'homologacao')).origin
}
export async function getDocsContext(config: DocsRuntimeConfig, environment: DocsEnvironmentId, dependencies: DocsProxyDependencies = {}): Promise<DocsContext> {
  validateDocsRoute(environment)
  const context = await loadContext(config, environment, dependencies)
  // Effective permission, not merely the upstream switch. Fail closed for missing pins.
  return { ...context, allowWrites: environment === 'homologacao' && sandboxPinned(config, context) }
}

const blockedHeader = /^(?:authorization|proxy-authorization|cookie|set-cookie|host|connection|content-length|content-type|transfer-encoding|te|trailer|upgrade|expect|origin|referer|forwarded|x-forwarded-.*|x-original-.*|x-rewrite-.*|x-http-method.*|x-method-override|x-docs-.*|sec-.*|proxy-.*)$/i
function scalar(value: unknown): boolean { return typeof value === 'string' || typeof value === 'boolean' || (typeof value === 'number' && Number.isFinite(value)) }
function validateParameters(operation: DocsOperation, parameters: DocsParameters): Record<string, string> {
  if (!isRecord(parameters) || Object.keys(parameters).some(key => !['path', 'query', 'headers'].includes(key))) fail(400, 'DOCS_PARAMETERS_INVALID', 'Os parâmetros devem conter somente path, query e headers.')
  const headers: Record<string, string> = { Accept: 'application/json' }
  let count = 0
  for (const [group, location] of [['path', 'path'], ['query', 'query'], ['headers', 'header']] as const) {
    const values = parameters[group] ?? {}
    if (!isRecord(values)) fail(400, 'DOCS_PARAMETERS_INVALID', 'Grupo de parâmetros inválido.')
    const caseNames = new Set<string>()
    for (const [key, value] of Object.entries(values)) {
      if (++count > DOCS_LIMITS.parameters) fail(400, 'DOCS_PARAMETERS_INVALID', 'Há parâmetros demais na requisição.')
      const normalized = location === 'header' ? key.toLowerCase() : key
      if (caseNames.has(normalized)) fail(400, 'DOCS_PARAMETERS_INVALID', 'Parâmetro duplicado.')
      caseNames.add(normalized)
      if (['__proto__', 'prototype', 'constructor'].includes(key) || isSensitiveName(key)) fail(400, 'DOCS_PARAMETER_FORBIDDEN', 'Credenciais só podem ser informadas nos cabeçalhos dedicados.')
      const parameter = operation.parameters.find(item => item.in === location && (location === 'header' ? item.name.toLowerCase() === normalized : item.name === key))
      if (!parameter && !(location === 'header' && normalized === 'accept')) fail(400, 'DOCS_PARAMETER_UNKNOWN', 'Parâmetro não declarado na operação OpenAPI.')
      if (location === 'header') {
        if (blockedHeader.test(key) || !/^[!#$%&'*+.^_`|~0-9A-Za-z-]+$/.test(key) || !scalar(value) || /[\r\n\u0000]/.test(String(value)) || String(value).length > 8192) fail(400, 'DOCS_HEADER_FORBIDDEN', 'Cabeçalho não permitido no proxy.')
        headers[key] = String(value)
      } else if (value !== undefined && value !== null) {
        if (!scalar(value) && !(location === 'query' && Array.isArray(value) && value.length <= 100 && value.every(scalar))
          && !(location === 'query' && parameter?.style === 'deepObject' && isRecord(value) && Object.keys(value).length <= 100 && Object.entries(value).every(([key, item]) => !isSensitiveName(key) && scalar(item)))) {
          fail(400, 'DOCS_PARAMETERS_INVALID', 'Valor de parâmetro inválido.')
        }
        if (JSON.stringify(value).length > 8192) fail(400, 'DOCS_PARAMETERS_INVALID', 'Valor de parâmetro muito longo.')
      }
    }
    for (const parameter of operation.parameters.filter(item => item.in === location && item.required && !isSensitiveName(item.name))) {
      const key = Object.keys(values).find(key => location === 'header' ? key.toLowerCase() === parameter.name.toLowerCase() : key === parameter.name)
      if (!key || values[key] === null || values[key] === undefined || values[key] === '') fail(400, 'DOCS_PARAMETER_REQUIRED', 'Preencha os parâmetros obrigatórios da operação.')
    }
  }
  return headers
}

export function safeFilename(value: string | undefined): string {
  const name = (value || 'resposta.bin').split(/[\\/]/).pop()!.replace(/[\u0000-\u001f\u007f<>:"|?*]/g, '_').replace(/^\.+/, '').replace(/[. ]+$/, '').slice(0, 160)
  return name && !/^(?:con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i.test(name) ? name : 'resposta.bin'
}
function responseFilename(disposition: string | null): string {
  const encoded = disposition?.match(/filename\*=UTF-8''([^;]+)/i)?.[1]
  if (encoded) { try { return safeFilename(decodeURIComponent(encoded)) } catch { /* ordinary filename fallback */ } }
  return safeFilename(disposition?.match(/filename=(?:"([^"]*)"|([^;]+))/i)?.slice(1).find(Boolean)?.trim())
}
function buildBody(spec: OpenApiDocument, operation: DocsOperation, input: DocsExecutionInput, headers: Record<string, string>): BodyInit | undefined {
  const hasBody = input.body !== undefined && input.body !== ''
  if (['get', 'head'].includes(operation.method)) {
    if (hasBody || input.files.length) fail(400, 'DOCS_BODY_FORBIDDEN', 'GET e HEAD não aceitam corpo neste portal.')
    return undefined
  }
  const content = operation.requestBody?.content || {}
  if (!Object.keys(content).length) {
    if (hasBody || input.files.length) fail(400, 'DOCS_BODY_FORBIDDEN', 'A operação não declara um corpo de requisição.')
    return undefined
  }
  if (!hasBody && !input.files.length && !operation.requestBody?.required) return undefined
  const type = input.contentType || Object.keys(content)[0]!
  if (!own(content, type) || !/^[\w!#$&^_.+-]+\/[\w!#$&^_.+-]+$/.test(type)) fail(415, 'DOCS_CONTENT_TYPE_INVALID', 'Selecione um Content-Type declarado na operação.')
  const schema = resolveSchema(spec, content[type]?.schema)
  if (type === 'multipart/form-data' || type === 'application/x-www-form-urlencoded') {
    let fields: unknown = {}
    try { fields = hasBody ? JSON.parse(input.body!) : {} } catch { fail(400, 'DOCS_BODY_INVALID_JSON', 'O corpo do formulário deve ser um objeto JSON.') }
    if (!isRecord(fields)) fail(400, 'DOCS_BODY_INVALID_JSON', 'O corpo do formulário deve ser um objeto JSON.')
    const properties = schema.properties || {}
    const form = new FormData()
    const encoded = new URLSearchParams()
    const supplied = new Set<string>()
    for (const [key, value] of Object.entries(fields)) {
      if (!own(properties, key) || ['__proto__', 'constructor', 'prototype'].includes(key)) fail(400, 'DOCS_FORM_FIELD_UNKNOWN', 'Campo de formulário não declarado no OpenAPI.')
      const property = resolveSchema(spec, properties[key])
      if (property.format === 'binary' || property.items?.format === 'binary') fail(400, 'DOCS_FILE_REQUIRED', 'Envie arquivos usando campos file:.')
      supplied.add(key)
      for (const item of Array.isArray(value) ? value : [value]) {
        const text = typeof item === 'object' ? JSON.stringify(item) : String(item)
        form.append(key, text)
        encoded.append(key, text)
      }
    }
    for (const file of input.files) {
      const property = resolveSchema(spec, properties[file.name])
      if (type !== 'multipart/form-data' || !own(properties, file.name) || !(property.format === 'binary' || resolveSchema(spec, property.items).format === 'binary')) fail(400, 'DOCS_FILE_UNKNOWN', 'Arquivo não declarado na operação OpenAPI.')
      if (supplied.has(file.name) && property.type !== 'array') fail(400, 'DOCS_FILE_DUPLICATE', 'Este campo aceita somente um arquivo.')
      supplied.add(file.name)
      form.append(file.name, new Blob([new Uint8Array(file.data)], { type: file.contentType || 'application/octet-stream' }), safeFilename(file.filename))
    }
    for (const key of schema.required || []) if (!supplied.has(key)) fail(400, 'DOCS_FORM_FIELD_REQUIRED', 'Preencha os campos obrigatórios do formulário.')
    if (type === 'multipart/form-data') return form
    headers['Content-Type'] = type
    return encoded
  }
  if (input.files.length) {
    if (input.files.length !== 1 || hasBody || !(schema.format === 'binary' || /^(?:application\/(?:octet-stream|pdf|zip)|image\/|audio\/|video\/)/i.test(type))) fail(400, 'DOCS_FILES_FORBIDDEN', 'Este corpo não aceita os arquivos enviados.')
    headers['Content-Type'] = type
    return new Blob([new Uint8Array(input.files[0]!.data)], { type })
  }
  if (operation.requestBody?.required && !hasBody) fail(400, 'DOCS_BODY_REQUIRED', 'O corpo da requisição é obrigatório.')
  if (type === 'application/json' || type.endsWith('+json')) {
    try { JSON.parse(input.body || '') } catch { fail(400, 'DOCS_BODY_INVALID_JSON', 'O corpo deve conter JSON válido.') }
  }
  headers['Content-Type'] = type
  return input.body
}

export async function parseExecutionForm(form: FormData): Promise<DocsExecutionInput> {
  const fields: Record<string, string> = Object.create(null)
  const files: DocsUpload[] = []
  let size = 0
  let count = 0
  for (const [key, value] of form.entries()) {
    if (++count > 128 || key.length > 256) fail(400, 'DOCS_FORM_INVALID', 'Formulário de execução inválido.')
    if (typeof value !== 'string') {
      if (!key.startsWith('file:') || key.length === 5 || files.length >= DOCS_LIMITS.files) fail(400, 'DOCS_FILES_INVALID', 'Use no máximo dez arquivos com prefixo file:.')
      size += value.size
      if (size > DOCS_LIMITS.requestBytes) fail(413, 'DOCS_REQUEST_TOO_LARGE', 'A requisição excedeu o limite de 10 MiB.')
      files.push({ name: key.slice(5), filename: safeFilename(value.name), contentType: value.type, data: new Uint8Array(await value.arrayBuffer()) })
    } else {
      if (!['operationId', 'parameters', 'body', 'confirmedWrite', 'contentType', 'ContentType'].includes(key) || own(fields, key)) fail(400, 'DOCS_FORM_INVALID', 'Campo desconhecido ou duplicado no formulário.')
      const bytes = Buffer.byteLength(value)
      size += bytes
      if (bytes > DOCS_LIMITS.textFieldBytes || size > DOCS_LIMITS.requestBytes) fail(413, 'DOCS_REQUEST_TOO_LARGE', 'A requisição excedeu o limite de tamanho.')
      fields[key] = value
    }
  }
  if (!fields.operationId?.trim() || fields.operationId.length > 1024) fail(400, 'DOCS_OPERATION_REQUIRED', 'Informe o operationId da operação.')
  if (fields.contentType !== undefined && fields.ContentType !== undefined) fail(400, 'DOCS_FORM_INVALID', 'Informe apenas um campo contentType.')
  if (fields.confirmedWrite !== undefined && !['true', 'false'].includes(fields.confirmedWrite)) fail(400, 'DOCS_CONFIRMATION_INVALID', 'confirmedWrite deve ser true ou false.')
  let parameters: unknown = {}
  try { parameters = JSON.parse(fields.parameters || '{}') } catch { fail(400, 'DOCS_PARAMETERS_INVALID', 'Os parâmetros devem conter JSON válido.') }
  if (!isRecord(parameters)) fail(400, 'DOCS_PARAMETERS_INVALID', 'Os parâmetros devem ser um objeto JSON.')
  return { operationId: fields.operationId!, parameters: parameters as DocsParameters, body: fields.body, files, confirmedWrite: fields.confirmedWrite === 'true', contentType: fields.contentType ?? fields.ContentType }
}

export async function executeDocsOperation(config: DocsRuntimeConfig, environment: DocsEnvironmentId, definition: DocsDefinitionId, input: DocsExecutionInput, credentials: DocsCredentials = {}, dependencies: DocsProxyDependencies = {}): Promise<DocsExecutionResult> {
  validateDocsRoute(environment, definition)
  if (requiresDocsAdmin(definition)) await requireDocsAdmin(config, credentials, dependencies)
  const authorization = validateBearer(credentials.authorization)
  const spec = await loadSpec(config, environment, definition, credentials, dependencies)
  const matches = flattenOperations(spec).filter(operation => operation.id === input.operationId)
  if (matches.length !== 1) fail(400, 'DOCS_OPERATION_UNKNOWN', 'Selecione uma operação única declarada no OpenAPI deste ambiente.')
  const operation = matches[0]!
  if (!isDefinitionPath(definition, operation.path)) fail(403, 'DOCS_OPERATION_FORBIDDEN', 'A operação não pertence à definição selecionada.')
  // Denials precede context and execution requests, even when the method is GET.
  const initialPolicy = operationPolicy(operation, environment)
  if (isDeniedOperation(operation) || (!initialPolicy.allowed && environment !== 'homologacao')) fail(403, 'DOCS_OPERATION_FORBIDDEN', initialPolicy.reason)
  const headers = validateParameters(operation, input.parameters)
  if (authorization) headers.Authorization = authorization
  let url: string
  let displayUrl: string
  try {
    url = buildOperationUrl(apiBase(config, environment), operation, input.parameters)
    displayUrl = buildOperationUrl(publicBase(config, environment), operation, input.parameters)
  } catch (error) {
    if (error instanceof DocsProxyError) throw error
    return fail(400, 'DOCS_PATH_INVALID', 'Os parâmetros geram um caminho inválido ou inseguro.')
  }
  const body = buildBody(spec, operation, input, headers)
  if (isWriteOperation(operation)) {
    if (input.confirmedWrite !== true) fail(400, 'DOCS_WRITE_CONFIRMATION_REQUIRED', 'Confirme explicitamente a escrita em homologação.')
    const context = await loadContext(config, environment, dependencies)
    if (!sandboxPinned(config, context)) fail(403, 'DOCS_SANDBOX_NOT_VERIFIED', 'A instância de homologação não foi validada para escrita.')
  }
  const started = performance.now()
  const { response, bytes } = await upstream(dependencies, url, { method: operation.method.toUpperCase(), headers, body }, DOCS_LIMITS.responseBytes)
  const responseHeaders: Record<string, string> = {}
  for (const key of ['content-type', 'content-length', 'content-disposition', 'etag', 'last-modified', 'retry-after', 'x-request-id', 'x-script-download-source', 'x-script-download-suffix']) {
    const value = response.headers.get(key)
    if (value !== null) responseHeaders[key] = key === 'content-disposition' ? `attachment; filename="${responseFilename(value)}"` : value
  }
  const contentType = response.headers.get('content-type') || 'application/octet-stream'
  const textual = /^text\//i.test(contentType) || /^application\/(?:[\w.+-]*json|[\w.+-]*xml|javascript|x-www-form-urlencoded)(?:;|$)/i.test(contentType)
  const result: DocsExecutionResult = {
    status: response.status, statusText: response.statusText, headers: responseHeaders,
    durationMs: Math.round(performance.now() - started), body: textual ? Buffer.from(bytes).toString('utf8') : '', url: displayUrl,
  }
  if (!textual && bytes.length) result.binary = { base64: Buffer.from(bytes).toString('base64'), filename: responseFilename(response.headers.get('content-disposition')), contentType }
  return result
}
