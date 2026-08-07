export interface ScriptComparacaoResumo {
  codScriptLaudo: number
  nome: string
  descricao?: string
  sistema?: string
  linguagem?: string
}

export interface ScriptVersaoComparacaoItem {
  codVersao: number
  numeroVersao: string
  descricaoAlteracoes?: string
  alteracoesInterface?: string
  alteracoesCodigo?: string
  dataCriacao?: string
  usuarioResponsavel?: string
  ativo: boolean
  aprovado: boolean
  aprovadoPor?: string
}

export interface ScriptsComparacaoCatalogo {
  script: ScriptComparacaoResumo
  versoes: ScriptVersaoComparacaoItem[]
}

export interface ScriptsComparacaoResultado {
  script: ScriptComparacaoResumo
  versao1: ScriptVersaoComparacaoItem
  versao2: ScriptVersaoComparacaoItem
  diasEntreVersoes?: number
  mesmoResponsavel: boolean
  mesmoStatusAprovacao: boolean
}

export function useScriptsComparacaoApi() {
  const api = useApi()

  const listar = (scriptId: number) =>
    api.get<{ data: ScriptsComparacaoCatalogo }>(`/api/web/scripts/${scriptId}/comparacao-versoes`)

  const comparar = (scriptId: number, versao1: number, versao2: number) => {
    const params = new URLSearchParams({ versao1: String(versao1), versao2: String(versao2) })
    return api.get<{ data: ScriptsComparacaoResultado }>(
      `/api/web/scripts/${scriptId}/comparacao-versoes/comparar?${params}`
    )
  }

  return { listar, comparar }
}
