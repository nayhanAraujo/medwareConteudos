using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Services;
using System.Text.Json;

namespace MdwConteudos.Api.Modules.Web;

[ApiController]
[Route("api/web/variaveis")]
[Authorize]
public class VariaveisWebController : ControllerBase
{
    private readonly IVariaveisWebService _svc;
    private readonly IImportacaoVariaveisService _importacao;
    private readonly IWebHostEnvironment _env;
    public VariaveisWebController(IVariaveisWebService svc, IImportacaoVariaveisService importacao, IWebHostEnvironment env) { _svc = svc; _importacao = importacao; _env = env; }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10,
        [FromQuery] string? search = null,
        [FromQuery] int? grupo = null,
        CancellationToken ct = default)
        => Ok(await _svc.ListVariaveisAsync(skip, take, search, grupo, ct));

    [HttpGet("grupos")]
    public async Task<IActionResult> ListGrupos(CancellationToken ct)
        => Ok(await _svc.ListGruposAsync(ct));

    [HttpGet("meta")]
    public async Task<IActionResult> Meta(CancellationToken ct)
        => Ok(await _svc.GetMetaAsync(ct));

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] VariavelCreateRequest req, CancellationToken ct)
    {
        try
        {
            var cod = await _svc.CreateVariavelAsync(req, User.GetCodUsuario(), ct);
            return Ok(new { success = true, codVariavel = cod });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}/edicao")]
    public async Task<IActionResult> GetEdicao(int id, CancellationToken ct)
    {
        var data = await _svc.GetVariavelEdicaoAsync(id, ct);
        return data is null ? NotFound(new { message = "Variável não encontrada." }) : Ok(data);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int id, [FromBody] VariavelCreateRequest req, CancellationToken ct)
    {
        try
        {
            await _svc.UpdateVariavelAsync(id, req, User.GetCodUsuario(), ct);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}/detalhes-completos")]
    public async Task<IActionResult> GetDetalhesCompletos(int id, CancellationToken ct)
        => Ok(await _svc.GetDetalhesCompletosAsync(id, ct));

    [HttpGet("{id:int}/codigos-vinculados")]
    public async Task<IActionResult> GetCodigosVinculados(int id, CancellationToken ct)
        => Ok(await _svc.GetCodigosVinculadosAsync(id, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var row = await _svc.GetVariavelAsync(id, ct);
        return row is null ? NotFound() : Ok(ApiResponse.Ok(row));
    }

    [HttpGet("{id:int}/dependencias")]
    public async Task<IActionResult> Dependencias(int id, CancellationToken ct)
    {
        var data = await _svc.GetDependenciasAsync(id, ct);
        return data is null ? NotFound(new { message = "Variável não encontrada." }) : Ok(ApiResponse.Ok(data));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id, [FromQuery] bool force = false, CancellationToken ct = default)
        => await Handle(async () => { await _svc.DeleteVariavelAsync(id, force, ct); return Ok(new { success = true }); });

    [HttpPatch("{id:int}/grupo")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AlterarGrupo(int id, [FromBody] AlterarGrupoVariavelRequest req, CancellationToken ct)
        => await Handle(async () => { await _svc.AlterarGrupoAsync(id, req.CodGrupo, User.GetCodUsuario(), ct); return Ok(new { success = true }); });

    [HttpGet("classificacoes")]
    public async Task<IActionResult> Classificacoes(CancellationToken ct) => Ok(await _svc.ListClassificacoesAsync(ct));

    [HttpPost("classificacoes/grupos")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CriarGrupoClassificacao([FromBody] NomeRequest req, CancellationToken ct)
        => await Handle(async () => Ok(new { success = true, codGrupo = await _svc.CreateGrupoClassificacaoAsync(req.Nome, User.GetCodUsuario(), ct) }));

    [HttpPut("classificacoes/grupos/{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> EditarGrupoClassificacao(int id, [FromBody] NomeRequest req, CancellationToken ct)
        => await Handle(async () => { await _svc.UpdateGrupoClassificacaoAsync(id, req.Nome, User.GetCodUsuario(), ct); return Ok(new { success = true }); });

    [HttpDelete("classificacoes/grupos/{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ExcluirGrupoClassificacao(int id, CancellationToken ct)
        => await Handle(async () => { await _svc.DeleteGrupoClassificacaoAsync(id, ct); return Ok(new { success = true }); });

    [HttpPost("classificacoes")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CriarClassificacao([FromBody] ClassificacaoRequest req, CancellationToken ct)
        => await Handle(async () => Ok(new { success = true, codClassificacao = await _svc.CreateClassificacaoAsync(req, User.GetCodUsuario(), ct) }));

    [HttpPut("classificacoes/{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> EditarClassificacao(int id, [FromBody] ClassificacaoRequest req, CancellationToken ct)
        => await Handle(async () => { await _svc.UpdateClassificacaoAsync(id, req, User.GetCodUsuario(), ct); return Ok(new { success = true }); });

    [HttpDelete("classificacoes/{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ExcluirClassificacao(int id, CancellationToken ct)
        => await Handle(async () => { await _svc.DeleteClassificacaoAsync(id, ct); return Ok(new { success = true }); });

    [HttpGet("{id:int}/classificacoes")]
    public async Task<IActionResult> ClassificacoesVariavel(int id, CancellationToken ct) => Ok(await _svc.GetVariavelClassificacoesAsync(id, ct));

    [HttpPut("{id:int}/classificacoes")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> VincularClassificacoes(int id, [FromBody] VariavelClassificacoesRequest req, CancellationToken ct)
        => await Handle(async () => { await _svc.SetVariavelClassificacoesAsync(id, req.Classificacoes, User.GetCodUsuario(), ct); return Ok(new { success = true }); });

    [HttpGet("codigos-universais")]
    public async Task<IActionResult> CodigosUniversais([FromQuery] string? search, CancellationToken ct) => Ok(await _svc.ListCodigosUniversaisAsync(search, ct));

    [HttpPut("{id:int}/codigos-universais")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> VincularCodigos(int id, [FromBody] CodigosUniversaisRequest req, CancellationToken ct)
        => await Handle(async () => { await _svc.SetCodigosUniversaisAsync(id, req.Codigos, User.GetCodUsuario(), ct); return Ok(new { success = true }); });

    [HttpGet("{id:int}/especialidades")]
    public async Task<IActionResult> Especialidades(int id, CancellationToken ct) => Ok(await _svc.GetEspecialidadesAsync(id, ct));

    [HttpPut("{id:int}/especialidades")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> VincularEspecialidade(int id, [FromBody] EspecialidadeVinculoRequest req, CancellationToken ct)
        => await Handle(async () => { await _svc.SetEspecialidadeAsync(id, req, User.GetCodUsuario(), ct); return Ok(new { success = true }); });

    [HttpDelete("{id:int}/especialidades/{codEspecialidade:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DesvincularEspecialidade(int id, int codEspecialidade, CancellationToken ct)
        => await Handle(async () => { await _svc.RemoveEspecialidadeAsync(id, codEspecialidade, ct); return Ok(new { success = true }); });

    [HttpPost("especialidades/lote")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> EspecialidadesLote([FromBody] EspecialidadesLoteRequest req, CancellationToken ct)
        => await Handle(async () => { await _svc.SetEspecialidadesLoteAsync(req, User.GetCodUsuario(), ct); return Ok(new { success = true, total = req.Variaveis.Distinct().Count() }); });

    [HttpGet("{id:int}/anexos")]
    public async Task<IActionResult> Anexos(int id, CancellationToken ct) => Ok(await _svc.GetAnexosContextoAsync(id, ct));

    [HttpPost("{id:int}/anexos")]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> CriarAnexo(int id, [FromForm] string tipoAnexo, [FromForm] string? nome, [FromForm] string? descricao,
        [FromForm] string? link, [FromForm] int? codFormula, [FromForm] int? codReferencia, [FromForm] IFormFile? arquivo, CancellationToken ct)
    {
        string? relativePath = null;
        string? physicalPath = null;
        try
        {
            if (!tipoAnexo.Equals("URL", StringComparison.OrdinalIgnoreCase))
            {
                if (arquivo is null || arquivo.Length == 0) return BadRequest(new { message = "O arquivo é obrigatório." });
                var folder = Path.Combine(FindMigracaoRoot(_env.ContentRootPath), "static", "uploads");
                Directory.CreateDirectory(folder);
                var safeName = Path.GetFileName(arquivo.FileName);
                var storedName = $"{Guid.NewGuid():N}_{safeName}";
                physicalPath = Path.Combine(folder, storedName);
                await using var stream = System.IO.File.Create(physicalPath);
                await arquivo.CopyToAsync(stream, ct);
                relativePath = $"/static/uploads/{storedName}";
            }
            var code = await _svc.CreateAnexoAsync(id, tipoAnexo, nome, descricao, link, relativePath, codFormula, codReferencia, User.GetCodUsuario(), ct);
            return Ok(new { success = true, codAnexo = code });
        }
        catch (Exception ex)
        {
            if (physicalPath is not null && System.IO.File.Exists(physicalPath)) System.IO.File.Delete(physicalPath);
            return Error(ex);
        }
    }

    [HttpDelete("{id:int}/anexos/{codAnexo:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ExcluirAnexo(int id, int codAnexo, CancellationToken ct)
        => await Handle(async () => { await _svc.DeleteAnexoAsync(id, codAnexo, ct); return Ok(new { success = true }); });

    [HttpGet("{id:int}/estudos")]
    public async Task<IActionResult> Estudos(int id, CancellationToken ct) => Ok(await _svc.GetEstudosAsync(id, ct));

    [HttpGet("modelos-modo-texto")]
    public async Task<IActionResult> ModelosModoTexto([FromQuery] string? search, CancellationToken ct) => Ok(await _svc.ListModelosModoTextoAsync(search, User.GetCodUsuario(), ct));

    [HttpGet("modelos-modo-texto/{id:int}/arquivo")]
    public async Task<IActionResult> GerarModoTexto(int id, CancellationToken ct)
    {
        var file = await _svc.GerarModoTextoAsync(id, User.GetCodUsuario(), ct);
        if (file is null) return NotFound(new { message = "Modelo não encontrado ou sem permissão." });
        var name = string.Concat(file.Value.Nome.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return File(System.Text.Encoding.UTF8.GetBytes(file.Value.Conteudo), "text/plain; charset=utf-8", $"modo_texto_{name}.txt");
    }

    [HttpPost("importacao-cs/preview")]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> PreviewImportacao([FromForm] IFormFile arquivo, CancellationToken ct)
    {
        if (!Path.GetExtension(arquivo.FileName).Equals(".cs", StringComparison.OrdinalIgnoreCase)) return BadRequest(new { message = "Apenas arquivos .cs são permitidos." });
        using var reader = new StreamReader(arquivo.OpenReadStream());
        var data = await _svc.PreviewImportacaoAsync(await reader.ReadToEndAsync(ct), ct);
        return data.Variaveis.Count == 0 ? BadRequest(new { message = "Nenhuma variável encontrada no arquivo." }) : Ok(ApiResponse.Ok(data));
    }

    [HttpPost("importacao-cs/confirmar")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ConfirmarImportacao([FromBody] ImportacaoCsConfirmarRequest req, CancellationToken ct)
        => await Handle(async () => Ok(await _svc.ImportarCsAsync(req, User.GetCodUsuario(), ct)));

    [HttpPost("importacoes/preview")]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(ImportacaoVariaveisService.MaxFileSize + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImportacaoVariaveisService.MaxFileSize + 1024 * 1024)]
    public async Task<IActionResult> PreviewImportacaoUnificada([FromForm] IFormFile arquivo, [FromForm] int? codReferencia, CancellationToken ct)
        => await Handle(async () => Ok(ApiResponse.Ok(await _importacao.PreviewAsync(arquivo, codReferencia, ct))));

    [HttpPost("importacoes/confirmar")]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(ImportacaoVariaveisService.MaxFileSize + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImportacaoVariaveisService.MaxFileSize + 1024 * 1024)]
    public async Task<IActionResult> ConfirmarImportacaoUnificada(
        [FromForm] IFormFile arquivo,
        [FromForm] string? decisoes,
        [FromForm] string? variaveisSelecionadas,
        [FromForm] int? codReferencia,
        CancellationToken ct)
        => await Handle(async () =>
        {
            IReadOnlyList<ImportacaoVariavelDecisao> selected;
            try
            {
                if (!string.IsNullOrWhiteSpace(decisoes))
                {
                    selected = JsonSerializer.Deserialize<List<ImportacaoVariavelDecisao>>(decisoes, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
                }
                else
                {
                    var legacy = JsonSerializer.Deserialize<List<string>>(variaveisSelecionadas ?? "[]", new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
                    selected = legacy.Select(codigo => new ImportacaoVariavelDecisao(codigo, "criar")).ToList();
                }
            }
            catch (JsonException) { throw new InvalidOperationException("A seleção de variáveis é inválida."); }
            return Ok(ApiResponse.Ok(await _importacao.ConfirmarAsync(arquivo, selected, codReferencia, User.GetCodUsuario(), ct)));
        });

    private async Task<IActionResult> Handle(Func<Task<IActionResult>> action)
    {
        try { return await action(); } catch (Exception ex) { return Error(ex); }
    }

    private IActionResult Error(Exception ex) => ex switch
    {
        KeyNotFoundException => NotFound(new { message = ex.Message }),
        InvalidOperationException => BadRequest(new { message = ex.Message }),
        _ => StatusCode(500, new { message = ex.Message })
    };

    private static string FindMigracaoRoot(string start)
    {
        var current = new DirectoryInfo(start);
        while (current is not null)
        {
            if (current.Name.Equals("mdw-migracao", StringComparison.OrdinalIgnoreCase)) return current.FullName;
            if (Directory.Exists(Path.Combine(current.FullName, "mdw-migracao"))) return Path.Combine(current.FullName, "mdw-migracao");
            current = current.Parent;
        }
        throw new InvalidOperationException("Não foi possível localizar a raiz mdw-migracao para salvar o anexo.");
    }
}
