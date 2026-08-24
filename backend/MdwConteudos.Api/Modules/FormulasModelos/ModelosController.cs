using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Services;

namespace MdwConteudos.Api.Modules.FormulasModelos;

[ApiController]
[Route("api/web/modelos")]
[Authorize]
public sealed class ModelosController : ControllerBase
{
    private readonly IFormulasModelosService _service;

    public ModelosController(IFormulasModelosService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? nome = null,
        CancellationToken ct = default)
    {
        var result = await _service.ListModelosAsync(User.GetCodUsuario(), page, pageSize, nome, ct);
        return Ok(new
        {
            success = true,
            data = result.Items,
            result.Page,
            result.PageSize,
            result.TotalItems,
            result.TotalPages
        });
    }

    [HttpGet("variaveis")]
    public async Task<IActionResult> Variaveis(CancellationToken ct)
        => Ok(ApiResponse.Ok(await _service.ListVariaveisAsync(ct)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var result = await _service.GetModeloAsync(id, User.GetCodUsuario(), ct);
        return result is null
            ? NotFound(ApiResponse.Fail("not_found", "Modelo não encontrado.", 404))
            : Ok(ApiResponse.Ok(result));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] ModeloUpsertRequest request, CancellationToken ct)
    {
        try
        {
            var id = await _service.CreateModeloAsync(request, User.GetCodUsuario(), ct);
            return Ok(ApiResponse.Ok(new { codModelo = id }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail("validation_error", ex.Message));
        }
    }

    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] ModeloUpsertRequest request, CancellationToken ct)
        => await Write(() => _service.UpdateModeloAsync(id, request, User.GetCodUsuario(), ct), "Modelo atualizado com sucesso.");

    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await Write(() => _service.DeleteModeloAsync(id, User.GetCodUsuario(), ct), "Modelo excluído com sucesso.");

    [HttpPost("{modeloId:int}/secoes")]
    [Authorize]
    public async Task<IActionResult> CreateSecao(int modeloId, [FromBody] SecaoUpsertRequest request, CancellationToken ct)
    {
        try
        {
            var id = await _service.CreateSecaoAsync(modeloId, request, User.GetCodUsuario(), ct);
            return Ok(ApiResponse.Ok(new { codSecao = id }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail("validation_error", ex.Message));
        }
    }

    [HttpPut("{modeloId:int}/secoes/{secaoId:int}")]
    [Authorize]
    public async Task<IActionResult> UpdateSecao(int modeloId, int secaoId, [FromBody] SecaoUpsertRequest request, CancellationToken ct)
        => await Write(() => _service.UpdateSecaoAsync(modeloId, secaoId, request, User.GetCodUsuario(), ct), "Seção atualizada com sucesso.");

    [HttpDelete("{modeloId:int}/secoes/{secaoId:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteSecao(int modeloId, int secaoId, CancellationToken ct)
        => await Write(() => _service.DeleteSecaoAsync(modeloId, secaoId, User.GetCodUsuario(), ct), "Seção excluída com sucesso.");

    [HttpPut("{modeloId:int}/secoes/ordem")]
    [Authorize]
    public async Task<IActionResult> OrdenarSecoes(int modeloId, [FromBody] OrdenarSecoesRequest request, CancellationToken ct)
        => await Write(() => _service.OrderSecoesAsync(modeloId, request.SecaoIds ?? [], User.GetCodUsuario(), ct), "Ordem das seções atualizada.");

    [HttpPut("{modeloId:int}/secoes/{secaoId:int}/variaveis/ordem")]
    [Authorize]
    public async Task<IActionResult> OrdenarVariaveis(int modeloId, int secaoId, [FromBody] OrdenarVariaveisRequest request, CancellationToken ct)
        => await Write(() => _service.OrderVariaveisAsync(modeloId, secaoId, request.VariavelIds ?? [], User.GetCodUsuario(), ct), "Ordem das variáveis atualizada.");

    [HttpPut("{modeloId:int}/layout")]
    [Authorize]
    public async Task<IActionResult> SalvarLayout(int modeloId, [FromBody] SalvarLayoutRequest request, CancellationToken ct)
        => await Write(() => _service.SaveLayoutAsync(modeloId, request.Layout ?? [], User.GetCodUsuario(), ct), "Layout atualizado com sucesso.");

    [HttpPut("{modeloId:int}/composicao")]
    [Authorize]
    public async Task<IActionResult> SalvarComposicao(int modeloId, [FromBody] SalvarComposicaoRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.SaveComposicaoAsync(modeloId, request, User.GetCodUsuario(), ct);
            return Ok(ApiResponse.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail("validation_error", ex.Message));
        }
    }

    [HttpPost("{modeloId:int}/preview")]
    [Authorize]
    public async Task<IActionResult> Preview(int modeloId, [FromBody] SalvarComposicaoRequest request, CancellationToken ct)
    {
        try
        {
            var text = await _service.PreviewComposicaoAsync(modeloId, request, User.GetCodUsuario(), ct);
            return Ok(ApiResponse.Ok(new { conteudo = text }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail("validation_error", ex.Message));
        }
    }

    [HttpGet("{modeloId:int}/gerar")]
    public async Task<IActionResult> Gerar(int modeloId, [FromQuery] string formato = "html", [FromQuery] bool download = false, CancellationToken ct = default)
    {
        try
        {
            var generated = await _service.GenerateModeloAsync(modeloId, User.GetCodUsuario(), formato, ct);
            if (download)
                return File(Encoding.UTF8.GetBytes(generated.Conteudo), generated.ContentType, generated.NomeArquivo);

            return Content(generated.Conteudo, generated.ContentType, Encoding.UTF8);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse.Fail("not_found", ex.Message, 404));
        }
    }

    private async Task<IActionResult> Write(Func<Task> action, string message)
    {
        try
        {
            await action();
            return Ok(ApiResponse.OkMessage(message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail("validation_error", ex.Message));
        }
    }
}
