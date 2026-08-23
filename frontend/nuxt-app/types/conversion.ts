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

export interface VariableMatchCandidate {
  codVariavel: number
  nome: string
  sigla: string
  variavel?: string | null
  unidade?: string | null
  score: number
  motivo: string
}

export interface AnalyzedMeasure {
  id: string
  label: string
  variableName?: string | null
  section?: string | null
  unit?: string | null
  originalText?: string | null
  candidates: VariableMatchCandidate[]
  selectedCandidate?: VariableMatchCandidate | null
  status: 'matched' | 'lowConfidence' | 'unresolved' | string
}

export interface ConversionAnalysisResponse {
  sourceFileName: string
  analyzedAt: string
  provider: string
  measures: AnalyzedMeasure[]
}

export interface ReviewedMeasure {
  id?: string
  label: string
  section?: string | null
  unit?: string | null
  originalText?: string | null
  codVariavel?: number | null
  codReferencia?: number | null
  normalityMode?: 'simple' | 'comment' | 'classificacao' | 'texto' | null
  decision: 'keep' | 'ignore'
}

export interface VariableReferenceOption {
  codigo: number
  titulo?: string | null
  ano?: number | string | null
  multiRange: boolean
}

export interface VariableNormalidade {
  sexo?: string | null
  valor_min?: number | string | null
  valor_max?: number | string | null
  classificacao?: string | null
  comentario_texto?: string | null
  referencia?: { codigo?: number | null; titulo?: string | null; ano?: number | string | null } | null
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
