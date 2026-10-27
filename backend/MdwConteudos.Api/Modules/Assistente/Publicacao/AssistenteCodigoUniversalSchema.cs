using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Assistente.Publicacao;

public static class AssistenteCodigoUniversalSchema
{
    public static string ValidateSourceCode(string? code)
    {
        if (code is null || !Regex.IsMatch(code, @"\AMOD-[0-9]{6,20}\z", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Codigo universal da origem ausente ou invalido. Aplique a migracao de Conteudos antes de sincronizar.");
        return code;
    }

    public static async Task<int> MigrateAsync(IFirebirdConnectionFactory sourceFactory,
        IAssistantFirebirdConnectionFactory targetFactory, string origin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(origin) || origin.Length > 64)
            throw new InvalidOperationException("Configure PublicacaoAssistente:SourceKey antes da migracao.");
        await using var source = await sourceFactory.OpenConnectionAsync(ct);
        if (await source.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM RDB$RELATION_FIELDS
            WHERE RDB$RELATION_NAME='SCRIPTLAUDO' AND RDB$FIELD_NAME='CODUNIVERSAL'
            """, cancellationToken: ct)) != 1)
            throw new InvalidOperationException("Aplique primeiro a migracao de codigo universal no banco Conteudos.");

        await using var target = await targetFactory.OpenConnectionAsync(ct);
        if (await target.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME='CON_PUBLICACAO'", cancellationToken: ct)) != 1)
            throw new InvalidOperationException("Aplique primeiro a migracao de publicacao no banco Assistente.");
        using var reader = new StreamReader(typeof(AssistenteCodigoUniversalSchema).Assembly
            .GetManifestResourceStream("assistente-codigo-universal.sql")!);
        var sql = await reader.ReadToEndAsync(ct);
        var statements = sql.Split("-- NEXT", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // Commit metadata separately; retrying never renumbers models or recreates tables.
        await target.ExecuteAsync(new CommandDefinition(statements[0], cancellationToken: ct));
        var compatible = await target.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM RDB$RELATION_FIELDS r
            JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME=r.RDB$FIELD_SOURCE
            JOIN RDB$CHARACTER_SETS c ON c.RDB$CHARACTER_SET_ID=f.RDB$CHARACTER_SET_ID
            WHERE r.RDB$RELATION_NAME='SCRIPTLAUDO' AND r.RDB$FIELD_NAME='CODUNIVERSAL'
              AND f.RDB$FIELD_TYPE=37 AND f.RDB$CHARACTER_LENGTH=24
              AND c.RDB$CHARACTER_SET_NAME='ASCII' AND COALESCE(r.RDB$NULL_FLAG,0)=0
            """, cancellationToken: ct));
        if (compatible != 1)
            throw new InvalidOperationException("SCRIPTLAUDO.CODUNIVERSAL no Assistente possui esquema incompativel; nenhuma coluna existente foi recriada.");
        foreach (var statement in statements.Skip(1))
            await target.ExecuteAsync(new CommandDefinition(statement, cancellationToken: ct));
        var unique = await target.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM RDB$RELATION_CONSTRAINTS r
            JOIN RDB$INDEX_SEGMENTS s ON s.RDB$INDEX_NAME=r.RDB$INDEX_NAME
            JOIN RDB$INDICES i ON i.RDB$INDEX_NAME=r.RDB$INDEX_NAME
            WHERE r.RDB$RELATION_NAME='SCRIPTLAUDO' AND r.RDB$CONSTRAINT_NAME='UNQ_SCRIPTLAUDO_CODUNIVERSAL'
              AND r.RDB$CONSTRAINT_TYPE='UNIQUE' AND s.RDB$FIELD_NAME='CODUNIVERSAL' AND i.RDB$SEGMENT_COUNT=1
            """, cancellationToken: ct));
        if (unique != 1)
            throw new InvalidOperationException("Constraint UNQ_SCRIPTLAUDO_CODUNIVERSAL incompativel no Assistente.");

        await using var tx = await target.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await target.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT RDB$SET_CONTEXT('USER_TRANSACTION','PUBLICACAO','1') FROM RDB$DATABASE", transaction: tx, cancellationToken: ct));
        var mappings = (await target.QueryAsync<Mapping>(new CommandDefinition("""
            SELECT p.CODORIGEM AS SourceId, s.CODSCRIPTLAUDO AS DestinationId
            FROM CON_PUBLICACAO p JOIN SCRIPTLAUDO s ON s.CODSCRIPTLAUDO=p.CODDESTINO
            WHERE p.ORIGEM=@origin
            """, new { origin }, tx, cancellationToken: ct))).ToArray();
        var filled = 0;
        foreach (var batch in mappings.Chunk(100))
        {
            var codes = (await source.QueryAsync<SourceCode>(new CommandDefinition(
                "SELECT CODSCRIPTLAUDO AS SourceId,CODUNIVERSAL AS Code FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO IN @ids",
                new { ids = batch.Select(x => x.SourceId).ToArray() }, cancellationToken: ct))).ToDictionary(x => x.SourceId, x => ValidateSourceCode(x.Code));
            foreach (var mapping in batch)
                if (codes.TryGetValue(mapping.SourceId, out var code))
                    filled += await AssignAsync(target, tx, mapping.DestinationId, code, ct);
        }
        await tx.CommitAsync(ct);
        return filled;
    }

    internal static async Task<int> AssignAsync(FbConnection target, IDbTransaction tx, int destination,
        string code, CancellationToken ct)
    {
        ValidateSourceCode(code);
        var row = await target.QuerySingleAsync<DestinationCode>(new CommandDefinition(
            "SELECT CODUNIVERSAL AS Code FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=@destination WITH LOCK",
            new { destination }, tx, cancellationToken: ct));
        if (row.Code is not null && row.Code != code)
            throw new InvalidOperationException($"O modelo Assistente #{destination} ja possui outro codigo universal. Revise o mapeamento antes de sincronizar.");
        if (row.Code == code) return 0;
        if (await target.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM SCRIPTLAUDO WHERE CODUNIVERSAL=@code AND CODSCRIPTLAUDO<>@destination",
            new { code, destination }, tx, cancellationToken: ct)) > 0)
            throw new InvalidOperationException($"O codigo universal {code} ja pertence a outro modelo no Assistente. Nenhuma associacao automatica foi realizada.");
        return await target.ExecuteAsync(new CommandDefinition(
            "UPDATE SCRIPTLAUDO SET CODUNIVERSAL=@code WHERE CODSCRIPTLAUDO=@destination",
            new { code, destination }, tx, cancellationToken: ct));
    }

    private sealed record Mapping(int SourceId, int DestinationId);
    private sealed record SourceCode(int SourceId, string? Code);
    private sealed class DestinationCode { public string? Code { get; set; } }
}
