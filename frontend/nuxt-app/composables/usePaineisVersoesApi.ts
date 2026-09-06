export interface VersaoResumo {
  codVersaoPainel: number
  numeroVersao: string
  dataCriacao?: string
  publicado: number
  observacoes?: string
  totalImagens: number
  totalArquivos: number
}

export interface VersaoDetalhe {
  versao: Record<string, any>
  configuracao?: Record<string, any> | null
  imagens: Record<string, any>[]
  arquivos: Record<string, any>[]
  metricas: Record<string, any>[]
  dimensoes: Record<string, any>[]
  fontes: Record<string, any>[]
}

export function usePaineisVersoesApi() {
  const api = useApi()
  const base = (painelId: number) => `/api/web/paineis/${painelId}/versoes`

  const list = (painelId: number) => api.get<{ success: boolean; data: VersaoResumo[] }>(base(painelId))
  const get = (painelId: number, versaoId: number) =>
    api.get<{ success: boolean; data: VersaoDetalhe }>(`${base(painelId)}/${versaoId}`)
  const create = (painelId: number, form: FormData) =>
    api.postForm<{ success: boolean; data: { codVersaoPainel: number } }>(base(painelId), form)
  const update = (painelId: number, versaoId: number, form: FormData) =>
    api.putForm<{ success: boolean }>(`${base(painelId)}/${versaoId}`, form)
  const remove = (painelId: number, versaoId: number) => api.del(`${base(painelId)}/${versaoId}`)
  const removeImage = (painelId: number, versaoId: number, imagemId: number) =>
    api.del(`${base(painelId)}/${versaoId}/imagens/${imagemId}`)
  const addImages = (painelId: number, versaoId: number, form: FormData) =>
    api.postForm<{ success: boolean; data: { total: number } }>(`${base(painelId)}/${versaoId}/imagens`, form)
  const addMetric = (painelId: number, versaoId: number, body: unknown) =>
    api.post(`${base(painelId)}/${versaoId}/metricas`, body)
  const addDimension = (painelId: number, versaoId: number, body: unknown) =>
    api.post(`${base(painelId)}/${versaoId}/dimensoes`, body)
  const addSource = (painelId: number, versaoId: number, body: unknown) =>
    api.post(`${base(painelId)}/${versaoId}/fontes`, body)
  const download = async (painelId: number, versaoId: number, tipo: string, filename: string, imagemId?: number) => {
    const query = imagemId ? `?imagemId=${imagemId}` : ''
    const blob = await api.getBlob(`${base(painelId)}/${versaoId}/download/${tipo}${query}`)
    const anchor = document.createElement('a')
    anchor.href = URL.createObjectURL(blob)
    anchor.download = filename
    anchor.click()
    URL.revokeObjectURL(anchor.href)
  }

  return { list, get, create, update, remove, removeImage, addImages, addMetric, addDimension, addSource, download }
}
