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
    deleteModulo
  }
}
