export function quoteCanEmail(status:string,current:boolean,archived:boolean,canRegister:boolean):boolean {
  return current && !archived && canRegister && ['aprovado','enviado'].includes(status)
}
export function pendingQuoteEmailAuthorization<T extends {action:string;revision:number;reference:string}>(events:T[],revision:number):T|undefined {
  const latest=events.find(e=>e.revision===revision&&e.action==='email_autorizado')
  return latest&&!events.some(e=>e.reference===latest.reference&&['email_iniciado','email_revogado'].includes(e.action))?latest:undefined
}
