import { Buffer } from 'node:buffer'
import {
  getHeader, getRouterParam, setResponseHeader, setResponseStatus,
  type H3Event,
} from 'h3'
import type { DocsDefinitionId, DocsEnvironmentId } from '../../types/apiDocs.ts'
import { DOCS_LIMITS } from '../../utils/docsPolicy.ts'
import {
  DocsProxyError, executeDocsOperation, getDocsConfig, getDocsContext, getDocsOpenApi,
  parseExecutionForm, validateDocsRoute, type DocsRuntimeConfig,
} from './docsProxy.ts'

/** Bound bytes as they arrive, BEFORE invoking the multipart parser (including chunked uploads). */
export async function readDocsMultipart(event: H3Event): Promise<FormData> {
  const contentType = getHeader(event, 'content-type') || ''
  if (!/^multipart\/form-data\s*;/i.test(contentType) || contentType.length > 256) throw new DocsProxyError(415, 'DOCS_MULTIPART_REQUIRED', 'Envie a execução como multipart/form-data.')
  const advertised = Number(getHeader(event, 'content-length'))
  if (Number.isFinite(advertised) && advertised > DOCS_LIMITS.requestBytes) throw new DocsProxyError(413, 'DOCS_REQUEST_TOO_LARGE', 'A requisição excedeu o limite de 10 MiB.')
  const request = event.node.req
  const data = await new Promise<Buffer>((resolve, reject) => {
    const chunks: Buffer[] = []
    let size = 0
    const cleanup = () => {
      clearTimeout(timer)
      request.off('data', onData)
      request.off('end', onEnd)
      request.off('error', onError)
      request.off('aborted', onAborted)
    }
    const stop = (error: DocsProxyError) => {
      cleanup()
      // Preserve the connection long enough to send the structured 4xx response.
      setResponseHeader(event, 'Connection', 'close')
      request.resume()
      reject(error)
    }
    const onData = (chunk: Buffer | string) => {
      const buffer = Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk)
      size += buffer.length
      if (size > DOCS_LIMITS.requestBytes) return stop(new DocsProxyError(413, 'DOCS_REQUEST_TOO_LARGE', 'A requisição excedeu o limite de 10 MiB.'))
      chunks.push(buffer)
    }
    const onEnd = () => { cleanup(); resolve(Buffer.concat(chunks, size)) }
    const onError = () => stop(new DocsProxyError(400, 'DOCS_UPLOAD_INTERRUPTED', 'O envio da requisição foi interrompido.'))
    const onAborted = () => onError()
    const timer = setTimeout(() => stop(new DocsProxyError(408, 'DOCS_UPLOAD_TIMEOUT', 'O envio da requisição excedeu o tempo limite.')), DOCS_LIMITS.timeoutMs)
    request.on('data', onData)
    request.once('end', onEnd)
    request.once('error', onError)
    request.once('aborted', onAborted)
    if (request.readableEnded) onEnd()
  })
  try {
    return await new Response(new Uint8Array(data), { headers: { 'Content-Type': contentType } }).formData()
  } catch { throw new DocsProxyError(400, 'DOCS_MULTIPART_INVALID', 'O formulário multipart está malformado.') }
}

export type DocsHttpAction = 'config' | 'contexto' | 'openapi' | 'executar'
/** Errors never serialize upstream URLs, headers, bodies, causes or stack traces. */
export async function handleDocsHttp(event: H3Event, action: DocsHttpAction, config: DocsRuntimeConfig): Promise<unknown> {
  setResponseHeader(event, 'Cache-Control', 'no-store, private')
  setResponseHeader(event, 'Pragma', 'no-cache')
  setResponseHeader(event, 'X-Content-Type-Options', 'nosniff')
  setResponseHeader(event, 'Vary', 'Authorization, X-Docs-Web-Authorization')
  try {
    if (action === 'config') return getDocsConfig(config)
    const environment = getRouterParam(event, 'environment')
    const definition = getRouterParam(event, 'definition')
    validateDocsRoute(environment, action === 'contexto' ? undefined : definition || '')
    if (action === 'contexto') return await getDocsContext(config, environment as DocsEnvironmentId)
    const credentials = {
      authorization: getHeader(event, 'authorization'),
      webAuthorization: getHeader(event, 'x-docs-web-authorization'),
    }
    if (action === 'openapi') return await getDocsOpenApi(config, environment, definition as DocsDefinitionId, credentials)
    // A browser must not be able to replay state-changing form submissions cross-site.
    // CLI clients without Fetch Metadata remain supported; credentials are never cookies.
    if (getHeader(event, 'sec-fetch-site') === 'cross-site') throw new DocsProxyError(403, 'DOCS_CROSS_SITE_FORBIDDEN', 'Execuções entre sites não são permitidas.')
    const input = await parseExecutionForm(await readDocsMultipart(event))
    return await executeDocsOperation(config, environment, definition as DocsDefinitionId, input, credentials)
  } catch (error) {
    const failure = error instanceof DocsProxyError ? error : new DocsProxyError(500, 'DOCS_INTERNAL_ERROR', 'Não foi possível processar a solicitação de documentação.')
    setResponseStatus(event, failure.statusCode)
    return { statusCode: failure.statusCode, message: failure.message, error: { code: failure.code, message: failure.message } }
  }
}
