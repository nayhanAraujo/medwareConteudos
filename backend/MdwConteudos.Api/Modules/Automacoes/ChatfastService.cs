using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Infrastructure;
using FirebirdSql.Data.FirebirdClient;

namespace MdwConteudos.Api.Modules.Automacoes;

public sealed record ChatfastStored(long Id, string Hash, string Document);
public sealed class ChatfastService(IFirebirdConnectionFactory db, IOptions<ChatfastOptions> options)
{
    static CommandDefinition Q(string sql, object? p = null, IDbTransaction? tx = null, CancellationToken ct = default) => new(sql, p, tx, cancellationToken: ct);
    public void Enabled() { if (!options.Value.Enabled) throw new ChatfastException(503, "Integração ChatFast desabilitada."); }
    public void Authenticate(string supplied, bool reports)
    {
        Enabled();
        var key = reports ? options.Value.ReportsKey : options.Value.ResponsesKey;
        if (!ChatfastContract.KeyMatches(key, supplied)) throw new ChatfastException(401, "Credencial de integração inválida.");
        if (options.Value.ReportsKey == options.Value.ResponsesKey) throw new ChatfastException(503, "Use credenciais distintas para cada produtor.");
    }
    public async Task<object> Receive(ChatfastResponse response, CancellationToken ct)
    {
        return await Retry(async () => {
            Enabled(); await using var c = await db.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
            var result = await Insert(c, tx, response, null, null, ct); await tx.CommitAsync(ct); return result;
        }, ct);
    }
    // A concurrent winner may commit between SELECT and INSERT. Reopen the transaction and compare hashes.
    public static async Task<object> Retry(Func<Task<object>> operation, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
            try { return await operation(); }
            catch (FbException ex) when (ex.ErrorCode is 335544665 or 335544336 && attempt < 2)
            { await Task.Delay(100 * (attempt + 1), ct); }
    }
    static async Task<object> Insert(IDbConnection c, IDbTransaction tx, ChatfastResponse r, long? batch, int? row, CancellationToken ct)
    {
        var existing = await c.QuerySingleOrDefaultAsync<ChatfastStored>(Q("SELECT CODCHATFASTRESPOSTA AS Id,SHA256 AS Hash,DOCUMENTO AS Document FROM CHATFASTRESPOSTA WHERE CHAVE=@key", new { key = r.EventKey }, tx, ct));
        if (existing is not null)
        {
            if (existing.Hash != r.Hash) throw new ChatfastException(409, "Chave de evento reutilizada com conteúdo diferente.");
            return new { success = true, id = existing.Id.ToString(), duplicate = true };
        }
        var id = await c.ExecuteScalarAsync<long>(Q("INSERT INTO CHATFASTRESPOSTA(CHAVE,SHA256,ORIGEM,DTHRRESPOSTA,DTHRCRIACAO,CODCHATFASTIMPORTACAO,LINHA,DOCUMENTO) VALUES(@key,@hash,@source,@date,@now,@batch,@row,@doc) RETURNING CODCHATFASTRESPOSTA",
            new { key = r.EventKey, hash = r.Hash, source = r.Source, date = r.RespondedAtUtc, now = DateTime.UtcNow, batch, row, doc = new FirebirdTextBlob(JsonSerializer.Serialize(r, ChatfastContract.Json)) }, tx, ct));
        for (var i = 0; i < r.Items.Count; i++)
            await c.ExecuteAsync(Q("INSERT INTO CHATFASTRESPOSTAITEM(CODCHATFASTRESPOSTA,POSICAO,CODIGOEXTERNO,TRATADO) VALUES(@id,@position,@code,@treated)",
                new { id, position = i + 1, code = r.Items[i].ExternalId, treated = r.Items[i].Treated ? 1 : 0 }, tx, ct));
        return new { success = true, id = id.ToString(), duplicate = false };
    }
    public Task<object> Report(ChatfastReportRequest r, CancellationToken ct) => Retry(() => SaveReport(r, ct), ct);
    async Task<object> SaveReport(ChatfastReportRequest r, CancellationToken ct)
    {
        Enabled(); ChatfastContract.Validate(r);
        var document = ChatfastContract.Canonical(JsonSerializer.SerializeToElement(r, ChatfastContract.Json)); var hash = ChatfastContract.Sha(document);
        var key = ChatfastContract.Sha($"report\n{r.Type}\n{r.BatchId}");
        await using var c = await db.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        var old = await c.QuerySingleOrDefaultAsync<ChatfastStored>(Q("SELECT CODCHATFASTRELATORIO AS Id,SHA256 AS Hash,DOCUMENTO AS Document FROM CHATFASTRELATORIO WHERE CHAVE=@key", new { key }, tx, ct));
        if (old is not null)
        {
            if (old.Hash != hash) throw new ChatfastException(409, "Lote já registrado com outro conteúdo.");
            return new { success = true, id = old.Id.ToString(), duplicate = true };
        }
        var id = await c.ExecuteScalarAsync<long>(Q("INSERT INTO CHATFASTRELATORIO(CHAVE,SHA256,TIPO,INICIO,FIM,COMPLETO,DTHRGERACAO,DTHRCRIACAO,DOCUMENTO) VALUES(@key,@hash,@type,@start,@end,@complete,@generated,@now,@doc) RETURNING CODCHATFASTRELATORIO",
            new { key, hash, type = r.Type, start = r.PeriodStart.UtcDateTime, end = r.PeriodEnd.UtcDateTime, complete = r.Complete ? 1 : 0, generated = r.GeneratedAt.UtcDateTime, now = DateTime.UtcNow, doc = new FirebirdTextBlob(document) }, tx, ct));
        await tx.CommitAsync(ct); return new { success = true, id = id.ToString(), duplicate = false };
    }
    public async Task<ChatfastPublicReport?> Public(string type, DateOnly? day, CancellationToken ct)
    {
        Enabled(); if (type is not ("diario" or "semanal")) throw new ChatfastException(404, "Relatório inexistente.");
        if (day == DateOnly.MaxValue) throw new ChatfastException(400, "Data fora do intervalo de consulta.");
        await using var c = await db.OpenConnectionAsync(ct);
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var start = day.HasValue ? TimeZoneInfo.ConvertTimeToUtc(day.Value.ToDateTime(TimeOnly.MinValue), tz) : DateTime.MinValue;
        var end = day.HasValue ? TimeZoneInfo.ConvertTimeToUtc(day.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), tz) : DateTime.MaxValue;
        var document = await c.QueryFirstOrDefaultAsync<string>(Q("SELECT FIRST 1 DOCUMENTO FROM CHATFASTRELATORIO WHERE TIPO=@type AND COMPLETO=1" +
            (day.HasValue ? " AND INICIO>=@start AND INICIO<@end" : "") + " ORDER BY FIM DESC,DTHRGERACAO DESC,CODCHATFASTRELATORIO DESC", new { type, start, end }, ct: ct));
        if (document is null) return null;
        var report = JsonSerializer.Deserialize<ChatfastReportRequest>(document, ChatfastContract.Json)!;
        var incomplete = await c.ExecuteScalarAsync<int>(Q("SELECT COUNT(*) FROM CHATFASTRELATORIO WHERE TIPO=@type AND COMPLETO=0 AND (FIM>@end OR (FIM=@end AND DTHRGERACAO>@generated))", new { type, end = report.PeriodEnd.UtcDateTime, generated = report.GeneratedAt.UtcDateTime }, ct: ct));
        var result = ChatfastContract.Public(report, DateTimeOffset.UtcNow);
        return result with { Stale = result.Stale || incomplete > 0 };
    }
    public async Task<object> List(string kind, long? before, CancellationToken ct)
    {
        Enabled(); if (kind is not ("relatorios" or "respostas") || before < 1) throw new ChatfastException(400, "Consulta inválida.");
        var reports = kind == "relatorios";
        await using var c = await db.OpenConnectionAsync(ct);
        var table = reports ? "CHATFASTRELATORIO" : "CHATFASTRESPOSTA"; var key = reports ? "CODCHATFASTRELATORIO" : "CODCHATFASTRESPOSTA";
        var rows = await c.QueryAsync<ChatfastStored>(Q($"SELECT FIRST 50 {key} AS Id,SHA256 AS Hash,DOCUMENTO AS Document FROM {table}" + (before.HasValue ? $" WHERE {key}<@before" : "") + $" ORDER BY {key} DESC", new { before }, ct: ct));
        return new { success = true, data = rows.Select(r => new { id = r.Id.ToString(), document = JsonSerializer.Deserialize<JsonElement>(r.Document) }).ToArray() };
    }
    public async Task<object> Import(ChatfastImportRequest r, int userId, CancellationToken ct)
    {
        Enabled();
        if (!System.Text.RegularExpressions.Regex.IsMatch(r.FileSha256 ?? "", "^[a-f0-9]{64}$") || r.Rows is null || r.Rows.Count is < 1 or > 5000 || r.Rows.Select(x => x.Row).Distinct().Count() != r.Rows.Count)
            throw new ChatfastException(400, "Lote de importação inválido.");
        ChatfastContract.Required(r.FileName, 255, "fileName");
        var parsed = new List<(ChatfastImportRow Row, ChatfastResponse Response)>(); var errors = new List<object>();
        foreach (var row in r.Rows)
        {
            try { parsed.Add((row, ChatfastContract.Legacy(row, r.FileSha256!))); }
            catch (Exception ex) when (ex is ChatfastException or JsonException) { errors.Add(new { row = row.Row, message = "Data/hora ou conteúdo da linha inválido." }); }
        }
        if (parsed.Sum(item => item.Response.Items.Count) > 10000) throw new ChatfastException(400, "Máximo de 10.000 marcações por importação. Divida o arquivo sem sobrepor linhas.");
        var preview = new { success = true, preview = true, fileSha256 = r.FileSha256, total = r.Rows.Count, valid = parsed.Count, errors };
        if (!r.Confirm) return preview;
        if (errors.Count > 0) throw new ChatfastException(400, "Corrija as linhas rejeitadas antes de confirmar o lote.");
        await using var c = await db.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        var id = await c.ExecuteScalarAsync<long?>(Q("SELECT CODCHATFASTIMPORTACAO FROM CHATFASTIMPORTACAO WHERE SHA256=@sha", new { sha = r.FileSha256 }, tx, ct));
        if (id.HasValue) { await tx.CommitAsync(ct); return new { success = true, duplicate = true, batch = id.Value.ToString() }; }
        id = await c.ExecuteScalarAsync<long>(Q("INSERT INTO CHATFASTIMPORTACAO(SHA256,NOMEARQUIVO,CODUSUARIO,DTHRCRIACAO) VALUES(@sha,@name,@userId,@now) RETURNING CODCHATFASTIMPORTACAO", new { sha = r.FileSha256, name = r.FileName, userId, now = DateTime.UtcNow }, tx, ct));
        foreach (var item in parsed) await Insert(c, tx, item.Response, id, item.Row.Row, ct);
        await tx.CommitAsync(ct); return new { success = true, duplicate = false, batch = id.Value.ToString(), imported = parsed.Count };
    }
}
