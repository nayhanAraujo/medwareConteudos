/** Public contract shared by the portal and the server. No credentials belong here. */
export type DocsEnvironmentId = 'atual' | 'homologacao'
export type DocsDefinitionId = 'parceiros' | 'interna' | 'web'
export type HttpMethod = 'GET' | 'HEAD' | 'OPTIONS' | 'POST' | 'PUT' | 'PATCH' | 'DELETE' | 'TRACE'
export type JsonValue = null | boolean | number | string | JsonValue[] | { [key: string]: JsonValue }

export interface OpenApiSchema {
  $ref?: string
  type?: string | string[]
  format?: string
  title?: string
  description?: string
  nullable?: boolean
  enum?: unknown[]
  default?: unknown
  example?: unknown
  examples?: unknown[]
  properties?: Record<string, OpenApiSchema>
  required?: string[]
  items?: OpenApiSchema
  allOf?: OpenApiSchema[]
  oneOf?: OpenApiSchema[]
  anyOf?: OpenApiSchema[]
  additionalProperties?: boolean | OpenApiSchema
  readOnly?: boolean
  writeOnly?: boolean
  [key: string]: unknown
}
export interface OpenApiParameter {
  $ref?: string
  name: string
  in: 'path' | 'query' | 'header' | 'cookie'
  required?: boolean
  description?: string
  schema?: OpenApiSchema
  example?: unknown
  style?: string
  explode?: boolean
  [key: string]: unknown
}
export interface OpenApiMediaType {
  schema?: OpenApiSchema
  example?: unknown
  examples?: Record<string, unknown>
  encoding?: Record<string, unknown>
}
export interface OpenApiRequestBody {
  $ref?: string
  required?: boolean
  description?: string
  content?: Record<string, OpenApiMediaType>
}
export interface OpenApiResponse {
  $ref?: string
  description?: string
  content?: Record<string, OpenApiMediaType>
  headers?: Record<string, unknown>
}
export interface OpenApiOperation {
  operationId?: string
  summary?: string
  description?: string
  tags?: string[]
  parameters?: OpenApiParameter[]
  requestBody?: OpenApiRequestBody
  responses?: Record<string, OpenApiResponse>
  security?: Record<string, string[]>[]
  deprecated?: boolean
  'x-docs-safe-try'?: boolean
  [key: string]: unknown
}
export interface OpenApiDocument {
  openapi?: string
  info?: { title?: string; version?: string; description?: string }
  paths?: Record<string, { parameters?: OpenApiParameter[]; [key: string]: unknown }>
  components?: {
    schemas?: Record<string, OpenApiSchema>
    parameters?: Record<string, OpenApiParameter>
    requestBodies?: Record<string, OpenApiRequestBody>
    responses?: Record<string, OpenApiResponse>
    securitySchemes?: Record<string, unknown>
    [key: string]: unknown
  }
  security?: Record<string, string[]>[]
  [key: string]: unknown
}
export interface DocsOperation extends OpenApiOperation {
  id: string
  method: string
  path: string
  tag: string
  summary: string
  parameters: OpenApiParameter[]
  responses: Record<string, OpenApiResponse>
  raw?: OpenApiOperation
}
export type ApiDocsOperation = DocsOperation
export type ApiOperation = DocsOperation
export type ApiDocsSchema = OpenApiSchema
export interface DocsParameters {
  path?: Record<string, unknown>
  query?: Record<string, unknown>
  headers?: Record<string, unknown>
}
export interface DocsContext {
  environment: string
  allowWrites: boolean
  instanceId: string
}
export interface DocsConfig {
  environments: { id: DocsEnvironmentId; label: string; publicBaseUrl: string }[]
  supportUrl: string
}
export interface DocsExecutionResult {
  status: number
  statusText: string
  headers: Record<string, string>
  durationMs: number
  body: string
  binary?: { base64: string; filename: string; contentType: string }
  url: string
}
export interface DocsErrorBody {
  error: { code: string; message: string }
}
export interface DocsCodeSampleInput {
  parameters?: DocsParameters
  body?: unknown
  contentType?: string
  /** Field names, not local paths or the user's File objects. */
  files?: string[]
  publicBaseUrl?: string
}
export interface DocsCodeSamples { curl: string; javascript: string; python: string }
