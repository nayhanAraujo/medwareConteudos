import type { DsThemeName } from '../composables/useDsTheme'

export interface ContentModule {
  type: string
  title: string
  desc: string
  icon: string
  themeName: DsThemeName
  domain: string
  nuxtPath: string
  badge?: string
}

// Shared by the content hub and the library; each destination has one canonical entry.
export const contentModules: ContentModule[] = [
  { type: 'avaliacoes', title: 'Central de Avaliações', desc: 'Avaliações e acompanhamento dos modelos.', icon: 'chat-square-heart', themeName: 'green', domain: 'avaliacoes', nuxtPath: '/avaliacoes' },
  { type: 'modelos_laudos', title: 'Modelos de Laudos', desc: 'Scripts e modelos para laudos médicos.', icon: 'file-earmark-medical', themeName: 'blue', domain: 'scripts', nuxtPath: '/scripts/sistema', badge: 'Mais usado' },
  { type: 'pacotes', title: 'Pacotes', desc: 'Pacotes e seus modelos de laudo.', icon: 'box-seam', themeName: 'purple', domain: 'pacotes', nuxtPath: '/pacotes' },
  { type: 'frases_assistente', title: 'Grupo de frases do Assistente', desc: 'Frases e grupos do banco Assistente.', icon: 'quote', themeName: 'green', domain: 'assistente', nuxtPath: '/assistente/frases' },
  { type: 'banco_frases', title: 'Banco de frases', desc: 'Grupos e frases em HTML ou RTF.', icon: 'chat-left-text', themeName: 'orange', domain: 'conteudos', nuxtPath: '/conteudos/banco-de-frases' },
  { type: 'modelos_mensagens', title: 'Modelos de mensagens', desc: 'Grupos e mensagens reutilizáveis.', icon: 'chat-square-text', themeName: 'purple', domain: 'conteudos', nuxtPath: '/conteudos/modelos-mensagens' },
  { type: 'paineis', title: 'Painéis', desc: 'Dashboards e visualizações de dados.', icon: 'bar-chart-line', themeName: 'orange', domain: 'paineis', nuxtPath: '/paineis' },
  { type: 'relatorios', title: 'Relatórios', desc: 'Relatórios personalizados e documentos.', icon: 'file-earmark-bar-graph', themeName: 'gray', domain: 'relatorios', nuxtPath: '/relatorios' },
  { type: 'variaveis', title: 'Banco de referências e variáveis', desc: 'Variáveis e faixas de normalidade.', icon: 'database', themeName: 'purple', domain: 'variaveis', nuxtPath: '/variaveis' },
  { type: 'formulas', title: 'Fórmulas', desc: 'Fórmulas utilizadas nos cálculos.', icon: 'calculator', themeName: 'blue', domain: 'formulas', nuxtPath: '/formulas' },
  { type: 'referencias', title: 'Referências bibliográficas', desc: 'Referências, autores e documentos de apoio.', icon: 'journal-bookmark', themeName: 'green', domain: 'referencias', nuxtPath: '/referencias' },
  { type: 'impressos', title: 'Impressos', desc: 'Documentos MRD e scripts VBS.', icon: 'printer', themeName: 'slate', domain: 'conteudos', nuxtPath: '/impressos' },
  { type: 'modelos', title: 'Modelos e seções', desc: 'Modelos de modo texto e seus layouts.', icon: 'layout-text-window-reverse', themeName: 'gray', domain: 'formulas', nuxtPath: '/modelos' },
  { type: 'assistente', title: 'Assistente', desc: 'Procedimentos, scripts e modelos do banco Assistente.', icon: 'database-gear', themeName: 'dark', domain: 'assistente', nuxtPath: '/assistente' },
  { type: 'studio', title: 'Studio', desc: 'Conversão de laudos e edição de modelos.', icon: 'magic', themeName: 'rose', domain: 'studio', nuxtPath: '/studio' }
]

export function contentRouteDomain(path: string): string | undefined {
  if (path.startsWith('/conteudos/')) return 'conteudos'
  const root = path.split('/')[1]
  return ({ scripts: 'scripts', pacotes: 'pacotes', paineis: 'paineis', relatorios: 'relatorios', variaveis: 'variaveis', formulas: 'formulas', referencias: 'referencias', impressos: 'conteudos', modelos: 'formulas', assistente: 'assistente', avaliacoes: 'avaliacoes', studio: 'studio' } as Record<string, string>)[root || '']
}
