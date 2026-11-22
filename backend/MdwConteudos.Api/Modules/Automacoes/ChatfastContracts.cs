using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MdwConteudos.Api.Modules.Automacoes;

public sealed class ChatfastOptions
{
    public bool Enabled { get; set; }
    public bool MigrateWithDatabase { get; set; }
    public string ResponsesKey { get; set; } = "";
    public string ReportsKey { get; set; } = "";
    public string DemoDirectory { get; set; } = "";
}
public sealed class ChatfastException(int status, string message) : Exception(message) { public int Status { get; } = status; }
public sealed record ChatfastResponseRequest(string SchemaVersion, string FlowId, string RunId, JsonElement Response);
public sealed record ChatfastItem(string ExternalId, bool Treated);
public sealed record ChatfastResponse(string EventKey, string Hash, DateTime? RespondedAtUtc, string? MessageId,
    string? Round, string? Manager, string? Responder, string? Observation, string? Action, string? CaseId,
    string? CardVersion, string Source, IReadOnlyList<ChatfastItem> Items, string Document, string? FlowId = null, string? RunId = null);
public sealed record ChatfastMetrics(int? Analyzed, int? Waiting, int? Deviations, int? Critical, int? Treated);
public sealed record ChatfastReportRequest(string SchemaVersion, string BatchId, string Type,
    [property: JsonConverter(typeof(ChatfastOffsetConverter))] DateTimeOffset PeriodStart,
    [property: JsonConverter(typeof(ChatfastOffsetConverter))] DateTimeOffset PeriodEnd,
    [property: JsonConverter(typeof(ChatfastOffsetConverter))] DateTimeOffset GeneratedAt, bool Complete, ChatfastMetrics Metrics, JsonElement? Details,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ChatfastSummary? Summary = null);
public sealed class ChatfastOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (text is null || !(text.EndsWith('Z') || System.Text.RegularExpressions.Regex.IsMatch(text, @"[+-]\d{2}:\d{2}$")) ||
            !DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
            throw new JsonException("Informe um instante ISO 8601 com fuso explícito.");
        return date;
    }
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}
// Public contract deliberately has no free text, names, teams, IDs or private document.
public sealed record ChatfastPublicReport(string Type, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd,
    DateTimeOffset GeneratedAt, bool Complete, ChatfastMetrics Metrics, bool Stale,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ChatfastSummary? Summary = null);
public sealed record ChatfastImportRow(int Row, string? Data, string? Rodada, string? Gestor, string? ItensTratados,
    string? Observacao, string? RespondidoPor, string? Hora);
public sealed record ChatfastImportRequest(string FileSha256, string FileName, IReadOnlyList<ChatfastImportRow> Rows, bool Confirm = false);

public static class ChatfastContract
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Sha(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static bool KeyMatches(string? configured, string? supplied) => !string.IsNullOrWhiteSpace(configured) && configured.Length >= 32 && supplied is { Length: >= 32 and <= 512 } &&
        CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(configured)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));
    public static string Required(string? value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max || value.Any(char.IsControl)) throw new ChatfastException(400, $"Campo inválido: {field}.");
        return value;
    }
    public static string? Text(JsonElement obj, string key, int max)
    {
        if (!obj.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        if (v.ValueKind != JsonValueKind.String || v.GetString()!.Length > max) throw new ChatfastException(400, $"Campo inválido: {key}.");
        return v.GetString();
    }
    public static ChatfastResponse Parse(ChatfastResponseRequest request)
    {
        if (request.SchemaVersion != "1.0") throw new ChatfastException(400, "Versão de contrato não suportada.");
        Required(request.FlowId, 200, "flowId"); Required(request.RunId, 200, "runId");
        var r = request.Response;
        if (r.ValueKind != JsonValueKind.Object || !r.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            throw new ChatfastException(400, "Resposta do Teams sem data válido.");
        var time = Required(Text(r, "responseTime", 80), 80, "responseTime");
        if (!DateTimeOffset.TryParse(time, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date) ||
            !(time.EndsWith('Z') || System.Text.RegularExpressions.Regex.IsMatch(time, @"[+-]\d{2}:\d{2}$")))
            throw new ChatfastException(400, "responseTime precisa informar o fuso.");
        string? responder = null;
        if (r.TryGetProperty("responder", out var person) && person.ValueKind == JsonValueKind.Object)
            responder = Text(person, "userPrincipalName", 320) ?? Text(person, "email", 320);
        var doc = Canonical(r);
        return new(Sha($"powerautomate\n{request.FlowId}\n{request.RunId}"), Sha(doc), date.UtcDateTime,
            Text(r, "messageId", 500), Text(data, "rodada", 100), Text(data, "gestor", 255), responder,
            Text(data, "obs", 8000), Text(data, "acao", 100), Text(data, "caso_id", 200), Version(data),
            "powerautomate", Items(data), doc, request.FlowId, request.RunId);
    }
    public static string Canonical(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(value, writer);
        return Encoding.UTF8.GetString(stream.ToArray());
        static void Write(JsonElement v, Utf8JsonWriter w)
        {
            if (v.ValueKind == JsonValueKind.Object)
            {
                var props = v.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                if (props.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != props.Length)
                    throw new ChatfastException(400, "Propriedade JSON duplicada.");
                w.WriteStartObject(); foreach (var p in props) { w.WritePropertyName(p.Name); Write(p.Value, w); } w.WriteEndObject();
            }
            else if (v.ValueKind == JsonValueKind.Array) { w.WriteStartArray(); foreach (var item in v.EnumerateArray()) Write(item, w); w.WriteEndArray(); }
            else v.WriteTo(w);
        }
    }
    static string? Version(JsonElement data) => data.TryGetProperty("versao", out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)
        ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : Text(data, "versao", 80);
    public static IReadOnlyList<ChatfastItem> Items(JsonElement data)
    {
        var items = new List<ChatfastItem>();
        foreach (var p in data.EnumerateObject().Where(p => p.Name.StartsWith("tratado_", StringComparison.Ordinal)))
        {
            Required(p.Name, 120, "item");
            var value = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText();
            if (value is not ("1" or "0" or "true" or "false")) throw new ChatfastException(400, "Marcação de tratado inválida.");
            if (items.Any(i => i.ExternalId == p.Name)) throw new ChatfastException(400, "Item duplicado.");
            items.Add(new(p.Name, value is "1" or "true"));
        }
        if (items.Count > 500) throw new ChatfastException(400, "Excesso de itens.");
        return items;
    }
    public static void Validate(ChatfastReportRequest r)
    {
        if (r.SchemaVersion is not ("1.0" or "1.1") || r.Type is not ("diario" or "semanal") || r.Metrics is null ||
            r.PeriodEnd <= r.PeriodStart || r.PeriodEnd - r.PeriodStart > TimeSpan.FromHours(r.Type == "diario" ? 25 : 192) ||
            r.GeneratedAt < r.PeriodEnd || r.GeneratedAt > DateTimeOffset.UtcNow.AddMinutes(10)) throw new ChatfastException(400, "Relatório inválido.");
        Required(r.BatchId, 200, "batchId");
        if (new[] { r.Metrics.Analyzed, r.Metrics.Waiting, r.Metrics.Deviations, r.Metrics.Critical, r.Metrics.Treated }.Any(x => x < 0))
            throw new ChatfastException(400, "Indicadores negativos não são permitidos.");
        if (r.SchemaVersion == "1.0" && r.Summary is not null || r.SchemaVersion == "1.1" && r.Summary is null)
            throw new ChatfastException(400, "summary requer contrato 1.1.");
        if (r.Summary is not null) ChatfastSummaryContract.Validate(r.Summary, r);
        if (r.SchemaVersion == "1.1") ChatfastPrivateDetails.Validate(r.Details);
    }
    public static ChatfastPublicReport Public(ChatfastReportRequest r, DateTimeOffset now)
    {
        if (r.Summary is not null) ChatfastSummaryContract.Validate(r.Summary, r);
        return new(r.Type, r.PeriodStart, r.PeriodEnd, r.GeneratedAt, r.Complete, r.Metrics,
            now - r.GeneratedAt > TimeSpan.FromHours(r.Type == "diario" ? 48 : 192), r.Summary);
    }
    public static ChatfastResponse Legacy(ChatfastImportRow row, string fileHash)
    {
        if (row.Row < 2) throw new ChatfastException(400, "Número de linha inválido.");
        foreach (var value in new[] { row.Rodada, row.Gestor, row.RespondidoPor, row.Observacao })
            if (value?.Length > 8000) throw new ChatfastException(400, "Texto legado excede o limite.");
        if (!DateTime.TryParseExact($"{row.Data} {row.Hora}", "dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var local)) throw new ChatfastException(400, "Data/hora legada inválida.");
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        if (tz.IsInvalidTime(local) || tz.IsAmbiguousTime(local)) throw new ChatfastException(400, "Horário legado ambíguo.");
        using var parsed = JsonDocument.Parse(row.ItensTratados ?? "{}");
        if (parsed.RootElement.ValueKind != JsonValueKind.Object) throw new ChatfastException(400, "itens_tratados precisa ser objeto JSON.");
        var data = parsed.RootElement;
        var document = JsonSerializer.Serialize(row, Json);
        return new(Sha($"excel\n{fileHash}\n{row.Row}"), Sha(document), TimeZoneInfo.ConvertTimeToUtc(local, tz), null,
            row.Rodada, row.Gestor, row.RespondidoPor, row.Observacao, Text(data, "acao", 100), Text(data, "caso_id", 200),
            Version(data), "excel", Items(data), document);
    }
}
