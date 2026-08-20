export type ConversionFormat = 'html' | 'modoTexto'

export interface ValidationResponse {
  isValid: boolean
  errors: string[]
  warnings: string[]
}

export interface ConversionResponse {
  format: ConversionFormat
  html: string
  text: string
  sourceFileName: string
  convertedAt: string
  provider: string
  validation: ValidationResponse
}

export interface ConversionRecord {
  id: string
  format: ConversionFormat
  html: string
  text: string
  sourceFileName: string
  convertedAt: string
  provider: string
  imagePreviewUrl?: string
  validation: ValidationResponse
}

export interface ConversionHealthResponse {
  status: string
  timestamp?: string
  provider?: string
  formats?: string[]
}
