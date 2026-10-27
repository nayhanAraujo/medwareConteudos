export interface AdminClientPreviewData {
  resolution:{state:string;message:string;canImport:boolean;localId:number|null;candidate:{codigoAdmin:number;name:string;cnpj:string;active:boolean}|null}
  consultationId:string|null;expiresAtUtc:string|null
}
export function canImportAdminClient(preview:AdminClientPreviewData|null,now=Date.now()):boolean {
  return !!preview?.resolution.canImport && ['encontrado_admin','encontrado_local'].includes(preview.resolution.state)
    && !!preview.resolution.candidate?.active && preview.resolution.candidate.codigoAdmin>1
    && /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(preview.consultationId||'')
    && Number.isFinite(Date.parse(preview.expiresAtUtc||'')) && Date.parse(preview.expiresAtUtc!)>now
}
export function adminClientImportPayload(preview:AdminClientPreviewData|null,reason:string,confirmed:boolean,now=Date.now()) {
  if(!confirmed||!canImportAdminClient(preview,now)||!reason.trim()||reason.length>1000)throw new Error('Confirme uma consulta válida e justifique a importação. Consulte novamente se ela expirou.')
  // Only the server-owned consultation is submitted, never editable identity or commercial data.
  return {consultationId:preview!.consultationId,confirm:true,reason:reason.trim()}
}
