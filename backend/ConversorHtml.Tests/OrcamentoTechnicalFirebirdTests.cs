using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Orcamentos;
using MdwConteudos.Api.Modules.Permissions;

namespace ConversorHtml.Tests;

public sealed partial class OrcamentoFirebirdTests
{
    [FirebirdPublicationFact]
    public async Task OutstandingAnalysisBlocksWorkflowAndExplicitDiscardPreservesHistory()
    {
        await using (var c = await factory.OpenConnectionAsync()) { await c.ExecuteAsync("CREATE TABLE TIPOLINGUAGEM(CODLINGUAGEM INTEGER PRIMARY KEY,NOME VARCHAR(100))"); await c.ExecuteAsync("INSERT INTO TIPOLINGUAGEM VALUES(8,'HTML')"); }
        var options = new OrcamentoAiOptions { Enabled = true }; var fake = new TwoPhaseQuoteAi();
        await using var services = new ServiceCollection().AddSingleton<IFirebirdConnectionFactory>(factory).AddScoped<IPermissionService, PermissionService>().AddSingleton<IQuoteAiProvider>(fake).BuildServiceProvider();
        var worker = new OrcamentoAiWorker(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(options), NullLogger<OrcamentoAiWorker>.Instance);
        var id = await Service.Create(new(1, OrcamentoTests.Draft(OrcamentoTests.Item())), default); var quote = await Service.Get(id, default);
        var job = long.Parse(await Service.QueueAnalysis(id, new(quote.Header.Version, [], true, "Ajuste parcial"), options, default, true));
        quote = await Service.Get(id, default); Assert.True(quote.AnalysisGate.Processing);
        var blocked = await Assert.ThrowsAsync<OrcamentoException>(() => Service.Command(id, new(quote.Header.Version, "solicitar_aprovacao"), default)); Assert.Equal(409, blocked.Status);
        await worker.RunOne(default); quote = await Service.Get(id, default); Assert.True(quote.AnalysisGate.AwaitingClassification);
        await Assert.ThrowsAsync<OrcamentoException>(() => Service.Command(id, new(quote.Header.Version, "solicitar_aprovacao"), default));
        await Service.CancelAnalysis(id, job, default); quote = await Service.Get(id, default); Assert.False(quote.AnalysisGate.AwaitingClassification); Assert.False(quote.AnalysisGate.Processing);
        await using var verify = await factory.OpenConnectionAsync(); Assert.Equal("cancelada", await verify.ExecuteScalarAsync<string>("SELECT STATUS FROM ORCAMENTOANALISE WHERE CODORCAMENTOANALISE=@job", new { job }));
        Assert.NotNull(await verify.ExecuteScalarAsync<string>("SELECT RESULTADO FROM ORCAMENTOANALISE WHERE CODORCAMENTOANALISE=@job", new { job }));
        Assert.Equal("em_aprovacao", (await Act(id, "solicitar_aprovacao")).Header.Status);
    }
    [FirebirdPublicationFact]
    public async Task AdditiveMigrationPreservesLegacyJobsAndCanRepeat()
    {
        var id = await Service.Create(new(1, OrcamentoTests.Draft(OrcamentoTests.Item())), default);
        var before = OrcamentoJson.Write((await Service.Get(id, default)).Snapshot);
        await using (var c = await factory.OpenConnectionAsync())
        {
            var input = new QuoteAiInput("Legado", "Escopo anterior", "", [], []);
            await c.ExecuteAsync("INSERT INTO ORCAMENTOANALISE(CODORCAMENTO,REVISAO,VERSAOORCAMENTO,STATUS,ENTRADA,MODELO,CODUSUARIO,DTHRCRIACAO) VALUES(@id,1,1,'pendente',@input,'composer-2.5',1,CURRENT_TIMESTAMP)", new { id, input = QuoteTextBlob.Json(input) });
            await c.ExecuteAsync("ALTER TABLE ORCAMENTOANALISE DROP CONSTRAINT FK_ORCANALISE_ORIGEM");
            foreach (var column in new[] { "CONTEXTO", "ETAPA", "CODANALISEORIGEM" }) await c.ExecuteAsync($"ALTER TABLE ORCAMENTOANALISE DROP {column}");
            await c.ExecuteAsync("DROP TABLE ORCAMENTOREGRACONFIG"); await c.ExecuteAsync("DROP TABLE ORCAMENTOREGRAVERSAO");
        }
        var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs/sql/scripts/evolve-orcamentos-technical-rules.sql"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var isql = Path.Combine(Path.GetDirectoryName(Environment.GetEnvironmentVariable("PUBLICACAO_TEST_FBCLIENT"))!, OperatingSystem.IsWindows() ? "isql.exe" : "isql");
        if (File.Exists(isql))
            for (var repeat = 0; repeat < 2; repeat++)
            {
                var start = new System.Diagnostics.ProcessStartInfo(isql) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var a in new[] { "-b", "-ch", "UTF8", "-user", "SYSDBA", "-password", "masterkey", "-input", Path.Combine(dir!.FullName, "docs/sql/scripts/evolve-orcamentos-technical-rules.sql"), new FirebirdSql.Data.FirebirdClient.FbConnectionStringBuilder(cs).Database }) start.ArgumentList.Add(a);
                using var process = System.Diagnostics.Process.Start(start)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try { await process.WaitForExitAsync(timeout.Token); } catch { if (!process.HasExited) process.Kill(); throw; }
                Assert.True(process.ExitCode == 0, await output + await error);
            }
        await OrcamentoSchema.MigrateAsync(factory); await OrcamentoSchema.MigrateAsync(factory);
        Assert.Equal(before, OrcamentoJson.Write((await Service.Get(id, default)).Snapshot));
        await using var verify = await factory.OpenConnectionAsync();
        Assert.Equal("analise", await verify.ExecuteScalarAsync<string>("SELECT ETAPA FROM ORCAMENTOANALISE"));
        Assert.Equal("pendente", await verify.ExecuteScalarAsync<string>("SELECT STATUS FROM ORCAMENTOANALISE"));
        Assert.Equal(1, await verify.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ORCAMENTOREGRAVERSAO"));
    }
    private sealed class TwoPhaseQuoteAi : IQuoteAiProvider
    {
        public int Calls;
        public Task<QuoteAiSuggestion> Analyze(QuoteAiInput input, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(input.Phase == "classificacao"
                ? new QuoteAiSuggestion("Identificação sem horas", [], []) { Classification = new("Ajuste parcial de laudo", [OrcamentoTechnicalTests.Front()], []) }
                : OrcamentoAiTests.Suggestion() with { Items = [OrcamentoAiTests.Suggestion().Items[0] with { FrontId = "frente-1", BlockingPending = false, Pending = "" }] });
        }
    }
    [FirebirdPublicationFact]
    public async Task TwoExplicitCallsFreezeRulesRequireChecklistAndPreserveIndependentApproval()
    {
        await using (var c = await factory.OpenConnectionAsync())
        {
            await c.ExecuteAsync("CREATE TABLE TIPOLINGUAGEM(CODLINGUAGEM INTEGER PRIMARY KEY,NOME VARCHAR(100))");
            await c.ExecuteAsync("INSERT INTO TIPOLINGUAGEM VALUES(8,'HTML')"); await c.ExecuteAsync("INSERT INTO TIPOLINGUAGEM VALUES(15,'C#')");
        }
        await Service.PublishTechnicalCatalog(new("1", "Ativação revisada pela gestão", OrcamentoTechnicalTests.Catalog()), default);
        await Service.SavePolicy(new("1", QuotePolicy.Initial with { HourlyRate = 100 }, "Preço aprovado"), default);
        var options = new OrcamentoAiOptions { Enabled = true }; var fake = new TwoPhaseQuoteAi();
        await using var services = new ServiceCollection().AddSingleton<IFirebirdConnectionFactory>(factory).AddScoped<IPermissionService, PermissionService>().AddSingleton<IQuoteAiProvider>(fake).BuildServiceProvider();
        var worker = new OrcamentoAiWorker(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(options), NullLogger<OrcamentoAiWorker>.Instance);
        var id = await Service.Create(new(1, OrcamentoTests.Draft(OrcamentoTests.Item())), default); var quote = await Service.Get(id, default);
        await Assert.ThrowsAsync<OrcamentoException>(() => Service.QueueAnalysis(id, new(quote.Header.Version, [], true, ""), options, default, true));
        var classification = long.Parse(await Service.QueueAnalysis(id, new(quote.Header.Version, [], true, "Adicionar somente as medidas circuladas em vermelho"), options, default, true));
        Assert.True(await worker.RunOne(default)); Assert.Equal(1, fake.Calls); Assert.False(await worker.RunOne(default));
        quote = await Service.Get(id, default); Assert.Single(quote.Snapshot.Draft.Items);
        await Assert.ThrowsAsync<OrcamentoException>(() => Service.ApplyAnalysis(id, classification, new(quote.Header.Version, [0]), default));
        // Publication after classification must not silently replace the frozen rules.
        var changed = OrcamentoTechnicalTests.Catalog() with { Rules = OrcamentoTechnicalTests.Catalog().Rules.Select(r => r with { Instructions = "Orientação publicada posteriormente" }).ToList() };
        await Service.PublishTechnicalCatalog(new("2", "Atualização posterior", changed), default);
        var technical = long.Parse(await Service.ConfirmClassification(id, classification, new(quote.Header.Version, [OrcamentoTechnicalTests.Front()], true, false), options, default));
        Assert.Equal(1, fake.Calls); Assert.True(await worker.RunOne(default)); Assert.Equal(2, fake.Calls);
        quote = await Service.Get(id, default);
        await Assert.ThrowsAsync<OrcamentoException>(() => Service.ConfirmClassification(id, classification, new(quote.Header.Version, [OrcamentoTechnicalTests.Front()], true, false), options, default));
        await Service.ApplyAnalysis(id, technical, new(quote.Header.Version, [0]), default); quote = await Service.Get(id, default);
        var item = quote.Snapshot.Draft.Items[^1]; Assert.Equal("2", item.TechnicalContext!.CatalogVersion);
        Assert.DoesNotContain(item.TechnicalContext.Rules, r => r.Instructions == "Orientação publicada posteriormente");
        Assert.Contains(quote.Issues, i => i.Code.StartsWith("checklist:") && i.ItemIndex == 1); Assert.False(quote.Snapshot.Calculation.Complete);
        await Assert.ThrowsAsync<OrcamentoException>(() => Service.Command(id, new(quote.Header.Version, "solicitar_aprovacao"), default));
        Context(2);
        var answered = item with { CheckAnswers = [new("dependencias:conferir-dependencias", "confirmado", "Dependências verificadas com a equipe", 999, "falso")] };
        await Service.Save(id, new(quote.Header.Version, quote.Snapshot.Draft with { Items = [quote.Snapshot.Draft.Items[0], answered] }), default);
        quote = await Service.Get(id, default); Assert.True(quote.Snapshot.Calculation.Complete); Assert.Equal(2, quote.Snapshot.Draft.Items[1].CheckAnswers[0].ActorId);
        quote = await Act(id, "solicitar_aprovacao");
        await Assert.ThrowsAsync<OrcamentoException>(() => Service.Command(id, new(quote.Header.Version, "aprovar"), default));
        Context(3); Assert.Equal("aprovado", (await Act(id, "aprovar")).Header.Status);
    }
    [FirebirdPublicationFact]
    public async Task GenericAnalysisRequiresSeparateAcknowledgementAndCatalogPublicationUsesVersionCheck()
    {
        await using (var c = await factory.OpenConnectionAsync()) { await c.ExecuteAsync("CREATE TABLE TIPOLINGUAGEM(CODLINGUAGEM INTEGER PRIMARY KEY,NOME VARCHAR(100))"); await c.ExecuteAsync("INSERT INTO TIPOLINGUAGEM VALUES(8,'HTML')"); }
        var options = new OrcamentoAiOptions { Enabled = true };
        var id = await Service.Create(new(1, OrcamentoTests.Draft(OrcamentoTests.Item())), default); var quote = await Service.Get(id, default);
        var job = long.Parse(await Service.QueueAnalysis(id, new(quote.Header.Version, [], true, "Somente campos destacados"), options, default, true));
        await using var c2 = await factory.OpenConnectionAsync();
        var result = new QuoteAiSuggestion("Classificado", [], []) { Classification = new("Frente", [OrcamentoTechnicalTests.Front()], []) };
        await c2.ExecuteAsync("UPDATE ORCAMENTOANALISE SET STATUS='concluida',RESULTADO=@result WHERE CODORCAMENTOANALISE=@job", new { result = QuoteTextBlob.Json(result), job });
        quote = await Service.Get(id, default);
        await Assert.ThrowsAsync<OrcamentoException>(() => Service.ConfirmClassification(id, job, new(quote.Header.Version, [OrcamentoTechnicalTests.Front()], true, false), options, default));
        _ = await Service.ConfirmClassification(id, job, new(quote.Header.Version, [OrcamentoTechnicalTests.Front()], true, true), options, default);
        await Service.PublishTechnicalCatalog(new("1", "Publicação explícita", QuoteTechnicalCatalog.Initial), default);
        await Assert.ThrowsAsync<OrcamentoException>(() => Service.PublishTechnicalCatalog(new("1", "Concorrente", QuoteTechnicalCatalog.Initial), default));
        Assert.Equal(2, await c2.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ORCAMENTOREGRAVERSAO"));
    }
}
