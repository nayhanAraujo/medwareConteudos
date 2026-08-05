import type { ConversionResponse, ValidationResponse } from '~/types/conversion'

export function useConversionApi() {
  const config = useRuntimeConfig()
  const apiBase = config.public.apiBase as string

  const convertImage = async (file: File): Promise<ConversionResponse> => {
    const formData = new FormData()
    formData.append('image', file)

    return await $fetch<ConversionResponse>(`${apiBase}/api/conversions`, {
      method: 'POST',
      body: formData
    })
  }

  const validateHtml = async (html: string): Promise<ValidationResponse> => {
    return await $fetch<ValidationResponse>(`${apiBase}/api/conversions/validate`, {
      method: 'POST',
      body: { html }
    })
  }

  const checkHealth = async () => {
    return await $fetch<{ status: string }>(`${apiBase}/api/conversions/health`)
  }

  return { convertImage, validateHtml, checkHealth }
}
