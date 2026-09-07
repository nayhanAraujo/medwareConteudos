export interface ReferenciaItem {
  codReferencia: number
  titulo: string
  ano?: number | null
  autores?: string | null
  descricao?: string | null
  especialidade?: string | null
  totalVariaveis?: number
  totalNormalidades?: number
}

export interface ReferenciaDetail extends ReferenciaItem {
  doi?: string | null
  isbn?: string | null
  volume?: string | null
  paginas?: string | null
  codEspecialidade?: number | null
  codTipoRef?: number | null
  tipoReferencia?: string | null
}

export interface ReferenciaFormData {
  titulo: string
  ano: number
  descricao?: string
  doi?: string
  isbn?: string
  volume?: string
  paginas?: string
  codEspecialidade?: number | null
  codTipoRef?: number | null
}

export interface ReferenciaAnexo {
  codAnexo: number
  codReferencia: number
  descricao?: string | null
  nome?: string | null
  link?: string | null
  caminho?: string | null
  tipoAnexo?: string | null
}

export interface AutorItem {
  codAutor: number
  nome: string
  abreviacao?: string | null
  selecionado?: boolean
}

interface ListReferenciasResponse {
  success: boolean
  data: ReferenciaItem[]
  page: number
  totalPages: number
  totalItems: number
}

export function useReferenciasApi() {
  const api = useApi()

  const listReferencias = (q: {
    page?: number
    pageSize?: number
    titulo?: string
    ano?: string
    autor?: string
    abreviacao?: string
  }) => {
    const params = new URLSearchParams()
    Object.entries(q).forEach(([k, v]) => {
      if (v !== undefined && v !== null && String(v).trim() !== '') params.set(k, String(v))
    })
    return api.get<ListReferenciasResponse>(`/api/web/referencias?${params.toString()}`)
  }

  const getReferencia = (id: number) =>
    api.get<{ success: boolean; data: ReferenciaDetail }>(`/api/web/referencias/${id}`)

  const createReferencia = (body: ReferenciaFormData) =>
    api.post<{ success: boolean; codReferencia: number }>(`/api/web/referencias`, body)

  const updateReferencia = (id: number, body: ReferenciaFormData) =>
    api.put<{ success: boolean }>(`/api/web/referencias/${id}`, body)

  const deleteReferencia = (id: number) =>
    api.del<{ success: boolean }>(`/api/web/referencias/${id}`)

  const getMeta = () =>
    api.get<{
      success: boolean
      especialidades: { codEspecialidade: number; nome: string }[]
      tipos: { codTipoRef: number; nome?: string; descricao: string }[]
    }>(`/api/web/referencias/meta`)

  const searchReferencias = (q: string) =>
    api.get<{ id: number; text: string; titulo: string; ano?: number; autores?: string }[]>(
      `/api/web/referencias/search?q=${encodeURIComponent(q)}`
    )

  const listAnexos = (codReferencia: number) =>
    api.get<{ success: boolean; data: ReferenciaAnexo[] }>(`/api/web/referencias/${codReferencia}/anexos`)

  const createAnexo = (codReferencia: number, payload: FormData) =>
    api.postForm<{ success: boolean; codAnexo: number }>(`/api/web/referencias/${codReferencia}/anexos`, payload)

  const updateAnexo = (codAnexo: number, payload: FormData) =>
    api.putForm<{ success: boolean; codReferencia: number }>(`/api/web/referencias/anexos/${codAnexo}`, payload)

  const deleteAnexo = (codAnexo: number) =>
    api.del<{ success: boolean; codReferencia: number }>(`/api/web/referencias/anexos/${codAnexo}`)

  const listAutores = () =>
    api.get<{ success: boolean; data: AutorItem[] }>(`/api/web/referencias/autores`)

  const listAutoresByReferencia = (codReferencia: number) =>
    api.get<{ success: boolean; data: AutorItem[] }>(`/api/web/referencias/${codReferencia}/autores`)

  const saveAutoresByReferencia = (codReferencia: number, autorIds: number[]) =>
    api.post<{ success: boolean }>(`/api/web/referencias/${codReferencia}/autores`, { autorIds })

  return {
    listReferencias,
    getReferencia,
    createReferencia,
    updateReferencia,
    deleteReferencia,
    getMeta,
    searchReferencias,
    listAnexos,
    createAnexo,
    updateAnexo,
    deleteAnexo,
    listAutores,
    listAutoresByReferencia,
    saveAutoresByReferencia
  }
}
