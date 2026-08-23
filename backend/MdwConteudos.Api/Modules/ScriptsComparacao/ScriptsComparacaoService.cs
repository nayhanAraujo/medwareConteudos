using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.ScriptsComparacao;

public sealed class ScriptsComparacaoService(IFirebirdConnectionFactory db)
{
    public async Task<ScriptsComparacaoCatalogo?> ListarAsync(int scriptId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var script = await BuscarScriptAsync(conn, scriptId, ct);
        if (script is null) return null;

        var command = new CommandDefinition(@"
            SELECT CODVERSAO, NUMERO_VERSAO, DESCRICAO_ALTERACOES,
                   ALTERACOES_INTERFACE, ALTERACOES_CODIGO, DATA_CRIACAO,
                   USUARIO_RESPONSAVEL, ATIVO, APROVADO, APROVADO_POR
            FROM SCRIPTVERSOES
            WHERE CODSCRIPTLAUDO = @scriptId
            ORDER BY DATA_CRIACAO DESC, CODVERSAO DESC",
            new { scriptId }, cancellationToken: ct);
        var rows = await conn.QueryAsync(command);
        return new ScriptsComparacaoCatalogo(script, rows.Select(MapearVersao).ToList());
    }

    public async Task<ScriptsComparacaoResultado?> CompararAsync(
        int scriptId,
        int versao1Id,
        int versao2Id,
        CancellationToken ct)
    {
        if (versao1Id == versao2Id)
            throw new InvalidOperationException("Selecione duas versões diferentes para comparar.");

        await using var conn = await db.OpenConnectionAsync(ct);
        var script = await BuscarScriptAsync(conn, scriptId, ct);
        if (script is null) return null;

        var command = new CommandDefinition(@"
            SELECT CODVERSAO, NUMERO_VERSAO, DESCRICAO_ALTERACOES,
                   ALTERACOES_INTERFACE, ALTERACOES_CODIGO, DATA_CRIACAO,
                   USUARIO_RESPONSAVEL, ATIVO, APROVADO, APROVADO_POR
            FROM SCRIPTVERSOES
            WHERE CODSCRIPTLAUDO = @scriptId
              AND CODVERSAO IN (@versao1Id, @versao2Id)",
            new { scriptId, versao1Id, versao2Id }, cancellationToken: ct);
        var rows = (await conn.QueryAsync(command)).Select(MapearVersao).ToDictionary(x => x.CodVersao);

        if (!rows.TryGetValue(versao1Id, out var versao1) || !rows.TryGetValue(versao2Id, out var versao2))
            throw new KeyNotFoundException("Uma ou ambas as versões não foram encontradas para este script.");

        int? dias = null;
        if (versao1.DataCriacao.HasValue && versao2.DataCriacao.HasValue)
            dias = Math.Abs((versao2.DataCriacao.Value.Date - versao1.DataCriacao.Value.Date).Days);

        return new ScriptsComparacaoResultado(
            script,
            versao1,
            versao2,
            dias,
            string.Equals(versao1.UsuarioResponsavel?.Trim(), versao2.UsuarioResponsavel?.Trim(), StringComparison.OrdinalIgnoreCase),
            versao1.Aprovado == versao2.Aprovado);
    }

    private static async Task<ScriptComparacaoResumo?> BuscarScriptAsync(
        System.Data.Common.DbConnection conn,
        int scriptId,
        CancellationToken ct)
    {
        var command = new CommandDefinition(@"
            SELECT CODSCRIPTLAUDO, NOME, DESCRICAO, SISTEMA, LINGUAGEM
            FROM SCRIPTLAUDO
            WHERE CODSCRIPTLAUDO = @scriptId",
            new { scriptId }, cancellationToken: ct);
        var row = await conn.QueryFirstOrDefaultAsync(command);
        if (row is null) return null;

        var values = (IDictionary<string, object>)row;
        return new ScriptComparacaoResumo(
            Convert.ToInt32(values["CODSCRIPTLAUDO"]),
            values["NOME"]?.ToString() ?? string.Empty,
            Texto(values, "DESCRICAO"),
            Texto(values, "SISTEMA"),
            Texto(values, "LINGUAGEM"));
    }

    private static ScriptVersaoComparacaoItem MapearVersao(dynamic row)
    {
        var values = (IDictionary<string, object>)row;
        return new ScriptVersaoComparacaoItem(
            Convert.ToInt32(values["CODVERSAO"]),
            values["NUMERO_VERSAO"]?.ToString() ?? string.Empty,
            Texto(values, "DESCRICAO_ALTERACOES"),
            Texto(values, "ALTERACOES_INTERFACE"),
            Texto(values, "ALTERACOES_CODIGO"),
            values["DATA_CRIACAO"] is DateTime date ? date : null,
            Texto(values, "USUARIO_RESPONSAVEL"),
            Flag(values, "ATIVO"),
            Flag(values, "APROVADO"),
            Texto(values, "APROVADO_POR"));
    }

    private static string? Texto(IDictionary<string, object> values, string key) =>
        values.TryGetValue(key, out var value) && value is not null and not DBNull
            ? value.ToString()
            : null;

    private static bool Flag(IDictionary<string, object> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null or DBNull) return false;
        return value.ToString()?.Trim().ToUpperInvariant() is "T" or "1" or "TRUE" or "S" or "Y";
    }
}
