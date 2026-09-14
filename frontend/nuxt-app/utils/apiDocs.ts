import type {
  DocsCodeSampleInput, DocsCodeSamples, DocsOperation, DocsParameters, OpenApiDocument,
  OpenApiOperation, OpenApiParameter, OpenApiRequestBody, OpenApiResponse, OpenApiSchema,
} from '../types/apiDocs.ts'
import { isSensitiveName } from './docsPolicy.ts'

const METHODS = ['get', 'head', 'options', 'post', 'put', 'patch', 'delete', 'trace'] as const
const own = (value: object, key: string) => Object.prototype.hasOwnProperty.call(value, key)
const record = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)

/** Local JSON pointers only: external references are never fetched. */
export function resolveLocalRef<T>(spec: OpenApiDocument, value: T, seen = new Set<string>()): T {
  if (!record(value) || typeof value.$ref !== 'string') return value
  const ref = value.$ref
  if (!ref.startsWith('#/') || seen.has(ref) || seen.size >= 32) return {} as T
  let target: unknown = spec
  for (const part of ref.slice(2).split('/').map(part => part.replace(/~1/g, '/').replace(/~0/g, '~'))) {
    if (!record(target) || !own(target, part) || ['__proto__', 'constructor', 'prototype'].includes(part)) return {} as T
    target = target[part]
  }
  if (!record(target)) return {} as T
  const { $ref: _ref, ...siblings } = value
  return { ...resolveLocalRef(spec, target, new Set([...seen, ref])), ...siblings } as T
}

/** Resolves the current schema and allOf; children stay lazy to preserve recursive schemas. */
export function resolveSchema(spec: OpenApiDocument, schema?: OpenApiSchema, depth = 0): OpenApiSchema {
  if (!schema || depth > 24) return {}
  const resolved = resolveLocalRef(spec, schema)
  if (!resolved.allOf?.length) return resolved
  const { allOf, ...rest } = resolved
  const members = [...allOf.map(item => resolveSchema(spec, item, depth + 1)), rest]
  const properties = Object.assign({}, ...members.map(item => item.properties || {}))
  const required = [...new Set(members.flatMap(item => item.required || []))]
  return { ...Object.assign({}, ...members), ...(Object.keys(properties).length ? { properties } : {}), ...(required.length ? { required } : {}) }
}

/** Stable fallback IDs support existing Swashbuckle specs without operationId. */
export function flattenOperations(spec: OpenApiDocument): DocsOperation[] {
  const operations: DocsOperation[] = []
  for (const [path, pathItem] of Object.entries(spec.paths || {})) {
    if (!record(pathItem)) continue
    for (const method of METHODS) {
      const raw = pathItem[method] as OpenApiOperation | undefined
      if (!record(raw)) continue
      const inherited = (pathItem.parameters || []).map(parameter => resolveLocalRef(spec, parameter))
      const parameters = new Map<string, OpenApiParameter>()
      for (const parameter of [...inherited, ...(raw.parameters || []).map(parameter => resolveLocalRef(spec, parameter))]) {
        if (parameter.name && parameter.in) parameters.set(`${parameter.in}:${parameter.name}`, parameter)
      }
      const id = raw.operationId || `${method.toUpperCase()} ${path}`
      const tags = raw.tags?.length ? raw.tags : ['Geral']
      operations.push({
        ...raw, id, method, path, tag: tags[0]!, tags, summary: raw.summary || raw.operationId || `${method.toUpperCase()} ${path}`,
        parameters: [...parameters.values()],
        requestBody: raw.requestBody ? resolveLocalRef<OpenApiRequestBody>(spec, raw.requestBody) : undefined,
        responses: Object.fromEntries(Object.entries(raw.responses || {}).map(([status, response]) => [status, resolveLocalRef<OpenApiResponse>(spec, response)])),
        security: raw.security ?? spec.security,
        'x-docs-safe-try': raw['x-docs-safe-try'] === false || pathItem['x-docs-safe-try'] === false ? false : raw['x-docs-safe-try'],
        raw,
      })
    }
  }
  return operations
}

export function redactExample(value: unknown, name = '', depth = 0): unknown {
  if (isSensitiveName(name)) return 'SEU_SEGREDO_AQUI'
  if (depth > 20) return null
  if (Array.isArray(value)) return value.map(item => redactExample(item, '', depth + 1))
  if (record(value)) return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, redactExample(item, key, depth + 1)]))
  if (typeof value === 'string') return value.replace(/Bearer\s+[^\s"'<>]+/gi, 'Bearer SEU_TOKEN_AQUI').replace(/\beyJ[\w-]+\.[\w-]+\.[\w-]+\b/g, 'SEU_TOKEN_AQUI')
  return value
}

export function exampleForSchema(spec: OpenApiDocument, schema?: OpenApiSchema, depth = 0, name = ''): unknown {
  if (isSensitiveName(name)) return 'SEU_SEGREDO_AQUI'
  if (!schema || depth > 8) return null
  const resolved = resolveSchema(spec, schema)
  if (resolved.writeOnly || resolved.format === 'password') return 'SEU_SEGREDO_AQUI'
  if (resolved.example !== undefined) return redactExample(resolved.example, name)
  if (resolved.examples?.length) return redactExample(resolved.examples[0], name)
  if (resolved.default !== undefined) return redactExample(resolved.default, name)
  if (resolved.enum?.length) return redactExample(resolved.enum[0], name)
  const alternatives = resolved.oneOf || resolved.anyOf
  if (alternatives?.length) return exampleForSchema(spec, alternatives[0], depth + 1, name)
  const type = Array.isArray(resolved.type) ? resolved.type.find(type => type !== 'null') : resolved.type
  if (type === 'object' || resolved.properties) {
    return Object.fromEntries(Object.entries(resolved.properties || {}).filter(([, property]) => !resolveSchema(spec, property).readOnly)
      .map(([key, property]) => [key, exampleForSchema(spec, property, depth + 1, key)]))
  }
  if (type === 'array') return [exampleForSchema(spec, resolved.items, depth + 1)]
  if (type === 'integer' || type === 'number') return typeof resolved.minimum === 'number' ? resolved.minimum : 1
  if (type === 'boolean') return true
  if (type === 'null') return null
  const formats: Record<string, string> = { date: '2026-01-01', 'date-time': '2026-01-01T12:00:00Z', uuid: '00000000-0000-4000-8000-000000000001', email: 'usuario@example.com', binary: 'ARQUIVO_EXEMPLO', byte: 'QVJRVUlWT19FWEVNUExP' }
  return formats[resolved.format || ''] || 'exemplo'
}

export function encodePathParameter(value: unknown): string {
  if (!['string', 'number', 'boolean'].includes(typeof value)) throw new Error('Parâmetro de caminho deve ser escalar.')
  const text = String(value)
  // Reject nested encodings too: upstream frameworks may decode more than once.
  if (!text || /[\\/%?#\u0000-\u001f\u007f]/.test(text) || text === '.' || text === '..') throw new Error('Parâmetro de caminho inseguro.')
  return encodeURIComponent(text).replace(/[!'()*]/g, character => `%${character.charCodeAt(0).toString(16).toUpperCase()}`)
}

/** Uses a trusted base, never OAS servers, and never interpolates query strings. */
export function buildOperationUrl(baseUrl: string, operation: Pick<DocsOperation, 'path' | 'parameters'>, parameters: DocsParameters = {}): string {
  const base = new URL(baseUrl)
  if (!['http:', 'https:'].includes(base.protocol) || base.username || base.password || base.search || base.hash) throw new Error('Base da API inválida.')
  if (!/^\/(?!\/)/.test(operation.path) || /[\\%?#\u0000-\u001f\u007f]/.test(operation.path) || operation.path.split('/').some(segment => segment === '.' || segment === '..')) throw new Error('Caminho de operação inseguro.')
  const path = operation.path.replace(/\{([^{}]+)\}/g, (_match, key: string) => {
    if (!own(parameters.path || {}, key)) throw new Error('Parâmetro de caminho obrigatório ausente.')
    return encodePathParameter(parameters.path![key])
  })
  if (/[{}]/.test(path)) throw new Error('Caminho de operação inválido.')
  const target = new URL(`${base.href.replace(/\/$/, '')}${path}`)
  for (const [key, value] of Object.entries(parameters.query || {})) {
    if (value === undefined || value === null) continue
    const definition = operation.parameters.find(parameter => parameter.in === 'query' && parameter.name === key)
    if (Array.isArray(value)) {
      const delimiter = definition?.style === 'spaceDelimited' ? ' ' : definition?.style === 'pipeDelimited' ? '|' : ','
      if (definition?.explode === false || ['spaceDelimited', 'pipeDelimited'].includes(definition?.style || '')) target.searchParams.append(key, value.map(String).join(delimiter))
      else value.forEach(item => target.searchParams.append(key, String(item)))
    } else if (record(value)) {
      if (definition?.style !== 'deepObject') throw new Error('Objeto de query exige style deepObject.')
      Object.entries(value).forEach(([child, item]) => target.searchParams.append(`${key}[${child}]`, String(item)))
    } else target.searchParams.append(key, String(value))
  }
  return target.href
}

const shellQuote = (value: string) => `'${value.replace(/'/g, `'"'"'`)}'`
// JSON string literals are valid Python strings; JSON booleans/null are handled via json.loads.
const literal = (value: unknown) => JSON.stringify(value).replace(/\u2028/g, '\\u2028').replace(/\u2029/g, '\\u2029')

export function buildCodeSamples(operation: DocsOperation, parameters: DocsParameters, body: unknown, baseUrl: string, contentType?: string): DocsCodeSamples
export function buildCodeSamples(operation: DocsOperation, options?: DocsCodeSampleInput): DocsCodeSamples
export function buildCodeSamples(operation: DocsOperation, input: DocsParameters | DocsCodeSampleInput = {}, body?: unknown, baseUrl?: string, contentType?: string): DocsCodeSamples {
  const options: DocsCodeSampleInput = baseUrl !== undefined ? { parameters: input as DocsParameters, body, publicBaseUrl: baseUrl, contentType } : input as DocsCodeSampleInput
  const parameters = redactExample(options.parameters || {}) as DocsParameters
  const pathParameters = { ...parameters.path }
  for (const match of operation.path.matchAll(/\{([^{}]+)\}/g)) if (!own(pathParameters, match[1]!)) pathParameters[match[1]!] = 'VALOR_EXEMPLO'
  const url = buildOperationUrl(options.publicBaseUrl || 'https://api.example.com', operation, { ...parameters, path: pathParameters })
  const method = operation.method.toUpperCase()
  const selectedType = options.contentType || Object.keys(operation.requestBody?.content || {})[0] || 'application/json'
  const multipart = selectedType === 'multipart/form-data'
  const binary = !multipart && /^(?:application\/(?:octet-stream|pdf|zip)|image\/|audio\/|video\/)/i.test(selectedType)
  const headers: Record<string, string> = {}
  for (const [key, value] of Object.entries(parameters.headers || {})) {
    if (!/^[\w!#$%&'*+.^`|~-]+$/.test(key) || /[\r\n]/.test(String(value)) || /^(?:cookie|host|content-length|content-type|x-docs-web-authorization)$/i.test(key)) continue
    headers[key] = isSensitiveName(key) ? (key.toLowerCase() === 'authorization' ? 'Bearer SEU_TOKEN_AQUI' : 'SEU_SEGREDO_AQUI') : String(value)
  }
  if (operation.security?.some(requirement => Object.keys(requirement).length) && !Object.keys(headers).some(key => key.toLowerCase() === 'authorization')) headers.Authorization = 'Bearer SEU_TOKEN_AQUI'
  let safeBody: unknown = options.body
  if (typeof safeBody === 'string') { try { safeBody = JSON.parse(safeBody) } catch { /* plain text */ } }
  safeBody = redactExample(safeBody)
  const hasBody = !['GET', 'HEAD'].includes(method) && (safeBody !== undefined || multipart || binary)
  const bodyText = typeof safeBody === 'string' ? safeBody : JSON.stringify(safeBody ?? {})
  const curl = ['curl --globoff', `  --request ${method}`, `  --url ${shellQuote(url)}`]
  const js = [`const url = ${literal(url)};`, `const headers = ${JSON.stringify(headers, null, 2)};`]
  const py = ['import json', 'import requests', '', `url = ${literal(url)}`, `headers = json.loads(${literal(JSON.stringify(headers))})`]
  let jsBody = ''
  let pyBody = ''
  if (hasBody && multipart) {
    const schema = operation.requestBody?.content?.[selectedType]?.schema
    const inferredFiles = Object.entries(schema?.properties || {}).filter(([, property]) => property.format === 'binary' || property.items?.format === 'binary').map(([key]) => key)
    const fileFields = [...new Set([...(options.files || []), ...inferredFiles])]
    const fields = record(safeBody) ? Object.entries(safeBody).filter(([key]) => !fileFields.includes(key)) : []
    js.push('const form = new FormData();')
    for (const [key, value] of fields) {
      const values = Array.isArray(value) ? value : [value]
      for (const value of values) {
        const text = typeof value === 'object' ? JSON.stringify(value) : String(value)
        curl.push(`  --form-string ${shellQuote(`${key}=${text}`)}`)
        js.push(`form.append(${literal(key)}, ${literal(text)});`)
      }
    }
    const pyFields = fields.flatMap(([key, value]) => (Array.isArray(value) ? value : [value]).map(item => [key, typeof item === 'object' ? JSON.stringify(item) : String(item)]))
    py.push(`fields = json.loads(${literal(JSON.stringify(pyFields))})`)
    if (fileFields.length) {
      py.unshift('from contextlib import ExitStack')
      py.push('with ExitStack() as stack:', '    files = []')
    }
    for (const [index, key] of fileFields.entries()) {
      // curl's form field grammar has its own quoting in addition to shell quoting.
      const formName = `"${key.replace(/\\/g, '\\\\').replace(/"/g, '\\"')}"`
      curl.push(`  --form ${shellQuote(`${formName}=@./arquivo-exemplo.bin`)}`)
      js.push(`const file${index + 1} = new File([new Uint8Array([0])], "arquivo-exemplo.bin", { type: "application/octet-stream" });`)
      js.push(`form.append(${literal(key)}, file${index + 1});`)
      py.push(`    files.append((${literal(key)}, ("arquivo-exemplo.bin", stack.enter_context(open("./arquivo-exemplo.bin", "rb")), "application/octet-stream")))`)
    }
    jsBody = ', body: form'
    pyBody = `, data=fields${fileFields.length ? ', files=files' : ''}`
    py.push(`${fileFields.length ? '    ' : ''}response = requests.request(${literal(method)}, url, headers=headers${pyBody}, timeout=20, allow_redirects=False)`)
  } else if (hasBody) {
    headers['Content-Type'] = selectedType
    if (binary) {
      curl.push('  --data-binary @./arquivo-exemplo.bin')
      js.push('const body = new Uint8Array([0]); // Substitua pelos bytes do arquivo de exemplo.')
      py.push('with open("./arquivo-exemplo.bin", "rb") as body:', `    response = requests.request(${literal(method)}, url, headers=headers, data=body, timeout=20, allow_redirects=False)`)
    } else if (selectedType === 'application/x-www-form-urlencoded') {
      const form = new URLSearchParams()
      if (record(safeBody)) for (const [key, value] of Object.entries(safeBody)) form.append(key, String(value))
      curl.push(`  --data-raw ${shellQuote(form.toString())}`)
      js.push(`const body = ${literal(form.toString())};`)
      py.push(`response = requests.request(${literal(method)}, url, headers=headers, data=${literal(form.toString())}, timeout=20, allow_redirects=False)`)
    } else {
      curl.push(`  --data-raw ${shellQuote(bodyText)}`)
      js.push(`const body = ${literal(bodyText)};`)
      py.push(`response = requests.request(${literal(method)}, url, headers=headers, data=${literal(bodyText)}.encode("utf-8"), timeout=20, allow_redirects=False)`)
    }
    jsBody = ', body'
  } else py.push(`response = requests.request(${literal(method)}, url, headers=headers, timeout=20, allow_redirects=False)`)
  // Headers are materialized after selecting the body type; multipart boundaries are automatic.
  js[1] = `const headers = ${JSON.stringify(headers, null, 2)};`
  const pyHeader = py.findIndex(line => line.startsWith('headers = '))
  py[pyHeader] = `headers = json.loads(${literal(JSON.stringify(headers))})`
  for (const [key, value] of Object.entries(headers)) curl.push(`  --header ${shellQuote(`${key}: ${value}`)}`)
  const binaryResponse = Object.values(operation.responses).some(response => Object.entries(response.content || {}).some(([type, media]) => media.schema?.format === 'binary' || /application\/(?:octet-stream|pdf|zip)|^(?:image|audio|video)\//i.test(type)))
  js.push(`const response = await fetch(url, { method: ${literal(method)}, headers${jsBody}, redirect: "error", signal: AbortSignal.timeout(20000) });`)
  if (binaryResponse) {
    curl.push('  --output resposta.bin')
    js.push('const result = await response.blob(); // Faça o download do Blob conforme a interface.')
    py.push('with open("resposta.bin", "wb") as output:', '    output.write(response.content)')
  } else {
    js.push('console.log(response.status, await response.text());')
    py.push('print(response.status_code, response.text)')
  }
  return { curl: curl.join(' \\\n'), javascript: js.join('\n'), python: py.join('\n') }
}
