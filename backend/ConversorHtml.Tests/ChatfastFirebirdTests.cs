using System.Text.Json;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Automacoes;

namespace ConversorHtml.Tests;

// The only possible target is a freshly created random temporary embedded database.
public sealed partial class ChatfastFirebirdTests : IAsyncLifetime
{
    string cs = ""; Factory factory = null!;
    ChatfastService Service => new(factory, Options.Create(new ChatfastOptions { Enabled = true, ResponsesKey = new('a', 64), ReportsKey = new('b', 64) }));
    [FirebirdPublicationFact] public async Task ExtendedSnapshotSurvivesPersistenceWithoutPublicPrivateDataLeak()
    {
        var report = ChatfastExtendedTests.Report(); await Service.Report(report, default); await Service.Report(report, default);
        var result = await Service.Public("diario", null, default); Assert.Equal(2, result!.Summary!.Statistics.Count);
        var json = JsonSerializer.Serialize(result, ChatfastContract.Json); Assert.DoesNotContain("Pessoa reservada", json);
        await using var c = await factory.OpenConnectionAsync(); Assert.Equal(1, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CHATFASTRELATORIO"));
        Assert.Contains("Pessoa reservada", await c.ExecuteScalarAsync<string>("SELECT DOCUMENTO FROM CHATFASTRELATORIO"));
    }
    public async Task InitializeAsync()
    {
        var library = Environment.GetEnvironmentVariable("PUBLICACAO_TEST_FBCLIENT"); if (string.IsNullOrEmpty(library)) return;
        cs = new FbConnectionStringBuilder { Database = Path.Combine(Path.GetTempPath(), $"chatfast-test-{Guid.NewGuid():N}.fdb"), ServerType = FbServerType.Embedded, ClientLibrary = library, UserID = "SYSDBA", Password = "masterkey", Charset = "UTF8", Pooling = false }.ToString();
        FbConnection.CreateDatabase(cs); factory = new(cs);
        await using var c = await factory.OpenConnectionAsync();
        await c.ExecuteAsync("CREATE TABLE USUARIO(CODUSUARIO INTEGER CONSTRAINT PK_USUARIO PRIMARY KEY,PERFIL VARCHAR(20),STATUS INTEGER)");
        await c.ExecuteAsync("INSERT INTO USUARIO VALUES(1,'admin',-1)");
        await ChatfastSchema.MigrateAsync(factory);
    }
    public Task DisposeAsync() { if (cs.Length > 0) FbConnection.DropDatabase(cs); return Task.CompletedTask; }
    [FirebirdPublicationFact] public async Task MigrationIsRepeatableAndPreservesEventsAndForeignKeys()
    {
        await Service.Receive(ChatfastContract.Parse(ChatfastTests.Event()), default); await ChatfastSchema.MigrateAsync(factory); await ChatfastSchema.MigrateAsync(factory);
        await using var c = await factory.OpenConnectionAsync();
        Assert.Equal(1, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CHATFASTRESPOSTA"));
        Assert.Equal(1, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CHATFASTRESPOSTAITEM"));
        await Assert.ThrowsAsync<FbException>(() => c.ExecuteAsync("INSERT INTO CHATFASTRESPOSTAITEM VALUES(999,1,'tratado_a',1)"));
        await Assert.ThrowsAsync<FbException>(() => c.ExecuteAsync("UPDATE CHATFASTRESPOSTAITEM SET TRATADO=2"));
        Assert.Equal(0, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$RELATION_CONSTRAINTS WHERE RDB$RELATION_NAME STARTING WITH 'CHATFAST' AND RDB$CONSTRAINT_TYPE<>'NOT NULL' AND RDB$CONSTRAINT_NAME STARTING WITH 'INTEG_'"));
    }
    [FirebirdPublicationFact] public async Task ReplaysAreIdempotentAndChangedPayloadConflicts()
    {
        var reply = ChatfastContract.Parse(ChatfastTests.Event()); await Service.Receive(reply, default);
        var result = JsonSerializer.SerializeToElement(await Service.Receive(reply, default)); Assert.True(result.GetProperty("duplicate").GetBoolean());
        Assert.Equal(409, (await Assert.ThrowsAsync<ChatfastException>(() => Service.Receive(ChatfastContract.Parse(ChatfastTests.Event("{}")), default))).Status);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Service.Receive(ChatfastContract.Parse(ChatfastTests.Event(run: "concurrent")), default)));
        await using var c = await factory.OpenConnectionAsync(); Assert.Equal(2, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CHATFASTRESPOSTA"));
    }
    [FirebirdPublicationFact] public async Task PublicOnlyUsesNewestCompletePeriodAndSignalsIncompleteUpdate()
    {
        var first = ChatfastTests.Report(); await Service.Report(first, default);
        Assert.Null((await Service.Public("diario", null, default))!.Metrics.Waiting);
        await Service.Report(first with { BatchId = "incomplete", GeneratedAt = first.GeneratedAt.AddHours(1), Complete = false }, default);
        Assert.True((await Service.Public("diario", null, default))!.Stale);
        var older = first with { BatchId = "late-old", PeriodStart = first.PeriodStart.AddDays(-1), PeriodEnd = first.PeriodEnd.AddDays(-1), GeneratedAt = first.GeneratedAt.AddHours(2) };
        await Service.Report(older, default); Assert.Equal(first.PeriodEnd, (await Service.Public("diario", null, default))!.PeriodEnd);
        Assert.NotNull(await Service.Public("diario", new DateOnly(2026, 11, 18), default));
        Assert.Null(await Service.Public("diario", new DateOnly(2026, 11, 10), default));
        Assert.Equal(409, (await Assert.ThrowsAsync<ChatfastException>(() => Service.Report(first with { Metrics = new(99, 0, 0, 0, 0) }, default))).Status);
    }
    [FirebirdPublicationFact] public async Task ImportPreviewsWithoutWritingThenCommitsOnceWithProvenance()
    {
        var request = ChatfastSpreadsheet.Read(ChatfastTests.Spreadsheet(), "Tratados.xlsx", false);
        await Service.Import(request, 1, default); await using var c = await factory.OpenConnectionAsync();
        Assert.Equal(0, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CHATFASTRESPOSTA"));
        await Service.Import(request with { Confirm = true }, 1, default); await Service.Import(request with { Confirm = true }, 1, default);
        Assert.Equal(1, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CHATFASTRESPOSTA")); Assert.Equal(1, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CHATFASTIMPORTACAO"));
        Assert.Equal(2, await c.ExecuteScalarAsync<int>("SELECT LINHA FROM CHATFASTRESPOSTA"));
        Assert.Equal("excel", await c.ExecuteScalarAsync<string>("SELECT ORIGEM FROM CHATFASTRESPOSTA"));
        var bad = request with { FileSha256 = new('b', 64), Rows = [request.Rows[0] with { Data = "inválida" }], Confirm = true };
        await Assert.ThrowsAsync<ChatfastException>(() => Service.Import(bad, 1, default)); Assert.Equal(1, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CHATFASTIMPORTACAO"));
    }
    [FirebirdPublicationFact] public async Task IncompatibleSchemaAbortsWithoutDeletingExistingData()
    {
        await Service.Receive(ChatfastContract.Parse(ChatfastTests.Event()), default); await using var c = await factory.OpenConnectionAsync();
        await c.ExecuteAsync("ALTER TABLE CHATFASTRESPOSTA ALTER ORIGEM TYPE VARCHAR(40) CHARACTER SET ASCII");
        await Assert.ThrowsAsync<InvalidOperationException>(() => ChatfastSchema.MigrateAsync(factory));
        Assert.Equal(1, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CHATFASTRESPOSTA"));
    }
    [FirebirdPublicationFact] public async Task VersionedSqlInstallsCleanlyAndCanBeRepeatedBeforeCanonicalValidation()
    {
        await using var c = await factory.OpenConnectionAsync();
        await c.ExecuteAsync("DROP TABLE CHATFASTRESPOSTAITEM"); await c.ExecuteAsync("DROP TABLE CHATFASTRESPOSTA");
        await c.ExecuteAsync("DROP TABLE CHATFASTIMPORTACAO"); await c.ExecuteAsync("DROP TABLE CHATFASTRELATORIO");
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs/sql/scripts/add-chatfast-20261120.sql"))) dir = dir.Parent;
        Assert.NotNull(dir); var sql = File.ReadAllText(Path.Combine(dir.FullName, "docs/sql/scripts/add-chatfast-20261120.sql"));
        for (var i = 0; i < 2; i++)
            foreach (System.Text.RegularExpressions.Match block in System.Text.RegularExpressions.Regex.Matches(sql, @"EXECUTE BLOCK AS[\s\S]*?END\^"))
                await c.ExecuteAsync(block.Value.TrimEnd('^'));
        await ChatfastSchema.MigrateAsync(factory);
        await Service.Receive(ChatfastContract.Parse(ChatfastTests.Event()), default);
        Assert.Equal(1, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CHATFASTRESPOSTA"));
    }
    sealed class Factory(string cs) : IFirebirdConnectionFactory
    { public async Task<FbConnection> OpenConnectionAsync(CancellationToken ct = default) { var c = new FbConnection(cs); await c.OpenAsync(ct); return c; } }
}
