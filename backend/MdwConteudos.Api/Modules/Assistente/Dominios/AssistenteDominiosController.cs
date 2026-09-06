using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Permissions;

namespace MdwConteudos.Api.Modules.Assistente.Dominios;

[ApiController]
[Authorize]
[Route("api/web/assistente")]
public sealed class AssistenteDominiosController(IAssistenteDominiosService service) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, Type> RequestTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
    {
        ["especialidades"] = typeof(EspecialidadeRequest), ["grupos"] = typeof(GrupoRequest), ["frases"] = typeof(FraseRequest),
        ["operadoras"] = typeof(OperadoraRequest), ["grupos-operadoras"] = typeof(GrupoOperadoraRequest),
        ["tabela-procedimentos"] = typeof(TabelaProcedimentoRequest), ["procedimentos"] = typeof(ProcedimentoRequest),
        ["referencias"] = typeof(ReferenciaRequest), ["esquemas"] = typeof(EsquemaRequest), ["esquemas-fotos"] = typeof(EsquemaFotosRequest),
        ["scripts"] = typeof(ScriptRequest), ["paginas-fotos"] = typeof(PaginaFotosRequest)
    };

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) => Ok(ApiResponse.Ok(await service.Dashboard(ct)));

    [HttpGet("scripts/especialidades")]
    public async Task<IActionResult> ScriptEspecialidades(
        [FromQuery] string? search = null,
        [FromQuery] int? tipoScript = null,
        [FromQuery] int? status = null,
        CancellationToken ct = default)
        => await Handle(async () => Ok(ApiResponse.Ok(await service.ListScriptEspecialidades(search, tipoScript, status, ct))));

    [HttpGet("{domain}")]
    public async Task<IActionResult> List(
        string domain,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery] string? search = null,
        [FromQuery] int? groupId = null,
        [FromQuery] int? tipoScript = null,
        [FromQuery] int? status = null,
        [FromQuery] int? especialidade = null,
        CancellationToken ct = default)
        => await Handle(async () => { EnsureDomain(domain); var result = await service.List(domain, page, pageSize, search, groupId, tipoScript, status, especialidade, ct); return Ok(ApiResponse.Ok(new { result.Items, result.Page, result.PageSize }, result.Total)); });

    [HttpGet("{domain}/{id:int}")]
    public async Task<IActionResult> Get(string domain, int id, CancellationToken ct)
        => await Handle(async () => { EnsureDomain(domain); var row = await service.Get(domain, id, ct); return row is null ? NotFound(ApiResponse.Fail("Registro não encontrado")) : Ok(ApiResponse.Ok(row)); });

    [HttpPost("{domain}"), RequirePermission(PermissionDomains.Assistente, PermissionActions.Criar)]
    public async Task<IActionResult> Create(string domain, [FromBody] JsonElement body, CancellationToken ct)
        => await Handle(async () => { var request = Parse(domain, body); Validate(domain, request, isCreate: true); var id = await service.Create(domain, request, ct); return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(new { id })); });

    [HttpPut("{domain}/{id:int}"), RequirePermission(PermissionDomains.Assistente, PermissionActions.Editar)]
    public async Task<IActionResult> Update(string domain, int id, [FromBody] JsonElement body, CancellationToken ct)
        => await Handle(async () => { var request = Parse(domain, body); Validate(domain, request, isCreate: false); return await service.Update(domain, id, request, ct) ? Ok(ApiResponse.OkMessage("Registro atualizado.")) : NotFound(ApiResponse.Fail("Registro não encontrado")); });

    [HttpPut("{domain}/{id:int}/status"), RequirePermission(PermissionDomains.Assistente, PermissionActions.Ativar)]
    public async Task<IActionResult> SetStatus(string domain, int id, [FromBody] AssistenteStatusRequest request, CancellationToken ct)
        => await Handle(async () => { EnsureDomain(domain); return await service.SetStatus(domain, id, request.Status, ct) ? Ok(ApiResponse.OkMessage("Status atualizado.")) : NotFound(ApiResponse.Fail("Registro não encontrado")); });

    [HttpDelete("{domain}/{id:int}"), RequirePermission(PermissionDomains.Assistente, PermissionActions.Excluir)]
    public async Task<IActionResult> Delete(string domain, int id, CancellationToken ct)
        => await Handle(async () => { EnsureDomain(domain); return await service.Delete(domain, id, ct) ? Ok(ApiResponse.OkMessage("Registro excluído.")) : NotFound(ApiResponse.Fail("Registro não encontrado")); });

    [HttpPost("{domain}/excluir-lote"), RequirePermission(PermissionDomains.Assistente, PermissionActions.Excluir)]
    public async Task<IActionResult> DeleteMany(string domain, [FromBody] DeleteManyRequest request, CancellationToken ct)
        => await Handle(async () =>
        {
            EnsureDomain(domain);
            var deleted = await service.DeleteMany(domain, request.Ids ?? [], ct);
            return Ok(ApiResponse.Ok(new { totalExcluido = deleted }));
        });

    private static object Parse(string domain, JsonElement body)
    {
        EnsureDomain(domain);
        return JsonSerializer.Deserialize(body.GetRawText(), RequestTypes[domain], new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Corpo da requisição inválido.");
    }

    private static void EnsureDomain(string domain)
    {
        if (!RequestTypes.ContainsKey(domain)) throw new ArgumentException("Domínio inválido.");
    }

    private static void Validate(string domain, object request, bool isCreate)
    {
        static string Required(string? value, string field, int max)
        { var v = value?.Trim(); if (string.IsNullOrWhiteSpace(v)) throw new InvalidOperationException($"{field} é obrigatório."); if (v.Length > max) throw new InvalidOperationException($"{field} deve ter no máximo {max} caracteres."); return v; }
        static void Status(int value) { if (value is not (-1 or 0)) throw new InvalidOperationException("Status deve ser -1 (ativo) ou 0 (inativo)."); }
        switch (request)
        {
            case EspecialidadeRequest x: Required(x.Descricao, "Descrição", 80); break;
            case GrupoRequest x: Required(x.Grupo, "Grupo", 252); Status(x.Status); break;
            case FraseRequest x: Required(x.Codigo, "Código", 32); Required(x.Titulo, "Título", 252); Status(x.Status); break;
            case OperadoraRequest x: Required(x.RazaoSocial, "Razão social", 252); Required(x.NomeFantasia, "Nome fantasia", 252); if (x.RegistroAns?.Length > 6) throw new InvalidOperationException("Registro ANS deve ter no máximo 6 caracteres."); if (x.Cnpj?.Length > 14) throw new InvalidOperationException("CNPJ deve ter no máximo 14 caracteres."); break;
            case GrupoOperadoraRequest x: Required(x.Descricao, "Descrição", 252); break;
            case TabelaProcedimentoRequest x: Required(x.CodigoTuss, "Código TUSS", 10); Required(x.DescricaoTuss, "Descrição TUSS", 256); break;
            case ProcedimentoRequest x: if (x.CodEspecialidade <= 0) throw new InvalidOperationException("Especialidade é obrigatória."); if (x.Descricao is not null && x.Descricao.Length > 256) throw new InvalidOperationException("Descrição deve ter no máximo 256 caracteres."); Status(x.Status); break;
            case ReferenciaRequest x: Required(x.Descricao, "Descrição", 80); Required(x.Valor, "Valor", int.MaxValue); break;
            case EsquemaRequest x: Required(x.Descricao, "Descrição", 80); Required(x.Imagem, "Imagem", int.MaxValue); break;
            case EsquemaFotosRequest x: Required(x.Titulo, "Título", 252); break;
            case ScriptRequest x: Required(x.Titulo, "Título", 252); if (isCreate) Required(x.EstruturaScript, "Estrutura do script", int.MaxValue); if (x.TipoScript is < 1 or > 3) throw new InvalidOperationException("Tipo de script inválido."); Status(x.Status); break;
            case PaginaFotosRequest x: Required(x.Titulo, "Título", 128); Status(x.Status); break;
            default: throw new ArgumentException($"Requisição inválida para {domain}.");
        }
    }

    private async Task<IActionResult> Handle(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail("Requisição inválida", ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail("Validação", ex.Message)); }
    }
}
