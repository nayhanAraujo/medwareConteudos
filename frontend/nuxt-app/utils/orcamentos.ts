export const quoteStatuses: Record<string, string> = {
  rascunho: 'Rascunho', analise: 'Em análise', em_aprovacao: 'Aprovação interna', devolvido: 'Devolvido', aprovado: 'Aprovado internamente',
  enviado: 'Enviado', aceito: 'Aceito pelo cliente', pago: 'Pagamento confirmado', encaminhado: 'Encaminhado', cancelado: 'Cancelado', recusado: 'Recusado'
}
export const quoteCategories: Record<string, string> = { laudos: 'Laudos', impressoes: 'Impressões', relatorios: 'Relatórios', migracoes: 'Migrações / exportações', unificacoes: 'Unificações', frases: 'Frases', outras: 'Outras personalizações' }
export const quoteActions: Record<string, string> = { analise: 'Iniciar análise', solicitar_aprovacao: 'Solicitar aprovação', aprovar: 'Aprovar revisão', devolver: 'Devolver para ajustes', enviar: 'Registrar envio', aceitar: 'Registrar aceite', confirmar_pagamento: 'Confirmar pagamento', encaminhar: 'Encaminhar para execução', recusar: 'Registrar recusa', cancelar: 'Cancelar orçamento', arquivar: 'Arquivar', nova_revisao: 'Criar nova revisão', criar: 'Criação', editar: 'Edição', upload: 'Anexo adicionado', analisar_ia: 'Análise por IA solicitada', aplicar_ia: 'Sugestão de IA aplicada', cancelar_ia: 'Análise por IA cancelada' }
Object.assign(quoteActions, { email_iniciado:'Envio de e-mail iniciado',email_enviado:'Proposta aceita pelo SMTP',email_incerto:'Envio de e-mail não confirmado' })
Object.assign(quoteActions,{importar_email:'Solicitação importada do Outlook',confirmar_email:'Origem e destinatário confirmados',email_autorizado:'Envio pelo bot autorizado',email_revogado:'Autorização de envio revogada'})
export interface QuoteItem {
  key?:string|null; technicalContext?:import('./orcamentoTechnical').TechnicalContext|null; checkAnswers?:import('./orcamentoTechnical').CheckAnswer[]; pendingResolution?:{reason:string;actorId?:number|null;resolvedAtUtc?:string|null}|null
  category: string; description: string; quantity: number; complexity: string; complexityReason: string; reuse: string; dependencies: string; exclusions: string; assumptions: string; pending: string; blockingPending: boolean
  hours: { analysis: number; planning: number; development: number; tests: number; validation: number }
  mode: string; ruleId: string | null; unitPrice: number | null; manualValue: number | null; justification: string; discountPercent: number | null; discountValue: number | null; discountReason: string
}
export interface QuoteDraft { title: string; request: string; items: QuoteItem[]; validUntil: string | null; paymentTerms: string; deadlineValidated: boolean; deadline: string }
export interface QuotePolicy { hourlyRate: number | null; internalHourlyCost: number | null; validityDays: number | null; paymentTerms: string; rules: { id: string; label: string; category: string; formula: string; value: number; requiresManual: boolean }[] }
export interface CommercialClient { fiscalId: string; contact: string; email: string; monthlyFee: number | null }
export interface QuoteSnapshot {
  draft: QuoteDraft; client: { id: number; name: string; commercial: CommercialClient }; policy: QuotePolicy; policyVersion: string
  calculation: { hours: number; gross: number | null; discount: number | null; net: number | null; internalCost: number | null; complete: boolean; items: { hours: number; gross: number | null; discount: number | null; net: number | null; internalCost: number | null; pending: string | null }[] }
}
export interface QuoteHeader { id: string; clientId: number; clientName: string; title: string; revision: number; status: string; archived: boolean; total: number | null; version: string; updatedAtUtc: string }
export interface QuoteDetail {
  emailSource?:QuoteEmailSource|null
  analysisGate?:{processing:boolean;awaitingClassification:boolean;awaitingApplication:boolean}
  issues?:import('./orcamentoTechnical').QuoteIssue[]
  header: QuoteHeader; snapshot: QuoteSnapshot; editors: number[]
  events: { id: string; revision: number; action: string; fromStatus: string; toStatus: string; actorId: number; actorName: string; reason: string; reference: string; destination: string; createdAtUtc: string }[]
  attachments: { id: string; revision: number; name: string; mime: string; size: number; sha256: string; kind: string; actorId: number; createdAtUtc: string }[]
  revisions: { number: number; status: string; snapshot: QuoteSnapshot; createdBy: number; createdByName: string; createdAtUtc: string }[]
}
export interface QuoteEmailSource {
  origin:{mailbox:string;messageId:string;internetMessageId:string;conversationId:string|null;from:string;replyTo:string|null;subject:string;receivedAt:string}
  importedBy:number;confirmed:boolean
  confirmation:{revision:number;clientId:number;recipient:string;scopeHash:string;actorId:number;actorName:string;confirmedAtUtc:string}|null
}
export const newQuoteItem = (): QuoteItem => ({ category: 'laudos', description: '', quantity: 1, complexity: 'simples', complexityReason: '', reuse: '', dependencies: '', exclusions: '', assumptions: '', pending: '', blockingPending: false, hours: { analysis: 0, planning: 0, development: 0, tests: 0, validation: 0 }, mode: 'horas', ruleId: null, unitPrice: null, manualValue: null, justification: '', discountPercent: null, discountValue: null, discountReason: '' })
export const newQuoteDraft = (): QuoteDraft => ({ title: '', request: '', items: [newQuoteItem()], validUntil: null, paymentTerms: '', deadlineValidated: false, deadline: '' })
export const quoteEditable = (status: string) => ['rascunho', 'analise', 'devolvido'].includes(status)
export const quoteApproved = (status: string) => ['aprovado', 'enviado', 'aceito', 'pago', 'encaminhado'].includes(status)
export function quoteCommands(status: string, can: (permission: string) => boolean, isEditor: boolean, archived = false): string[] {
  if (archived) return []
  const actions: string[] = []
  if (can('elaborar')) {
    if (['rascunho', 'devolvido'].includes(status)) actions.push('analise')
    if (quoteEditable(status)) actions.push('solicitar_aprovacao')
    if (!quoteEditable(status)) actions.push('nova_revisao')
    if (!['cancelado', 'recusado', 'encaminhado'].includes(status)) actions.push('cancelar')
    else actions.push('arquivar')
  }
  if (can('aprovar') && status === 'em_aprovacao') { if (!isEditor) actions.push('aprovar'); actions.push('devolver') }
  if (can('registrar_marcos')) {
    if (status === 'aprovado') actions.push('enviar')
    if (status === 'enviado') actions.push('aceitar', 'recusar')
    if (status === 'aceito') actions.push('confirmar_pagamento')
    if (status === 'pago') actions.push('encaminhar')
  }
  return actions
}
export function quotePayload(draft: QuoteDraft): QuoteDraft {
  const copy = JSON.parse(JSON.stringify(draft)) as QuoteDraft
  const optional = (n: unknown) => n === '' || n == null ? null : Number(n)
  copy.validUntil ||= null
  copy.items = copy.items.map(i => ({ ...i, quantity: Number(i.quantity), ruleId: i.ruleId || null, unitPrice: optional(i.unitPrice), manualValue: optional(i.manualValue), discountValue: optional(i.discountValue), discountPercent: optional(i.discountPercent), hours: Object.fromEntries(Object.entries(i.hours).map(([k, v]) => [k, Number(v || 0)])) as QuoteItem['hours'] }))
  return copy
}
export const quoteMoney = (value: number | null | undefined) => value == null ? 'Pendente' : new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value)
export const quoteDate = (value: string) => new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))
