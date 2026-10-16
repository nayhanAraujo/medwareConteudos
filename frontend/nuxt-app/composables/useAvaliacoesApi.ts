export interface AvaliacaoPublica {
  id: number
  codScriptLaudo: number
  codVersao: number | null
  numeroVersao: string | null
  nomeAvaliador: string
  notaGeral: number
  recomendacao: string
  facilidadeUso: number | null
  qualidadeConteudo: number | null
  completude: number | null
  aparencia: number | null
  utilidadeClinica: number | null
  titulo: string | null
  comentario: string | null
  criadoEm: string
  atualizadoEm: string
}
export interface Avaliacao extends AvaliacaoPublica {
  origem: string
  clienteId: string
  usuarioId: string
  nomeClinica: string
  nomeScript: string
  situacao: string
  visivel: boolean
}
export interface AvaliacaoEvento {
  id: number
  tipo: string
  autor: string
  codUsuario: number | null
  dataEvento: string
  antes: string | null
  depois: string | null
  observacao: string | null
}
export interface AvaliacaoResumo {
  quantidade: number
  mediaGeral: number | null
  facilidadeUso: number | null
  qualidadeConteudo: number | null
  completude: number | null
  aparencia: number | null
  utilidadeClinica: number | null
  distribuicaoNotas: Record<string, number>
  recomendacoes: Record<string, number>
}
export function useAvaliacoesApi() {
  const api = useApi()
  return {
    list: (query: string) => api.get<{ data: Avaliacao[]; page: number; totalItems: number; totalPages: number }>(`/api/web/avaliacoes?${query}`),
    summary: (query: string) => api.get<{ data: AvaliacaoResumo }>(`/api/web/avaliacoes/resumo?${query}`),
    detail: (id: number) => api.get<{ data: Avaliacao; historico: AvaliacaoEvento[] }>(`/api/web/avaliacoes/${id}`),
    follow: (id: number, situacao: string) => api.put(`/api/web/avaliacoes/${id}/acompanhamento`, { situacao }),
    note: (id: number, texto: string) => api.post(`/api/web/avaliacoes/${id}/anotacoes`, { texto }),
    visibility: (id: number, visivel: boolean, justificativa: string) => api.put(`/api/web/avaliacoes/${id}/visibilidade`, { visivel, justificativa })
  }
}
