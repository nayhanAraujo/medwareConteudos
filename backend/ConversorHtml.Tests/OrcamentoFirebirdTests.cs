using System.Security.Claims;
using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Http;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Auditoria;
using MdwConteudos.Api.Modules.Orcamentos;
using MdwConteudos.Api.Modules.Permissions;

namespace ConversorHtml.Tests;

public sealed partial class OrcamentoFirebirdTests : IAsyncLifetime
{
    private string cs = ""; private Factory factory = null!; private readonly TestAccessor accessor = new();
    private OrcamentoService Service => new(factory, accessor, TimeProvider.System);
    public async Task InitializeAsync()
    {
        var library = Environment.GetEnvironmentVariable("PUBLICACAO_TEST_FBCLIENT"); if (string.IsNullOrWhiteSpace(library)) return;
        cs = new FbConnectionStringBuilder { Database = Path.Combine(Path.GetTempPath(), $"budget-test-{Guid.NewGuid():N}.fdb"), ServerType = FbServerType.Embedded, ClientLibrary = library, UserID = "SYSDBA", Password = "masterkey", Charset = "UTF8", Pooling = false }.ToString();
        FbConnection.CreateDatabase(cs); factory = new(cs, accessor);
        await using (var c = await factory.OpenConnectionAsync())
        {
            await c.ExecuteAsync("CREATE TABLE USUARIO(CODUSUARIO INTEGER PRIMARY KEY,NOME VARCHAR(100),PERFIL VARCHAR(20),STATUS INTEGER)");
            await c.ExecuteAsync("INSERT INTO USUARIO VALUES(1,'Elaborador','admin',-1)"); await c.ExecuteAsync("INSERT INTO USUARIO VALUES(2,'Aprovador','admin',-1)"); await c.ExecuteAsync("INSERT INTO USUARIO VALUES(3,'Gestão','admin',-1)");
            await c.ExecuteAsync("INSERT INTO USUARIO VALUES(4,'Consulta','usuario',-1)");
            await c.ExecuteAsync("CREATE TABLE CLIENTES(CODCLIENTE INTEGER PRIMARY KEY,NOME VARCHAR(100),STATUS INTEGER)"); await c.ExecuteAsync("INSERT INTO CLIENTES VALUES(1,'Clínica de teste',1)");
        }
        await new PermissionService(factory).EnsureSchemaAsync();
        await OrcamentoSchema.MigrateAsync(factory); await AuditSchema.MigrateAsync(factory); Context(1);
    }
    public Task DisposeAsync() { accessor.HttpContext = null; if(cs != "") FbConnection.DropDatabase(cs); return Task.CompletedTask; }
    private void Context(int actor)
    {
        var context = new DefaultHttpContext(); context.TraceIdentifier = "budget-correlation"; context.Request.Path = "/api/web/orcamentos"; context.Request.Method = "POST";
        context.User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString()), new Claim(ClaimTypes.Role, "admin")], "test")); context.Items[AuditRequest.ItemKey] = new AuditRequest(); accessor.HttpContext = context;
    }
    private async Task<QuoteDetail> Act(long id, string action, string reason = "", string reference = "", string destination = "")
    {
        var quote = await Service.Get(id, default); await Service.Command(id, new(quote.Header.Version, action, reason, reference, destination), default); return await Service.Get(id, default);
    }
    [FirebirdPublicationFact]
    public async Task RepeatedMigrationSnapshotsNoopCoauthorsApprovalAndNewRevisionRemainConsistent()
    {
        accessor.HttpContext = null; await OrcamentoSchema.MigrateAsync(factory); await AuditSchema.MigrateAsync(factory); Context(1);
        var id = await Service.Create(new(1, OrcamentoTests.Draft(OrcamentoTests.Item())), default); var quote = await Service.Get(id, default);
        Assert.Equal("1", quote.Header.Version); Assert.Single(quote.Editors); Assert.True(quote.Snapshot.Calculation.Complete);
        Context(2); await Service.Save(id, new(quote.Header.Version, quote.Snapshot.Draft), default); Assert.Single((await Service.Get(id, default)).Editors);
        Context(1); await Act(id, "solicitar_aprovacao");
        Assert.Equal(403, (await Assert.ThrowsAsync<OrcamentoException>(() => Act(id, "aprovar"))).Status);
        Context(2); quote = await Act(id, "aprovar"); Assert.Equal("aprovado", quote.Header.Status);
        await Assert.ThrowsAsync<OrcamentoException>(() => Service.Save(id, new(quote.Header.Version, quote.Snapshot.Draft), default));
        await Service.PrepareProposal(id, quote.Header.Version, default);
        var prepared = await Service.Proposal(id, null, default);
        Assert.Equal(id.ToString(), prepared.Document.Id);
        await Service.PrepareProposal(id, "1", default); // Idempotent retries return the same immutable artifact.
        Assert.Equal(prepared.Pdf, (await Service.Proposal(id, null, default)).Pdf);
        Context(1); quote = await Act(id, "nova_revisao", "Novo escopo"); Assert.Equal(2, quote.Header.Revision); Assert.Equal("rascunho", quote.Header.Status);
        Assert.Equal("aprovado", quote.Revisions.Single(r => r.Number == 1).Status); Assert.Equal(2, quote.Revisions.Count);
        await using var c = await factory.OpenConnectionAsync(); Assert.True(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AUDITORIA_EVENTO WHERE MODULE='orcamentos'") > 0);
        var content = string.Join(" ", await c.QueryAsync<string>("SELECT SUMMARY FROM AUDITORIA_EVENTO WHERE MODULE='orcamentos'")); Assert.DoesNotContain("Serviço público", content);
    }
    [FirebirdPublicationFact]
    public async Task EveryCommercialEditorIsDeniedAndOldVersionCannotOverwrite()
    {
        var id = await Service.Create(new(1, OrcamentoTests.Draft(OrcamentoTests.Item())), default); var first = await Service.Get(id, default);
        Context(2); await Service.Save(id, new(first.Header.Version, first.Snapshot.Draft with { Title = "Editado por outro usuário" }), default);
        Assert.Equal(409, (await Assert.ThrowsAsync<OrcamentoException>(() => Service.Save(id, new(first.Header.Version, first.Snapshot.Draft), default))).Status);
        await Act(id,"solicitar_aprovacao"); Assert.Equal(403,(await Assert.ThrowsAsync<OrcamentoException>(()=>Act(id,"aprovar"))).Status);
        Context(1); Assert.Equal(403,(await Assert.ThrowsAsync<OrcamentoException>(()=>Act(id,"aprovar"))).Status);
        Context(3); Assert.Equal("aprovado",(await Act(id,"aprovar")).Header.Status);
    }
    [FirebirdPublicationFact]
    public async Task PaymentAndDestinationAreRequiredAndNoFactsCarryIntoNewRevision()
    {
        var id = await Service.Create(new(1, OrcamentoTests.Draft(OrcamentoTests.Item())), default); await Act(id,"solicitar_aprovacao"); Context(2); await Act(id,"aprovar");
        await Act(id,"enviar"); await Assert.ThrowsAsync<OrcamentoException>(()=>Act(id,"encaminhar",destination:"Equipe")); await Act(id,"aceitar");
        await Assert.ThrowsAsync<OrcamentoException>(()=>Act(id,"confirmar_pagamento")); await Act(id,"confirmar_pagamento",reference:"CONFIRMACAO_TESTE");
        await Assert.ThrowsAsync<OrcamentoException>(()=>Act(id,"encaminhar")); await Act(id,"encaminhar",destination:"Equipe de demandas");
        var revision = await Act(id,"nova_revisao","Solicitação adicional"); Assert.Equal("rascunho",revision.Header.Status);
        Assert.Equal("encaminhado",revision.Revisions.Single(r=>r.Number==1).Status); await Assert.ThrowsAsync<OrcamentoException>(()=>Act(id,"encaminhar",destination:"Equipe"));
    }
    [FirebirdPublicationFact]
    public async Task AuditFailureRollsBackTheWholeQuoteAndClientHistoryPreventsDeletion()
    {
        await using (var c=await factory.OpenConnectionAsync()) { await c.ExecuteAsync("CREATE EXCEPTION FORCE_BUDGET_AUDIT 'forced test'");await c.ExecuteAsync("CREATE TRIGGER FORCE_BUDGET_AUDIT FOR AUDITORIA_EVENTO BEFORE INSERT AS BEGIN EXCEPTION FORCE_BUDGET_AUDIT; END"); }
        await Assert.ThrowsAsync<FbException>(()=>Service.Create(new(1,OrcamentoTests.Draft(OrcamentoTests.Item())),default));
        await using(var c=await factory.OpenConnectionAsync()){foreach(var table in new[]{"ORCAMENTO","ORCAMENTOREVISAO","ORCAMENTOITEM","ORCAMENTO_USUARIO","ORCAMENTOEVENTO"})Assert.Equal(0,await c.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {table}"));await c.ExecuteAsync("DROP TRIGGER FORCE_BUDGET_AUDIT");}
        await Service.Create(new(1,OrcamentoTests.Draft(OrcamentoTests.Item())),default);
        await using(var c=await factory.OpenConnectionAsync())await Assert.ThrowsAsync<FbException>(()=>c.ExecuteAsync("DELETE FROM CLIENTES WHERE CODCLIENTE=1"));
    }
    [FirebirdPublicationFact]
    public async Task PolicyAndCommercialClientAreVersionedAndDoNotSilentlyChangeDrafts()
    {
        await Service.SaveClient(1,new("0",new(MonthlyFee:800)),default);
        var id=await Service.Create(new(1,OrcamentoTests.Draft(OrcamentoTests.Item())),default);var initial=await Service.Get(id,default);
        await Service.SaveClient(1,new("1",new(MonthlyFee:2000)),default);
        Assert.Equal(800,(await Service.Get(id,default)).Snapshot.Client.Commercial.MonthlyFee);
        await Service.Save(id,new(initial.Header.Version,initial.Snapshot.Draft,RefreshClient:true),default);
        Assert.Equal(2000,(await Service.Get(id,default)).Snapshot.Client.Commercial.MonthlyFee);
        var json=System.Text.Json.JsonSerializer.SerializeToElement(await Service.GetPolicy(default),OrcamentoJson.Options);var version=json.GetProperty("version").GetString()!;
        await Service.SavePolicy(new(version,QuotePolicy.Initial with{HourlyRate=120},"Definição aprovada"),default);
        await Assert.ThrowsAsync<OrcamentoException>(()=>Service.SavePolicy(new(version,QuotePolicy.Initial,"Tentativa antiga"),default));
        Assert.Null((await Service.Get(id,default)).Snapshot.Policy.HourlyRate);
    }
    [FirebirdPublicationFact]
    public async Task RealHttpEnforcesIndependentPermissionsPrivateDownloadsAndApproval()
    {
        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IHttpContextAccessor>(accessor);builder.Services.AddSingleton<IFirebirdConnectionFactory>(factory);builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<OrcamentoService>();builder.Services.AddScoped<AuditWriter>();builder.Services.AddPermissionsModule();
        builder.Services.AddAuthentication("BudgetTest").AddScheme<AuthenticationSchemeOptions,TestAuthentication>("BudgetTest",_=>{});builder.Services.AddAuthorization();
        builder.Services.AddControllers(o=>o.Filters.Add<AuditFileResultFilter>()).AddApplicationPart(typeof(OrcamentosController).Assembly);
        await using var app=builder.Build();app.UseRouting();app.UseAuthentication();app.UseMiddleware<AuditMiddleware>();app.UseAuthorization();app.MapControllers();await app.StartAsync();
        try
        {
            var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();using var client=new HttpClient{BaseAddress=new(address)};
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/web/orcamentos")).StatusCode);
            client.DefaultRequestHeaders.Add("X-Test-User","4");Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/web/orcamentos")).StatusCode);
            // New domain is not silently granted to ordinary users by initial permission seeding.
            await using(var c=await factory.OpenConnectionAsync())await c.ExecuteAsync("INSERT INTO USUARIO_PERMISSAO(CODUSUARIO,CHAVE,MODO) VALUES(4,'orcamentos.visualizar','PERMITIR')");
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/web/orcamentos")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/web/orcamentos",new CreateQuoteRequest(1,OrcamentoTests.Draft(OrcamentoTests.Item())),OrcamentoJson.Options)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/web/orcamentos/configuracoes")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/web/orcamentos/1/analises")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PutAsJsonAsync("/api/web/orcamentos/regras-tecnicas",new QuoteCatalogPublication("1","Não autorizado",QuoteTechnicalCatalog.Initial),OrcamentoJson.Options)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/web/orcamentos/regras-tecnicas")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/web/orcamentos/regras-tecnicas/aplicaveis")).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-User");client.DefaultRequestHeaders.Add("X-Test-User","1");
            var response=await client.PostAsJsonAsync("/api/web/orcamentos",new CreateQuoteRequest(1,OrcamentoTests.Draft(OrcamentoTests.Item())),OrcamentoJson.Options);response.EnsureSuccessStatusCode();
            var json=await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();var id=json.GetProperty("id").GetString()!;
            Assert.Equal(HttpStatusCode.Conflict,(await client.GetAsync($"/api/web/orcamentos/{id}/proposta.pdf")).StatusCode);
            var detail=await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/web/orcamentos/{id}");var version=detail.GetProperty("data").GetProperty("header").GetProperty("version").GetString()!;
            response=await client.PostAsJsonAsync($"/api/web/orcamentos/{id}/transicoes",new QuoteCommand(version,"solicitar_aprovacao"),OrcamentoJson.Options);response.EnsureSuccessStatusCode();
            json=await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();version=json.GetProperty("data").GetProperty("header").GetProperty("version").GetString()!;
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync($"/api/web/orcamentos/{id}/transicoes",new QuoteCommand(version,"aprovar"),OrcamentoJson.Options)).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-User");client.DefaultRequestHeaders.Add("X-Test-User","2");
            response=await client.PostAsJsonAsync($"/api/web/orcamentos/{id}/transicoes",new QuoteCommand(version,"aprovar"),OrcamentoJson.Options);response.EnsureSuccessStatusCode();
            json=await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();version=json.GetProperty("data").GetProperty("header").GetProperty("version").GetString()!;
            response=await client.PostAsJsonAsync($"/api/web/orcamentos/{id}/proposta",new PrepareQuoteRequest(version),OrcamentoJson.Options);response.EnsureSuccessStatusCode();
            var pdf=await client.GetAsync($"/api/web/orcamentos/{id}/proposta.pdf");pdf.EnsureSuccessStatusCode();Assert.Equal("application/pdf",pdf.Content.Headers.ContentType!.MediaType);Assert.Contains("no-store",pdf.Headers.CacheControl!.ToString());
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/web/orcamentos/{id}/anexos/99999")).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-User");Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync($"/api/web/orcamentos/{id}/proposta.pdf")).StatusCode);
        }
        finally{await app.StopAsync();}
    }
    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if(!int.TryParse(Request.Headers["X-Test-User"],out var id))return Task.FromResult(AuthenticateResult.NoResult());
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,id.ToString()),new Claim(ClaimTypes.Role,"admin")],Scheme.Name)),Scheme.Name)));
        }
    }
    private sealed class Factory(string cs,IHttpContextAccessor accessor):IFirebirdConnectionFactory
    {
        public async Task<FbConnection> OpenConnectionAsync(CancellationToken ct=default){var c=new FbConnection(cs);await c.OpenAsync(ct);await AuditSession.InitializeAsync(c,accessor.HttpContext,ct);return c;}
    }
    private sealed class TestAccessor : IHttpContextAccessor { public HttpContext? HttpContext { get; set; } }
}
