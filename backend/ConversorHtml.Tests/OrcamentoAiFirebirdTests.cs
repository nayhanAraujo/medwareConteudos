using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Orcamentos;
using MdwConteudos.Api.Modules.Permissions;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace ConversorHtml.Tests;

public sealed partial class OrcamentoFirebirdTests
{
    private sealed class FakeQuoteAi : IQuoteAiProvider
    {
        public int Calls;
        public QuoteAiSuggestion Result = OrcamentoAiTests.Suggestion();
        public Task<QuoteAiSuggestion> Analyze(QuoteAiInput input, CancellationToken ct) { Calls++; return Task.FromResult(Result); }
    }
    [FirebirdPublicationFact]
    public async Task LargeAiInputAndResultUseTextBlobsWithoutDapperSizeMinusOne()
    {
        var id = await Service.Create(new(1, OrcamentoTests.Draft(OrcamentoTests.Item())), default);
        var quote = await Service.Get(id, default);
        var text = "Descrição;unidade\n" + string.Concat(Enumerable.Repeat("Medidas cardíacas;milímetros\n", 250));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var file = new FormFile(stream, 0, stream.Length, "file", "escopo.csv");
        await Service.Upload(id, quote.Header.Version, "escopo", file, default);
        quote = await Service.Get(id, default);
        var attachment = Assert.Single(quote.Attachments);
        var options = new OrcamentoAiOptions { Enabled = true };
        var job = long.Parse(await Service.QueueAnalysis(id, new(quote.Header.Version, [attachment.Id], true), options, default));
        await using var c = await factory.OpenConnectionAsync();
        var input = OrcamentoJson.Read<QuoteAiInput>(await c.ExecuteScalarAsync<string>("SELECT ENTRADA FROM ORCAMENTOANALISE WHERE CODORCAMENTOANALISE=@job", new { job }));
        Assert.Equal(text, Assert.Single(input.Sources).Text);
        var fake = new FakeQuoteAi { Result = OrcamentoAiTests.Suggestion() with { Summary = new string('ç', 1500), Items = [OrcamentoAiTests.Suggestion().Items[0] with { Description = new string('á', 1500) }] } };
        await using var services = new ServiceCollection().AddSingleton<IFirebirdConnectionFactory>(factory).AddScoped<IPermissionService, PermissionService>().AddSingleton<IQuoteAiProvider>(fake).BuildServiceProvider();
        var worker = new OrcamentoAiWorker(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(options), NullLogger<OrcamentoAiWorker>.Instance);
        Assert.True(await worker.RunOne(default));
        var result = await c.ExecuteScalarAsync<string>("SELECT RESULTADO FROM ORCAMENTOANALISE WHERE CODORCAMENTOANALISE=@job", new { job });
        Assert.True(result.Length > 4000); Assert.Equal(fake.Result.Summary, QuoteAiSuggestion.Parse(result).Summary);
        quote = await Service.Get(id, default);
        await Service.ApplyAnalysis(id, job, new(quote.Header.Version, [0]), default);
        quote = await Service.Get(id, default); Assert.Equal(fake.Result.Items[0].Description, quote.Snapshot.Draft.Items[^1].Description);
        await Service.Save(id, new(quote.Header.Version, quote.Snapshot.Draft with { Title = "Revisão com texto extenso" }), default);
        quote = await Service.Get(id, default);
        await Service.Command(id, new(quote.Header.Version, "nova_revisao", "Conferir cópia de BLOB extenso"), default);
        Assert.Equal(fake.Result.Items[0].Description, (await Service.Get(id, default)).Snapshot.Draft.Items[^1].Description);
    }
    private sealed class WaitingQuoteAi : IQuoteAiProvider
    {
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Cancelled;
        public async Task<QuoteAiSuggestion> Analyze(QuoteAiInput input, CancellationToken ct)
        {
            Started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, ct); return OrcamentoAiTests.Suggestion(); }
            finally { Cancelled = ct.IsCancellationRequested; }
        }
    }
    [FirebirdPublicationFact]
    public async Task CancellingRunningAnalysisStopsProviderAndCannotPublishLateResult()
    {
        var options = new OrcamentoAiOptions { Enabled = true }; var fake = new WaitingQuoteAi();
        await using var services = new ServiceCollection().AddSingleton<IFirebirdConnectionFactory>(factory).AddScoped<IPermissionService, PermissionService>()
            .AddSingleton<IQuoteAiProvider>(fake).BuildServiceProvider();
        var worker = new OrcamentoAiWorker(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(options), NullLogger<OrcamentoAiWorker>.Instance);
        var id = await Service.Create(new(1, OrcamentoTests.Draft(OrcamentoTests.Item())), default);
        var quote = await Service.Get(id, default);
        var job = long.Parse(await Service.QueueAnalysis(id, new(quote.Header.Version, [], true), options, default));
        var running = worker.RunOne(default); await fake.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Service.CancelAnalysis(id, job, default); await running.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(fake.Cancelled);
        await using var c = await factory.OpenConnectionAsync();
        Assert.Equal("cancelada", await c.ExecuteScalarAsync<string>("SELECT STATUS FROM ORCAMENTOANALISE WHERE CODORCAMENTOANALISE=@job", new { job }));
        Assert.Null(await c.ExecuteScalarAsync<string?>("SELECT RESULTADO FROM ORCAMENTOANALISE WHERE CODORCAMENTOANALISE=@job", new { job }));
    }
    [FirebirdPublicationFact]
    public async Task PersistentAiQueueRequiresConsentAndHumanApplicationAndRejectsStaleResults()
    {
        var options = new OrcamentoAiOptions { Enabled = true }; var fake = new FakeQuoteAi();
        await using var services = new ServiceCollection().AddSingleton<IFirebirdConnectionFactory>(factory).AddScoped<IPermissionService, PermissionService>()
            .AddSingleton<IQuoteAiProvider>(fake).BuildServiceProvider();
        var worker = new OrcamentoAiWorker(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(options), NullLogger<OrcamentoAiWorker>.Instance);
        var id = await Service.Create(new(1, OrcamentoTests.Draft(OrcamentoTests.Item())), default);
        var quote = await Service.Get(id, default);
        await Assert.ThrowsAsync<OrcamentoException>(() => Service.QueueAnalysis(id, new(quote.Header.Version, [], false), options, default));
        var job = long.Parse(await Service.QueueAnalysis(id, new(quote.Header.Version, [], true), options, default));
        quote = await Service.Get(id, default);
        await Assert.ThrowsAsync<OrcamentoException>(() => Service.QueueAnalysis(id, new(quote.Header.Version, [], true), options, default));
        Assert.True(await worker.RunOne(default)); Assert.Equal(1, fake.Calls);
        Assert.Single((await Service.Get(id, default)).Snapshot.Draft.Items);
        Context(2); await Service.ApplyAnalysis(id, job, new(quote.Header.Version, [0]), default);
        quote = await Service.Get(id, default); Assert.Equal(2, quote.Snapshot.Draft.Items.Count); Assert.Contains(2, quote.Editors);
        Assert.Null(quote.Snapshot.Draft.Items[1].UnitPrice); Assert.True(quote.Snapshot.Draft.Items[1].BlockingPending);
        await Assert.ThrowsAsync<OrcamentoException>(() => Service.ApplyAnalysis(id, job, new(quote.Header.Version, [0]), default));
        var stale = long.Parse(await Service.QueueAnalysis(id, new(quote.Header.Version, [], true), options, default));
        quote = await Service.Get(id, default);
        await Service.Save(id, new(quote.Header.Version, quote.Snapshot.Draft with { Title = "Escopo alterado depois da solicitação" }), default);
        Assert.True(await worker.RunOne(default)); quote = await Service.Get(id, default);
        await Assert.ThrowsAsync<OrcamentoException>(() => Service.ApplyAnalysis(id, stale, new(quote.Header.Version, [0]), default));
        var cancelled = long.Parse(await Service.QueueAnalysis(id, new(quote.Header.Version, [], true), options, default));
        await Service.CancelAnalysis(id, cancelled, default); Assert.False(await worker.RunOne(default)); Assert.Equal(2, fake.Calls);
        await using var c = await factory.OpenConnectionAsync();
        Assert.Equal("aplicada", await c.ExecuteScalarAsync<string>("SELECT STATUS FROM ORCAMENTOANALISE WHERE CODORCAMENTOANALISE=@job", new { job }));
        Assert.True(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AUDITORIA_EVENTO WHERE MODULE='orcamentos' AND ACTION IN ('analisar_ia','aplicar_ia','resultado_ia','cancelar_ia')") >= 6);
        Assert.Equal(0, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ORCAMENTOANALISE WHERE STATUS='executando'"));
    }
}
