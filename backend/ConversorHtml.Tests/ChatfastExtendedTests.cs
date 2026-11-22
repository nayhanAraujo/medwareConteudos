using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Modules.Automacoes;

namespace ConversorHtml.Tests;

public sealed class ChatfastExtendedTests
{
    internal static ChatfastReportRequest Report() => ChatfastTests.Report() with {
        SchemaVersion = "1.1", Summary = new([new("handledConversations", 100), new("deviationsPer100", 12.5m, 14m)], [new(new(2026,11,18), 14)], [new(new(2026,11,9), new(2026,11,13), 14m)], ["uniqueTechnicianConversation"], ["snapshotNotLive"]),
        Details = JsonSerializer.SerializeToElement(new { tables = new[] { new { title = "Tabela privada", columns = new[] { "Nome" }, rows = new[] { new[] { "Pessoa reservada" } } } }, cases = Array.Empty<object>(), recommendations = new[] { "Recomendação interna" }, positiveExamples = Array.Empty<string>(), sources = Array.Empty<object>(), tracker = new { count = 0, updatedAt = "18/11", note = "Snapshot" } })
    };
    [Fact] public void PublicSummaryIsWhitelistedAndPrivateTablesNeverLeak()
    {
        var r = Report(); ChatfastContract.Validate(r);
        var json = JsonSerializer.Serialize(ChatfastContract.Public(r, r.GeneratedAt), ChatfastContract.Json);
        Assert.Contains("deviationsPer100", json); Assert.DoesNotContain("Pessoa", json); Assert.DoesNotContain("Recomendação", json); Assert.DoesNotContain("details", json); Assert.DoesNotContain("Tabela privada", json);
    }
    [Fact] public void Version10PreservesSerializedShapeAndRejectsExtendedFields()
    {
        Assert.DoesNotContain("summary", JsonSerializer.Serialize(ChatfastTests.Report(), ChatfastContract.Json));
        Assert.Throws<ChatfastException>(() => ChatfastContract.Validate(Report() with { SchemaVersion = "1.0" }));
        Assert.Throws<ChatfastException>(() => ChatfastContract.Validate(Report() with { Summary = null }));
    }
    [Theory] [InlineData("nomeDoTecnico")] [InlineData("<script>")] [InlineData("https://private.invalid")]
    public void PublicCodeCannotCarryPrivateText(string code)
    { var r = Report(); Assert.Throws<ChatfastException>(() => ChatfastContract.Validate(r with { Summary = r.Summary! with { Statistics = [new(code, 1)] } })); }
    [Fact] public void SummaryRejectsFractionsInCountsDuplicatesNegativeAndForeignDates()
    {
        var r = Report();
        foreach (var stats in new IReadOnlyList<ChatfastStatistic>[] { [new("events", 1.5m)], [new("events", -1)], [new("events", 1),new("events", 2)], [null!] })
            Assert.Throws<ChatfastException>(() => ChatfastContract.Validate(r with { Summary = r.Summary! with { Statistics = stats } }));
        Assert.Throws<ChatfastException>(() => ChatfastContract.Validate(r with { Summary = r.Summary! with { DailyVolume = [new(new(2026,1,1), 3)] } }));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ChatfastSummary>("{\"statistics\":[],\"dailyVolume\":[],\"trend\":[],\"methodCodes\":[],\"limitationCodes\":[],\"client\":\"SECRET\"}", ChatfastContract.Json));
    }
    [Fact] public void PrivateSchemaRejectsMalformedTablesButRetainsLegacyDetails()
    {
        var r = Report() with { Details = JsonSerializer.SerializeToElement(new { tables = new[] { new { title = "x", columns = new[] { "a" }, rows = new[] { new[] { "one", "two" } } } }, cases = Array.Empty<object>(), recommendations = Array.Empty<string>(), positiveExamples = Array.Empty<string>(), sources = Array.Empty<object>() }) };
        Assert.Throws<ChatfastException>(() => ChatfastContract.Validate(r)); ChatfastContract.Validate(ChatfastTests.Report());
    }
    [Fact] public async Task DemonstrationIsDisabledInProductionEvenWhenDirectoryIsConfigured()
    {
        var options = Options.Create(new ChatfastOptions { DemoDirectory = Path.GetTempPath() });
        Assert.Equal(404, (await Assert.ThrowsAsync<ChatfastException>(() => ChatfastDemo.Read("diario", new Environment("Production"), options, default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<ChatfastException>(() => ChatfastDemo.Read("diario", new Environment("Homologacao"), options, default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<ChatfastException>(() => ChatfastDemo.Read("../secret", new Environment("Development"), options, default))).Status);
    }
    sealed class Environment(string name) : IHostEnvironment
    { public string EnvironmentName { get; set; } = name; public string ApplicationName { get; set; } = "test"; public string ContentRootPath { get; set; } = ""; public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider(); }
}
