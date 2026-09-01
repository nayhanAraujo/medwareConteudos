using MdwConteudos.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MdwConteudos.Api.Modules.Assistente.Importacao;

[ApiController]
[Authorize]
[Route("api/web/assistente/modelos")]
public sealed class AssistenteImportacaoController(IAssistenteImportacaoService service) : ControllerBase
{
    [HttpPost("importar")]
    [Authorize(Roles = "admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * AssistenteImportacaoValidator.MaxFileSize + 1024 * 1024)]
    public async Task<IActionResult> Import([FromForm] AssistenteImportacaoForm form, CancellationToken ct)
    {
        try { return Ok(ApiResponse.Ok(await service.ImportAsync(form, ct))); }
        catch (AssistenteImportacaoException ex) { return Conflict(ApiResponse.Fail("Validação", ex.Message, 409)); }
        catch (Exception ex) { return StatusCode(500, ApiResponse.Fail("Erro ao importar", ex.Message, 500)); }
    }

    [HttpGet("scripts/{id:int}/download")]
    public async Task<IActionResult> DownloadScript(int id, CancellationToken ct)
    {
        var stored = await service.DownloadScriptAsync(id, ct);
        if (stored is null) return NotFound(ApiResponse.Fail("Não encontrado", "Script não encontrado.", 404));
        try
        {
            return File(AssistenteImportacaoValidator.DecodeStored(stored.TipoScript ?? 1, stored.Conteudo), stored.ContentType, stored.NomeArquivo);
        }
        catch (FormatException) { return UnprocessableEntity(ApiResponse.Fail("Conteúdo inválido", "O conteúdo armazenado não está em Base64 válido.", 422)); }
    }

    [HttpGet("scripts/{id:int}/exportar")]
    public async Task<IActionResult> ExportScript(int id, CancellationToken ct)
    {
        try
        {
            var package = await service.ExportScriptPackageAsync(id, ct);
            return package is null
                ? NotFound(ApiResponse.Fail("Não encontrado", "Script não encontrado.", 404))
                : File(package.Conteudo, package.ContentType, package.NomeArquivo);
        }
        catch (FormatException) { return UnprocessableEntity(ApiResponse.Fail("Conteúdo inválido", "O conteúdo armazenado não está em Base64 válido.", 422)); }
    }

    [HttpGet("scripts/exportar")]
    public async Task<IActionResult> ExportScripts([FromQuery] List<int> ids, CancellationToken ct)
    {
        try
        {
            var package = await service.ExportScriptsPackageAsync(ids, ct);
            return package is null
                ? NotFound(ApiResponse.Fail("Não encontrado", "Nenhum script válido encontrado para exportação.", 404))
                : File(package.Conteudo, package.ContentType, package.NomeArquivo);
        }
        catch (FormatException) { return UnprocessableEntity(ApiResponse.Fail("Conteúdo inválido", "Um dos conteúdos armazenados não está em Base64 válido.", 422)); }
    }

    [HttpGet("paginas-fotos/{id:int}/download")]
    public async Task<IActionResult> DownloadMrd(int id, CancellationToken ct)
    {
        var stored = await service.DownloadMrdAsync(id, ct);
        if (stored is null) return NotFound(ApiResponse.Fail("Não encontrado", "MRD não encontrado.", 404));
        try { return File(Convert.FromBase64String(stored.Conteudo), stored.ContentType, stored.NomeArquivo); }
        catch (FormatException) { return UnprocessableEntity(ApiResponse.Fail("Conteúdo inválido", "O MRD armazenado não está em Base64 válido.", 422)); }
    }
}
