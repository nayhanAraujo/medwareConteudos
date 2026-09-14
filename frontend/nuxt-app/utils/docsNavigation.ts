export type DocsLocation = { definition: string; environment: string; section: string; operationId: string; tab: string; search: string }
export function readDocsLocation(query: Record<string, unknown>): DocsLocation {
  const text = (key: string) => typeof query[key] === 'string' ? query[key] as string : ''
  const operationId = text('operacao')
  return {
    definition: ['parceiros','interna','web'].includes(text('definicao')) ? text('definicao') : 'parceiros',
    environment: text('ambiente') === 'homologacao' ? 'homologacao' : 'atual',
    section: text('secao') || (operationId ? 'endpoints' : 'visao-geral'),
    operationId,
    tab: ['informacoes','parametros','respostas','esquema'].includes(text('aba')) ? text('aba') : 'informacoes',
    search: text('q'),
  }
}
export function writeDocsLocation(state: DocsLocation): Record<string, string> {
  const query: Record<string,string> = { definicao: state.definition, ambiente: state.environment, secao: state.section }
  if (state.operationId) { query.operacao = state.operationId; query.aba = state.tab }
  if (state.search) query.q = state.search
  return query
}
