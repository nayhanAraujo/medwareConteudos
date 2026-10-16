using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Avaliacoes;

public sealed class AvaliacaoService(IFirebirdConnectionFactory db)
{
    private const string Select = """
        SELECT a.ID, a.CODSCRIPTLAUDO AS CodScriptLaudo, a.CODVERSAO AS CodVersao,
          a.ORIGEM, a.CLIENTE_ID AS ClienteId, a.USUARIO_ID AS UsuarioId,
          a.NOME_AVALIADOR AS NomeAvaliador, a.NOME_CLINICA AS NomeClinica,
          a.NOME_SCRIPT AS NomeScript, a.NUMERO_VERSAO AS NumeroVersao, a.NOTA_GERAL AS NotaGeral,
          a.RECOMENDACAO, a.FACILIDADE_USO AS FacilidadeUso, a.QUALIDADE_CONTEUDO AS QualidadeConteudo,
          a.COMPLETUDE, a.APARENCIA, a.UTILIDADE_CLINICA AS UtilidadeClinica,
          a.TITULO, a.COMENTARIO, a.CRIADO_EM AS CriadoEm, a.ATUALIZADO_EM AS AtualizadoEm, a.SITUACAO, a.VISIVEL
        FROM SCRIPT_AVALIACAO a
        """;
    private const string IdentityWhere = "a.IDENTIDADE=@identity AND a.CODSCRIPTLAUDO=@script AND a.VERSAO_CHAVE=@version AND a.ORIGEM=@origem AND a.CLIENTE_ID=@cliente AND a.USUARIO_ID=@usuario";
    private static string Identity(AvaliacaoInput input) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { input.Origem, input.ClienteId, input.UsuarioId }))));

    public async Task<AvaliacaoSave> SaveAsync(int script, AvaliacaoInput input, CancellationToken ct = default)
    {
        Normalize(input);
        for (var attempt = 0; ; attempt++)
        {
            try { return await SaveOnceAsync(script, input, ct); }
            catch (FbException ex) when (attempt < 4 && ex.Errors.Cast<FbError>().Any(e => e.Number is 335544336 or 335544345 or 335544665))
            {
                await Task.Delay(30 * (attempt + 1), ct);
            }
        }
    }

    private async Task<AvaliacaoSave> SaveOnceAsync(int script, AvaliacaoInput input, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await AvaliacaoSchema.ValidateAsync(conn);
        await using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        // Serialize writes per script, including first submissions, before testing the natural key.
        var name = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT NOME FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=@script WITH LOCK", new { script }, tx);
        if (name is null) throw new AvaliacaoException(404, "Script nao encontrado.");
        string? number = null;
        if (input.CodVersao.HasValue)
        {
            number = await conn.QueryFirstOrDefaultAsync<string>("SELECT NUMERO_VERSAO FROM SCRIPTVERSOES WHERE CODVERSAO=@version AND CODSCRIPTLAUDO=@script",
                new { version = input.CodVersao, script }, tx);
            if (number is null) throw new AvaliacaoException(400, "A versao informada nao pertence ao script.");
        }
        var identity = new { script, version = input.CodVersao ?? 0, origem = input.Origem, cliente = input.ClienteId, usuario = input.UsuarioId, identity = Identity(input) };
        var current = await conn.QueryFirstOrDefaultAsync<AvaliacaoRecord>(Select + " WHERE " + IdentityWhere + " WITH LOCK", identity, tx);
        var payload = JsonSerializer.Serialize(input);
        if (current is not null && JsonSerializer.Serialize(InputFrom(current)) == payload)
        {
            await tx.CommitAsync(ct);
            return new(current.Id, false, false, current.Publica());
        }
        var now = DateTime.UtcNow;
        var args = new DynamicParameters(input);
        args.Add("script", script); args.Add("versionKey", input.CodVersao ?? 0);
        args.Add("name", name); args.Add("number", number); args.Add("now", now);
        args.Add("identity", Identity(input));
        long id;
        if (current is null)
        {
            id = await conn.ExecuteScalarAsync<long>("""
                INSERT INTO SCRIPT_AVALIACAO
                (CODSCRIPTLAUDO,CODVERSAO,VERSAO_CHAVE,IDENTIDADE,ORIGEM,CLIENTE_ID,USUARIO_ID,NOME_AVALIADOR,NOME_CLINICA,
                 NOME_SCRIPT,NUMERO_VERSAO,NOTA_GERAL,RECOMENDACAO,FACILIDADE_USO,QUALIDADE_CONTEUDO,COMPLETUDE,
                 APARENCIA,UTILIDADE_CLINICA,TITULO,COMENTARIO,CRIADO_EM,ATUALIZADO_EM,SITUACAO,VISIVEL)
                VALUES (@script,@CodVersao,@versionKey,@identity,@Origem,@ClienteId,@UsuarioId,@NomeAvaliador,@NomeClinica,
                 @name,@number,@NotaGeral,@Recomendacao,@FacilidadeUso,@QualidadeConteudo,@Completude,
                 @Aparencia,@UtilidadeClinica,@Titulo,@Comentario,@now,@now,'nova',1) RETURNING ID
                """, args, tx);
        }
        else
        {
            id = current.Id;
            args.Add("id", id);
            await conn.ExecuteAsync("""
                UPDATE SCRIPT_AVALIACAO SET NOME_AVALIADOR=@NomeAvaliador,NOME_CLINICA=@NomeClinica,
                 NOTA_GERAL=@NotaGeral,RECOMENDACAO=@Recomendacao,FACILIDADE_USO=@FacilidadeUso,
                 QUALIDADE_CONTEUDO=@QualidadeConteudo,COMPLETUDE=@Completude,APARENCIA=@Aparencia,
                 UTILIDADE_CLINICA=@UtilidadeClinica,TITULO=@Titulo,COMENTARIO=@Comentario,
                 ATUALIZADO_EM=@now,SITUACAO='nova' WHERE ID=@id
                """, args, tx);
        }
        var after = await conn.QuerySingleAsync<AvaliacaoRecord>(Select + " WHERE a.ID=@id", new { id }, tx);
        await EventAsync(conn, tx, id, current is null ? "criacao" : "revisao", $"{input.Origem}:{input.ClienteId}:{input.UsuarioId}",
            null, current, after, null);
        await tx.CommitAsync(ct);
        return new(id, current is null, current is not null, after.Publica());
    }

    public async Task<AvaliacaoPublica?> MineAsync(int script, int? version, string origem, string cliente, string usuario, CancellationToken ct)
    {
        var identity = new AvaliacaoInput { Origem = origem, ClienteId = cliente, UsuarioId = usuario };
        ValidateIdentity(identity);
        if (version < 0) throw new AvaliacaoException(400, "codversao deve ser zero ou um identificador de versao.");
        await using var conn = await db.OpenConnectionAsync(ct);
        await AvaliacaoSchema.ValidateAsync(conn);
        var row = await conn.QueryFirstOrDefaultAsync<AvaliacaoRecord>(Select + " WHERE " + IdentityWhere,
            new { script, version = version ?? 0, origem = identity.Origem, cliente = identity.ClienteId, usuario = identity.UsuarioId, identity = Identity(identity) });
        return row?.Publica();
    }

    public async Task<AvaliacaoPagina<AvaliacaoRecord>> ListAsync(AvaliacaoFiltro filter, bool publicOnly, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await AvaliacaoSchema.ValidateAsync(conn);
        var (where, args) = Filter(filter, publicOnly);
        var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SCRIPT_AVALIACAO a JOIN SCRIPTLAUDO s ON s.CODSCRIPTLAUDO=a.CODSCRIPTLAUDO WHERE " + where, args);
        args.Add("first", filter.PageSize); args.Add("skip", (long)(filter.Page - 1) * filter.PageSize);
        var query = Select.Replace("SELECT a.ID", "SELECT FIRST @first SKIP @skip a.ID") +
            " JOIN SCRIPTLAUDO s ON s.CODSCRIPTLAUDO=a.CODSCRIPTLAUDO WHERE " + where + " ORDER BY a.ATUALIZADO_EM DESC,a.ID DESC";
        var rows = (await conn.QueryAsync<AvaliacaoRecord>(query, args)).ToList();
        return new(rows, filter.Page, filter.PageSize, total, (int)Math.Ceiling(total / (double)filter.PageSize));
    }

    public async Task<AvaliacaoResumo> SummaryAsync(AvaliacaoFiltro filter, bool publicOnly, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await AvaliacaoSchema.ValidateAsync(conn);
        var (where, args) = Filter(filter, publicOnly);
        var from = " FROM SCRIPT_AVALIACAO a JOIN SCRIPTLAUDO s ON s.CODSCRIPTLAUDO=a.CODSCRIPTLAUDO WHERE " + where;
        var result = await conn.QuerySingleAsync<AvaliacaoResumo>("""
            SELECT COUNT(*) AS Quantidade, AVG(CAST(a.NOTA_GERAL AS DOUBLE PRECISION)) AS MediaGeral,
              AVG(CAST(a.FACILIDADE_USO AS DOUBLE PRECISION)) AS FacilidadeUso,
              AVG(CAST(a.QUALIDADE_CONTEUDO AS DOUBLE PRECISION)) AS QualidadeConteudo,
              AVG(CAST(a.COMPLETUDE AS DOUBLE PRECISION)) AS Completude,
              AVG(CAST(a.APARENCIA AS DOUBLE PRECISION)) AS Aparencia,
              AVG(CAST(a.UTILIDADE_CLINICA AS DOUBLE PRECISION)) AS UtilidadeClinica
            """ + from, args);
        result.DistribuicaoNotas = Enumerable.Range(1, 5).ToDictionary(x => x, _ => 0);
        foreach (var row in await conn.QueryAsync("SELECT a.NOTA_GERAL AS Nota,COUNT(*) AS Quantidade" + from + " GROUP BY a.NOTA_GERAL", args))
            result.DistribuicaoNotas[(int)row.NOTA] = Convert.ToInt32(row.QUANTIDADE);
        result.Recomendacoes = new() { ["sim"] = 0, ["talvez"] = 0, ["nao"] = 0 };
        foreach (var row in await conn.QueryAsync("SELECT a.RECOMENDACAO,COUNT(*) AS Quantidade" + from + " GROUP BY a.RECOMENDACAO", args))
            result.Recomendacoes[(string)row.RECOMENDACAO] = Convert.ToInt32(row.QUANTIDADE);
        return result;
    }

    public async Task<(AvaliacaoRecord Avaliacao, IReadOnlyList<AvaliacaoEvento> Historico)> DetailAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await AvaliacaoSchema.ValidateAsync(conn);
        var row = await conn.QueryFirstOrDefaultAsync<AvaliacaoRecord>(Select + " WHERE a.ID=@id", new { id });
        if (row is null) throw new AvaliacaoException(404, "Avaliacao nao encontrada.");
        var events = (await conn.QueryAsync<AvaliacaoEvento>("""
            SELECT ID,TIPO,AUTOR,CODUSUARIO AS CodUsuario,DATA_EVENTO AS DataEvento,ANTES,DEPOIS,OBSERVACAO
            FROM SCRIPT_AVALIACAO_EVENTO WHERE AVALIACAO_ID=@id ORDER BY ID DESC
            """, new { id })).ToList();
        return (row, events);
    }

    public async Task ChangeAsync(long id, string kind, string? value, bool? visible, string actor, int user, CancellationToken ct)
    {
        value = value?.Trim();
        if (kind == "triagem" && value is not ("nova" or "em_analise" or "concluida"))
            throw new AvaliacaoException(400, "Situacao invalida.");
        if (kind is "anotacao" or "visibilidade" && (string.IsNullOrWhiteSpace(value) || value.Length > 2000))
            throw new AvaliacaoException(400, "Informe um texto de ate 2000 caracteres.");
        await using var conn = await db.OpenConnectionAsync(ct);
        await AvaliacaoSchema.ValidateAsync(conn);
        await using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        var locked = await conn.QueryFirstOrDefaultAsync<long?>("SELECT ID FROM SCRIPT_AVALIACAO WHERE ID=@id WITH LOCK", new { id }, tx);
        if (!locked.HasValue) throw new AvaliacaoException(404, "Avaliacao nao encontrada.");
        var before = await conn.QuerySingleAsync<AvaliacaoRecord>(Select + " WHERE a.ID=@id", new { id }, tx);
        if (kind == "triagem") await conn.ExecuteAsync("UPDATE SCRIPT_AVALIACAO SET SITUACAO=@value WHERE ID=@id", new { value, id }, tx);
        if (kind == "visibilidade") await conn.ExecuteAsync("UPDATE SCRIPT_AVALIACAO SET VISIVEL=@flag WHERE ID=@id", new { flag = visible == true ? 1 : 0, id }, tx);
        var after = await conn.QuerySingleAsync<AvaliacaoRecord>(Select + " WHERE a.ID=@id", new { id }, tx);
        await EventAsync(conn, tx, id, kind == "visibilidade" ? visible == true ? "restauracao" : "ocultacao" : kind,
            actor, user, before, after, kind == "triagem" ? null : value);
        await tx.CommitAsync(ct);
    }

    private static Task<int> EventAsync(IDbConnection conn, IDbTransaction tx, long id, string kind, string actor, int? user,
        AvaliacaoRecord? before, AvaliacaoRecord after, string? observation) => conn.ExecuteAsync("""
        INSERT INTO SCRIPT_AVALIACAO_EVENTO (AVALIACAO_ID,TIPO,AUTOR,CODUSUARIO,DATA_EVENTO,ANTES,DEPOIS,OBSERVACAO)
        VALUES (@id,@kind,@actor,@user,@now,@before,@after,@observation)
        """, new { id, kind, actor, user, now = DateTime.UtcNow, before = before is null ? null : JsonSerializer.Serialize(before), after = JsonSerializer.Serialize(after), observation }, tx);

    private static (string Where, DynamicParameters Args) Filter(AvaliacaoFiltro f, bool publicOnly)
    {
        try { Validator.ValidateObject(f, new ValidationContext(f), true); }
        catch (ValidationException e) { throw new AvaliacaoException(400, e.Message); }
        if (f.Inicio > f.Fim || f.Versao < 0 || f.Fim?.Date == DateTime.MaxValue.Date
            || (long)(f.Page - 1) * f.PageSize > int.MaxValue)
            throw new AvaliacaoException(400, "Periodo, versao ou pagina invalida.");
        var where = new List<string> { "1=1" }; var args = new DynamicParameters();
        void Add(string clause, string name, object? value) { if (value is not null) { where.Add(clause); args.Add(name, value); } }
        Add("a.CODSCRIPTLAUDO=@script", "script", f.Script);
        Add("a.VERSAO_CHAVE=@version", "version", f.Versao);
        if (publicOnly) where.Add("a.VISIVEL=1");
        else Add("a.VISIVEL=@visible", "visible", f.Visivel.HasValue ? f.Visivel.Value ? 1 : 0 : null);
        Add("s.SISTEMA=@system", "system", Empty(f.Sistema)); Add("s.CODPACOTE=@package", "package", f.Pacote);
        Add("a.ORIGEM=@origin", "origin", Empty(f.Origem)?.ToLowerInvariant());
        Add("a.NOME_CLINICA CONTAINING @clinic", "clinic", Empty(f.Clinica));
        Add("a.ATUALIZADO_EM>=@start", "start", f.Inicio?.Date);
        Add("a.ATUALIZADO_EM<@end", "end", f.Fim?.Date.AddDays(1));
        Add("a.NOTA_GERAL=@rating", "rating", f.Nota); Add("a.RECOMENDACAO=@recommendation", "recommendation", Empty(f.Recomendacao));
        Add("a.SITUACAO=@state", "state", Empty(f.Situacao));
        return (string.Join(" AND ", where), args);
    }

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void ValidateIdentity(AvaliacaoInput input)
    {
        input.Origem = (input.Origem ?? "").Trim().ToLowerInvariant();
        input.ClienteId = (input.ClienteId ?? "").Trim(); input.UsuarioId = (input.UsuarioId ?? "").Trim();
        if (input.Origem.Length is 0 or > 40 || input.ClienteId.Length is 0 or > 80 || input.UsuarioId.Length is 0 or > 80)
            throw new AvaliacaoException(400, "Origem, cliente_id e usuario_id sao obrigatorios e devem respeitar os limites.");
    }
    private static void Normalize(AvaliacaoInput input)
    {
        ValidateIdentity(input);
        input.NomeAvaliador = (input.NomeAvaliador ?? "").Trim(); input.NomeClinica = (input.NomeClinica ?? "").Trim();
        input.Titulo = Empty(input.Titulo); input.Comentario = Empty(input.Comentario);
        try { Validator.ValidateObject(input, new ValidationContext(input), true); }
        catch (ValidationException ex) { throw new AvaliacaoException(400, ex.Message); }
        if (input.CodVersao <= 0) throw new AvaliacaoException(400, "codversao deve ser null ou um identificador positivo.");
    }
    private static AvaliacaoInput InputFrom(AvaliacaoRecord row) => new()
    {
        CodVersao = row.CodVersao, Origem = row.Origem, ClienteId = row.ClienteId, UsuarioId = row.UsuarioId,
        NomeAvaliador = row.NomeAvaliador, NomeClinica = row.NomeClinica, NotaGeral = row.NotaGeral,
        Recomendacao = row.Recomendacao, FacilidadeUso = row.FacilidadeUso, QualidadeConteudo = row.QualidadeConteudo,
        Completude = row.Completude, Aparencia = row.Aparencia, UtilidadeClinica = row.UtilidadeClinica,
        Titulo = row.Titulo, Comentario = row.Comentario
    };
}
