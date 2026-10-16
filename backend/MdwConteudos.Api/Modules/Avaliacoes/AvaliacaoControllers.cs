using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MdwConteudos.Api.Modules.Permissions;
using Swashbuckle.AspNetCore.Annotations;

namespace MdwConteudos.Api.Modules.Avaliacoes;

public sealed class AvaliacaoExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not AvaliacaoException error) return;
        context.Result = new ObjectResult(new { success = false, error = error.Message, message = error.Message }) { StatusCode = error.Status };
        context.ExceptionHandled = true;
    }
}

[ApiController, TypeFilter(typeof(AvaliacaoExceptionFilter))]
[Route("apiconteudos/v1/scripts/{id:int}/avaliacoes")]
[Route("api/v1/scripts/{id:int}/avaliacoes")]
public sealed class AvaliacaoPublicaController(AvaliacaoService service) : ControllerBase
{
    [HttpPut]
    [SwaggerOperation(Summary = "Criar ou revisar avaliacao do script", Tags = new[] { "Avaliações" },
        Description = "Integracao servidor a servidor com JWT parceiro. codversao deve estar presente: null para principal, ID positivo para versao. Chave: origem + cliente_id + usuario_id + script + versao. Reenvio identico nao altera historico. Nao recebe dados de moderacao.")]
    [ProducesResponseType(typeof(AvaliacaoEnvelope<AvaliacaoSave>), 200), ProducesResponseType(typeof(AvaliacaoEnvelope<AvaliacaoSave>), 201)]
    public async Task<IActionResult> Save(int id, [FromBody] AvaliacaoInput body, CancellationToken ct)
    {
        var result = await service.SaveAsync(id, body, ct);
        return StatusCode(result.Criada ? 201 : 200, new { success = true, data = result });
    }

    [HttpGet]
    [ProducesResponseType(typeof(AvaliacaoPublicPage), 200)]
    [SwaggerOperation(Summary = "Listar avaliacoes publicadas", Tags = new[] { "Avaliações" })]
    public async Task<IActionResult> List(int id, [FromQuery(Name = "codversao")] int? version,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var rows = await service.ListAsync(new() { Script = id, Versao = version, Page = page, PageSize = pageSize }, true, ct);
        return Ok(new { success = true, data = rows.Data.Select(x => x.Publica()), rows.Page, rows.PageSize, rows.TotalItems, rows.TotalPages });
    }

    [HttpGet("resumo")]
    [ProducesResponseType(typeof(AvaliacaoEnvelope<AvaliacaoResumo>), 200)]
    [SwaggerOperation(Summary = "Indicadores das avaliacoes publicadas", Tags = new[] { "Avaliações" }, Description = "Sem codversao consolida todas as versoes; codversao=0 filtra o modelo principal. Criterios ausentes nao entram na media. Avaliacoes ocultas sao excluidas.")]
    public async Task<IActionResult> Summary(int id, [FromQuery(Name = "codversao")] int? version, CancellationToken ct)
        => Ok(new { success = true, data = await service.SummaryAsync(new() { Script = id, Versao = version }, true, ct) });

    [HttpGet("minha")]
    [ProducesResponseType(typeof(AvaliacaoEnvelope<AvaliacaoPublica>), 200)]
    [SwaggerOperation(Summary = "Recuperar avaliacao para revisao", Tags = new[] { "Avaliações" }, Description = "Uso exclusivo pelo backend parceiro, que valida a sessao do usuario. Informe codversao=0 para o principal. Nao devolve dados internos, da clinica ou da moderacao.")]
    public async Task<IActionResult> Mine(int id, [FromQuery(Name = "codversao")] int? version,
        [FromQuery] string origem, [FromQuery(Name = "cliente_id")] string cliente,
        [FromQuery(Name = "usuario_id")] string usuario, CancellationToken ct)
    {
        if (!Request.Query.ContainsKey("codversao")) throw new AvaliacaoException(400, "Informe codversao (0 para principal).");
        var result = await service.MineAsync(id, version, origem, cliente, usuario, ct);
        return result is null ? NotFound(new { success = false, message = "Avaliacao nao encontrada." }) : Ok(new { success = true, data = result });
    }
}

[ApiController, Authorize, TypeFilter(typeof(AvaliacaoExceptionFilter)), Route("api/web/avaliacoes")]
public sealed class AvaliacaoWebController(AvaliacaoService service) : ControllerBase
{
    [HttpGet, RequirePermission(PermissionDomains.Avaliacoes, PermissionActions.Visualizar)]
    public async Task<IActionResult> List([FromQuery] AvaliacaoFiltro filter, CancellationToken ct)
        => Ok(await service.ListAsync(filter, false, ct));

    [HttpGet("resumo"), RequirePermission(PermissionDomains.Avaliacoes, PermissionActions.Visualizar)]
    public async Task<IActionResult> Summary([FromQuery] AvaliacaoFiltro filter, CancellationToken ct)
        => Ok(new { data = await service.SummaryAsync(filter, false, ct) });

    [HttpGet("{id:long}"), RequirePermission(PermissionDomains.Avaliacoes, PermissionActions.Visualizar)]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
    {
        var result = await service.DetailAsync(id, ct);
        return Ok(new { data = result.Avaliacao, historico = result.Historico });
    }

    [HttpPut("{id:long}/acompanhamento"), RequirePermission(PermissionDomains.Avaliacoes, PermissionActions.Acompanhar)]
    public Task<IActionResult> Follow(long id, [FromBody] AcompanhamentoInput input, CancellationToken ct)
        => Change(id, "triagem", input.Situacao, null, ct);

    [HttpPost("{id:long}/anotacoes"), RequirePermission(PermissionDomains.Avaliacoes, PermissionActions.Acompanhar)]
    public Task<IActionResult> Note(long id, [FromBody] AnotacaoInput input, CancellationToken ct)
        => Change(id, "anotacao", input.Texto, null, ct);

    [HttpPut("{id:long}/visibilidade"), RequirePermission(PermissionDomains.Avaliacoes, PermissionActions.Moderar)]
    public Task<IActionResult> Visibility(long id, [FromBody] VisibilidadeInput input, CancellationToken ct)
        => Change(id, "visibilidade", input.Justificativa, input.Visivel, ct);

    private async Task<IActionResult> Change(long id, string kind, string text, bool? visible, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var user)) return Unauthorized();
        await service.ChangeAsync(id, kind, text, visible, $"usuario:{user}", user, ct);
        return Ok(new { success = true });
    }
}
