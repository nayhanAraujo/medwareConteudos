using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Permissions;
using MdwConteudos.Api.Services;

namespace MdwConteudos.Api.Modules.ConteudosImpressos;

[ApiController]
[Route("api/web/modelos-mensagens")]
[Authorize]
public class ModelosMensagensWebController : ControllerBase
{
    private readonly IFirebirdConnectionFactory _db;
    public ModelosMensagensWebController(IFirebirdConnectionFactory db) => _db = db;

    [HttpGet("grupos")]
    public async Task<IActionResult> ListGrupos([FromQuery] bool somenteAtivos = false, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var where = somenteAtivos ? "WHERE ATIVO = 1" : string.Empty;
        var rows = (await conn.QueryAsync<GrupoMensagemDto>(new CommandDefinition($"""
            SELECT CODGRUPOMENSAGEM AS CodGrupoMensagem, NOME AS Nome, DESCRICAO AS Descricao,
                   ATIVO AS Ativo, DTHRULTMODIFICACAO AS DthrUltModificacao
              FROM GRUPOSMENSAGENS {where} ORDER BY NOME
            """, cancellationToken: ct))).AsList();
        return Ok(ApiResponse.Ok(rows, rows.Count));
    }

    [HttpPost("grupos"), RequirePermission(PermissionDomains.Conteudos, PermissionActions.Criar)]
    public Task<IActionResult> CreateGrupo(GrupoMensagemRequest request, CancellationToken ct) => Handle(async () =>
    {
        var nome = Required(request.Nome, "Nome do grupo");
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM GRUPOSMENSAGENS WHERE UPPER(NOME) = UPPER(@Nome)", new { Nome = nome }, tx, cancellationToken: ct)) > 0)
            throw new ConteudosImpressosException("Já existe um grupo com este nome.", 409);
        var id = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO GRUPOSMENSAGENS (NOME, DESCRICAO, ATIVO, CODUSUARIO, DTHRULTMODIFICACAO)
            VALUES (@Nome, @Descricao, @Ativo, @CodUsuario, @Agora) RETURNING CODGRUPOMENSAGEM
            """, new { Nome = nome, Descricao = ConteudoTextCodec.Clean(request.Descricao), Ativo = Flag(request.Ativo), CodUsuario = User.GetCodUsuario(), Agora = DateTime.UtcNow }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return ApiResponse.Ok(new { codGrupoMensagem = id });
    });

    [HttpPut("grupos/{id:int}"), RequirePermission(PermissionDomains.Conteudos, PermissionActions.Editar)]
    public Task<IActionResult> UpdateGrupo(int id, GrupoMensagemRequest request, CancellationToken ct) => Handle(async () =>
    {
        var nome = Required(request.Nome, "Nome do grupo");
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM GRUPOSMENSAGENS WHERE UPPER(NOME) = UPPER(@Nome) AND CODGRUPOMENSAGEM <> @Id",
                new { Nome = nome, Id = id }, tx, cancellationToken: ct)) > 0)
            throw new ConteudosImpressosException("Já existe outro grupo com este nome.", 409);
        var count = await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE GRUPOSMENSAGENS SET NOME = @Nome, DESCRICAO = @Descricao, ATIVO = @Ativo,
                   CODUSUARIO = @CodUsuario, DTHRULTMODIFICACAO = @Agora WHERE CODGRUPOMENSAGEM = @Id
            """, new { Nome = nome, Descricao = ConteudoTextCodec.Clean(request.Descricao), Ativo = Flag(request.Ativo), CodUsuario = User.GetCodUsuario(), Agora = DateTime.UtcNow, Id = id }, tx, cancellationToken: ct));
        if (count == 0) throw new ConteudosImpressosException("Grupo não encontrado.", 404);
        await tx.CommitAsync(ct);
        return ApiResponse.OkMessage("Grupo atualizado com sucesso.");
    });

    [HttpDelete("grupos/{id:int}"), RequirePermission(PermissionDomains.Conteudos, PermissionActions.Excluir)]
    public Task<IActionResult> DeleteGrupo(int id, CancellationToken ct) => Handle(async () =>
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var associated = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM MODELOSMENSAGENS WHERE CODGRUPOMENSAGEM = @Id", new { Id = id }, tx, cancellationToken: ct));
        if (associated > 0) throw new ConteudosImpressosException($"Não é possível excluir o grupo. Existem {associated} mensagem(ns) associada(s).", 409);
        var count = await conn.ExecuteAsync(new CommandDefinition("DELETE FROM GRUPOSMENSAGENS WHERE CODGRUPOMENSAGEM = @Id", new { Id = id }, tx, cancellationToken: ct));
        if (count == 0) throw new ConteudosImpressosException("Grupo não encontrado.", 404);
        await tx.CommitAsync(ct);
        return ApiResponse.OkMessage("Grupo excluído com sucesso.");
    });

    [HttpGet("grupos/{codGrupo:int}/mensagens")]
    public async Task<IActionResult> ListMensagens(int codGrupo, [FromQuery] bool somenteAtivas = false, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var where = somenteAtivas ? "AND ATIVO = 1" : string.Empty;
        var rows = (await conn.QueryAsync<ModeloMensagemDto>(new CommandDefinition($"""
            SELECT CODMODELOMENSAGEM AS CodModeloMensagem, CODGRUPOMENSAGEM AS CodGrupoMensagem,
                   TITULO AS Titulo, CONTEUDO AS Conteudo, TIPO_MENSAGEM AS TipoMensagem,
                   ATIVO AS Ativo, DTHRULTMODIFICACAO AS DthrUltModificacao
              FROM MODELOSMENSAGENS WHERE CODGRUPOMENSAGEM = @CodGrupo {where} ORDER BY TITULO
            """, new { CodGrupo = codGrupo }, cancellationToken: ct))).AsList();
        return Ok(ApiResponse.Ok(rows, rows.Count));
    }

    [HttpGet("mensagens/{id:int}")]
    public async Task<IActionResult> GetMensagem(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<ModeloMensagemDto>(new CommandDefinition("""
            SELECT CODMODELOMENSAGEM AS CodModeloMensagem, CODGRUPOMENSAGEM AS CodGrupoMensagem,
                   TITULO AS Titulo, CONTEUDO AS Conteudo, TIPO_MENSAGEM AS TipoMensagem,
                   ATIVO AS Ativo, DTHRULTMODIFICACAO AS DthrUltModificacao
              FROM MODELOSMENSAGENS WHERE CODMODELOMENSAGEM = @Id
            """, new { Id = id }, cancellationToken: ct));
        return row is null ? NotFound(ApiResponse.Fail("Não encontrado", "Mensagem não encontrada.")) : Ok(ApiResponse.Ok(row));
    }

    [HttpPost("mensagens"), RequirePermission(PermissionDomains.Conteudos, PermissionActions.Criar)]
    public Task<IActionResult> CreateMensagem(ModeloMensagemRequest request, CancellationToken ct) => Handle(async () =>
    {
        var titulo = Required(request.Titulo, "Título");
        var conteudo = Required(request.Conteudo, "Conteúdo");
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM GRUPOSMENSAGENS WHERE CODGRUPOMENSAGEM = @Id", new { Id = request.CodGrupoMensagem }, tx, cancellationToken: ct)) == 0)
            throw new ConteudosImpressosException("Grupo não encontrado.", 404);
        if (await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM MODELOSMENSAGENS WHERE UPPER(TITULO) = UPPER(@Titulo) AND CODGRUPOMENSAGEM = @Grupo",
                new { Titulo = titulo, Grupo = request.CodGrupoMensagem }, tx, cancellationToken: ct)) > 0)
            throw new ConteudosImpressosException("Já existe uma mensagem com este título neste grupo.", 409);
        var id = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO MODELOSMENSAGENS (CODGRUPOMENSAGEM, TITULO, CONTEUDO, TIPO_MENSAGEM, ATIVO, CODUSUARIO, DTHRULTMODIFICACAO)
            VALUES (@Grupo, @Titulo, @Conteudo, @Tipo, @Ativo, @Usuario, @Agora) RETURNING CODMODELOMENSAGEM
            """, new { Grupo = request.CodGrupoMensagem, Titulo = titulo, Conteudo = conteudo, Tipo = NormalizeTipo(request.TipoMensagem), Ativo = Flag(request.Ativo), Usuario = User.GetCodUsuario(), Agora = DateTime.UtcNow }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return ApiResponse.Ok(new { codModeloMensagem = id });
    });

    [HttpPut("mensagens/{id:int}"), RequirePermission(PermissionDomains.Conteudos, PermissionActions.Editar)]
    public Task<IActionResult> UpdateMensagem(int id, ModeloMensagemRequest request, CancellationToken ct) => Handle(async () =>
    {
        var titulo = Required(request.Titulo, "Título");
        var conteudo = Required(request.Conteudo, "Conteúdo");
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var grupoAtual = await conn.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT CODGRUPOMENSAGEM FROM MODELOSMENSAGENS WHERE CODMODELOMENSAGEM = @Id", new { Id = id }, tx, cancellationToken: ct));
        if (grupoAtual is null) throw new ConteudosImpressosException("Mensagem não encontrada.", 404);
        if (await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM GRUPOSMENSAGENS WHERE CODGRUPOMENSAGEM = @Id", new { Id = request.CodGrupoMensagem }, tx, cancellationToken: ct)) == 0)
            throw new ConteudosImpressosException("Grupo não encontrado.", 404);
        if (await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM MODELOSMENSAGENS WHERE UPPER(TITULO) = UPPER(@Titulo) AND CODGRUPOMENSAGEM = @Grupo AND CODMODELOMENSAGEM <> @Id",
                new { Titulo = titulo, Grupo = request.CodGrupoMensagem, Id = id }, tx, cancellationToken: ct)) > 0)
            throw new ConteudosImpressosException("Já existe outra mensagem com este título neste grupo.", 409);
        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE MODELOSMENSAGENS SET CODGRUPOMENSAGEM = @Grupo, TITULO = @Titulo, CONTEUDO = @Conteudo,
                   TIPO_MENSAGEM = @Tipo, ATIVO = @Ativo, CODUSUARIO = @Usuario, DTHRULTMODIFICACAO = @Agora
             WHERE CODMODELOMENSAGEM = @Id
            """, new { Grupo = request.CodGrupoMensagem, Titulo = titulo, Conteudo = conteudo, Tipo = NormalizeTipo(request.TipoMensagem), Ativo = Flag(request.Ativo), Usuario = User.GetCodUsuario(), Agora = DateTime.UtcNow, Id = id }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return ApiResponse.OkMessage("Mensagem atualizada com sucesso.");
    });

    [HttpDelete("mensagens/{id:int}"), RequirePermission(PermissionDomains.Conteudos, PermissionActions.Excluir)]
    public Task<IActionResult> DeleteMensagem(int id, CancellationToken ct) => Handle(async () =>
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var count = await conn.ExecuteAsync(new CommandDefinition("DELETE FROM MODELOSMENSAGENS WHERE CODMODELOMENSAGEM = @Id", new { Id = id }, tx, cancellationToken: ct));
        if (count == 0) throw new ConteudosImpressosException("Mensagem não encontrada.", 404);
        await tx.CommitAsync(ct);
        return ApiResponse.OkMessage("Mensagem excluída com sucesso.");
    });

    [HttpGet("variaveis")]
    public async Task<IActionResult> Variaveis(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = (await conn.QueryAsync(new CommandDefinition("""
            SELECT CODVARIAVEISPACCONECTADO AS CodVariaveisPacConectado, VARIAVEL AS Variavel, DESCRICAO AS Descricao
              FROM VARIAVEISPACCONECTADO ORDER BY VARIAVEL
            """, cancellationToken: ct))).AsList();
        return Ok(ApiResponse.Ok(rows, rows.Count));
    }

    [HttpGet("emojis")]
    public IActionResult Emojis() => Ok(ApiResponse.Ok(EmojiCatalog.All));

    private async Task<IActionResult> Handle(Func<Task<object>> action)
    {
        try { return Ok(await action()); }
        catch (ConteudosImpressosException ex)
        {
            return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.StatusCode == 404 ? "Não encontrado" : "Validação", ex.Message));
        }
    }

    private static string Required(string? value, string label)
    {
        var clean = ConteudoTextCodec.Clean(value);
        if (string.IsNullOrWhiteSpace(clean)) throw new ConteudosImpressosException($"{label} é obrigatório.");
        return clean;
    }
    private static int Flag(int value) => value == 0 ? 0 : 1;
    private static string NormalizeTipo(string? value) => string.IsNullOrWhiteSpace(value) ? "TEXTO" : ConteudoTextCodec.Clean(value).ToUpperInvariant();
}

internal static class EmojiCatalog
{
    public static IReadOnlyDictionary<string, EmojiDto[]> All { get; } = new Dictionary<string, EmojiDto[]>
    {
        ["expressoes"] = [new("😊", "Rosto sorridente", "U+1F60A"), new("😄", "Rosto sorrindo", "U+1F604"), new("😂", "Chorando de rir", "U+1F602"), new("😉", "Piscada", "U+1F609"), new("😍", "Olhos de coração", "U+1F60D"), new("🤔", "Pensativo", "U+1F914"), new("😷", "Máscara médica", "U+1F637")],
        ["gestos"] = [new("👍", "Polegar para cima", "U+1F44D"), new("👎", "Polegar para baixo", "U+1F44E"), new("👌", "Sinal de OK", "U+1F44C"), new("✌️", "Paz", "U+270C"), new("👏", "Palmas", "U+1F44F"), new("🙏", "Mãos juntas", "U+1F64F")],
        ["coracoes"] = [new("❤️", "Coração vermelho", "U+2764"), new("💙", "Coração azul", "U+1F499"), new("💚", "Coração verde", "U+1F49A"), new("💛", "Coração amarelo", "U+1F49B"), new("💜", "Coração roxo", "U+1F49C"), new("💔", "Coração partido", "U+1F494")],
        ["saude"] = [new("🩺", "Estetoscópio", "U+1FA7A"), new("💊", "Comprimido", "U+1F48A"), new("💉", "Seringa", "U+1F489"), new("🩹", "Curativo", "U+1FA79"), new("🏥", "Hospital", "U+1F3E5"), new("⚕️", "Símbolo da medicina", "U+2695")],
        ["objetos"] = [new("📱", "Telefone celular", "U+1F4F1"), new("💻", "Computador", "U+1F4BB"), new("🖨️", "Impressora", "U+1F5A8"), new("📎", "Clipe", "U+1F4CE"), new("📌", "Alfinete", "U+1F4CC"), new("💡", "Lâmpada", "U+1F4A1")]
    };
}
