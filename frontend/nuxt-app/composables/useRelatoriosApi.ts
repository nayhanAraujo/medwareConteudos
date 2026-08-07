export interface RelatorioItem {
  codrelatorio: number
  nome: string
  modulo: string
  formato: string
  dthrcriacao?: string | null
  ativo: number
  tem_multiselecao: boolean
  tem_validacao?: boolean
}

export interface RelatorioDetail extends RelatorioItem {
  conteudo: string
}

export interface RelatorioFormData {
  nome: string
  modulo: string
  formato: 'XML' | 'JSON' | string
  conteudo: string
  ativo: number
}

export interface RelatorioSistema {
  codsistema: number
  nome: string
  descricao?: string
  qtd_modulos: number
}

export interface RelatorioModulo {
  codmodulo: number
  nome: string
  descricao?: string
  ativo?: number
  total_relatorios?: number
  ultima_atualizacao?: string | null
  sistemas?: { codsistema: number; nome: string }[]
}

export interface RelatorioModuloSimples {
  nome: string
  descricao?: string
}

export interface RelatorioValidacao {
  codvalidacao: number
  status_validacao: 'A' | 'R' | 'P'
  metodo_validacao: string
  criterios_validacao?: string | null
  observacoes?: string | null
  dthrvalidacao?: string | null
  dthrproxima_validacao?: string | null
  validador?: string | null
}

export interface RelatorioFiltro {
  codfiltro: number
  nome: string
  descricao: string
  tipo: string
  sql_filtro?: string | null
  sql_query: string
  ativo: string
}

export interface RelatorioColuna {
  nome_coluna: string
  posicao_coluna: number
  tipo_coluna?: string | null
  dthrcriacao?: string | null
}

export interface RelatorioColunaStatus {
  tabelaExiste: boolean
  message?: string
  totalRelatoriosXml?: number
  totalColunasIndexadas?: number
  relatoriosComColunas?: number
  relatoriosSemColunas?: number
  relatoriosSemColunasLista?: Array<{ codrelatorio: number; nome: string; dthrcriacao?: string | null }>
}

interface ComplementResponse<T> {
  success: boolean
  data: T
  total: number
}

interface ComplementList<T> {
  data: T[]
  total: number
  tabelaExiste: boolean
}

interface ListRelatoriosResponse {
  success: boolean
  data: RelatorioItem[]
  page: number
  perPage: number
  totalPages: number
  totalItems: number
}

export function useRelatoriosApi() {
  const api = useApi()

  const listRelatorios = (q: {
    search?: string
    modulo?: string
    formato?: string
    status?: string
    multiselecao?: string
    page?: number
    perPage?: number
  }) => {
    const params = new URLSearchParams()
    Object.entries(q).forEach(([k, v]) => {
      if (v !== undefined && v !== null && String(v).trim() !== '') params.set(k, String(v))
    })
    return api.get<ListRelatoriosResponse>(`/api/web/relatorios?${params.toString()}`)
  }

  const getRelatorio = (id: number) =>
    api.get<{ success: boolean; data: RelatorioDetail }>(`/api/web/relatorios/${id}`)

  const createRelatorio = (body: RelatorioFormData) =>
    api.post<{ success: boolean; codRelatorio: number; message?: string }>(`/api/web/relatorios`, body)

  const updateRelatorio = (id: number, body: RelatorioFormData) =>
    api.put<{ success: boolean; message?: string }>(`/api/web/relatorios/${id}`, body)

  const setStatus = (id: number, ativo: number) =>
    api.request<{ success: boolean; message?: string }>(`/api/web/relatorios/${id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ ativo })
    })

  const deleteRelatorio = (id: number) =>
    api.del<{ success: boolean }>(`/api/web/relatorios/${id}`)

  const downloadRelatorio = (id: number, filename: string) =>
    downloadBlob(`/api/web/relatorios/${id}/download`, filename)

  const exportMultiplos = async (codRelatorios: number[], filename?: string) => {
    const config = useRuntimeConfig()
    const auth = useAuthStore()
    const headers: Record<string, string> = { 'Content-Type': 'application/json' }
    if (auth.token) headers.Authorization = `Bearer ${auth.token}`
    const res = await fetch(`${config.public.apiBase}/api/web/relatorios/exportar-multiplos`, {
      method: 'POST',
      headers,
      body: JSON.stringify({ codRelatorios })
    })
    if (!res.ok) {
      const data = await res.json().catch(() => ({}))
      throw new Error(data?.message || data?.error || `Erro HTTP ${res.status}`)
    }
    const blob = await res.blob()
    const cd = res.headers.get('Content-Disposition') || ''
    const match = cd.match(/filename="?([^"]+)"?/i)
    triggerDownload(blob, match?.[1] || filename || 'relatorios.zip')
  }

  const importLote = (modulo: string, ativo: number, files: File[]) => {
    const fd = new FormData()
    fd.append('modulo', modulo)
    fd.append('ativo', String(ativo))
    files.forEach((f) => fd.append('arquivos', f))
    return api.postForm<{ success: boolean; message?: string; created?: number; errors?: unknown[] }>(
      `/api/web/relatorios/importar-lote`,
      fd
    )
  }

  const listSistemas = () =>
    api.get<{ success: boolean; sistemas: RelatorioSistema[] }>(`/api/web/relatorios/sistemas`)

  const listModulos = (codsistema?: number) => {
    const q = codsistema ? `?codsistema=${codsistema}` : ''
    return api.get<{ success: boolean; modulos: RelatorioModulo[]; tabela_existe: boolean }>(
      `/api/web/relatorios/modulos${q}`
    )
  }

  const listModulosSimples = (codsistema?: number) => {
    const q = codsistema ? `?codsistema=${codsistema}` : ''
    return api.get<{ success: boolean; modulos: RelatorioModuloSimples[] }>(
      `/api/web/relatorios/modulos/simples${q}`
    )
  }

  const createModulo = (body: { nomeModulo: string; descricao?: string; sistemas: number[] }) =>
    api.post<{ success: boolean; message?: string }>(`/api/web/relatorios/modulos`, body)

  const updateModulo = (
    nomeModulo: string,
    body: { novoNome: string; descricao?: string; sistemas?: number[] }
  ) =>
    api.put<{ success: boolean }>(
      `/api/web/relatorios/modulos/${encodeURIComponent(nomeModulo)}`,
      body
    )

  const deleteModulo = (nomeModulo: string) =>
    api.del<{ success: boolean }>(`/api/web/relatorios/modulos/${encodeURIComponent(nomeModulo)}`)

  const listValidacoes = (id: number) =>
    api.get<ComplementResponse<ComplementList<RelatorioValidacao>>>(`/api/web/relatorios/${id}/validacoes`)

  const createValidacao = (id: number, body: {
    statusValidacao: 'A' | 'R' | 'P'
    metodoValidacao: string
    criteriosValidacao?: string | null
    observacoes?: string | null
    dthrProximaValidacao?: string | null
  }) => api.post<{ success: boolean; message?: string }>(`/api/web/relatorios/${id}/validacoes`, body)

  const listFiltros = () =>
    api.get<ComplementResponse<ComplementList<RelatorioFiltro>>>(`/api/web/relatorios/filtros`)

  const createFiltro = (body: Omit<RelatorioFiltro, 'codfiltro'>) =>
    api.post<{ success: boolean; data: { codFiltro: number } }>(`/api/web/relatorios/filtros`, body)

  const updateFiltro = (id: number, body: Omit<RelatorioFiltro, 'codfiltro'>) =>
    api.put<{ success: boolean; message?: string }>(`/api/web/relatorios/filtros/${id}`, body)

  const deleteFiltro = (id: number) =>
    api.del<{ success: boolean; message?: string }>(`/api/web/relatorios/filtros/${id}`)

  const listColunas = (id: number) =>
    api.get<ComplementResponse<ComplementList<RelatorioColuna>>>(`/api/web/relatorios/${id}/colunas`)

  const searchColunas = (query: string) =>
    api.get<ComplementResponse<{
      data: Array<RelatorioItem & { colunasEncontradas: Array<{ nome: string; posicao: number }> }>
      total: number
      colunaBuscada: string
      colunasProcessadas: string[]
      tipoBusca: string
    }>>(`/api/web/relatorios/colunas/search?q=${encodeURIComponent(query)}`)

  const reindexarColunas = () =>
    api.post<ComplementResponse<{
      message: string
      totalRelatorios: number
      sucessos: number
      falhas: number
      resultados: Array<{ codRelatorio: number; nome: string; sucesso: boolean; colunas?: number; erro?: string }>
    }>>(`/api/web/relatorios/colunas/reindexar`)

  const getStatusColunas = () =>
    api.get<ComplementResponse<RelatorioColunaStatus>>(`/api/web/relatorios/colunas/status`)

  const downloadBlob = (path: string, filename: string) =>
    api.getBlob(path).then((blob) => triggerDownload(blob, filename))

  function triggerDownload(blob: Blob, filename: string) {
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob)
    a.download = filename
    a.click()
    URL.revokeObjectURL(a.href)
  }

  return {
    listRelatorios,
    getRelatorio,
    createRelatorio,
    updateRelatorio,
    setStatus,
    deleteRelatorio,
    downloadRelatorio,
    exportMultiplos,
    importLote,
    listSistemas,
    listModulos,
    listModulosSimples,
    createModulo,
    updateModulo,
    deleteModulo,
    listValidacoes,
    createValidacao,
    listFiltros,
    createFiltro,
    updateFiltro,
    deleteFiltro,
    listColunas,
    searchColunas,
    reindexarColunas,
    getStatusColunas
  }
}
