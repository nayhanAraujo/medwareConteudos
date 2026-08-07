using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Services;

namespace MdwConteudos.Api.Modules.PaineisComplementos;

[ApiController]
[Authorize]
[Route("api/web/paineis/{painelId:int}/versoes")]
public sealed class PaineisVersoesController(IPaineisVersoesService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int painelId, CancellationToken ct) => await Read(async () =>
    {
        var rows = await service.ListAsync(painelId, ct);
        return Ok(ApiResponse.Ok(rows, rows.Count));
    });

    [HttpGet("{versaoId:int}")]
    public async Task<IActionResult> Get(int painelId, int versaoId, CancellationToken ct) => await Read(async () =>
    {
        var data = await service.GetAsync(painelId, versaoId, ct);
        return data is null ? NotFound(ApiResponse.Fail("Não encontrado", "Versão não encontrada.", 404)) : Ok(ApiResponse.Ok(data));
    });

    [HttpPost]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> Create(int painelId, [FromForm] VersaoPainelForm form, CancellationToken ct) => await Write(async () =>
    {
        var id = await service.CreateAsync(painelId, form, User.GetCodUsuario(), User.Identity?.Name ?? User.GetCodUsuario().ToString(), ct);
        return Ok(ApiResponse.Ok(new { codVersaoPainel = id }));
    });

    [HttpPut("{versaoId:int}")]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> Update(int painelId, int versaoId, [FromForm] VersaoPainelForm form, CancellationToken ct) => await Write(async () =>
    {
        await service.UpdateAsync(painelId, versaoId, form, User.Identity?.Name ?? User.GetCodUsuario().ToString(), ct);
        return Ok(ApiResponse.OkMessage("Versão atualizada com sucesso."));
    });

    [HttpDelete("{versaoId:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int painelId, int versaoId, CancellationToken ct) => await Write(async () =>
    {
        await service.DeleteAsync(painelId, versaoId, ct);
        return Ok(ApiResponse.OkMessage("Versão excluída com sucesso."));
    });

    [HttpGet("{versaoId:int}/download/{tipo}")]
    public async Task<IActionResult> Download(int painelId, int versaoId, string tipo, [FromQuery] int? imagemId, CancellationToken ct)
    {
        try
        {
            var file = await service.DownloadAsync(painelId, versaoId, tipo, imagemId, ct);
            return File(file.Bytes, file.ContentType, file.FileName);
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail("Não encontrado", ex.Message, 404)); }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse.Fail("Validação", ex.Message)); }
    }

    [HttpDelete("{versaoId:int}/imagens/{imagemId:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteImage(int painelId, int versaoId, int imagemId, CancellationToken ct) => await Write(async () =>
    {
        await service.DeleteImageAsync(painelId, versaoId, imagemId, ct);
        return Ok(ApiResponse.OkMessage("Imagem excluída com sucesso."));
    });

    [HttpPost("{versaoId:int}/metricas")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AddMetric(int painelId, int versaoId, MetricaRequest request, CancellationToken ct) => await Write(async () =>
    {
        await service.AddMetricaAsync(painelId, versaoId, request, ct);
        return Ok(ApiResponse.OkMessage("Métrica adicionada com sucesso."));
    });

    [HttpPost("{versaoId:int}/dimensoes")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AddDimension(int painelId, int versaoId, DimensaoRequest request, CancellationToken ct) => await Write(async () =>
    {
        await service.AddDimensaoAsync(painelId, versaoId, request, ct);
        return Ok(ApiResponse.OkMessage("Dimensão adicionada com sucesso."));
    });

    [HttpPost("{versaoId:int}/fontes")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AddSource(int painelId, int versaoId, FonteDadosRequest request, CancellationToken ct) => await Write(async () =>
    {
        await service.AddFonteAsync(painelId, versaoId, request, ct);
        return Ok(ApiResponse.OkMessage("Fonte de dados adicionada com sucesso."));
    });

    private static async Task<IActionResult> Read(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (KeyNotFoundException ex) { return new NotFoundObjectResult(ApiResponse.Fail("Não encontrado", ex.Message, 404)); }
    }

    private static async Task<IActionResult> Write(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (KeyNotFoundException ex) { return new NotFoundObjectResult(ApiResponse.Fail("Não encontrado", ex.Message, 404)); }
        catch (InvalidOperationException ex) { return new BadRequestObjectResult(ApiResponse.Fail("Validação", ex.Message)); }
    }
}
