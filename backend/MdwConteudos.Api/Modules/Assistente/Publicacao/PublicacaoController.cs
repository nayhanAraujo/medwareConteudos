using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Modules.Permissions;

namespace MdwConteudos.Api.Modules.Assistente.Publicacao;

[ApiController, Authorize]
[Route("api/web/assistente/publicacao")]
public sealed class PublicacaoController(PublicacaoService service) : ControllerBase
{
    [RequirePermission(PermissionDomains.Assistente, PermissionActions.Visualizar)]
    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken ct) => await Handle(() => service.Status(ct));

    [RequirePermission(PermissionDomains.Assistente, PermissionActions.Visualizar)]
    [HttpGet("scripts")]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int? scriptId = null,
        [FromQuery] string? search = null, [FromQuery] int? pacote = null, [FromQuery] string? estado = null,
        [FromQuery] int? aprovado = null, CancellationToken ct = default)
        => await Handle(() => service.List(page, scriptId, ct, search: search, package: pacote, state: estado, approved: aprovado));

    [RequirePermission(PermissionDomains.Assistente, PermissionActions.Visualizar)]
    [HttpGet("pacotes/{id:int}/scripts")]
    public async Task<IActionResult> PackageScripts(int id, [FromQuery] int page = 1, [FromQuery] string? search = null, [FromQuery] int ativo = 1, CancellationToken ct = default)
        => await Handle(() => service.PackageScripts(id, page, search, ativo, ct));

    [RequirePermission(PermissionDomains.Assistente, PermissionActions.Visualizar)]
    [HttpPost("origens")]
    public async Task<IActionResult> Origins([FromBody] int[] ids, CancellationToken ct)
        => await Handle(async () => await service.TargetOrigins(ids.Distinct().Take(100).ToArray(), ct));

    [RequirePermission(PermissionDomains.Assistente, PermissionActions.Visualizar)]
    [HttpPost("estados")]
    public async Task<IActionResult> States([FromBody] int[] ids, CancellationToken ct)
        => await Handle(() => service.List(1, null, ct, ids.Distinct().Take(30).ToArray()));

    [RequirePermission(PermissionDomains.Assistente, PermissionActions.Vincular)]
    [HttpPut("pacotes/{id:int}")]
    public async Task<IActionResult> Mapping(int id, [FromBody] MappingRequest request, CancellationToken ct)
        => await Handle(async () => { await service.SaveMapping(id, request.Especialidades, ct); return new { ok = true }; });

    [RequirePermission(PermissionDomains.Assistente, PermissionActions.Vincular)]
    [HttpPut("pacotes/{package:int}/scripts/{script:int}")]
    public async Task<IActionResult> ScriptMapping(int package, int script, [FromBody] MappingRequest request, CancellationToken ct)
        => await Handle(async () => { await service.SaveScriptMapping(package, script, request.Especialidades, ct); return new { ok = true }; });

    [RequirePermission(PermissionDomains.Assistente, PermissionActions.Vincular)]
    [HttpPut("pacotes/{package:int}/scripts")]
    public async Task<IActionResult> BulkScriptMapping(int package, [FromBody] BulkMappingRequest request, CancellationToken ct)
        => await Handle(async () => { await service.SaveBulkScriptMapping(package, request.Scripts, request.Especialidades, ct); return new { ok = true }; });

    [RequirePermission(PermissionDomains.Assistente, PermissionActions.Aprovar)]
    [HttpPost("scripts/{id:int}/repetir")]
    public async Task<IActionResult> Retry(int id, CancellationToken ct)
        => await Handle(async () => { await service.Enqueue(id, false, ct); return new { ok = true }; });

    [RequirePermission(PermissionDomains.Assistente, PermissionActions.Aprovar)]
    [HttpPost("scripts/{id:int}/retomar")]
    public async Task<IActionResult> Resume(int id, CancellationToken ct)
        => await Handle(async () => { await service.Enqueue(id, true, ct); return new { ok = true }; });

    [RequirePermission(PermissionDomains.Assistente, PermissionActions.Visualizar)]
    [HttpGet("scripts/{id:int}/candidatos")]
    public async Task<IActionResult> Candidates(int id, CancellationToken ct)
        => await Handle(() => service.Candidates(id, ct));

    [RequirePermission(PermissionDomains.Assistente, PermissionActions.Vincular)]
    [HttpPost("scripts/{id:int}/conciliar")]
    public async Task<IActionResult> Reconcile(int id, [FromBody] ReconcileRequest request, CancellationToken ct)
        => await Handle(async () => { await service.Reconcile(id, request.Destino, ct, request.MrdDestino); return new { ok = true }; });

    private async Task<IActionResult> Handle(Func<Task<object>> action)
    {
        try { return Ok(await action()); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    public sealed record MappingRequest(int[] Especialidades);
    public sealed record BulkMappingRequest(int[] Scripts, int[] Especialidades);
    public sealed record ReconcileRequest(int Destino, int? MrdDestino = null);
}
