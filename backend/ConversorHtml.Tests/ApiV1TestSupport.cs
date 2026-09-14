using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using FirebirdSql.Data.FirebirdClient;
using MdwConteudos.Api.Infrastructure;

namespace ConversorHtml.Tests;

internal static class ApiV1TestSupport
{
    private static readonly Lazy<JsonElement> Fixture = new(ReadFixture);
    public static JsonElement Reference => Fixture.Value;

    private static JsonElement ReadFixture() => ReadFixtureAtSource();

    // No csproj edits/content-copy dependency: fixtures live beside these tests.
    private static JsonElement ReadFixtureAtSource([CallerFilePath] string source = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(source)!, "Fixtures", "ApiV1", "python_reference.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }

    public static IEnumerable<object[]> Cases(string section) => Reference.GetProperty(section).EnumerateArray()
        .Select(item => new object[] { item.GetProperty("name").GetString()!, item.GetRawText() });

    public static void EqualJson(JsonNode? expected, JsonNode? actual) =>
        Assert.True(JsonNode.DeepEquals(expected, actual), $"Expected: {expected}\nActual: {actual}");

    public static dynamic Row(JsonElement values)
    {
        string[] columns = ["VARIAVEL", "SEXO", "VALORMIN", "VALORMAX", "IDADE_MIN", "IDADE_MAX",
            "PAGINA_REFERENCIA", "CLASSIFICACAO", "REFERENCIA_TITULO", "REFERENCIA_ANO"];
        IDictionary<string, object?> row = new ExpandoObject();
        for (var i = 0; i < columns.Length; i++)
            row[columns[i]] = Value(values[i]);
        return (ExpandoObject)row;
    }

    private static object? Value(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt32(out var n) => n,
        JsonValueKind.Number => value.GetDouble(),
        _ => throw new ArgumentException("Unexpected fixture value.")
    };
}

internal sealed class ApiV1FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class ApiV1NoDatabase : IFirebirdConnectionFactory
{
    public Task<FbConnection> OpenConnectionAsync(CancellationToken ct = default) =>
        throw new InvalidOperationException("Contract tests must never access a database.");
}
