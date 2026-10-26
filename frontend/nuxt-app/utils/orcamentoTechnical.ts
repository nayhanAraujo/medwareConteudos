export interface NamedCode { id:string; name:string; active:boolean }
export interface Technology { id:number; name:string }
export interface RuleCheck { id:string; label:string; required:boolean }
export interface TechnicalRule { id:string; name:string; active:boolean; category:string|null; technologyId:number|null; profileId:string|null; serviceIds:string[]; instructions:string; checks:RuleCheck[] }
export interface TechnicalCatalog { profiles:NamedCode[]; services:NamedCode[]; rules:TechnicalRule[] }
export interface TechnicalFront { id:string; category:string; technologyId:number|null; profileId:string|null; serviceIds:string[]; description:string }
export interface TechnicalContext { catalogVersion:string; front:TechnicalFront; technologyName:string|null; profileName:string; rules:TechnicalRule[] }
export interface CheckAnswer { id:string; status:string; reason:string; actorId?:number|null; resolvedAtUtc?:string|null }
export interface QuoteIssue { code:string; severity:string; itemIndex:number|null; message:string; effect:string; field:string; action:string; step:number }
export function needsGenericConfirmation(fronts:TechnicalFront[],catalog:TechnicalCatalog):boolean {
 return fronts.some(f=>!catalog.rules.some(r=>r.active&&(r.category==null||r.category===f.category)&&(r.technologyId==null||r.technologyId===f.technologyId)&&(r.profileId==null||r.profileId===f.profileId)&&(r.serviceIds.length===0||r.serviceIds.some(s=>f.serviceIds.includes(s)))&&(r.technologyId!=null||r.profileId!=null||r.serviceIds.length>0)))
}
