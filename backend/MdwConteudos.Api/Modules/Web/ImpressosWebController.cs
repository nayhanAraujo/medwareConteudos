using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.ConteudosImpressos;
using MdwConteudos.Api.Modules.Permissions;
using MdwConteudos.Api.Services;

namespace MdwConteudos.Api.Modules.Web;

[ApiController]
[Route("api/web/impressos")]
[Authorize]
public class ImpressosWebController : ControllerBase
{
    private const long MaxUploadSize = 20 * 1024 * 1024;
    private readonly IFirebirdConnectionFactory _db;
    public ImpressosWebController(IFirebirdConnectionFactory db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = (await conn.QueryAsync<ImpressoListRow>(new CommandDefinition("""
            SELECT i.CODIMPRESSO AS CodImpresso, i.TITULO AS Titulo, i.USOGERAL AS UsoGeral,
                   i.IMPRIMIRCABECALHO AS ImprimirCabecalho, i.DTHRULTMODIFICACAO AS DthrUltModificacao,
                   u.NOME AS UsuarioNome,
                   CASE WHEN EXISTS (SELECT 1 FROM IMPRESSOVBS v WHERE v.CODIMPRESSO = i.CODIMPRESSO) THEN 1 ELSE 0 END AS TemVbs
              FROM IMPRESSO i LEFT JOIN USUARIO u ON u.CODUSUARIO = i.CODUSUARIO
             ORDER BY i.DTHRULTMODIFICACAO DESC
            """, cancellationToken: ct))).Select(x => new ImpressoListDto(x.CodImpresso, x.Titulo ?? string.Empty, x.UsoGeral,
                x.ImprimirCabecalho, x.DthrUltModificacao, x.UsuarioNome, x.TemVbs == 1)).ToList();
        return Ok(ApiResponse.Ok(rows, rows.Count));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<ImpressoRow>(new CommandDefinition("""
            SELECT CODIMPRESSO AS CodImpresso, TITULO AS Titulo, IMPRESSO AS Conteudo, USOGERAL AS UsoGeral,
                   IMPRIMIRCABECALHO AS ImprimirCabecalho, CODUSUARIO AS CodUsuario, DTHRULTMODIFICACAO AS DthrUltModificacao
              FROM IMPRESSO WHERE CODIMPRESSO = @Id
            """, new { Id = id }, cancellationToken: ct));
        if (row is null) return NotFound(ApiResponse.Fail("Não encontrado", "Impresso não encontrado."));
        var vbs = await conn.QuerySingleOrDefaultAsync<VbsRow>(new CommandDefinition("""
            SELECT SCRIPT_VBS AS ScriptVbs, DTHRULTMODIFICACAO AS DthrUltModificacao
              FROM IMPRESSOVBS WHERE CODIMPRESSO = @Id ROWS 1
            """, new { Id = id }, cancellationToken: ct));
        return Ok(ApiResponse.Ok(new ImpressoDto(row.CodImpresso, row.Titulo ?? string.Empty, ConteudoTextCodec.Decode(row.Conteudo),
            row.UsoGeral, row.ImprimirCabecalho, row.CodUsuario, row.DthrUltModificacao, ConteudoTextCodec.Decode(vbs?.ScriptVbs), vbs?.DthrUltModificacao)));
    }

    [HttpPost, RequirePermission(PermissionDomains.Conteudos, PermissionActions.Criar), RequestSizeLimit(MaxUploadSize)]
    public Task<IActionResult> Create([FromForm] ImpressoForm form, CancellationToken ct) => Handle(async () =>
    {
        ValidateForm(form);
        var vbs = await ReadVbs(form.ArquivoVbs, ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await EnsureUniqueTitle(conn, tx, form.Titulo, null, ct);
        var id = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO IMPRESSO (TITULO, UCASE_TITULO, IMPRESSO, USOGERAL, IMPRIMIRCABECALHO, CODUSUARIO, DTHRULTMODIFICACAO)
            VALUES (@Titulo, @TituloUpper, @Conteudo, @UsoGeral, @Cabecalho, @Usuario, @Agora) RETURNING CODIMPRESSO
            """, new { Titulo = form.Titulo.Trim(), TituloUpper = form.Titulo.Trim().ToUpperInvariant(), Conteudo = form.Conteudo,
                UsoGeral = Flag(form.UsoGeral), Cabecalho = Flag(form.ImprimirCabecalho), Usuario = User.GetCodUsuario(), Agora = DateTime.UtcNow }, tx, cancellationToken: ct));
        if (vbs is not null) await InsertVbs(conn, tx, id, vbs, User.GetCodUsuario(), ct);
        await tx.CommitAsync(ct);
        return ApiResponse.Ok(new { codImpresso = id });
    });

    [HttpPut("{id:int}"), RequirePermission(PermissionDomains.Conteudos, PermissionActions.Editar), RequestSizeLimit(MaxUploadSize)]
    public Task<IActionResult> Update(int id, [FromForm] ImpressoForm form, CancellationToken ct) => Handle(async () =>
    {
        ValidateForm(form);
        var vbs = await ReadVbs(form.ArquivoVbs, ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await EnsureUniqueTitle(conn, tx, form.Titulo, id, ct);
        var count = await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE IMPRESSO SET TITULO = @Titulo, UCASE_TITULO = @TituloUpper, IMPRESSO = @Conteudo,
                   USOGERAL = @UsoGeral, IMPRIMIRCABECALHO = @Cabecalho, CODUSUARIO = @Usuario,
                   DTHRULTMODIFICACAO = @Agora WHERE CODIMPRESSO = @Id
            """, new { Titulo = form.Titulo.Trim(), TituloUpper = form.Titulo.Trim().ToUpperInvariant(), Conteudo = form.Conteudo,
                UsoGeral = Flag(form.UsoGeral), Cabecalho = Flag(form.ImprimirCabecalho), Usuario = User.GetCodUsuario(), Agora = DateTime.UtcNow, Id = id }, tx, cancellationToken: ct));
        if (count == 0) throw new ConteudosImpressosException("Impresso não encontrado.", 404);
        if (vbs is not null)
        {
            var updated = await conn.ExecuteAsync(new CommandDefinition("""
                UPDATE IMPRESSOVBS SET SCRIPT_VBS = @Vbs, CODUSUARIO = @Usuario, DTHRULTMODIFICACAO = @Agora WHERE CODIMPRESSO = @Id
                """, new { Vbs = vbs, Usuario = User.GetCodUsuario(), Agora = DateTime.UtcNow, Id = id }, tx, cancellationToken: ct));
            if (updated == 0) await InsertVbs(conn, tx, id, vbs, User.GetCodUsuario(), ct);
        }
        await tx.CommitAsync(ct);
        return ApiResponse.OkMessage("Impresso atualizado com sucesso.");
    });

    [HttpDelete("{id:int}"), RequirePermission(PermissionDomains.Conteudos, PermissionActions.Excluir)]
    public Task<IActionResult> Delete(int id, CancellationToken ct) => Handle(async () =>
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM IMPRESSOVBS WHERE CODIMPRESSO = @Id", new { Id = id }, tx, cancellationToken: ct));
        var count = await conn.ExecuteAsync(new CommandDefinition("DELETE FROM IMPRESSO WHERE CODIMPRESSO = @Id", new { Id = id }, tx, cancellationToken: ct));
        if (count == 0) throw new ConteudosImpressosException("Impresso não encontrado.", 404);
        await tx.CommitAsync(ct);
        return ApiResponse.OkMessage("Impresso excluído com sucesso.");
    });

    [HttpDelete("{id:int}/vbs"), RequirePermission(PermissionDomains.Conteudos, PermissionActions.Excluir)]
    public Task<IActionResult> DeleteVbs(int id, CancellationToken ct) => Handle(async () =>
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var count = await conn.ExecuteAsync(new CommandDefinition("DELETE FROM IMPRESSOVBS WHERE CODIMPRESSO = @Id", new { Id = id }, tx, cancellationToken: ct));
        if (count == 0) throw new ConteudosImpressosException("Arquivo VBS não encontrado.", 404);
        await tx.CommitAsync(ct);
        return ApiResponse.OkMessage("Arquivo VBS removido com sucesso.");
    });

    [HttpGet("{id:int}/download/mrd")]
    public Task<IActionResult> DownloadMrd(int id, CancellationToken ct) => Download(id, false, ct);

    [HttpGet("{id:int}/download/vbs")]
    public Task<IActionResult> DownloadVbs(int id, CancellationToken ct) => Download(id, true, ct);

    [HttpPost("importar"), RequirePermission(PermissionDomains.Conteudos, PermissionActions.Importar), RequestSizeLimit(MaxUploadSize)]
    public Task<IActionResult> Import([FromForm] ImpressoImportForm form, CancellationToken ct) => Handle(async () =>
    {
        var titulo = form.Titulo?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(titulo)) throw new ConteudosImpressosException("Título é obrigatório.");
        ValidateUpload(form.ArquivoMrd, ".mrd", "Arquivo MRD");
        var mrdBytes = await ReadAll(form.ArquivoMrd!, ct);
        var conteudo = ConteudoTextCodec.DecodeUploaded(mrdBytes);
        if (string.IsNullOrWhiteSpace(conteudo)) throw new ConteudosImpressosException("O arquivo MRD está vazio.");
        var vbs = await ReadVbs(form.ArquivoVbs, ct);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await EnsureUniqueTitle(conn, tx, titulo, null, ct);
        var id = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO IMPRESSO (TITULO, UCASE_TITULO, IMPRESSO, USOGERAL, IMPRIMIRCABECALHO, CODUSUARIO, DTHRULTMODIFICACAO)
            VALUES (@Titulo, @TituloUpper, @Conteudo, @UsoGeral, @Cabecalho, @Usuario, @Agora) RETURNING CODIMPRESSO
            """, new { Titulo = titulo, TituloUpper = titulo.ToUpperInvariant(), Conteudo = conteudo,
                UsoGeral = Flag(form.UsoGeral), Cabecalho = Flag(form.ImprimirCabecalho), Usuario = User.GetCodUsuario(), Agora = DateTime.UtcNow }, tx, cancellationToken: ct));
        if (vbs is not null) await InsertVbs(conn, tx, id, vbs, User.GetCodUsuario(), ct);
        await tx.CommitAsync(ct);
        return ApiResponse.Ok(new { codImpresso = id });
    });

    private async Task<IActionResult> Download(int id, bool vbs, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var title = await conn.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT TITULO FROM IMPRESSO WHERE CODIMPRESSO = @Id", new { Id = id }, cancellationToken: ct));
        if (title is null) return NotFound(ApiResponse.Fail("Não encontrado", "Impresso não encontrado."));
        var content = vbs
            ? await conn.QuerySingleOrDefaultAsync<object>(new CommandDefinition("SELECT SCRIPT_VBS FROM IMPRESSOVBS WHERE CODIMPRESSO = @Id ROWS 1", new { Id = id }, cancellationToken: ct))
            : await conn.QuerySingleOrDefaultAsync<object>(new CommandDefinition("SELECT IMPRESSO FROM IMPRESSO WHERE CODIMPRESSO = @Id", new { Id = id }, cancellationToken: ct));
        if (content is null) return NotFound(ApiResponse.Fail("Não encontrado", vbs ? "Arquivo VBS não encontrado." : "Conteúdo MRD não encontrado."));
        var suffix = vbs ? "_script.vbs" : ".mrd";
        return File(ConteudoTextCodec.EncodeWindows1252(ConteudoTextCodec.Decode(content)), "text/plain; charset=windows-1252", SafeFileName(title) + suffix);
    }

    private async Task<IActionResult> Handle(Func<Task<object>> action)
    {
        try { return Ok(await action()); }
        catch (ConteudosImpressosException ex)
        {
            return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.StatusCode == 404 ? "Não encontrado" : "Validação", ex.Message));
        }
    }

    private static void ValidateForm(ImpressoForm form)
    {
        if (string.IsNullOrWhiteSpace(form.Titulo)) throw new ConteudosImpressosException("Título é obrigatório.");
        if (string.IsNullOrWhiteSpace(form.Conteudo)) throw new ConteudosImpressosException("Conteúdo é obrigatório.");
        if (form.ArquivoVbs is not null) ValidateUpload(form.ArquivoVbs, ".vbs", "Arquivo VBS");
    }

    private static void ValidateUpload(IFormFile? file, string extension, string label)
    {
        if (file is null || file.Length == 0) throw new ConteudosImpressosException($"{label} é obrigatório.");
        if (file.Length > MaxUploadSize) throw new ConteudosImpressosException($"{label} excede o limite de 20 MB.");
        if (!string.Equals(Path.GetExtension(file.FileName), extension, StringComparison.OrdinalIgnoreCase))
            throw new ConteudosImpressosException($"{label} deve ter extensão {extension}.");
    }

    private static async Task<string?> ReadVbs(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return null;
        ValidateUpload(file, ".vbs", "Arquivo VBS");
        return ConteudoTextCodec.DecodeUploaded(await ReadAll(file, ct));
    }

    private static async Task<byte[]> ReadAll(IFormFile file, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        return stream.ToArray();
    }

    private static async Task EnsureUniqueTitle(System.Data.IDbConnection conn, System.Data.IDbTransaction tx, string title, int? excludeId, CancellationToken ct)
    {
        var sql = excludeId.HasValue
            ? "SELECT COUNT(*) FROM IMPRESSO WHERE UCASE_TITULO = @Titulo AND CODIMPRESSO <> @Id"
            : "SELECT COUNT(*) FROM IMPRESSO WHERE UCASE_TITULO = @Titulo";
        var count = await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql,
            new { Titulo = title.Trim().ToUpperInvariant(), Id = excludeId }, tx, cancellationToken: ct));
        if (count > 0) throw new ConteudosImpressosException("Já existe um impresso com este título.", 409);
    }

    private static Task<int> InsertVbs(System.Data.IDbConnection conn, System.Data.IDbTransaction tx, int id, string vbs, int userId, CancellationToken ct) =>
        conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO IMPRESSOVBS (CODIMPRESSO, SCRIPT_VBS, CODUSUARIO, DTHRULTMODIFICACAO)
            VALUES (@Id, @Vbs, @Usuario, @Agora)
            """, new { Id = id, Vbs = vbs, Usuario = userId, Agora = DateTime.UtcNow }, tx, cancellationToken: ct));

    private static int Flag(bool value) => value ? 1 : 0;
    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string(value.Where(c => !invalid.Contains(c) && c is not '\r' and not '\n').ToArray()).Trim();
        return string.IsNullOrWhiteSpace(result) ? "impresso" : result;
    }

    private sealed class ImpressoListRow
    {
        public int CodImpresso { get; set; }
        public string? Titulo { get; set; }
        public int UsoGeral { get; set; }
        public int ImprimirCabecalho { get; set; }
        public DateTime? DthrUltModificacao { get; set; }
        public string? UsuarioNome { get; set; }
        public int TemVbs { get; set; }
    }
    private sealed class ImpressoRow
    {
        public int CodImpresso { get; set; }
        public string? Titulo { get; set; }
        public object? Conteudo { get; set; }
        public int UsoGeral { get; set; }
        public int ImprimirCabecalho { get; set; }
        public int? CodUsuario { get; set; }
        public DateTime? DthrUltModificacao { get; set; }
    }
    private sealed class VbsRow
    {
        public object? ScriptVbs { get; set; }
        public DateTime? DthrUltModificacao { get; set; }
    }
}
