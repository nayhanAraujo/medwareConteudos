using System.Globalization;
using System.Text;
using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Auditoria;

public sealed class AuditReader(IFirebirdConnectionFactory db, TimeProvider clock)
{
    private const string Columns = """
        ID AS Id,OCCURRED_UTC AS OccurredAtUtc,MODULE AS Module,ACTION AS Action,RESULT AS Result,
        ENTITY_TYPE AS EntityType,ENTITY_ID AS EntityId,ENTITY_NAME AS EntityName,SUMMARY AS Summary,
        ACTOR_ID AS ActorId,ACTOR_NAME AS ActorName,ACTOR_KIND AS ActorKind,ORIGIN AS Origin,
        CORRELATION_ID AS CorrelationId,OPERATION_ID AS OperationId
        """;
    public async Task<AuditPage> ListAsync(AuditQuery q, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var from = q.From ?? now.AddDays(-7);
        var to = q.To ?? now;
        if (q.Take is < 1 or > 100 || from >= to || q.ActorId is <= 0) throw new ArgumentException("Período, autor ou tamanho de página inválido.");
        Check(q.Module, AuditCatalog.Modules); Check(q.Action, AuditCatalog.Actions); Check(q.Result, AuditCatalog.Results);
        foreach (var value in new[] { q.EntityType, q.EntityId, q.CorrelationId })
            if (value?.Length > 255 || value?.Any(char.IsControl) == true) throw new ArgumentException("Filtro inválido.");
        var p = new DynamicParameters(new { from = from.UtcDateTime, to = to.UtcDateTime });
        var filters = new List<string> { "OCCURRED_UTC>=@from", "OCCURRED_UTC<@to" };
        void Filter(string column, string key, object? value)
        { if (value is not null) { filters.Add(column + "=@" + key); p.Add(key, value); } }
        Filter("ACTOR_ID", "actor", q.ActorId); Filter("MODULE", "module", q.Module); Filter("ACTION", "action", q.Action);
        Filter("RESULT", "result", q.Result); Filter("ENTITY_TYPE", "entityType", q.EntityType?.ToUpperInvariant());
        Filter("ENTITY_ID", "entityId", q.EntityId); Filter("CORRELATION_ID", "correlation", q.CorrelationId);
        if (q.Cursor is not null)
        {
            var (time, id) = DecodeCursor(q.Cursor);
            filters.Add("(OCCURRED_UTC<@cursorTime OR (OCCURRED_UTC=@cursorTime AND ID<@cursorId))");
            p.Add("cursorTime", time); p.Add("cursorId", id);
        }
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = (await conn.QueryAsync<AuditRow>(new CommandDefinition(
            $"SELECT FIRST {q.Take + 1} {Columns} FROM AUDITORIA_EVENTO WHERE {string.Join(" AND ", filters)} ORDER BY OCCURRED_UTC DESC,ID DESC", p, cancellationToken: ct))).ToList();
        var page = rows.Take(q.Take).ToArray();
        return new(page.Select(r => r.Event()).ToArray(), rows.Count > q.Take ? EncodeCursor(page[^1].OccurredAtUtc, page[^1].Id) : null);
    }

    public async Task<AuditDetail?> GetAsync(long id, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<AuditRow>(new CommandDefinition(
            $"SELECT {Columns},REQUEST_METHOD AS RequestMethod,REQUEST_PATH AS RequestPath,IP_ADDRESS AS IpAddress,REASON_CODE AS ReasonCode FROM AUDITORIA_EVENTO WHERE ID=@id", new { id }, cancellationToken: ct));
        if (row is null) return null;
        var changes = await conn.QueryAsync<ChangeRow>(new CommandDefinition("""
            SELECT FIELD_NAME AS Field,VALUE_TYPE AS ValueType,BEFORE_VALUE AS BeforeValue,AFTER_VALUE AS AfterValue
            FROM AUDITORIA_ALTERACAO WHERE EVENT_ID=@id ORDER BY ID
            """, new { id }, cancellationToken: ct));
        return row.Detail(changes.Select(c => new AuditChange(c.Field, AuditCatalog.SafeValue(c.BeforeValue, c.ValueType), AuditCatalog.SafeValue(c.AfterValue, c.ValueType))).ToArray());
    }
    private sealed class ChangeRow
    {
        public string Field { get; set; } = "";
        public string ValueType { get; set; } = "string";
        public string? BeforeValue { get; set; }
        public string? AfterValue { get; set; }
    }
    private static void Check(string? value, string[] allowed)
    { if (value is not null && !allowed.Contains(value, StringComparer.Ordinal)) throw new ArgumentException("Filtro inválido."); }
    private static string EncodeCursor(DateTime time, long id) => Convert.ToBase64String(Encoding.ASCII.GetBytes($"{time.Ticks}:{id}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static (DateTime, long) DecodeCursor(string cursor)
    {
        try
        {
            if (cursor.Length > 100) throw new FormatException();
            var raw = cursor.Replace('-', '+').Replace('_', '/');
            raw = raw.PadRight((raw.Length + 3) / 4 * 4, '=');
            var parts = Encoding.ASCII.GetString(Convert.FromBase64String(raw)).Split(':');
            if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
                !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0) throw new FormatException();
            return (new DateTime(ticks, DateTimeKind.Utc), id);
        }
        catch (Exception e) when (e is FormatException or ArgumentException) { throw new ArgumentException("Cursor inválido."); }
    }
}
