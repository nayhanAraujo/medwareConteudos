export interface ValidationResponse {
  isValid: boolean
  errors: string[]
  warnings: string[]
}

export interface ConversionResponse {
  html: string
  sourceFileName: string
  convertedAt: string
  provider: string
  validation: ValidationResponse
}

export interface ConversionRecord {
  id: string
  html: string
  sourceFileName: string
  convertedAt: string
  provider: string
  imagePreviewUrl?: string
  validation: ValidationResponse
}
