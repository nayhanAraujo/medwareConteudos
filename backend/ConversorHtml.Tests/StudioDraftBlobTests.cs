using System.Data;
using System.Text.Json;
using ConversorHtml.Application.Services;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.FormulasModelos;
using MdwConteudos.Api.Modules.StudioDrafts;

namespace ConversorHtml.Tests;

public class StudioDraftBlobTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4000)]
    [InlineData(4001)]
    [InlineData(55000)]
    [InlineData(250000)]
    public void JsonParametersAlwaysUseTextBlobWithoutNegativeSize(int length)
    {
        var value = new string('x', length);
        using var command = new FbCommand();
        new FirebirdTextBlob(value).AddParameter(command, "documento");
        var parameter = command.Parameters["documento"];
        Assert.Equal(FbDbType.Text, parameter.FbDbType);
        Assert.Equal(DbType.String, parameter.DbType);
        Assert.True(parameter.Size >= 0);
        Assert.Equal(value, parameter.Value);
    }

    [Fact]
    public void NullTextBlobKeepsDatabaseNull()
    {
        using var command = new FbCommand();
        new FirebirdTextBlob(null).AddParameter(command, "documento");
        Assert.Equal(FbDbType.Text, command.Parameters[0].FbDbType);
        Assert.Equal(DBNull.Value, command.Parameters[0].Value);
    }

    // This fact can only create a new temporary embedded database; no configured application DB is used.
    [FirebirdPublicationFact]
    public async Task ImportedDocumentAndHistorySurviveCreateEditRetryAndRollback()
    {
        var directory = Path.Combine(Path.GetTempPath(), "studio-blob-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var cs = new FbConnectionStringBuilder { Database = Path.Combine(directory, "isolated.fdb"),
            ServerType = FbServerType.Embedded, ClientLibrary = Environment.GetEnvironmentVariable("PUBLICACAO_TEST_FBCLIENT"),
            UserID = "SYSDBA", Password = "isolated-test-only", Charset = "UTF8", Pooling = false }.ToString();
        FbConnection.CreateDatabase(cs, overwrite: false);
        var factory = new Factory(cs);
        await using (var c = await factory.OpenConnectionAsync())
        {
            foreach (var sql in new[] {
                "CREATE TABLE USUARIO(CODUSUARIO INTEGER NOT NULL,CONSTRAINT PK_USUARIO PRIMARY KEY(CODUSUARIO))",
                "CREATE TABLE MODELOMODOTEXTO(CODMODELO INTEGER NOT NULL,CONSTRAINT PK_MODELOMODOTEXTO PRIMARY KEY(CODMODELO))",
                "CREATE TABLE SECAOMODOTEXTO(CODSECAO INTEGER NOT NULL,CODMODELO INTEGER NOT NULL,CONSTRAINT PK_SECAOMODOTEXTO PRIMARY KEY(CODSECAO))",
                "CREATE TABLE SECAO_VARIAVEL(CODSECAO INTEGER NOT NULL,CODVARIAVEL INTEGER NOT NULL,CONSTRAINT PK_SECAO_VARIAVEL PRIMARY KEY(CODSECAO,CODVARIAVEL))",
                "INSERT INTO USUARIO VALUES(1)" }) await c.ExecuteAsync(sql);
        }
        await StudioDraftSchema.MigrateAsync(factory, default);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Studio", "ecocardiograma.json"));
        var document = StudioJsonImport.Parse(bytes, "Ecodopplercardiograma.json", []);
        document.ImportedJson!.Root["unicode"] = "Átrio esquerdo · m² · ≤ · " + new string('ç', 50000);
        Assert.True(JsonSerializer.Serialize(document, StudioDocumentEditor.Json).Length > 32767);
        StudioDraftService Service() => new(factory, new FormulasModelosService(factory), new InMemoryVoiceSessionStore(), Options.Create(new StudioDraftOptions {Enabled = true}));
        var workId = Guid.NewGuid().ToString();
        var created = await Service().Create(1, new(document.Nome), default, document, workId);
        var recovered = await Service().Get(workId, 1, default);
        Assert.Equal(StudioJsonImport.Export(document), StudioJsonImport.Export(recovered.Document));
        Assert.Equal(StudioJsonImport.Export(document), StudioJsonImport.Export(JsonSerializer.Deserialize<StudioDocument>((await Service().History(workId, 1, default)).Single().Document, StudioDocumentEditor.Json)!));
        var again = await Service().Create(1, new(document.Nome), default, document, workId);
        Assert.Equal(created.Id, again.Id);
        Assert.Single(await Service().History(workId, 1, default));
        var command = new DraftCommand(Guid.NewGuid().ToString(), 0, "rename", JsonSerializer.SerializeToElement(new {name = "Importação revisada"}));
        var edited = await Service().Command(workId, 1, command, default);
        Assert.Equal(1L, edited.Revision);
        var reopened = await Service().Get(workId, 1, default);
        Assert.Equal(edited.Revision, reopened.Revision);
        Assert.Equal("Importação revisada", reopened.Document.Nome);
        Assert.Equal(1L, (await Service().Command(workId, 1, command, default)).Revision);
        Assert.Equal(2, (await Service().History(workId, 1, default)).Count);
        Assert.Equal(StudioJsonImport.Export(document), StudioJsonImport.Export((await Service().Get(workId, 1, default)).Document));
        Assert.Equal(404, (await Assert.ThrowsAsync<StudioException>(() => Service().Get(workId, 2, default))).Status);
        await using (var c = await factory.OpenConnectionAsync())
            await c.ExecuteAsync("ALTER TABLE STUDIOREVISAO ADD CONSTRAINT CK_SV_TESTE CHECK(NUMERO < 2)");
        await Assert.ThrowsAsync<FbException>(() => Service().Command(workId, 1, command with {CommandId = Guid.NewGuid().ToString(), ExpectedRevision = 1, Payload = JsonSerializer.SerializeToElement(new {name = "Não deve gravar"})}, default));
        var afterFailure = await Service().Get(workId, 1, default);
        Assert.Equal(1L, afterFailure.Revision);
        Assert.Equal("Importação revisada", afterFailure.Document.Nome);
        Assert.Equal(2, (await Service().History(workId, 1, default)).Count);
    }

    private sealed class Factory(string cs) : IFirebirdConnectionFactory
    {
        public async Task<FbConnection> OpenConnectionAsync(CancellationToken ct = default)
        { var c = new FbConnection(cs); try { await c.OpenAsync(ct); return c; } catch { await c.DisposeAsync(); throw; } }
    }
}
