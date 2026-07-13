using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Services;

namespace MdwConteudos.Api.Modules.Web;

[ApiController]
[Route("api/web/relatorios")]
[Authorize]
public class RelatoriosWebController : ControllerBase
{
    private readonly IRelatoriosWebService _svc;

    public RelatoriosWebController(IRelatoriosWebService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] string? modulo = null,
        [FromQuery] string? formato = null,
        [FromQuery] string? status = null,
        [FromQuery] string? multiselecao = null,
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 10,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await _svc.ListAsync(search, modulo, formato, status, multiselecao, page, perPage, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message, error = ex.Message });
        }
    }

    [HttpGet("sistemas")]
    public async Task<IActionResult> ListSistemas(CancellationToken ct)
        => Ok(await _svc.ListSistemasAsync(ct));

    [HttpGet("modulos")]
    public async Task<IActionResult> ListModulos([FromQuery] int? codsistema = null, CancellationToken ct = default)
        => Ok(await _svc.ListModulosAsync(codsistema, ct));

    [HttpGet("modulos/simples")]
    public async Task<IActionResult> ListModulosSimples([FromQuery] int? codsistema = null, CancellationToken ct = default)
        => Ok(await _svc.ListModulosSimplesAsync(codsistema, ct));

    [HttpPost("modulos")]
    public async Task<IActionResult> CreateModulo([FromBody] ModuloRelatorioCreateRequest req, CancellationToken ct)
    {
        try
        {
            return Ok(await _svc.CreateModuloAsync(req, User.GetCodUsuario(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message, error = ex.Message });
        }
    }

    [HttpPut("modulos/{nomeModulo}")]
    public async Task<IActionResult> UpdateModulo(string nomeModulo, [FromBody] ModuloRelatorioUpdateRequest req, CancellationToken ct)
    {
        try
        {
            await _svc.UpdateModuloAsync(Uri.UnescapeDataString(nomeModulo), req, User.GetCodUsuario(), ct);
            return Ok(ApiResponse.OkMessage("Módulo atualizado com sucesso"));
        }
        catch (InvalidOperationException ex)
        {
            var notFound = ex.Message.Contains("não encontrado", StringComparison.OrdinalIgnoreCase);
            return notFound
                ? NotFound(new { success = false, message = ex.Message, error = ex.Message })
                : BadRequest(new { success = false, message = ex.Message, error = ex.Message });
        }
    }

    [HttpDelete("modulos/{nomeModulo}")]
    public async Task<IActionResult> DeleteModulo(string nomeModulo, CancellationToken ct)
    {
        try
        {
            await _svc.DeleteModuloAsync(Uri.UnescapeDataString(nomeModulo), User.GetCodUsuario(), ct);
            return Ok(ApiResponse.OkMessage("Módulo excluído com sucesso"));
        }
        catch (InvalidOperationException ex)
        {
            var notFound = ex.Message.Contains("não encontrado", StringComparison.OrdinalIgnoreCase);
            return notFound
                ? NotFound(new { success = false, message = ex.Message, error = ex.Message })
                : BadRequest(new { success = false, message = ex.Message, error = ex.Message });
        }
    }

    [HttpPost("exportar-multiplos")]
    public async Task<IActionResult> ExportMultiplos([FromBody] RelatorioExportRequest req, CancellationToken ct)
    {
        try
        {
            var (bytes, fileName) = await _svc.ExportZipAsync(req.CodRelatorios ?? [], ct);
            return File(bytes, "application/zip", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message, error = ex.Message });
        }
    }

    [HttpPost("importar-lote")]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> ImportLote(
        [FromForm] string modulo,
        [FromForm] int ativo = 1,
        CancellationToken ct = default)
    {
        try
        {
            // Preferência: arquivos enviados com o nome "arquivos"; fallback para todos os files do form.
            var formFiles = Request.Form.Files
                .Where(f => string.Equals(f.Name, "arquivos", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (formFiles.Count == 0)
                formFiles = Request.Form.Files.ToList();

            var list = new List<(string, string)>();
            foreach (var file in formFiles)
            {
                if (file.Length <= 0) continue;
                await using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                var bytes = ms.ToArray();
                var content = DecodeFileContent(bytes);
                list.Add((file.FileName, content));
            }

            if (list.Count == 0)
                return BadRequest(new { success = false, message = "Nenhum arquivo enviado", error = "Nenhum arquivo enviado" });

            return Ok(await _svc.ImportLoteAsync(modulo, ativo, list, User.GetCodUsuario(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message, error = ex.Message });
        }
    }

    private static string DecodeFileContent(byte[] bytes)
    {
        // Preferência: UTF-8 (com BOM), senão Latin1 / Windows-1252 (XML Medware legado)
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        var utf8 = Encoding.UTF8.GetString(bytes);
        if (!utf8.Contains('\uFFFD'))
        {
            // Se o XML declara latin1/windows-1252, respeita a declaração
            var declared = DetectXmlEncodingName(utf8);
            if (declared is not null &&
                (declared.Contains("8859", StringComparison.OrdinalIgnoreCase) ||
                 declared.Contains("1252", StringComparison.OrdinalIgnoreCase) ||
                 declared.Contains("latin", StringComparison.OrdinalIgnoreCase)))
            {
                return TryGetLegacyEncoding(declared).GetString(bytes);
            }
            return utf8;
        }

        var latin1 = Encoding.Latin1.GetString(bytes);
        var win1252 = TryGetWindows1252().GetString(bytes);
        return ScoreImportText(latin1) >= ScoreImportText(win1252) ? latin1 : win1252;
    }

    private static Encoding TryGetLegacyEncoding(string declared)
    {
        if (declared.Contains("1252", StringComparison.OrdinalIgnoreCase))
            return TryGetWindows1252();
        return Encoding.Latin1; // ISO-8859-1 built-in
    }

    private static Encoding TryGetWindows1252()
    {
        try { return Encoding.GetEncoding(1252); }
        catch (NotSupportedException) { return Encoding.Latin1; }
    }

    private static string? DetectXmlEncodingName(string content)
    {
        var head = content.Length > 200 ? content[..200] : content;
        var m = System.Text.RegularExpressions.Regex.Match(
            head, @"encoding\s*=\s*[""']([^""']+)[""']",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static int ScoreImportText(string text)
    {
        if (string.IsNullOrEmpty(text)) return int.MinValue;
        var sample = text.Length > 4000 ? text[..4000] : text;
        var score = 0;
        foreach (var c in sample)
        {
            if (c == '\uFFFD') score -= 50;
            else if ("áàâãéêíóôõúçÁÀÂÃÉÊÍÓÔÕÚÇ".IndexOf(c) >= 0) score += 3;
        }
        return score;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var data = await _svc.GetAsync(id, ct);
        return data is null
            ? NotFound(new { success = false, message = "Relatório não encontrado", error = "Relatório não encontrado" })
            : Ok(ApiResponse.Ok(data));
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        try
        {
            var (bytes, fileName) = await _svc.DownloadAsync(id, ct);
            return File(bytes, "application/octet-stream", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { success = false, message = ex.Message, error = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] RelatorioUpsertRequest req, CancellationToken ct)
    {
        try
        {
            return Ok(await _svc.CreateAsync(req, User.GetCodUsuario(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message, error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int id, [FromBody] RelatorioUpsertRequest req, CancellationToken ct)
    {
        try
        {
            await _svc.UpdateAsync(id, req, ct);
            return Ok(ApiResponse.OkMessage("Relatório atualizado com sucesso"));
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
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] RelatorioStatusRequest req, CancellationToken ct)
    {
        try
        {
            await _svc.SetStatusAsync(id, req.Ativo, ct);
            return Ok(ApiResponse.OkMessage("Status alterado com sucesso"));
        }
        catch (InvalidOperationException ex)
        {
            var notFound = ex.Message.Contains("não encontrado", StringComparison.OrdinalIgnoreCase);
            return notFound
                ? NotFound(new { success = false, message = ex.Message, error = ex.Message })
                : BadRequest(new { success = false, message = ex.Message, error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await _svc.DeleteAsync(id, ct);
            return Ok(ApiResponse.OkMessage("Relatório excluído com sucesso"));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { success = false, message = ex.Message, error = ex.Message });
        }
    }
}
