using System.IO.Compression;
using System.Text;
using System.Text.Json;
using MdwConteudos.Api.Modules.Automacoes;

namespace ConversorHtml.Tests;

public sealed class ChatfastTests
{
    internal static ChatfastResponseRequest Event(string data = "{\"tratado_abc12345\":\"1\",\"obs\":\"Conferido\",\"versao\":1}", string run = "run-test") => new("1.0", "flow-test", run,
        JsonSerializer.Deserialize<JsonElement>("{\"responseTime\":\"2026-11-20T12:00:00Z\",\"responder\":{\"userPrincipalName\":\"test@example.invalid\"},\"data\":" + data + "}"));
    internal static ChatfastReportRequest Report(string batch = "batch-test", bool complete = true, string? privateText = "CLIENTE PRIVADO") => new("1.0", batch, "diario",
        new(2026, 11, 18, 3, 0, 0, TimeSpan.Zero), new(2026, 11, 19, 3, 0, 0, TimeSpan.Zero), new(2026, 11, 19, 20, 0, 0, TimeSpan.Zero), complete,
        new(10, null, 2, 1, 1), JsonSerializer.SerializeToElement(new { client = privateText, sessionId = "NEVER PUBLIC", html = "<script>private</script>" }));
    [Fact] public void EventIdentityAndCanonicalHashSurviveRetryAndPropertyReorder()
    {
        var first = ChatfastContract.Parse(Event());
        var retry = ChatfastContract.Parse(Event("{\"versao\":1,\"obs\":\"Conferido\",\"tratado_abc12345\":\"1\"}"));
        Assert.Equal(first.EventKey, retry.EventKey); Assert.Equal(first.Hash, retry.Hash);
        var changed = ChatfastContract.Parse(Event("{\"tratado_abc12345\":\"0\"}"));
        Assert.Equal(first.EventKey, changed.EventKey); Assert.NotEqual(first.Hash, changed.Hash);
        Assert.True(first.Items.Single().Treated); Assert.Equal("1", first.CardVersion); Assert.Null(first.CaseId);
        Assert.Equal("flow-test", first.FlowId); Assert.Equal("run-test", first.RunId);
    }
    [Theory]
    [InlineData("{\"tratado_a\":\"yes\"}")]
    [InlineData("{\"tratado_a\":1,\"tratado_a\":0}")]
    [InlineData("{\"obs\":1}")]
    public void BadRepliesAreRejected(string data) => Assert.Equal(400, Assert.Throws<ChatfastException>(() => ChatfastContract.Parse(Event(data))).Status);
    [Fact] public void PublicProjectionCannotContainPrivateDataOrMakeUnknownIntoZero()
    {
        var report = Report(); ChatfastContract.Validate(report);
        var view = ChatfastContract.Public(report, report.GeneratedAt.AddHours(49));
        var json = JsonSerializer.Serialize(view, ChatfastContract.Json);
        foreach (var sensitive in new[] { "CLIENTE", "sessionId", "html", "batchId", "details", "private" }) Assert.DoesNotContain(sensitive, json);
        Assert.Null(view.Metrics.Waiting); Assert.True(view.Stale); Assert.Contains("\"waiting\":null", json);
    }
    [Fact] public void InvalidReportsAndNaiveReplyDatesAreRejected()
    {
        Assert.Throws<ChatfastException>(() => ChatfastContract.Validate(Report() with { Metrics = new(-1, 0, 0, 0, 0) }));
        Assert.Throws<ChatfastException>(() => ChatfastContract.Validate(Report() with { SchemaVersion = "2" }));
        var e = Event(); Assert.Throws<ChatfastException>(() => ChatfastContract.Parse(e with { Response = JsonSerializer.Deserialize<JsonElement>(e.Response.GetRawText().Replace("12:00:00Z", "12:00:00")) }));
        var raw = JsonSerializer.Serialize(Report(), ChatfastContract.Json).Replace("2026-11-18T03:00:00+00:00", "2026-11-18T03:00:00");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ChatfastReportRequest>(raw, ChatfastContract.Json));
    }
    [Fact] public void CredentialsRequireStrongDistinctProducerKeys()
    {
        Assert.False(ChatfastContract.KeyMatches("", "")); Assert.False(ChatfastContract.KeyMatches(null, null));
        Assert.True(ChatfastContract.KeyMatches(new('a', 64), new('a', 64))); Assert.False(ChatfastContract.KeyMatches(new('a', 64), new('b', 64)));
    }
    [Fact] public void VersionedSqlMirrorsTheCanonicalDefinitions()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs/sql/scripts/add-chatfast-20261120.sql"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var sql = File.ReadAllText(Path.Combine(dir.FullName, "docs/sql/scripts/add-chatfast-20261120.sql"));
        foreach (var (name, ddl) in ChatfastSchema.Definitions) Assert.Contains($"CREATE TABLE {name} ({ddl.Replace("'", "''")})", sql);
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(sql, @"(?:CONSTRAINT|INDEX) ([A-Z0-9_]+)")) Assert.True(m.Groups[1].Value.Length <= 31);
    }
    [Fact] public void LegacyRowsPreserveUnknownFieldsAndConvertBrazilTime()
    {
        var r = ChatfastContract.Legacy(new(2, "20/11/2026", "08h", "Teste", "{\"tratado_a\":\"1\"}", "Obs", "test@example.invalid", "09:30"), new('a', 64));
        Assert.Equal(new DateTime(2026, 11, 20, 12, 30, 0, DateTimeKind.Utc), r.RespondedAtUtc); Assert.Null(r.CaseId); Assert.Null(r.MessageId); Assert.Equal("excel", r.Source);
    }
    [Fact] public void XlsxHashComesFromActualFileAndFormulaIsRejected()
    {
        var bytes = Spreadsheet(); var r = ChatfastSpreadsheet.Read(bytes, "Tratados.xlsx", false);
        Assert.Single(r.Rows); Assert.Equal(2, r.Rows[0].Row); Assert.Equal("Conferido", r.Rows[0].Observacao); Assert.Equal(64, r.FileSha256.Length);
        Assert.Equal(r.FileSha256, ChatfastSpreadsheet.Read(bytes, "Outro.xlsx", true).FileSha256);
        Assert.Throws<ChatfastException>(() => ChatfastSpreadsheet.Read(Spreadsheet(true), "Tratados.xlsx", false));
        Assert.Throws<ChatfastException>(() => ChatfastSpreadsheet.Read([1, 2, 3], "Tratados.xlsx", false));
    }
    internal static byte[] Spreadsheet(bool formula = false)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open(), Encoding.UTF8);
            string Row(int n, string[] values) => $"<row r=\"{n}\">" + string.Concat(values.Select((v, i) => $"<c r=\"{(char)('A' + i)}{n}\" t=\"inlineStr\">{(formula && n == 2 && i == 0 ? "<f>NOW()</f>" : "")}<is><t>{System.Security.SecurityElement.Escape(v)}</t></is></c>")) + "</row>";
            writer.Write("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>" +
                Row(1, ["data", "rodada", "gestor", "itens_tratados", "observacao", "respondido_por", "hora"]) +
                Row(2, ["20/11/2026", "08h", "Teste", "{\"tratado_a\":\"1\"}", "Conferido", "test@example.invalid", "09:30"]) + "</sheetData></worksheet>");
        }
        return stream.ToArray();
    }
}
