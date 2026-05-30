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
  }>
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

export function useVariaveisApi() {
  const api = useApi()

  const listGrupos = () =>
    api.get<{ success: boolean; data: GrupoVariavelDto[] }>('/api/web/variaveis/grupos')

  const listVariaveis = (q: { skip?: number; take?: number; search?: string; grupo?: number | '' }) => {
    const params = new URLSearchParams()
    Object.entries(q).forEach(([k, v]) => {
      if (v !== undefined && v !== null && String(v).trim() !== '') params.set(k, String(v))
    })
    return api.get<{ success: boolean; data: VariavelListItem[]; total: number }>(
      `/api/web/variaveis?${params.toString()}`
    )
  }

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

  return {
    listGrupos,
    listVariaveis,
    getReferenciasNormalidades,
    vincularNormalidadesReferencia,
    atualizarNormalidadeReferencia,
    desvincularNormalidadeReferencia,
    importarNormalidadesReferencia
  }
}
