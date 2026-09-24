import type {
  ConversionAnalysisResponse,
  ConversionFormat,
  ConversionHealthResponse,
  ConversionResponse,
  ReviewedMeasure,
  ValidationResponse
} from '~/types/conversion'

export function useConversionApi() {
  const api = useApi()

  const convertImage = async (file: File, format: ConversionFormat): Promise<ConversionResponse> => {
    const formData = new FormData()
    formData.append('image', file)
    formData.append('format', format)

    return await api.postForm<ConversionResponse>('/api/conversions', formData)
  }

  const validateContent = async (format: ConversionFormat, content: string): Promise<ValidationResponse> => {
    return await api.post<ValidationResponse>('/api/conversions/validate', { format, content })
  }

  const analyzeImage = async (file: File): Promise<ConversionAnalysisResponse> => {
    const formData = new FormData()
    formData.append('image', file)

    return await api.postForm<ConversionAnalysisResponse>('/api/conversions/analyze', formData)
  }

  const generateModoTextoFromAnalysis = async (
    sourceFileName: string,
    measures: ReviewedMeasure[],
    codPadraoCliente?: number | null
  ): Promise<ConversionResponse> => {
    return await api.post<ConversionResponse>('/api/conversions/generate-modo-texto-from-analysis', {
      sourceFileName, measures, codPadraoCliente: codPadraoCliente ?? undefined
    })
  }

  const generateJsonFromAnalysis = async (
    sourceFileName: string,
    measures: ReviewedMeasure[],
    codPadraoCliente?: number | null
  ): Promise<ConversionResponse> => {
    return await api.post<ConversionResponse>('/api/conversions/generate-json-from-analysis', {
      sourceFileName, measures, codPadraoCliente: codPadraoCliente ?? undefined
    })
  }

  const registerAlternativa = async (codVariavel: number, alternativa: string): Promise<void> => {
    await api.post('/api/conversions/register-alternativa', { codVariavel, alternativa })
  }

  const checkHealth = async (): Promise<ConversionHealthResponse> => {
    return await api.get<ConversionHealthResponse>('/api/conversions/health')
  }

  return {
    convertImage,
    validateContent,
    checkHealth,
    analyzeImage,
    generateModoTextoFromAnalysis,
    generateJsonFromAnalysis,
    registerAlternativa
  }
}
