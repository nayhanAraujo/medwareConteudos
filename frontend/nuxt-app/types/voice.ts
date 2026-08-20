export type VoiceSessionMode = 'fromScratch' | 'fromImage'
export type VoiceSttSource = 'browser' | 'server'
export type VoiceUtteranceIntent = 'build' | 'edit'

export interface VoiceTranscriptEntry {
  at: string
  transcript: string
  sttSource: VoiceSttSource
  summary: string
}

export interface VoiceSessionResponse {
  id: string
  mode: VoiceSessionMode | string
  camposScriptJson: string
  transcriptHistory: VoiceTranscriptEntry[]
  sourceFileName?: string
  createdAt: string
  updatedAt: string
}

export interface VoiceUtteranceResponse {
  camposScriptJson: string
  summary: string
  warnings: string[]
}

export interface VoiceGenerateResponse {
  format: import('~/types/conversion').ConversionFormat
  html: string
  text: string
  sourceFileName: string
  convertedAt: string
  provider: string
  validation: {
    isValid: boolean
    errors: string[]
    warnings: string[]
  }
}

export interface CampoScript {
  id?: number
  tipo?: string
  descricao?: string
  nome?: string
  medida?: string
  coluna?: number
  ordem?: number | string
  etiquetaPai?: string
  referenciaNormalidade?: Array<{
    sexo?: string
    valorMin?: string
    valorMax?: string
    unidadeMedida?: string
  }>
}

export interface CamposScriptRoot {
  camposScript: CampoScript[]
}
