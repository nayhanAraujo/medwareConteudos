using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Modules.Permissions;

namespace MdwConteudos.Api.Modules.Orcamentos;

[ApiController,Authorize,Route("api/web/orcamentos"),TypeFilter(typeof(OrcamentoExceptionFilter))]
public sealed class OrcamentoAdminClientsController(OrcamentoService service,IAdminClientDirectory directory):ControllerBase
{
    [HttpPost("integracoes/emails/clientes/resolver"),RequirePermission("orcamentos","consultar_clientes_admin")]
    public async Task<object> Resolve(ResolveAdminClientRequest request,CancellationToken ct){Response.Headers.CacheControl="no-store";return new{success=true,data=await service.ResolveAdminClient(request,directory,ct)};}
    [HttpPost("clientes/admin/consultar"),RequirePermission("orcamentos","visualizar"),RequirePermission("orcamentos","importar_cliente_admin")]
    public async Task<object> Preview(AdminClientPreviewRequest request,CancellationToken ct){Response.Headers.CacheControl="no-store";return new{success=true,data=await service.PreviewAdminClient(request,directory,ct)};}
    [HttpPost("clientes/importar-admin"),RequirePermission("orcamentos","visualizar"),RequirePermission("orcamentos","importar_cliente_admin")]
    public async Task<object> Import(ImportAdminClientRequest request,CancellationToken ct){Response.Headers.CacheControl="no-store";return new{success=true,data=await service.ImportAdminClient(request,directory,ct)};}
}
