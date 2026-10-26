export const quoteSteps = [
  { title: 'Solicitação', description: 'Identifique a demanda e reúna os materiais necessários.' },
  { title: 'Análise do escopo', description: 'Entenda a demanda com apoio opcional da IA ou prepare a elaboração manual.' },
  { title: 'Itens e valores', description: 'Revise serviços, estimativas e condições comerciais. Salve para recalcular no servidor.' },
  { title: 'Revisão e aprovação', description: 'Confira o escopo e as pendências antes de registrar a decisão de aprovação.' },
  { title: 'Proposta e acompanhamento', description: 'Consulte a proposta aprovada e acompanhe os marcos comerciais.' }
]
export function initialQuoteStep(status: string): number {
  if (status === 'em_aprovacao') return 4
  if (status === 'analise') return 2
  if (status === 'devolvido') return 3
  if (['aprovado','enviado','aceito','pago','encaminhado','recusado','cancelado'].includes(status)) return 5
  return 1
}
export function quoteStageActions(step: number, authorizedActions: string[]): string[] {
  const allowed = step === 4 ? ['solicitar_aprovacao','aprovar','devolver'] : step === 5 ? ['enviar','aceitar','confirmar_pagamento','encaminhar','recusar'] : []
  return authorizedActions.filter(action => allowed.includes(action))
}
export interface QuoteStepState {
  editStep?:number|null
  status:string; dirty:boolean; hasScope:boolean; hasTitle:boolean; itemCount:number; calculationComplete:boolean
  issues:{severity:string;step:number;message:string}[]
  analysisLoading:boolean; analysisDirty:boolean; processing:boolean; awaitingClassification:boolean; awaitingApplication:boolean
}
export interface QuoteAiProgress { loading:boolean;dirty:boolean;processing:boolean;awaitingClassification:boolean;awaitingApplication:boolean }
export function quoteStepBlock(current:number,next:number,state:QuoteStepState):{step:number;reason:string}|null {
  if(next<=current)return null
  if(state.dirty && next===state.editStep)return null // Return to an already-open editor to fix unsaved data.
  if(state.dirty)return {step:current,reason:'Salve as alterações antes de avançar para outra etapa.'}
  // Closed/approved records remain browsable; navigation never replaces server authorization.
  if(['aprovado','enviado','aceito','pago','encaminhado','cancelado','recusado'].includes(state.status))return null
  if(state.status==='em_aprovacao')return next>4 ? {step:4,reason:'A revisão precisa ser aprovada antes de acessar a proposta e o acompanhamento.'} : null
  const requestIssue=state.issues.find(i=>i.severity==='impedimento'&&i.step===1)
  if(requestIssue)return {step:1,reason:requestIssue.message}
  if(!state.hasTitle || !state.hasScope)return {step:1,reason:'Conclua e salve a solicitação: informe o título e descreva o escopo ou anexe material de escopo.'}
  if(next>=3){
    if(state.analysisLoading)return {step:2,reason:'Aguarde a conferência do estado da análise por IA.'}
    if(state.processing)return {step:2,reason:'A análise por IA está em processamento. Aguarde sua conclusão ou cancele-a antes de continuar.'}
    if(state.awaitingClassification)return {step:2,reason:'Confirme a classificação e conclua a análise, ou descarte a classificação para seguir manualmente.'}
    if(state.awaitingApplication)return {step:2,reason:'Revise e aplique as sugestões da IA, ou descarte-as explicitamente para seguir manualmente.'}
    if(state.analysisDirty)return {step:2,reason:'Há um briefing ou escolhas de análise não enviados. Solicite a identificação ou descarte essas alterações para seguir manualmente.'}
  }
  if(next>=4){
    const issues=state.issues.filter(i=>i.severity==='impedimento'&&i.step<=3)
    if(!state.itemCount || !state.calculationComplete || issues.length)return {step:3,reason:issues.length?`Resolva os ${issues.length} impedimento(s) e salve antes de avançar. ${issues[0]!.message}`:'Conclua os itens e valores e salve o orçamento antes de avançar para revisão.'}
  }
  if(next>=5)return {step:4,reason:'Solicite a aprovação e aguarde a decisão de outro usuário autorizado antes de acessar a proposta.'}
  return null
}
