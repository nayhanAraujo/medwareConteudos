using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Modules.Permissions;

namespace MdwConteudos.Api.Modules.Orcamentos;

[ApiController,Authorize,Route("api/web/orcamentos"),TypeFilter(typeof(OrcamentoExceptionFilter))]
public sealed class OrcamentoEmailIntegrationController(OrcamentoService service):ControllerBase
{
    [HttpPost("integracoes/emails/importar"),RequirePermission("orcamentos","importar_email")]
    public async Task<object> Import(ImportQuoteEmailRequest request,CancellationToken ct)=>new{success=true,data=await service.ImportEmail(request,ct)};
    [HttpPost("integracoes/emails/localizar"),RequirePermission("orcamentos","importar_email")]
    public async Task<object> Find(FindQuoteEmailRequest request,CancellationToken ct){Response.Headers.CacheControl="no-store";return new{success=true,id=await service.FindEmail(request,ct)};}
    [HttpGet("integracoes/emails/envios-pendentes"),RequirePermission("orcamentos","enviar_email_confirmado")]
    public Task<object> Pending([FromQuery]long? after,CancellationToken ct){Response.Headers.CacheControl="no-store";return service.PendingEmailDispatches(after,ct);}
    [HttpGet("integracoes/emails/clientes"),RequirePermission("orcamentos","importar_email")]
    public async Task<object> Clients(CancellationToken ct)=>new{success=true,data=await service.Clients(ct)};
    [HttpGet("integracoes/emails/{id:long}/resumo"),RequirePermission("orcamentos","importar_email")]
    public async Task<object> Summary(long id,CancellationToken ct){Response.Headers.CacheControl="no-store";return new{success=true,data=await service.EmailImportSummary(id,ct)};}
    [HttpPost("{id:long}/origem-email/confirmar"),RequirePermission("orcamentos","confirmar_email"),RequirePermission("orcamentos","visualizar")]
    public async Task<object> Confirm(long id,ConfirmQuoteEmailRequest request,CancellationToken ct){await service.ConfirmEmailSource(id,request,ct);return new{success=true,data=await service.Get(id,ct)};}
    [HttpPost("{id:long}/origem-email/autorizar-envio"),RequirePermission("orcamentos","confirmar_email"),RequirePermission("orcamentos","visualizar")]
    public async Task<object> Authorize(long id,AuthorizeQuoteEmailRequest request,CancellationToken ct){var requestId=await service.AuthorizeEmail(id,request,ct);return new{success=true,requestId,data=await service.Get(id,ct)};}
    [HttpPost("{id:long}/proposta/enviar-email-confirmado"),RequirePermission("orcamentos","enviar_email_confirmado")]
    public async Task<object> Dispatch(long id,DispatchQuoteEmailRequest request,[FromServices]IQuoteEmailSender sender,CancellationToken ct){await service.SendConfirmedEmail(id,request,sender,ct);return new{success=true,message="Mensagem aceita pelo SMTP. Confira o histórico; não repetir automaticamente."};}
    [HttpPost("{id:long}/origem-email/revogar-envio"),RequirePermission("orcamentos","confirmar_email"),RequirePermission("orcamentos","visualizar")]
    public async Task<object> Revoke(long id,RevokeQuoteEmailRequest request,CancellationToken ct){await service.RevokeEmail(id,request,ct);return new{success=true,data=await service.Get(id,ct)};}
    [HttpPost("{id:long}/origem-email/anexos"),RequirePermission("orcamentos","importar_email"),RequestSizeLimit(22*1024*1024),RequestFormLimits(MultipartBodyLengthLimit=22*1024*1024)]
    public async Task<object> Attach(long id,[FromForm]string version,IFormFile file,CancellationToken ct){await service.Upload(id,version,"escopo",file,ct,importedEmail:true);return new{success=true,data=await service.EmailImportSummary(id,ct)};}
}
