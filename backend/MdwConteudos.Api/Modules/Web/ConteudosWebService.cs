using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.ConteudosImpressos;

namespace MdwConteudos.Api.Modules.Web;

public interface IConteudosWebService
{
    Task<object> ListGruposFrasesAsync(CancellationToken ct);
    Task<object> CreateGrupoFraseAsync(GrupoFraseRequest request, CancellationToken ct);
    Task<object> UpdateGrupoFraseAsync(int id, GrupoFraseRequest request, CancellationToken ct);
    Task<object> DeleteGrupoFraseAsync(int id, CancellationToken ct);
    Task<object> ListFrasesAsync(int codGrupo, CancellationToken ct);
    Task<object> CreateFraseAsync(FraseRequest request, CancellationToken ct);
    Task<object> UpdateFraseAsync(int id, FraseRequest request, CancellationToken ct);
    Task<object> DeleteFraseAsync(int id, CancellationToken ct);
}

public class ConteudosWebService : IConteudosWebService
{
    private readonly IFirebirdConnectionFactory _db;
    public ConteudosWebService(IFirebirdConnectionFactory db) => _db = db;

    public async Task<object> ListGruposFrasesAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = (await conn.QueryAsync<GrupoFraseDto>(new CommandDefinition(
            "SELECT CODGRUPO AS CodGrupo, GRUPO AS Nome, STATUS AS Status FROM GRUPOFRASE ORDER BY GRUPO", cancellationToken: ct))).AsList();
        return ApiResponse.Ok(rows, rows.Count);
    }

    public async Task<object> CreateGrupoFraseAsync(GrupoFraseRequest request, CancellationToken ct)
    {
        var nome = Required(request.Nome, "Nome do grupo");
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM GRUPOFRASE WHERE UPPER(TRIM(GRUPO)) = @Nome", new { Nome = nome.ToUpperInvariant() }, tx, cancellationToken: ct)) > 0)
            throw new ConteudosImpressosException("Já existe um grupo com este nome.", 409);
        var id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COALESCE(MAX(CODGRUPO), 0) + 1 FROM GRUPOFRASE", transaction: tx, cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO GRUPOFRASE (CODGRUPO, CODGRUPOPAI, GRUPO, STATUS) VALUES (@Id, NULL, @Nome, @Status)",
            new { Id = id, Nome = nome, Status = Flag(request.Status) }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return ApiResponse.Ok(new GrupoFraseDto(id, nome, Flag(request.Status)));
    }

    public async Task<object> UpdateGrupoFraseAsync(int id, GrupoFraseRequest request, CancellationToken ct)
    {
        var nome = Required(request.Nome, "Nome do grupo");
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await EnsureExists(conn, tx, "GRUPOFRASE", "CODGRUPO", id, "Grupo não encontrado.", ct);
        if (await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM GRUPOFRASE WHERE UPPER(TRIM(GRUPO)) = @Nome AND CODGRUPO <> @Id",
                new { Nome = nome.ToUpperInvariant(), Id = id }, tx, cancellationToken: ct)) > 0)
            throw new ConteudosImpressosException("Já existe outro grupo com este nome.", 409);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE GRUPOFRASE SET GRUPO = @Nome, STATUS = @Status WHERE CODGRUPO = @Id",
            new { Nome = nome, Status = Flag(request.Status), Id = id }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return ApiResponse.Ok(new GrupoFraseDto(id, nome, Flag(request.Status)));
    }

    public async Task<object> DeleteGrupoFraseAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await EnsureExists(conn, tx, "GRUPOFRASE", "CODGRUPO", id, "Grupo não encontrado.", ct);
        var children = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM FRASE WHERE CODGRUPO = @Id", new { Id = id }, tx, cancellationToken: ct));
        if (children > 0) throw new ConteudosImpressosException("Não é possível excluir um grupo que possui frases associadas.", 409);
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM GRUPOFRASE WHERE CODGRUPO = @Id", new { Id = id }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return ApiResponse.OkMessage("Grupo excluído com sucesso.");
    }

    public async Task<object> ListFrasesAsync(int codGrupo, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var grupo = await conn.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT GRUPO FROM GRUPOFRASE WHERE CODGRUPO = @Id", new { Id = codGrupo }, cancellationToken: ct));
        if (grupo is null) throw new ConteudosImpressosException("Grupo não encontrado.", 404);
        var rows = (await conn.QueryAsync<FraseRow>(new CommandDefinition(
            "SELECT CODFRASE AS CodFrase, CODGRUPO AS CodGrupo, CODIGO AS Codigo, TITULO AS Titulo, FRASE AS ConteudoBlob, STATUS AS Status FROM FRASE WHERE CODGRUPO = @Id ORDER BY TITULO",
            new { Id = codGrupo }, cancellationToken: ct))).Select(MapFrase).ToList();
        return ApiResponse.Ok(new { grupo, frases = rows }, rows.Count);
    }

    public async Task<object> CreateFraseAsync(FraseRequest request, CancellationToken ct)
    {
        var codigo = Required(request.Codigo, "Código");
        var titulo = Required(request.Titulo, "Título");
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await EnsureExists(conn, tx, "GRUPOFRASE", "CODGRUPO", request.CodGrupo, "Grupo não encontrado.", ct);
        if (await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM FRASE WHERE CODIGO = @Codigo AND CODGRUPO = @CodGrupo",
                new { Codigo = codigo, request.CodGrupo }, tx, cancellationToken: ct)) > 0)
            throw new ConteudosImpressosException("Já existe uma frase com este código neste grupo.", 409);
        var id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COALESCE(MAX(CODFRASE), 0) + 1 FROM FRASE", transaction: tx, cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO FRASE (CODFRASE, CODGRUPO, CODIGO, TITULO, FRASE, STATUS) VALUES (@Id, @CodGrupo, @Codigo, @Titulo, @Conteudo, @Status)",
            new { Id = id, request.CodGrupo, Codigo = codigo, Titulo = titulo, Conteudo = ConteudoTextCodec.EncodeUtf8(request.Conteudo), Status = Flag(request.Status) }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return ApiResponse.Ok(new { codFrase = id });
    }

    public async Task<object> UpdateFraseAsync(int id, FraseRequest request, CancellationToken ct)
    {
        var codigo = Required(request.Codigo, "Código");
        var titulo = Required(request.Titulo, "Título");
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var codGrupo = await conn.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT CODGRUPO FROM FRASE WHERE CODFRASE = @Id", new { Id = id }, tx, cancellationToken: ct));
        if (codGrupo is null) throw new ConteudosImpressosException("Frase não encontrada.", 404);
        if (request.CodGrupo != codGrupo)
            await EnsureExists(conn, tx, "GRUPOFRASE", "CODGRUPO", request.CodGrupo, "Grupo não encontrado.", ct);
        if (await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM FRASE WHERE CODIGO = @Codigo AND CODGRUPO = @CodGrupo AND CODFRASE <> @Id",
                new { Codigo = codigo, request.CodGrupo, Id = id }, tx, cancellationToken: ct)) > 0)
            throw new ConteudosImpressosException("Já existe outra frase com este código neste grupo.", 409);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE FRASE SET CODGRUPO = @CodGrupo, CODIGO = @Codigo, TITULO = @Titulo, FRASE = @Conteudo, STATUS = @Status WHERE CODFRASE = @Id",
            new { request.CodGrupo, Codigo = codigo, Titulo = titulo, Conteudo = ConteudoTextCodec.EncodeUtf8(request.Conteudo), Status = Flag(request.Status), Id = id }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return ApiResponse.OkMessage("Frase atualizada com sucesso.");
    }

    public async Task<object> DeleteFraseAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var count = await conn.ExecuteAsync(new CommandDefinition("DELETE FROM FRASE WHERE CODFRASE = @Id", new { Id = id }, tx, cancellationToken: ct));
        if (count == 0) throw new ConteudosImpressosException("Frase não encontrada.", 404);
        await tx.CommitAsync(ct);
        return ApiResponse.OkMessage("Frase excluída com sucesso.");
    }

    private static FraseDto MapFrase(FraseRow row)
    {
        var raw = ConteudoTextCodec.Decode(row.ConteudoBlob);
        return new(row.CodFrase, row.CodGrupo, row.Codigo ?? string.Empty, row.Titulo ?? string.Empty, raw,
            ConteudoTextCodec.ToDisplayHtml(raw), ConteudoTextCodec.IsRtf(raw) ? raw : string.Empty, row.Status);
    }

    private static string Required(string? value, string label)
    {
        var clean = ConteudoTextCodec.Clean(value);
        if (string.IsNullOrWhiteSpace(clean)) throw new ConteudosImpressosException($"{label} é obrigatório.");
        return clean;
    }

    private static int Flag(int value) => value == 0 ? 0 : 1;

    private static async Task EnsureExists(System.Data.IDbConnection conn, System.Data.IDbTransaction tx, string table, string key, int id, string message, CancellationToken ct)
    {
        var count = await conn.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM {table} WHERE {key} = @Id", new { Id = id }, tx, cancellationToken: ct));
        if (count == 0) throw new ConteudosImpressosException(message, 404);
    }

    private sealed class FraseRow
    {
        public int CodFrase { get; set; }
        public int CodGrupo { get; set; }
        public string? Codigo { get; set; }
        public string? Titulo { get; set; }
        public object? ConteudoBlob { get; set; }
        public int Status { get; set; }
    }
}

[ApiController]
[Route("api/web/conteudos")]
[Authorize]
public class ConteudosWebController : ControllerBase
{
    private readonly IConteudosWebService _svc;
    public ConteudosWebController(IConteudosWebService svc) => _svc = svc;

    [HttpGet("grupos-frases")]
    public Task<IActionResult> GruposFrases(CancellationToken ct) => Handle(() => _svc.ListGruposFrasesAsync(ct));
    [HttpPost("grupos-frases"), Authorize(Roles = "admin")]
    public Task<IActionResult> CriarGrupo(GrupoFraseRequest request, CancellationToken ct) => Handle(() => _svc.CreateGrupoFraseAsync(request, ct));
    [HttpPut("grupos-frases/{id:int}"), Authorize(Roles = "admin")]
    public Task<IActionResult> AtualizarGrupo(int id, GrupoFraseRequest request, CancellationToken ct) => Handle(() => _svc.UpdateGrupoFraseAsync(id, request, ct));
    [HttpDelete("grupos-frases/{id:int}"), Authorize(Roles = "admin")]
    public Task<IActionResult> ExcluirGrupo(int id, CancellationToken ct) => Handle(() => _svc.DeleteGrupoFraseAsync(id, ct));
    [HttpGet("grupos-frases/{codGrupo:int}/frases")]
    public Task<IActionResult> Frases(int codGrupo, CancellationToken ct) => Handle(() => _svc.ListFrasesAsync(codGrupo, ct));
    [HttpPost("frases"), Authorize(Roles = "admin")]
    public Task<IActionResult> CriarFrase(FraseRequest request, CancellationToken ct) => Handle(() => _svc.CreateFraseAsync(request, ct));
    [HttpPut("frases/{id:int}"), Authorize(Roles = "admin")]
    public Task<IActionResult> AtualizarFrase(int id, FraseRequest request, CancellationToken ct) => Handle(() => _svc.UpdateFraseAsync(id, request, ct));
    [HttpDelete("frases/{id:int}"), Authorize(Roles = "admin")]
    public Task<IActionResult> ExcluirFrase(int id, CancellationToken ct) => Handle(() => _svc.DeleteFraseAsync(id, ct));

    private async Task<IActionResult> Handle(Func<Task<object>> action)
    {
        try { return Ok(await action()); }
        catch (ConteudosImpressosException ex)
        {
            return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.StatusCode == 404 ? "Não encontrado" : "Validação", ex.Message));
        }
    }
}
