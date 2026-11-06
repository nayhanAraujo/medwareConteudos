export interface StudioItem { id: string; codVariavel: number | null; descricao?: string | null; exibirGrafico: boolean; normalidadeOverride?: string | null }
export interface StudioSection { id: string; nome: string; coluna: 1 | 2; itens: StudioItem[]; codSecao?: number | null }
export interface StudioDocument { nome: string; secoes: StudioSection[]; modeloOrigem?: number | null; versaoOrigem?: number | null }
export interface StudioDraft { id: string; revision: number; archived: boolean; document: StudioDocument; updatedAt: string }
export interface StudioVariable { codVariavel: number; nome: string; sigla: string; grupo?: string; codGrupo?: number; unidade?: string; formula?: string; normalidade?: string; classificacoes?: string; dependencias?: number[] }
export interface StudioRevision { revision: number; commandId: string; type: string; source: string; group: string; description: string; createdAt: string; document: StudioDocument }
