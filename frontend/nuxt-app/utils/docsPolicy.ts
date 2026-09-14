import type { ApiDocsOperation, DocsContext, DocsDefinitionId, DocsEnvironmentId } from '../types/apiDocs.ts'

export const DOCS_DEFINITIONS = {
  parceiros: { id: 'parceiros', label: 'API Parceiros', swagger: 'apiconteudos', requiresAdmin: false },
  interna: { id: 'interna', label: 'API Interna', swagger: 'api-v1', requiresAdmin: true },
  web: { id: 'web', label: 'Web Admin', swagger: 'web', requiresAdmin: true },
} as const
export const DOCS_READ_METHODS = ['GET', 'HEAD', 'OPTIONS'] as const
export const DOCS_LIMITS = {
  requestBytes: 10 * 1024 * 1024,
  responseBytes: 10 * 1024 * 1024,
  specBytes: 5 * 1024 * 1024,
  metadataBytes: 64 * 1024,
  textFieldBytes: 1024 * 1024,
  files: 10,
  parameters: 100,
  timeoutMs: 20_000,
} as const

export function isDocsEnvironment(value: unknown): value is DocsEnvironmentId {
  return value === 'atual' || value === 'homologacao'
}
export function isDocsDefinition(value: unknown): value is DocsDefinitionId {
  return value === 'parceiros' || value === 'interna' || value === 'web'
}
export function requiresDocsAdmin(definition: DocsDefinitionId): boolean {
  return DOCS_DEFINITIONS[definition].requiresAdmin
}
export function isTokenOperation(operation: Pick<ApiDocsOperation, 'method' | 'path'>): boolean {
  return operation.method.toUpperCase() === 'POST' && /^\/(?:apiconteudos|api)\/v1\/token\/?$/i.test(operation.path)
}
export function isWriteOperation(operation: Pick<ApiDocsOperation, 'method' | 'path'>): boolean {
  return !DOCS_READ_METHODS.some(method => method === operation.method.toUpperCase()) && !isTokenOperation(operation)
}

/** Defense in depth: never rely solely on optional OAS annotations. */
export function isDeniedOperation(operation: Pick<ApiDocsOperation, 'method' | 'path'> & { 'x-docs-safe-try'?: boolean }): boolean {
  if (operation['x-docs-safe-try'] === false || operation.method.toUpperCase() === 'TRACE') return true
  return [
    /^\/api\/voice(?:\/|$)/i,
    /^\/api\/conversions(?:\/|$)/i,
    /\/publicacao|\/firebird|\/agente|email|enviar|notification|notificacao|azure/i,
    /(?:^|\/)(?:send-images-email|send-email|enviar-email|emails-notificacao|solicitar-aprovacao|aprovar|projeto-azure|webhooks?|smtp|openai|integracoes-externas)(?:\/|$)/i,
    /(?:^|\/)(?:forgot-password|reset-password)(?:\/|$)/i,
  ].some(pattern => pattern.test(operation.path))
}

export function isDefinitionPath(definition: DocsDefinitionId, path: string): boolean {
  if (definition === 'parceiros') return /^\/apiconteudos\/v1(?:\/|$)/i.test(path)
  if (definition === 'web') return /^\/api\/web(?:\/|$)/i.test(path)
  return /^\/api\/(?:v1|conversions|voice)(?:\/|$)/i.test(path)
}

/** The UI may show this reason, but execution ALWAYS repeats this check server-side. */
export function operationPolicy(
  operation: Pick<ApiDocsOperation, 'method' | 'path'> & { 'x-docs-safe-try'?: boolean },
  environment: DocsEnvironmentId,
  context?: DocsContext | null,
  expectedInstanceId?: string,
): { allowed: boolean; requiresConfirmation: boolean; reason: string } {
  const write = isWriteOperation(operation)
  if (isDeniedOperation(operation)) return { allowed: false, requiresConfirmation: write, reason: 'Operação indisponível para teste seguro.' }
  if (!write) return { allowed: true, requiresConfirmation: false, reason: '' }
  if (environment !== 'homologacao') return { allowed: false, requiresConfirmation: true, reason: 'Escritas só são permitidas em homologação.' }
  if (!expectedInstanceId || context?.environment !== 'Homologacao' || context.allowWrites !== true || context.instanceId !== expectedInstanceId) {
    return { allowed: false, requiresConfirmation: true, reason: 'A instância de homologação não foi validada para escrita.' }
  }
  return { allowed: true, requiresConfirmation: true, reason: '' }
}

export function isSensitiveName(name: string): boolean {
  return /authorization|cookie|senha|password|passwd|secret|token|api[-_]?key|credential|credencial/i.test(name)
}
