import type { ConversionFormat, ConversionHealthResponse, ConversionResponse, ValidationResponse } from '~/types/conversion'

export function useConversionApi() {
  const config = useRuntimeConfig()
  const apiBase = config.public.apiBase as string

  const convertImage = async (file: File, format: ConversionFormat): Promise<ConversionResponse> => {
    const formData = new FormData()
    formData.append('image', file)
    formData.append('format', format)

    return await $fetch<ConversionResponse>(`${apiBase}/api/conversions`, {
      method: 'POST',
      body: formData
    })
  }

  const validateContent = async (format: ConversionFormat, content: string): Promise<ValidationResponse> => {
    return await $fetch<ValidationResponse>(`${apiBase}/api/conversions/validate`, {
      method: 'POST',
      body: { format, content }
    })
  }

  const checkHealth = async (): Promise<ConversionHealthResponse> => {
    return await $fetch<ConversionHealthResponse>(`${apiBase}/api/conversions/health`)
  }

  return { convertImage, validateContent, checkHealth }
}
