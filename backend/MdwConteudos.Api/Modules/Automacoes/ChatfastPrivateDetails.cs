using System.Text.Json;
using System.Text.Json.Serialization;

namespace MdwConteudos.Api.Modules.Automacoes;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChatfastPrivateTable(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string?>> Rows);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChatfastPrivateCase(string Client, string Technicians, string Team, string Status, string Description, string Dates, int? Occurrences, string? Severity = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChatfastPrivateSource(string File, string Sha256);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChatfastPrivateTracker(int Count, string UpdatedAt, string Note);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChatfastPrivateDetails(IReadOnlyList<ChatfastPrivateTable> Tables, IReadOnlyList<ChatfastPrivateCase> Cases,
    IReadOnlyList<string> Recommendations, IReadOnlyList<string> PositiveExamples, IReadOnlyList<ChatfastPrivateSource> Sources, ChatfastPrivateTracker? Tracker)
{
    public static void Validate(JsonElement? value)
    {
        if (value is null || value.Value.ValueKind == JsonValueKind.Null) return;
        try
        {
            var d = value.Value.Deserialize<ChatfastPrivateDetails>(ChatfastContract.Json);
            bool Text(string? s, int length) => s is not null && s.Length <= length;
            if (d is null || d.Tables is not { Count: <= 20 } || d.Cases is not { Count: <= 500 } || d.Recommendations is not { Count: <= 100 } || d.PositiveExamples is not { Count: <= 100 } || d.Sources is not { Count: <= 10 }) throw new JsonException();
            if (d.Tables.Any(t => t is null || !Text(t.Title, 200) || t.Columns is not { Count: > 0 and <= 20 } || t.Rows is not { Count: <= 500 } || t.Columns.Any(c => !Text(c, 200)) || t.Rows.Any(r => r is null || r.Count != t.Columns.Count || r.Any(c => c?.Length > 8000))) ||
                d.Cases.Any(c => c is null || !Text(c.Client, 500) || !Text(c.Technicians, 1000) || !Text(c.Team, 200) || !Text(c.Status, 1000) || !Text(c.Description, 8000) || !Text(c.Dates, 200) || c.Occurrences < 0 || c.Severity?.Length > 100) ||
                d.Recommendations.Concat(d.PositiveExamples).Any(s => !Text(s, 8000)) || d.Sources.Any(s => s is null || !Text(s.File, 255) || !System.Text.RegularExpressions.Regex.IsMatch(s.Sha256 ?? "", "^[a-f0-9]{64}$")) ||
                d.Tracker is { } tracker && (tracker.Count < 0 || !Text(tracker.UpdatedAt, 200) || !Text(tracker.Note, 1000))) throw new JsonException();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        { throw new ChatfastException(400, "details inválido para o contrato 1.1. Use tabelas e detalhes privados documentados."); }
    }
}
