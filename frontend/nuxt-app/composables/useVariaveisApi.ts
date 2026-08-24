export interface UnidadeMedidaDto {
  codUnidadeMedida: number
  descricao: string
}

export interface VariavelCreatePayload {
  nome: string
  variavel: string
  sigla: string
  abreviacao: string
  descricao?: string
  codUnidadeMedida: number
  casasDecimais: number
  alternativas?: string[]
  nomesClinicos?: string[]
}

export interface VariavelEdicaoDto {
  codVariavel: number
  nome?: string
  variavel?: string
  sigla?: string
  abreviacao?: string
  descricao?: string
  codUnidadeMedida?: number | null
  casasDecimais?: number | null
  codGrupo?: number | null
  alternativas: string[]
  nomesClinicos: string[]
}

export interface GrupoVariavelDto {
  codGrupo: number
  nome: string
}

export interface VariavelScriptDto {
  codScriptLaudo: number
  nome?: string
}

export interface VariavelAnexoReferenciaDto {
  titulo?: string
  ano?: string
  autores?: string
}

export interface VariavelAnexoDto {
  codAnexo: number
  tipoAnexo?: string
  caminho?: string
  descricao?: string
  referencia?: VariavelAnexoReferenciaDto | null
}

export interface VariavelListItem {
  codVariavel: number
  nome?: string
  variavel?: string
  sigla?: string
  abreviacao?: string
  codGrupo?: number | null
  nomeGrupo?: string | null
  formula?: string | null
  casasDecimais?: number | null
  alternativas: string[]
  scripts: VariavelScriptDto[]
  anexos: VariavelAnexoDto[]
  possuiNormalidades: boolean
}

/** @deprecated use VariavelListItem */
export type VariavelItem = VariavelListItem

export interface VariavelReferenciaResumoDto {
  codigo?: number | null
  titulo?: string | null
  ano?: string | null
  descricao?: string | null
  autores?: string | null
}

export interface VariavelNormalidadeDetalheDto {
  codNormalidade: number
  sexo?: string | null
  valorMin?: number | null
  valorMax?: number | null
  idadeMin?: number | null
  idadeMax?: number | null
  referencia?: VariavelReferenciaResumoDto | null
}

export interface VariavelEquacaoDetalheDto {
  codEquacao: number
  equacao?: string | null
  linguagem?: string | null
  referencia?: VariavelReferenciaResumoDto | null
}

export interface VariavelDetalhesCompletosDto {
  normalidades: VariavelNormalidadeDetalheDto[]
  equacoes: VariavelEquacaoDetalheDto[]
}

export interface VariavelCodigoDicomDto {
  codigo: string
  descricaoPtBr?: string | null
}

export interface VariavelDependencias {
  formulas: number; normalidades: number; scripts: number; secoes: number
  codigosUniversais: number; alternativas: number; nomesClinicos: number; classificacoes: number
  especialidades: number; anexos: number; possuiVinculos: boolean
}
export interface ClassificacaoGrupo { codGrupo: number; nome: string }
export interface Classificacao { codClassificacao: number; codGrupo: number; nome: string }
export interface CodigoUniversal { codUniversal: number; codigo: string; descricaoPtBr?: string | null }
export interface Especialidade { codEspecialidade: number; nome: string; descricao?: string | null }
export interface AnexoContexto {
  anexos: Array<{ codAnexo: number; nome?: string; descricao?: string; tipoAnexo?: string; link?: string; caminho?: string; codFormula?: number; codReferencia?: number }>
  formulas: Array<{ codFormula: number; formula: string }>
  referencias: Array<{ codReferencia: number; titulo: string; ano?: number | null }>
}
export interface ImportacaoCsPreview {
  variaveis: Array<{ codigo: string; nome: string; sigla: string; abreviacao: string; unidade: string; existeNoBanco: boolean }>
  formulas: Array<{ variavel: string; expressao: string; casasDecimais: number }>
  normalidades: Array<{ variavel: string; sexo: string; valorMin: number; valorMax: number; idadeMin: number; idadeMax: number; referencia: string }>
}

export interface ReferenciasNormalidadesPainel {
  referencias: Array<{
    codigo: number
    titulo: string
    ano?: number | null
    autores?: string | null
    totalVariaveis: number
    totalNormalidades: number
  }>
  outrasReferencias: Array<{
    codigo: number
    titulo: string
    ano?: number | null
  }>
  referenciaSelecionada?: {
    codigo: number
    titulo: string
    ano?: number | null
    descricao?: string | null
    autores?: string | null
  } | null
  variaveisVinculadas: Array<{
    codVariavel: number
    nomeVariavel: string
    variavel?: string
    sigla?: string
    totalNormalidades: number
    comentarioTexto?: string | null
  }>
  comentariosPorVariavel?: Record<string, { codNormalidadeComentario: number; texto: string }>
  normalidadesPorVariavel: Record<
    string,
    Array<{
      codNormalidade: number
      sexo?: string | null
      valorMin?: number | null
      valorMax?: number | null
      idadeMin?: number | null
      idadeMax?: number | null
      pagina?: number | null
      classificacao?: string | null
    }>
  >
  normalidadesDisponiveis: Array<{
    codNormalidade: number
    codVariavel: number
    nomeVariavel: string
    variavel?: string
    sigla?: string
    sexo?: string | null
    valorMin?: number | null
    valorMax?: number | null
    idadeMin?: number | null
    idadeMax?: number | null
    classificacao?: string | null
  }>
  anexosReferencia: Array<{
    tipo?: string
    caminho?: string
    descricao?: string
  }>
  totalSemReferencia: number
  limiteDisponiveis: number
  filtros: { referenciaBusca: string; variavelBusca: string }
}

export interface PadroesClientePainel {
  codCliente: number
  padroes: Array<{
    codPadrao: number
    nome: string
    codigo: string
    codReferencia?: number | null
    referenciaTitulo?: string | null
    padraoVigente: number
    ativo: number
    descricao?: string | null
    totalFaixas: number
  }>
  padraoSelecionado?: {
    codPadrao: number
    nome: string
    codigo: string
    codReferencia?: number | null
    referenciaTitulo?: string | null
    padraoVigente: number
    ativo: number
    descricao?: string | null
    totalFaixas: number
  } | null
  variaveis: Array<{
    codVariavel: number
    nomeVariavel: string
    variavel?: string
    sigla?: string
    totalFaixas: number
    comentarioTexto?: string | null
  }>
  faixasPorVariavel: Record<
    string,
    Array<{
      codFaixa: number
      sexo?: string | null
      valorMin?: number | null
      valorMax?: number | null
      idadeMin?: number | null
      idadeMax?: number | null
      pagina?: number | null
      codClassificacao?: number | null
      classificacao?: string | null
    }>
  >
  comentariosPorVariavel?: Record<string, { codPadraoComentario: number; texto: string }>
  referencias: Array<{ codigo: number; titulo: string; ano?: number | null }>
  filtros: { variavelBusca: string }
}

export function useVariaveisApi() {
  const api = useApi()

  const listGrupos = () =>
    api.get<{ success: boolean; data: GrupoVariavelDto[] }>('/api/web/variaveis/grupos')

  const getMeta = () =>
    api.get<{ success: boolean; data: { unidades: UnidadeMedidaDto[] } }>('/api/web/variaveis/meta')

  const createVariavel = (body: VariavelCreatePayload) =>
    api.post<{ success: boolean; codVariavel: number }>('/api/web/variaveis', body)

  const getVariavelEdicao = (id: number) =>
    api.get<{ success: boolean; data: VariavelEdicaoDto }>(`/api/web/variaveis/${id}/edicao`)

  const updateVariavel = (id: number, body: VariavelCreatePayload) =>
    api.put<{ success: boolean }>(`/api/web/variaveis/${id}`, body)

  const listVariaveis = (q: { skip?: number; take?: number; search?: string; grupo?: number | '' }) => {
    const params = new URLSearchParams()
    Object.entries(q).forEach(([k, v]) => {
      if (v !== undefined && v !== null && String(v).trim() !== '') params.set(k, String(v))
    })
    return api.get<{ success: boolean; data: VariavelListItem[]; total: number }>(
      `/api/web/variaveis?${params.toString()}`
    )
  }

  const getDetalhesCompletos = (id: number) =>
    api.get<{ success: boolean; data: VariavelDetalhesCompletosDto }>(
      `/api/web/variaveis/${id}/detalhes-completos`
    )

  const getCodigosVinculados = (id: number) =>
    api.get<{ success: boolean; data: VariavelCodigoDicomDto[] }>(
      `/api/web/variaveis/${id}/codigos-vinculados`
    )

  const getReferenciasNormalidades = (q: {
    referenciaId?: number
    referenciaBusca?: string
    variavelBusca?: string
    limite?: number
  }) => {
    const params = new URLSearchParams()
    Object.entries(q).forEach(([k, v]) => {
      if (v !== undefined && v !== null && String(v).trim() !== '') params.set(k, String(v))
    })
    return api.get<{ success: boolean; data: ReferenciasNormalidadesPainel }>(
      `/api/web/variaveis/referencias-normalidades?${params.toString()}`
    )
  }

  const vincularNormalidadesReferencia = (body: {
    codReferencia: number
    normalidades: Array<{ codNormalidade: number; pagina?: number | null }>
  }) => api.post<{ success: boolean; message: string; total: number }>(`/api/web/variaveis/referencias-normalidades/vincular`, body)

  const atualizarNormalidadeReferencia = (body: {
    codNormalidade: number
    valorMin?: number | null
    valorMax?: number | null
    idadeMin?: number | null
    idadeMax?: number | null
    pagina?: number | null
  }) => api.post<{ success: boolean; message: string }>(`/api/web/variaveis/referencias-normalidades/atualizar`, body)

  const desvincularNormalidadeReferencia = (body: { codNormalidade: number; codReferencia?: number }) =>
    api.post<{ success: boolean; message: string }>(`/api/web/variaveis/referencias-normalidades/desvincular`, body)

  const importarNormalidadesReferencia = (body: { codReferenciaDestino: number; codReferenciaOrigem: number }) =>
    api.post<{ success: boolean; data: { inseridas: number; ignoradas: number; totalOrigem: number; message: string } }>(
      `/api/web/variaveis/referencias-normalidades/importar`,
      body
    )

  const salvarComentarioNormalidade = (body: { codVariavel: number; codReferencia: number; texto: string }) =>
    api.post<{ success: boolean; message: string }>(`/api/web/variaveis/referencias-normalidades/comentario`, body)

  const importarNormalidadesJson = (body: { codReferencia: number }) =>
    api.post<{
      success: boolean
      data: {
        message: string
        variaveisAtualizadas: number
        faixasInseridas: number
        comentariosGravados: number
        naoEncontradas: string[]
      }
    }>(`/api/web/variaveis/referencias-normalidades/importar-json`, body)

  const getPadroesCliente = (q: { codCliente: number; codPadrao?: number; variavelBusca?: string }) => {
    const params = new URLSearchParams()
    params.set('codCliente', String(q.codCliente))
    if (q.codPadrao) params.set('codPadrao', String(q.codPadrao))
    if (q.variavelBusca) params.set('variavelBusca', q.variavelBusca)
    return api.get<{ success: boolean; data: PadroesClientePainel }>(
      `/api/web/variaveis/padroes-cliente?${params.toString()}`
    )
  }

  const createPadraoCliente = (body: {
    codCliente: number
    nome: string
    codigo: string
    codReferencia?: number | null
    padraoVigente?: boolean
    ativo?: boolean
    descricao?: string
  }) => api.post<{ success: boolean; codPadrao: number; message: string }>(`/api/web/variaveis/padroes-cliente`, body)

  const updatePadraoCliente = (codPadrao: number, body: {
    nome?: string
    codigo?: string
    codReferencia?: number | null
    padraoVigente?: boolean
    ativo?: boolean
    descricao?: string
  }) => api.put<{ success: boolean; message: string }>(`/api/web/variaveis/padroes-cliente/${codPadrao}`, body)

  const deletePadraoCliente = (codPadrao: number) =>
    api.del<{ success: boolean; message: string }>(`/api/web/variaveis/padroes-cliente/${codPadrao}`)

  const setPadraoVigente = (codPadrao: number) =>
    api.put<{ success: boolean; message: string }>(`/api/web/variaveis/padroes-cliente/${codPadrao}/vigente`, {})

  const importarReferenciaPadrao = (codPadrao: number, codReferencia: number) =>
    api.post<{ success: boolean; data: { faixasImportadas: number; comentariosImportados: number; message: string } }>(
      `/api/web/variaveis/padroes-cliente/${codPadrao}/importar-referencia`,
      { codReferencia }
    )

  const atualizarFaixaPadrao = (codFaixa: number, body: {
    codVariavel?: number
    sexo?: string
    valorMin?: number | null
    valorMax?: number | null
    idadeMin?: number | null
    idadeMax?: number | null
    pagina?: number | null
    codClassificacao?: number | null
  }) => api.put<{ success: boolean; message: string }>(`/api/web/variaveis/padroes-cliente/faixas/${codFaixa}`, body)

  const salvarComentarioPadrao = (codPadrao: number, body: { codVariavel: number; texto: string }) =>
    api.post<{ success: boolean; message: string }>(`/api/web/variaveis/padroes-cliente/${codPadrao}/comentario`, body)

  const getDependencias = (id: number) => api.get<{ success: boolean; data: VariavelDependencias }>(`/api/web/variaveis/${id}/dependencias`)
  const deleteVariavel = (id: number, force = false) => api.del<{ success: boolean }>(`/api/web/variaveis/${id}?force=${force}`)
  const alterarGrupo = (id: number, codGrupo: number | null) => api.request<{ success: boolean }>(`/api/web/variaveis/${id}/grupo`, { method: 'PATCH', body: JSON.stringify({ codGrupo }) })
  const getClassificacoes = () => api.get<{ success: boolean; data: { grupos: ClassificacaoGrupo[]; classificacoes: Classificacao[] } }>('/api/web/variaveis/classificacoes')
  const createGrupoClassificacao = (nome: string) => api.post('/api/web/variaveis/classificacoes/grupos', { nome })
  const updateGrupoClassificacao = (id: number, nome: string) => api.put(`/api/web/variaveis/classificacoes/grupos/${id}`, { nome })
  const deleteGrupoClassificacao = (id: number) => api.del(`/api/web/variaveis/classificacoes/grupos/${id}`)
  const createClassificacao = (body: { nome: string; codGrupo: number }) => api.post('/api/web/variaveis/classificacoes', body)
  const updateClassificacao = (id: number, body: { nome: string; codGrupo: number }) => api.put(`/api/web/variaveis/classificacoes/${id}`, body)
  const deleteClassificacao = (id: number) => api.del(`/api/web/variaveis/classificacoes/${id}`)
  const getVariavelClassificacoes = (id: number) => api.get<{ success: boolean; data: Classificacao[] }>(`/api/web/variaveis/${id}/classificacoes`)
  const setVariavelClassificacoes = (id: number, classificacoes: number[]) => api.put(`/api/web/variaveis/${id}/classificacoes`, { classificacoes })
  const listCodigosUniversais = (search = '') => api.get<{ success: boolean; data: CodigoUniversal[] }>(`/api/web/variaveis/codigos-universais?search=${encodeURIComponent(search)}`)
  const setCodigosUniversais = (id: number, codigos: number[]) => api.put(`/api/web/variaveis/${id}/codigos-universais`, { codigos })
  const getEspecialidades = (id: number) => api.get<{ success: boolean; data: { vinculadas: Especialidade[]; disponiveis: Especialidade[] } }>(`/api/web/variaveis/${id}/especialidades`)
  const setEspecialidade = (id: number, body: { codEspecialidade: number; descricao?: string }) => api.put(`/api/web/variaveis/${id}/especialidades`, body)
  const removeEspecialidade = (id: number, codEspecialidade: number) => api.del(`/api/web/variaveis/${id}/especialidades/${codEspecialidade}`)
  const setEspecialidadesLote = (body: { codEspecialidade: number; descricao?: string; variaveis: number[] }) => api.post('/api/web/variaveis/especialidades/lote', body)
  const getAnexos = (id: number) => api.get<{ success: boolean; data: AnexoContexto }>(`/api/web/variaveis/${id}/anexos`)
  const createAnexo = (id: number, form: FormData) => api.postForm(`/api/web/variaveis/${id}/anexos`, form)
  const deleteAnexo = (id: number, codAnexo: number) => api.del(`/api/web/variaveis/${id}/anexos/${codAnexo}`)
  const getEstudos = (id: number) => api.get<{ success: boolean; data: Array<Record<string, unknown>> }>(`/api/web/variaveis/${id}/estudos`)
  const listModelosModoTexto = (search = '') => api.get<{ success: boolean; data: Array<{ codModelo: number; nome: string; totalSecoes: number }> }>(`/api/web/variaveis/modelos-modo-texto?search=${encodeURIComponent(search)}`)
  const downloadModoTexto = (id: number) => api.getBlob(`/api/web/variaveis/modelos-modo-texto/${id}/arquivo`)
  const previewImportacaoCs = (arquivo: File) => { const form = new FormData(); form.append('arquivo', arquivo); return api.postForm<{ success: boolean; data: ImportacaoCsPreview }>('/api/web/variaveis/importacao-cs/preview', form) }
  const confirmarImportacaoCs = (body: ImportacaoCsPreview & { variaveisSelecionadas: string[] }) => api.post<{ success: boolean; data: { inseridas: number; ignoradas: number } }>('/api/web/variaveis/importacao-cs/confirmar', body)

  return {
    listGrupos,
    getMeta,
    createVariavel,
    getVariavelEdicao,
    updateVariavel,
    listVariaveis,
    getDetalhesCompletos,
    getCodigosVinculados,
    getReferenciasNormalidades,
    vincularNormalidadesReferencia,
    atualizarNormalidadeReferencia,
    desvincularNormalidadeReferencia,
    importarNormalidadesReferencia,
    salvarComentarioNormalidade,
    importarNormalidadesJson,
    getPadroesCliente,
    createPadraoCliente,
    updatePadraoCliente,
    deletePadraoCliente,
    setPadraoVigente,
    importarReferenciaPadrao,
    atualizarFaixaPadrao,
    salvarComentarioPadrao,
    getDependencias, deleteVariavel, alterarGrupo,
    getClassificacoes, createGrupoClassificacao, updateGrupoClassificacao, deleteGrupoClassificacao,
    createClassificacao, updateClassificacao, deleteClassificacao, getVariavelClassificacoes, setVariavelClassificacoes,
    listCodigosUniversais, setCodigosUniversais,
    getEspecialidades, setEspecialidade, removeEspecialidade, setEspecialidadesLote,
    getAnexos, createAnexo, deleteAnexo, getEstudos,
    listModelosModoTexto, downloadModoTexto,
    previewImportacaoCs, confirmarImportacaoCs
  }
}
