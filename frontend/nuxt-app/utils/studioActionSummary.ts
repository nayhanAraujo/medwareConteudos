import type {StudioVariable} from '~/types/studioDraft'
export function studioActionSummary(name: string, result: any, catalog: StudioVariable[] = []): string {
  if(result?.status==='awaiting_user_choice')return 'Aguardando sua escolha de medida.'
  if(result?.status!=='completed')return result?.message || 'A alteração não foi aplicada.'
  const operation=result.operation, payload=operation?.payload, sections=result.draft?.document?.secoes || []
  const section=sections.find((s:any)=>s.id===payload?.sectionId)
  if(operation?.type==='createSection')return `Seção ${payload.name} criada.`
  if(operation?.type==='editSection')return `Seção ${payload.name} atualizada.`
  if(operation?.type==='addMeasures'){
    const names=(payload.ids || []).map((id:number)=>catalog.find(v=>v.codVariavel===id)?.nome || sections.flatMap((s:any)=>s.itens).find((i:any)=>i.codVariavel===id)?.descricao || 'Medida')
    return `${names.join(', ')} ${names.length===1?'adicionada':'adicionadas'}${section ? ` em ${section.nome}` : ''}.`
  }
  if(operation?.type==='moveItem')return `Medida movida${section ? ` para ${section.nome}` : ''}.`
  if(name==='desfazer_alteracao')return 'Última alteração desfeita.'
  if(name==='refazer_alteracao')return 'Alteração refeita.'
  if(name==='buscar_medidas')return 'Consulta de medidas concluída.'
  if(name==='consultar_rascunho')return 'Modelo consultado.'
  return 'Estrutura do modelo atualizada.'
}
