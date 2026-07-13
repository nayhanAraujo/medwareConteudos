export interface PainelItem {
  codpainel: number
  nome: string
  descricao?: string | null
  ativo: number
  tipo_painel: string
  codcliente?: number | null
  nome_cliente?: string | null
  codmodulo?: number | null
  nome_modulo?: string | null
  tem_arquivo_pbix: boolean
  nome_arquivo_pbix?: string | null
  ultima_versao?: string | null
  data_versao?: string | null
  publicado?: number | null
  pacotes: string[]
}

export interface PainelDetail extends PainelItem {
  pacotes: { codpacotecomercial: number; nome: string }[] | string[]
  versoes?: {
    codversaopainel: number
    numeroversao: string
    datacriacao?: string | null
    publicado: number
    observacoes?: string | null
  }[]
}

export interface PainelLookup {
  codCliente?: number
  codModulo?: number
  codPacote?: number
  nome: string
}

interface ListPaineisResponse {
  success: boolean
  data: PainelItem[]
  page: number
  perPage: number
  totalPages: number
  totalItems: number
}

export function usePaineisApi() {
  const api = useApi()

  const listPaineis = (q: {
    search?: string
    tipo?: string
    ativo?: string
    clienteId?: number | string
    moduloId?: number | string
    pacoteId?: number | string
    page?: number
    perPage?: number
  }) => {
    const params = new URLSearchParams()
    Object.entries(q).forEach(([k, v]) => {
      if (v !== undefined && v !== null && String(v).trim() !== '') params.set(k, String(v))
    })
    return api.get<ListPaineisResponse>(`/api/web/paineis?${params.toString()}`)
  }

  const getPainel = (id: number) =>
    api.get<{ success: boolean; data: PainelDetail }>(`/api/web/paineis/${id}`)

  const createPainel = (form: FormData) =>
    api.postForm<{ success: boolean; codPainel: number; message?: string }>(`/api/web/paineis`, form)

  const updatePainel = (id: number, form: FormData) =>
    api.putForm<{ success: boolean; message?: string }>(`/api/web/paineis/${id}`, form)

  const setStatus = (id: number, ativo: number) =>
    api.request<{ success: boolean }>(`/api/web/paineis/${id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ ativo })
    })

  const deletePainel = (id: number) =>
    api.del<{ success: boolean }>(`/api/web/paineis/${id}`)

  const downloadPbix = (id: number, filename: string) =>
    api.getBlob(`/api/web/paineis/${id}/download`).then((blob) => {
      const a = document.createElement('a')
      a.href = URL.createObjectURL(blob)
      a.download = filename
      a.click()
      URL.revokeObjectURL(a.href)
    })

  const listClientes = () =>
    api.get<{ success: boolean; data: { codCliente: number; nome: string }[] }>(`/api/web/paineis/clientes`)

  const listModulos = () =>
    api.get<{ success: boolean; data: { codModulo: number; nome: string }[] }>(`/api/web/paineis/modulos`)

  const listPacotes = () =>
    api.get<{ success: boolean; data: { codPacote: number; nome: string }[] }>(`/api/web/paineis/pacotes`)

  const getStats = () =>
    api.get<{ success: boolean; powerbi: number; api: number }>(`/api/web/paineis/stats`)

  return {
    listPaineis,
    getPainel,
    createPainel,
    updatePainel,
    setStatus,
    deletePainel,
    downloadPbix,
    listClientes,
    listModulos,
    listPacotes,
    getStats
  }
}
