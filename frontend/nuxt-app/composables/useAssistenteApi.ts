export type AssistenteEntity = Record<string, unknown> & {
  id?: number
  codigo?: number
  status?: number | boolean
}

export interface AssistentePage<T = AssistenteEntity> {
  items: T[]
  page: number
  pageSize: number
  total: number
}

export interface AssistenteOption {
  id: number
  nome: string
}

export interface AssistenteDashboard {
  procedimentos: number
  scripts: number
  paginasFotos: number
  frases: number
  especialidades: number
  referencias: number
  esquemas: number
  operadoras: number
}

export interface ImportarModeloPayload {
  tituloScript: string
  tipoScript: 1 | 2 | 3
  arquivoScript: File
  tituloMrd: string
  arquivoMrd: File
  especialidades: number[]
  procedimentos: number[]
}

export interface ImportarModeloResult {
  codScriptLaudo: number
  codPagFotos: number
  especialidadesVinculadas: number[]
  totalProcedimentosVinculados: number
}

export interface AssistenteLinkItem { id: number; nome: string; status?: number; sequencia?: number }
export interface AssistenteLinkGroup {
  relacao: string; titulo: string; dominioDestino: string; ordenavel: boolean
  selecaoUnica: boolean; obrigatorio: boolean; editavel: boolean; itens: AssistenteLinkItem[]
}
export interface AssistenteLinksDetail {
  dominio: string; id: number; nome: string; status?: number; relacoes: AssistenteLinkGroup[]
}
export interface AssistenteLinksSummaryItem { id: number; nome: string; status?: number; totalVinculos: number }

type ApiEnvelope<T> = { data?: T; items?: T[]; total?: number; page?: number; pageSize?: number }

const domainFields: Record<string, Record<string, string>> = {
  especialidades: { codigo: 'CODESPECIALIDADE', id: 'CODESPECIALIDADE', nome: 'DESCRICAO', descricao: 'DESCRICAO' },
  grupos: { codigo: 'CODGRUPO', id: 'CODGRUPO', nome: 'GRUPO', grupo: 'GRUPO', grupoPaiId: 'CODGRUPOPAI', status: 'STATUS' },
  frases: { codigo: 'CODFRASE', id: 'CODFRASE', titulo: 'TITULO', conteudo: 'FRASE', grupo: 'CODGRUPO', grupoId: 'CODGRUPO', status: 'STATUS' },
  operadoras: { codigo: 'CODOPERADORA', id: 'CODOPERADORA', nome: 'NOMEFANTASIA', nomeFantasia: 'NOMEFANTASIA', razaoSocial: 'RAZAOSOCIAL', registroAns: 'REGISTROANS', cnpj: 'CNPJ' },
  'grupos-operadoras': { codigo: 'CODGRUPOOPERADORA', id: 'CODGRUPOOPERADORA', nome: 'DESCRICAO', descricao: 'DESCRICAO' },
  'tabela-procedimentos': { codigo: 'CODTABELAPROCEDIMENTO', id: 'CODTABELAPROCEDIMENTO', codigoTuss: 'CODIGOTUSS', nome: 'DESCRICAOTUSS', descricaoTuss: 'DESCRICAOTUSS' },
  procedimentos: { codigo: 'CODPROCEDIMENTO', id: 'CODPROCEDIMENTO', nome: 'DESCRICAO_PROCED', descricao: 'DESCRICAO_PROCED', especialidade: 'CODESPECIALIDADE', especialidadeId: 'CODESPECIALIDADE', tabelaProcedimentoId: 'CODTABELAPROCEDIMENTO', status: 'STATUS' },
  referencias: { codigo: 'CODREFERENCIA', id: 'CODREFERENCIA', titulo: 'DESCRICAO', descricao: 'DESCRICAO', tipo: 'TIPO', conteudo: 'VALOR', valor: 'VALOR' },
  esquemas: { codigo: 'CODESQUEMA', id: 'CODESQUEMA', nome: 'DESCRICAO', descricao: 'DESCRICAO', conteudo: 'IMAGEM', imagem: 'IMAGEM' },
  'esquemas-fotos': { codigo: 'CODESQUEMAFOTOS', id: 'CODESQUEMAFOTOS', nome: 'TITULO', titulo: 'TITULO', conteudo: 'ESQUEMA', esquema: 'ESQUEMA' },
  scripts: { codigo: 'CODSCRIPTLAUDO', id: 'CODSCRIPTLAUDO', titulo: 'TITULO', tipo: 'TIPOSCRIPT', tipoScript: 'TIPOSCRIPT', conteudo: 'ESTRUTURASCRIPT', estruturaScript: 'ESTRUTURASCRIPT', status: 'STATUS' },
  'paginas-fotos': { codigo: 'CODPAGFOTOS', id: 'CODPAGFOTOS', titulo: 'TITULO', conteudo: 'ESTRUTURAPAGFOTOS', estruturaPagFotos: 'ESTRUTURAPAGFOTOS', status: 'STATUS' }
}

export function useAssistenteApi() {
  const api = useApi()
  const root = '/api/web/assistente'

  function query(params: Record<string, string | number | boolean | undefined>) {
    const search = new URLSearchParams()
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== '') search.set(key, String(value))
    })
    const value = search.toString()
    return value ? `?${value}` : ''
  }

  function unwrap<T>(response: T | ApiEnvelope<T>): T {
    return response && typeof response === 'object' && 'data' in response
      ? ((response as ApiEnvelope<T>).data as T)
      : (response as T)
  }

  function normalize<T extends AssistenteEntity>(domain: string, source: T): T {
    const result = { ...source } as AssistenteEntity
    for (const [target, firebird] of Object.entries(domainFields[domain] || {})) {
      if (result[target] === undefined && source[firebird] !== undefined) result[target] = source[firebird]
    }
    return result as T
  }

  async function list<T = AssistenteEntity>(domain: string, page = 1, pageSize = 20, search = ''): Promise<AssistentePage<T>> {
    const response = await api.get<ApiEnvelope<AssistentePage<T>> | ApiEnvelope<T[]> | T[]>(
      `${root}/${domain}${query({ page, pageSize, search })}`
    )
    const data = unwrap(response)
    if (Array.isArray(data)) {
      const envelope = response as ApiEnvelope<T[]>
      return { items: data.map(item => normalize(domain, item)), page: envelope.page || page, pageSize: envelope.pageSize || pageSize, total: envelope.total ?? data.length }
    }
    const result = data as AssistentePage<T>
    const envelope = response as ApiEnvelope<AssistentePage<T>>
    return { ...result, items: (result.items || []).map(item => normalize(domain, item)), total: envelope.total ?? result.total ?? result.items?.length ?? 0 }
  }

  return {
    dashboard: async () => {
      const data = unwrap<any>(await api.get<ApiEnvelope<any>>(`${root}/dashboard`))
      const domains = data.dominios || data
      return {
        procedimentos: domains.procedimentos || 0,
        scripts: domains.scripts || 0,
        paginasFotos: domains['paginas-fotos'] || domains.paginasFotos || 0,
        frases: domains.frases || 0,
        especialidades: domains.especialidades || 0,
        referencias: domains.referencias || 0,
        esquemas: domains.esquemas || 0,
        operadoras: domains.operadoras || 0
      } satisfies AssistenteDashboard
    },
    list,
    get: async <T = AssistenteEntity>(domain: string, id: number) => {
      const data = unwrap<any>(await api.get<ApiEnvelope<any>>(`${root}/${domain}/${id}`))
      return normalize(domain, (data.item || data) as AssistenteEntity) as T
    },
    create: async <T = AssistenteEntity>(domain: string, body: unknown) =>
      unwrap(await api.post<ApiEnvelope<T>>(`${root}/${domain}`, body)),
    update: async <T = AssistenteEntity>(domain: string, id: number, body: unknown) =>
      unwrap(await api.put<ApiEnvelope<T>>(`${root}/${domain}/${id}`, body)),
    remove: (domain: string, id: number) => api.del(`${root}/${domain}/${id}`),
    setStatus: (domain: string, id: number, active: boolean) =>
      api.put(`${root}/${domain}/${id}/status`, { status: active ? -1 : 0 }),
    options: async (domain: string, search = '') => {
      const page = await list<AssistenteOption>(domain, 1, 100, search)
      return page.items
    },
    links: async (domain: string, id: number) =>
      unwrap(await api.get<ApiEnvelope<AssistenteLinksDetail>>(`${root}/${domain}/${id}/vinculos`)),
    linksSummary: async (domain: string, search = '', filter = 'todos', page = 1, pageSize = 20) =>
      unwrap(await api.get<ApiEnvelope<AssistentePage<AssistenteLinksSummaryItem>>>(
        `${root}/vinculos/resumo${query({ domain, search, filter, page, pageSize })}`
      )),
    setLinks: (domain: string, id: number, relation: string, ids: number[], items?: { id: number; sequencia: number }[]) =>
      api.put(`${root}/${domain}/${id}/vinculos/${relation}`, { ids, items }),
    importModel: async (payload: ImportarModeloPayload) => {
      const form = new FormData()
      form.append('tituloScript', payload.tituloScript)
      form.append('tipoScript', String(payload.tipoScript))
      form.append('arquivoScript', payload.arquivoScript)
      form.append('tituloMrd', payload.tituloMrd)
      form.append('arquivoMrd', payload.arquivoMrd)
      payload.especialidades.forEach(id => form.append('especialidades', String(id)))
      payload.procedimentos.forEach(id => form.append('procedimentos', String(id)))
      return unwrap(await api.postForm<ApiEnvelope<ImportarModeloResult>>(`${root}/modelos/importar`, form))
    }
  }
}
