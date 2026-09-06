using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Permissions;
using MdwConteudos.Api.Services;

namespace MdwConteudos.Api.Modules.Web;

[ApiController]
[Route("api/web/paineis")]
[Authorize]
public class PaineisWebController : ControllerBase
{
    private readonly IPaineisWebService _svc;
    public PaineisWebController(IPaineisWebService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] string? tipo = null,
        [FromQuery] string? ativo = null,
        [FromQuery] int? clienteId = null,
        [FromQuery] int? moduloId = null,
        [FromQuery] int? pacoteId = null,
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 10,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await _svc.ListAsync(search, tipo, ativo, clienteId, moduloId, pacoteId, page, perPage, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message, error = ex.Message });
        }
    }

    [HttpGet("clientes")]
    public async Task<IActionResult> Clientes(CancellationToken ct) => Ok(await _svc.ListClientesAsync(ct));

    [HttpGet("modulos")]
    public async Task<IActionResult> Modulos(CancellationToken ct) => Ok(await _svc.ListModulosAsync(ct));

    [HttpGet("pacotes")]
    public async Task<IActionResult> Pacotes(CancellationToken ct) => Ok(await _svc.ListPacotesAsync(ct));

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct) => Ok(await _svc.GetTipoCountsAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var data = await _svc.GetAsync(id, ct);
        return data is null
            ? NotFound(new { success = false, message = "Painel não encontrado", error = "Painel não encontrado" })
            : Ok(ApiResponse.Ok(data));
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        try
        {
            var (bytes, fileName) = await _svc.DownloadPbixAsync(id, ct);
            return File(bytes, "application/octet-stream", fileName);
        }
        catch (InvalidOperationException ex)
        {
            var notFound = ex.Message.Contains("não encontrado", StringComparison.OrdinalIgnoreCase)
                           || ex.Message.Contains("não disponível", StringComparison.OrdinalIgnoreCase);
            return notFound
                ? NotFound(new { success = false, message = ex.Message, error = ex.Message })
                : BadRequest(new { success = false, message = ex.Message, error = ex.Message });
        }
    }

    [HttpPost]
    [RequirePermission(PermissionDomains.Paineis, PermissionActions.Criar)]
    [RequestSizeLimit(80_000_000)]
    public async Task<IActionResult> Create(
        [FromForm] string nome,
        [FromForm] int codModulo,
        [FromForm] string tipoPainel,
        [FromForm] string? descricao = null,
        [FromForm] int? codCliente = null,
        [FromForm] int ativo = 1,
        [FromForm] string? responsavelApi = null,
        [FromForm] string? diretorioPbix = null,
        [FromForm] string? nomeArquivoPbixMeta = null,
        [FromForm] string? workspacePowerbi = null,
        [FromForm] string? datasetPowerbi = null,
        [FromForm] string? gatewayPowerbi = null,
        [FromForm] string? frequenciaAtualizacao = null,
        [FromForm] string? responsavelAtualizacao = null,
        [FromForm] List<int>? pacotes = null,
        [FromForm] IFormFile? arquivoPbix = null,
        CancellationToken ct = default)
    {
        try
        {
            var (bytes, fileName) = await ReadPbixAsync(arquivoPbix, ct);
            var form = new PainelUpsertForm(
                nome, codModulo, tipoPainel, descricao, codCliente, ativo,
                responsavelApi, diretorioPbix, nomeArquivoPbixMeta, workspacePowerbi,
                datasetPowerbi, gatewayPowerbi, frequenciaAtualizacao, responsavelAtualizacao, pacotes);
            return Ok(await _svc.CreateAsync(form, bytes, fileName, User.GetCodUsuario(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message, error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission(PermissionDomains.Paineis, PermissionActions.Editar)]
    [RequestSizeLimit(80_000_000)]
    public async Task<IActionResult> Update(
        int id,
        [FromForm] string nome,
        [FromForm] int codModulo,
        [FromForm] string tipoPainel,
        [FromForm] string? descricao = null,
        [FromForm] int? codCliente = null,
        [FromForm] int ativo = 1,
        [FromForm] string? responsavelApi = null,
        [FromForm] string? diretorioPbix = null,
        [FromForm] string? nomeArquivoPbixMeta = null,
        [FromForm] string? workspacePowerbi = null,
        [FromForm] string? datasetPowerbi = null,
        [FromForm] string? gatewayPowerbi = null,
        [FromForm] string? frequenciaAtualizacao = null,
        [FromForm] string? responsavelAtualizacao = null,
        [FromForm] List<int>? pacotes = null,
        [FromForm] IFormFile? arquivoPbix = null,
        CancellationToken ct = default)
    {
        try
        {
            var (bytes, fileName) = await ReadPbixAsync(arquivoPbix, ct);
            var form = new PainelUpsertForm(
                nome, codModulo, tipoPainel, descricao, codCliente, ativo,
                responsavelApi, diretorioPbix, nomeArquivoPbixMeta, workspacePowerbi,
                datasetPowerbi, gatewayPowerbi, frequenciaAtualizacao, responsavelAtualizacao, pacotes);
            await _svc.UpdateAsync(id, form, bytes, fileName, ct);
            return Ok(ApiResponse.OkMessage("Painel atualizado com sucesso"));
        }
        catch (InvalidOperationException ex)
        {
            var notFound = ex.Message.Contains("não encontrado", StringComparison.OrdinalIgnoreCase);
            return notFound
                ? NotFound(new { success = false, message = ex.Message, error = ex.Message })
                : BadRequest(new { success = false, message = ex.Message, error = ex.Message });
        }
    }

    [HttpPatch("{id:int}/status")]
    [RequirePermission(PermissionDomains.Paineis, PermissionActions.Ativar)]
    public async Task<IActionResult> SetStatus(int id, [FromBody] PainelStatusRequest req, CancellationToken ct)
    {
        try
        {
            await _svc.SetStatusAsync(id, req.Ativo, ct);
            return Ok(ApiResponse.OkMessage("Status alterado com sucesso"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message, error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(PermissionDomains.Paineis, PermissionActions.Excluir)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await _svc.DeleteAsync(id, ct);
            return Ok(ApiResponse.OkMessage("Painel excluído com sucesso"));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { success = false, message = ex.Message, error = ex.Message });
        }
    }

    private static async Task<(byte[]? Bytes, string? FileName)> ReadPbixAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length <= 0) return (null, null);
        if (!file.FileName.EndsWith(".pbix", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Arquivo deve ter extensão .pbix");
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        return (ms.ToArray(), file.FileName);
    }
}
