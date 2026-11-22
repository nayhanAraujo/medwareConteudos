export type ChatfastStatistic = { code: string; value: number | null; previousValue?: number | null }
export type ChatfastSummary = { statistics: ChatfastStatistic[]; dailyVolume: { date: string; events: number }[]; trend: { start: string; end: string; deviationsPer100: number | null }[]; methodCodes: string[]; limitationCodes: string[] }
export type ChatfastPublicReport = { type: string; periodStart: string; periodEnd: string; generatedAt: string; complete: boolean; metrics: Record<string, number | null>; stale: boolean; summary?: ChatfastSummary }
export const chatfastStatistics: Record<string, { label: string; unit?: string }> = {
  analyzedConversations: { label: 'Conversas analisadas' }, handledConversations: { label: 'Conversas atendidas' }, newConversations: { label: 'Conversas novas' },
  outOfPatternConversations: { label: 'Conversas fora do padrão' }, longWaitOver60Snapshot: { label: 'Esperas > 1h no instante da coleta' }, criticalSelections: { label: 'Seleções críticas nos resumos' },
  deduplicatedDeviations: { label: 'Desvios deduplicados por técnico/conversa' }, deviationEvents: { label: 'Eventos de desvio' }, events: { label: 'Eventos no histórico' }, recurrenceEvents: { label: 'Eventos de reincidência' },
  noReplyNewConversations: { label: 'Conversas novas sem resposta' }, firstReplyMeasurements: { label: 'Medições de primeira resposta' }, audioMessages: { label: 'Áudios coletados' }, businessDays: { label: 'Dias úteis do período' },
  deviationsPer100: { label: 'Desvios a cada 100 conversas', unit: '/100' }, medianFirstReplyMinutes: { label: 'Primeira resposta · mediana', unit: 'min' }, meanFirstReplyMinutes: { label: 'Primeira resposta · média', unit: 'min' }
}
export const chatfastMethods: Record<string, string> = {
  businessHours08001800: 'Os tempos consideram o expediente: segunda a sexta, 08h–18h, no horário de Brasília.',
  uniqueTechnicianConversation: 'Desvios deduplicados por técnico e conversa. Uma transferência pode contar para mais de um técnico; não somar equipes para obter o total global.',
  newClientConversationFirstReply: 'Primeira resposta medida da primeira mensagem do cliente à primeira mensagem de técnico, apenas em conversas novas iniciadas pelo cliente.',
  globalConversationDeduplication: 'Conversas globais e volumes por técnico/equipe têm bases distintas. O total global não é a soma das equipes.',
  criticalSelectionsNotUniqueCases: 'Seleções críticas nos resumos podem repetir o mesmo caso em dias diferentes. Não representam necessariamente casos únicos.',
  longWaitSnapshot: 'Esperas superiores a uma hora representam uma fotografia no instante da coleta, não todos os atendimentos em acompanhamento.',
  dailyOutOfPatternWindows: 'Conversas fora do padrão foram selecionadas nas janelas da rodada e coleta da tarde; não equivalem a eventos de desvio do histórico.',
  botAuthoritativeState: 'O bot permanece responsável pelo estado dos casos. Uma marcação de tratado não comprova solução nem encerramento.'
}
export const chatfastLimitations: Record<string, string> = {
  unequalBusinessDays: 'Os períodos comparados têm quantidades diferentes de dias úteis. Quedas de totais brutos não comprovam melhora.',
  partialCollectionLastDay: 'A coleta do último dia termina antes do fim do expediente. Consulte o horário de cobertura exibido.',
  missingDeploymentSummary: 'Falta um dos resumos de Implantação no início do período.',
  incompleteCriticalEventExtraction: 'A extração de eventos críticos do histórico não cobre todos os resumos. Zero eventos não significa zero seleções críticas.',
  recurrenceCoverageChanged: 'A cobertura de reincidências mudou entre períodos. Não interpretar a redução isoladamente como melhora.',
  limitedFirstReplySample: 'Há técnicos/equipes com menos de cinco medições de primeira resposta. Essas comparações são inconclusivas.',
  audioTranscriptionPending: 'Parte da transcrição dos áudios ainda estava em andamento no fechamento do relatório.',
  deviationsOnlyMonitoringWindows: 'Os desvios são observados nas janelas de monitoramento e não cobrem continuamente todo o expediente.',
  snapshotNotLive: 'Relatório histórico: os dados não representam a situação ao vivo.', sourceFileNotRawAudit: 'Dados extraídos do arquivo da página fornecida; não houve nova auditoria dos atendimentos de origem.',
  noTreatmentEvidence: 'Os anexos não permitem estabelecer um total confiável de marcações de tratado.', trackerEmptyNotNoCriticalCases: 'O acompanhamento de casos está vazio neste snapshot; isso não significa ausência de seleções críticas nos resumos.'
}
export const formatChatfastNumber = (value: number | null | undefined) => value == null ? '—' : value.toLocaleString('pt-BR', { maximumFractionDigits: 2 })
