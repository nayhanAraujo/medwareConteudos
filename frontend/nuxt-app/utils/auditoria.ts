export interface AuditEvent {
  id: string
  occurredAtUtc: string
  module: string
  action: string
  result: string
  entityType: string | null
  entityId: string | null
  entityName: string | null
  summary: string
  actorId: number | null
  actorName: string | null
  actorKind: string
  origin: string
  correlationId: string | null
  operationId: string | null
}

export interface AuditDetail extends AuditEvent {
  requestMethod: string | null
  requestPath: string | null
  ipAddress: string | null
  reasonCode: string | null
  changes: { field: string; before: unknown; after: unknown }[]
}

export interface AuditCatalog { modules: string[]; actions: string[]; results: string[] }
export interface AuditFilters {
  from: string; to: string; actorId: string; module: string; action: string
  result: string; entityType: string; entityId: string; correlationId: string
}

export const auditEntityLabels: Record<string, string> = {
  USUARIO: 'Usuário', PERFIL_PERMISSAO: 'Permissão de perfil', USUARIO_PERMISSAO: 'Exceção de usuário',
  SCRIPTLAUDO: 'Script', SCRIPTVERSOES: 'Versão de script', SCRIPTARQUIVOS: 'Anexo de script',
  SCRIPTVERSAOARQUIVOS: 'Anexo de versão', SCRIPTLAUDOMRD: 'MRD de script', SCRIPTVERSAOMRD: 'MRD de versão',
  PAINEIS: 'Painel', VERSOESPAINEL: 'Versão de painel', IMAGENSPAINEL: 'Imagem de painel',
  ARQUIVOSVERSOES: 'Arquivo de painel', RELATORIOS: 'Relatório', MODULORELATORIO: 'Módulo de relatório',
  VARIAVEIS: 'Variável', FORMULAS: 'Fórmula', REFERENCIA: 'Referência', NORMALIDADE: 'Normalidade',
  PACOTES: 'Pacote', CLIENTES: 'Cliente', UNIDADEMEDIDA: 'Unidade de medida', ESPECIALIDADE: 'Especialidade',
  SCRIPTLAUDO_VARIAVEL: 'Variável vinculada ao script', PAINEIS_PACOTES: 'Pacote vinculado ao painel',
  CONFIGURACOESAPI: 'Configuração API de painel', CONFIGURACOESPOWERBI: 'Configuração Power BI',
  METRICAS: 'Métrica de painel', DIMENSOES: 'Dimensão de painel', FONTESDADOS: 'Fonte de dados de painel',
  MODULOSSISTEMA: 'Módulo de sistema', PACOTESCOMERCIAIS: 'Pacote comercial', SISTEMA_MODULO: 'Sistema vinculado ao módulo',
  RELATORIOCOLUNAS: 'Coluna de relatório', RELATORIOVALIDACOES: 'Validação de relatório', RELATORIOFILTROS: 'Filtro de relatório',
  VARIAVEISALTERNATIVAS: 'Alternativa de variável', VARIAVEISNOMESCLINICOS: 'Nome clínico de variável',
  VARIAVEIS_CLASSIFICACOES: 'Classificação vinculada à variável', VARIAVEL_CODIGO_UNIVERSAL: 'Código vinculado à variável',
  VARIAVEL_ESPECIALIDADE: 'Especialidade vinculada à variável', ANEXO_VARIAVEL_FORMULA: 'Anexo vinculado à variável ou fórmula',
  FORMULA_VARIAVEL: 'Variável vinculada à fórmula', EQUACOESLINGUAGEM: 'Equação por linguagem',
  REFERENCIA_AUTORES: 'Autor vinculado à referência', ANEXOS: 'Anexo de referência', TIPOLINGUAGEM: 'Linguagem',
  NORMALIDADECOMENTARIO: 'Comentário de normalidade', CLIENTESPADRAONORMALIDADE: 'Padrão de normalidade por cliente',
  PADRAONORMALIDADEFAIXA: 'Faixa de normalidade por cliente', PADRAONORMALIDADECOMENTARIO: 'Comentário de padrão por cliente',
  CODIGOUNIVERSAL: 'Código universal', TIPOAUTOR: 'Tipo de autor', AUTORES: 'Autor',
  GRUPOSVARIAVEIS: 'Grupo de variáveis', GRUPOSCLASSIFICACOES: 'Grupo de classificações', CLASSIFICACOES: 'Classificação'
}

export const auditLabels: Record<string, string> = {
  scripts: 'Scripts', usuarios: 'Usuários', permissoes: 'Permissões', paineis: 'Painéis',
  relatorios: 'Relatórios', variaveis: 'Variáveis', formulas: 'Fórmulas', referencias: 'Referências',
  pacotes: 'Pacotes', cadastros: 'Cadastros', autenticacao: 'Autenticação', biblioteca: 'Biblioteca',
  criar: 'Criação', editar: 'Edição', excluir: 'Exclusão', ativar: 'Ativação', inativar: 'Inativação',
  aprovar: 'Aprovação', reprovar: 'Reprovação', vincular: 'Vínculo', desvincular: 'Remoção de vínculo',
  importar: 'Importação', upload: 'Anexo enviado', remover_anexo: 'Anexo removido', login: 'Login',
  redefinir_senha: 'Redefinição de senha', alterar_senha: 'Alteração de senha', download: 'Download',
  exportar: 'Exportação', acesso_negado: 'Acesso negado', sucesso: 'Sucesso', falha: 'Falha',
  negado: 'Negado', iniciado: 'Disponibilizado', usuario: 'Usuário', anonimo: 'Anônimo',
  integracao: 'Integração', sistema: 'Sistema', web: 'Sistema web'
}

export function auditLocalInput(value: Date): string {
  const part = (n: number) => String(n).padStart(2, '0')
  const minutes = `${value.getFullYear()}-${part(value.getMonth() + 1)}-${part(value.getDate())}T${part(value.getHours())}:${part(value.getMinutes())}`
  if (!value.getSeconds() && !value.getMilliseconds()) return minutes
  const seconds = `${minutes}:${part(value.getSeconds())}`
  return value.getMilliseconds() ? `${seconds}.${String(value.getMilliseconds()).padStart(3, '0')}` : seconds
}

export function auditFiltersFromQuery(query: Record<string, unknown>, now = new Date()): AuditFilters {
  const value = (key: string) => typeof query[key] === 'string' ? String(query[key]) : ''
  const date = (key: string, fallback: Date) => {
    const raw = value(key)
    if (!raw) return auditLocalInput(fallback)
    const parsed = new Date(raw)
    return Number.isNaN(parsed.getTime()) ? raw : auditLocalInput(parsed)
  }
  return {
    from: date('from', new Date(now.getTime() - 7 * 86400000)), to: date('to', now),
    actorId: value('actorId'), module: value('module'), action: value('action'), result: value('result'),
    entityType: value('entityType'), entityId: value('entityId'), correlationId: value('correlationId')
  }
}

export function auditFilterQuery(filters: AuditFilters): Record<string, string> {
  const from = new Date(filters.from), to = new Date(filters.to)
  if (!filters.from || !filters.to || Number.isNaN(from.getTime()) || Number.isNaN(to.getTime()))
    throw new Error('Informe datas válidas para o período.')
  if (from >= to) throw new Error('O fim do período deve ser posterior ao início.')
  if (filters.actorId && (!/^\d+$/.test(filters.actorId) || Number(filters.actorId) < 1 || Number(filters.actorId) > 2147483647))
    throw new Error('Informe um código de usuário válido.')
  const query: Record<string, string> = { from: from.toISOString(), to: to.toISOString() }
  for (const key of ['actorId', 'module', 'action', 'result', 'entityType', 'entityId', 'correlationId'] as const) {
    const value = filters[key].trim()
    if (value) query[key] = value
  }
  return query
}

export function formatAuditValue(value: unknown): string {
  if (value === null) return 'Não informado'
  if (value === undefined) return 'Não registrado'
  if (typeof value === 'boolean') return value ? 'Sim' : 'Não'
  if (typeof value === 'string') return value === '' ? '(vazio)' : value
  return JSON.stringify(value, null, 2) ?? 'Não registrado'
}

export function formatAuditDate(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return 'Data indisponível'
  return new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'medium' }).format(date)
}
