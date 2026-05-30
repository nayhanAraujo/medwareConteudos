using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Services;

namespace MdwConteudos.Api.Modules.Web;

[ApiController]
[Route("api/web/referencias")]
[Authorize]
public class ReferenciasWebController : ControllerBase
{
    private readonly IReferenciasService _svc;

    public ReferenciasWebController(IReferenciasService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? titulo = null,
        [FromQuery] string? ano = null,
        [FromQuery] string? autor = null,
        [FromQuery] string? abreviacao = null,
        CancellationToken ct = default
    )
    {
        var result = await _svc.ListReferenciasAsync(page, pageSize, titulo, ano, autor, abreviacao, ct);
        return Ok(new
        {
            success = true,
            data = result.Items,
            page = result.Page,
            totalPages = result.TotalPages,
            totalItems = result.TotalItems
        });
    }

    [HttpGet("{codReferencia:int}")]
    public async Task<IActionResult> Get(int codReferencia, CancellationToken ct = default)
    {
        var data = await _svc.GetReferenciaAsync(codReferencia, ct);
        if (data is null) return NotFound(new { message = "Referência não encontrada." });
        return Ok(ApiResponse.Ok(data));
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] ReferenciaUpsertRequest req, CancellationToken ct = default)
    {
        try
        {
            var cod = await _svc.CreateReferenciaAsync(req, User.GetCodUsuario(), ct);
            return Ok(new { success = true, codReferencia = cod });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{codReferencia:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int codReferencia, [FromBody] ReferenciaUpsertRequest req, CancellationToken ct = default)
    {
        try
        {
            await _svc.UpdateReferenciaAsync(codReferencia, req, User.GetCodUsuario(), ct);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{codReferencia:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int codReferencia, CancellationToken ct = default)
    {
        try
        {
            await _svc.DeleteReferenciaAsync(codReferencia, ct);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("meta")]
    public async Task<IActionResult> Meta(CancellationToken ct = default)
    {
        var especialidades = await _svc.ListEspecialidadesAsync(ct);
        var tipos = await _svc.ListTiposReferenciaAsync(ct);
        return Ok(new { success = true, especialidades, tipos });
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q = null, CancellationToken ct = default)
    {
        var data = await _svc.SearchReferenciasAsync(q, ct);
        return Ok(data);
    }

    [HttpGet("{codReferencia:int}/anexos")]
    public async Task<IActionResult> ListAnexos(int codReferencia, CancellationToken ct = default)
    {
        var data = await _svc.ListAnexosAsync(codReferencia, ct);
        return Ok(ApiResponse.Ok(data, data.Count));
    }

    [HttpGet("get_anexos/{codReferencia:int}")]
    public async Task<IActionResult> GetAnexosCompatUnderscore(int codReferencia, CancellationToken ct = default)
    {
        var data = await _svc.ListAnexosCompatAsync(codReferencia, ct);
        return Ok(data);
    }

    [HttpGet("get-anexos/{codReferencia:int}")]
    public async Task<IActionResult> GetAnexosCompat(int codReferencia, CancellationToken ct = default)
    {
        var data = await _svc.ListAnexosCompatAsync(codReferencia, ct);
        return Ok(data);
    }

    [HttpPost("{codReferencia:int}/anexos")]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> CreateAnexo(
        int codReferencia,
        [FromForm] string descricao,
        [FromForm] string nome,
        [FromForm] string? link,
        [FromForm] IFormFile? arquivo,
        CancellationToken ct = default
    )
    {
        try
        {
            UploadedFileContent? file = null;
            if (arquivo is { Length: > 0 })
            {
                await using var ms = new MemoryStream();
                await arquivo.CopyToAsync(ms, ct);
                file = new UploadedFileContent(arquivo.FileName, ms.ToArray());
            }
            var cod = await _svc.CreateAnexoAsync(
                codReferencia,
                new AnexoUpsertRequest(descricao, nome, link),
                file,
                User.GetCodUsuario(),
                ct
            );
            return Ok(new { success = true, codAnexo = cod });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("anexos/{codAnexo:int}")]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> UpdateAnexo(
        int codAnexo,
        [FromForm] string descricao,
        [FromForm] string nome,
        [FromForm] string? link,
        [FromForm] IFormFile? arquivo,
        CancellationToken ct = default
    )
    {
        try
        {
            UploadedFileContent? file = null;
            if (arquivo is { Length: > 0 })
            {
                await using var ms = new MemoryStream();
                await arquivo.CopyToAsync(ms, ct);
                file = new UploadedFileContent(arquivo.FileName, ms.ToArray());
            }
            var codReferencia = await _svc.UpdateAnexoAsync(
                codAnexo,
                new AnexoUpsertRequest(descricao, nome, link),
                file,
                User.GetCodUsuario(),
                ct
            );
            return Ok(new { success = true, codReferencia });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("anexos/{codAnexo:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteAnexo(int codAnexo, CancellationToken ct = default)
    {
        try
        {
            var codReferencia = await _svc.DeleteAnexoAsync(codAnexo, ct);
            return Ok(new { success = true, codReferencia });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("autores")]
    public async Task<IActionResult> ListAutores(CancellationToken ct = default)
    {
        var data = await _svc.ListAutoresAsync(ct);
        return Ok(ApiResponse.Ok(data, data.Count));
    }

    [HttpGet("{codReferencia:int}/autores")]
    public async Task<IActionResult> ListAutoresByReferencia(int codReferencia, CancellationToken ct = default)
    {
        var data = await _svc.ListAutoresByReferenciaAsync(codReferencia, ct);
        return Ok(ApiResponse.Ok(data, data.Count));
    }

    [HttpPost("{codReferencia:int}/autores")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SaveAutoresByReferencia(
        int codReferencia,
        [FromBody] SaveAutoresRequest req,
        CancellationToken ct = default
    )
    {
        await _svc.SaveAutoresByReferenciaAsync(codReferencia, req.AutorIds ?? [], ct);
        return Ok(new { success = true });
    }
}
