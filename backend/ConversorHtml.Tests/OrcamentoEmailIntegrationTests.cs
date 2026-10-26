using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Auditoria;
using MdwConteudos.Api.Modules.Orcamentos;
using MdwConteudos.Api.Modules.Permissions;

namespace ConversorHtml.Tests;

public sealed partial class OrcamentoFirebirdTests
{
    private static ImportQuoteEmailRequest EmailImport()=>new(1,"Solicitação do Outlook","Incluir somente os campos indicados pelo cliente.",new("triagem@example.com","immutable-1","<request-1@example.com>","conversation-1","forwarder@example.com","client@example.com","Solicitação de orçamento",new DateTimeOffset(2026,10,26,12,0,0,TimeSpan.Zero)));
    [FirebirdPublicationFact]
    public async Task ImportedMessageRequiresHumanConfirmationAndOneUseDispatchAuthorization()
    {
        var input=EmailImport();var imported=await Service.ImportEmail(input,default);Assert.True(imported.Created);var id=long.Parse(imported.Id);
        Assert.False((await Service.ImportEmail(input with {Origin=input.Origin with {Mailbox="TRIAGEM@example.com",MessageId="moved-1"}},default)).Created);
        Assert.Equal(imported.Id,await Service.FindEmail(new(input.Origin.Mailbox,input.Origin.InternetMessageId),default));
        Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.ImportEmail(input with {Scope="Outro escopo"},default))).Status);
        var quote=await Service.Get(id,default);Assert.Equal("rascunho",quote.Header.Status);Assert.Empty(quote.Snapshot.Draft.Items);Assert.False(quote.EmailSource!.Confirmed);Assert.Contains(quote.Issues,i=>i.Code=="origem_email"&&i.Step==1);
        var csv=Encoding.UTF8.GetBytes("Campo;Solicitado\nAltura;Sim");
        IFormFile File()=>new FormFile(new MemoryStream(csv),0,csv.Length,"file","escopo.csv");
        await Service.Upload(id,quote.Header.Version,"escopo",File(),default,importedEmail:true);quote=await Service.Get(id,default);
        await Service.Upload(id,"1","escopo",File(),default,importedEmail:true);Assert.Single((await Service.Get(id,default)).Attachments);
        Context(2);Assert.Equal(403,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.FindEmail(new(input.Origin.Mailbox,input.Origin.InternetMessageId),default))).Status);
        var draft=OrcamentoTests.Draft(OrcamentoTests.Item()) with {Title=input.Title,Request=input.Scope};
        await Service.Save(id,new(quote.Header.Version,draft),default);quote=await Service.Get(id,default);
        Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Act(id,"solicitar_aprovacao"))).Status);
        Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.QueueAnalysis(id,new(quote.Header.Version,[],true,"Analisar o escopo"),new OrcamentoAiOptions{Enabled=true},default,classify:true))).Status);
        await using(var c=await factory.OpenConnectionAsync())await c.ExecuteAsync("INSERT INTO CLIENTES VALUES(2,'Cliente corrigido',1)");
        Assert.Equal(422,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.ConfirmEmailSource(id,new(quote.Header.Version,2,"client@example.com",false,"Revisado"),default))).Status);
        await Service.ConfirmEmailSource(id,new(quote.Header.Version,2,"client@example.com",true,"Cliente conferido pelo conteúdo e contato cadastrado."),default);
        quote=await Service.Get(id,default);Assert.True(quote.EmailSource!.Confirmed);Assert.Equal(2,quote.Header.ClientId);
        await Service.Save(id,new(quote.Header.Version,draft with {Request="Escopo corrigido após conversa com o cliente."}),default);quote=await Service.Get(id,default);Assert.False(quote.EmailSource!.Confirmed);
        await Service.ConfirmEmailSource(id,new(quote.Header.Version,2,"client@example.com",true,"Escopo corrigido e confirmado."),default);
        Context(1);quote=await Service.Get(id,default);Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.Upload(id,quote.Header.Version,"escopo",File(),default,importedEmail:true))).Status);
        await Act(id,"solicitar_aprovacao");Assert.Equal(403,(await Assert.ThrowsAsync<OrcamentoException>(()=>Act(id,"aprovar"))).Status);
        Context(2);Assert.Equal(403,(await Assert.ThrowsAsync<OrcamentoException>(()=>Act(id,"aprovar"))).Status);
        Context(3);quote=await Act(id,"aprovar");await Service.PrepareProposal(id,quote.Header.Version,default);quote=await Service.Get(id,default);
        var sender=new FakeQuoteMail();Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.SendConfirmedEmail(id,new(quote.Header.Version,1,Guid.NewGuid()),sender,default))).Status);
        Context(2);var authorization=await Service.AuthorizeEmail(id,new(quote.Header.Version,1,"client@example.com"),default);quote=await Service.Get(id,default);
        var pending=OrcamentoJson.Write(await Service.PendingEmailDispatches(null,default));Assert.Contains(authorization.ToString(),pending);Assert.DoesNotContain("internalCost",pending);
        Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.SendProposalEmail(id,new(quote.Header.Version,1,"client@example.com",Guid.NewGuid()),sender,default))).Status);
        await Service.RevokeEmail(id,new(quote.Header.Version,authorization,"Cliente pediu para aguardar."),default);quote=await Service.Get(id,default);
        Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.SendConfirmedEmail(id,new(quote.Header.Version,1,authorization),sender,default))).Status);Assert.Equal(0,sender.Calls);
        Assert.DoesNotContain(authorization.ToString(),OrcamentoJson.Write(await Service.PendingEmailDispatches(null,default)));
        authorization=await Service.AuthorizeEmail(id,new(quote.Header.Version,1,"client@example.com"),default);quote=await Service.Get(id,default);Context(1);
        var dispatch=new DispatchQuoteEmailRequest(quote.Header.Version,1,authorization);await Service.SendConfirmedEmail(id,dispatch,sender,default);await Service.SendConfirmedEmail(id,dispatch,sender,default);Assert.Equal(1,sender.Calls);
        Assert.DoesNotContain(authorization.ToString(),OrcamentoJson.Write(await Service.PendingEmailDispatches(null,default)));
        Context(2);quote=await Act(id,"nova_revisao","Novo pedido");Assert.False(quote.EmailSource!.Confirmed);Assert.Equal(2,quote.Header.Revision);
        Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.SendConfirmedEmail(id,new(quote.Header.Version,1,Guid.NewGuid()),sender,default))).Status);
        await using var verify=await factory.OpenConnectionAsync();Assert.Equal(1,await verify.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ORCAMENTOEMAIL"));Assert.Equal(1,await verify.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ORCAMENTO"));
    }
    [FirebirdPublicationFact]
    public async Task AdditiveEmailSourceScriptIsRepeatableAndPreservesExistingQuote()
    {
        var id=await Service.Create(new(1,OrcamentoTests.Draft(OrcamentoTests.Item())),default);var before=OrcamentoJson.Write((await Service.Get(id,default)).Snapshot);
        await using(var c=await factory.OpenConnectionAsync())await c.ExecuteAsync("DROP TABLE ORCAMENTOEMAIL");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>OrcamentoSchema.ValidateAsync(factory));
        var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null&&!System.IO.File.Exists(Path.Combine(dir.FullName,"docs/sql/scripts/add-orcamentoemail.sql")))dir=dir.Parent;
        Assert.NotNull(dir);var sql=System.IO.File.ReadAllText(Path.Combine(dir.FullName,"docs/sql/scripts/add-orcamentoemail.sql"));
        var block=Regex.Match(sql,@"EXECUTE BLOCK AS[\s\S]*?END\^").Value.TrimEnd('^');
        await using(var c=await factory.OpenConnectionAsync()){await c.ExecuteAsync(block);await c.ExecuteAsync(block);}
        await OrcamentoSchema.MigrateAsync(factory);await OrcamentoSchema.MigrateAsync(factory);Assert.Equal(before,OrcamentoJson.Write((await Service.Get(id,default)).Snapshot));
    }
    [FirebirdPublicationFact]
    public async Task LimitedBotHttpPermissionsCannotApproveConfirmOrReadCommercialSnapshots()
    {
        await using(var c=await factory.OpenConnectionAsync())foreach(var key in new[]{"orcamentos.importar_email","orcamentos.enviar_email_confirmado"})await c.ExecuteAsync("INSERT INTO USUARIO_PERMISSAO(CODUSUARIO,CHAVE,MODO) VALUES(4,@key,'PERMITIR')",new{key});
        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IHttpContextAccessor>(accessor);builder.Services.AddSingleton<IFirebirdConnectionFactory>(factory);builder.Services.AddSingleton(TimeProvider.System);builder.Services.AddScoped<OrcamentoService>();builder.Services.AddScoped<AuditWriter>();builder.Services.AddPermissionsModule();builder.Services.AddSingleton<IQuoteEmailSender>(new FakeQuoteMail());
        builder.Services.AddAuthentication("BudgetTest").AddScheme<AuthenticationSchemeOptions,TestAuthentication>("BudgetTest",_=>{});builder.Services.AddAuthorization();builder.Services.AddControllers().AddApplicationPart(typeof(OrcamentosController).Assembly);
        await using var app=builder.Build();app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.MapControllers();await app.StartAsync();
        try
        {
            using var client=new HttpClient{BaseAddress=new(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/web/orcamentos/integracoes/emails/envios-pendentes")).StatusCode);
            client.DefaultRequestHeaders.Add("X-Test-User","4");var result=await client.PostAsJsonAsync("/api/web/orcamentos/integracoes/emails/importar",EmailImport());result.EnsureSuccessStatusCode();var body=await result.Content.ReadFromJsonAsync<JsonElement>();var id=body.GetProperty("data").GetProperty("id").GetString();
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync($"/api/web/orcamentos/integracoes/emails/{id}/resumo")).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/web/orcamentos/integracoes/emails/envios-pendentes")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/api/web/orcamentos/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/api/web/orcamentos/{id}/proposta.pdf")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync($"/api/web/orcamentos/{id}/origem-email/confirmar",new ConfirmQuoteEmailRequest("1",1,"client@example.com",true,"Bot não pode confirmar"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync($"/api/web/orcamentos/{id}/origem-email/autorizar-envio",new AuthorizeQuoteEmailRequest("1",1,"client@example.com"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync($"/api/web/orcamentos/{id}/transicoes",new QuoteCommand("1","aprovar"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync($"/api/web/orcamentos/{id}/proposta/enviar-email",new SendQuoteEmailRequest("1",1,"evil@example.com",Guid.NewGuid()))).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/web/orcamentos/{id}/proposta/enviar-email-confirmado",new DispatchQuoteEmailRequest("1",1,Guid.NewGuid()))).StatusCode);
        }
        finally{await app.StopAsync();}
    }
}

public sealed class OrcamentoEmailSourceTests
{
    [Fact]
    public void SourceIdentityUsesMailboxAndStableInternetIdNotSubjectOrGraphId()
    {
        Assert.Equal(QuoteEmailSourceValidation.Key("TRIAGEM@example.com","<a@example.com>"),QuoteEmailSourceValidation.Key("triagem@example.com","<a@example.com>"));
        Assert.NotEqual(QuoteEmailSourceValidation.Key("triagem@example.com","<a@example.com>"),QuoteEmailSourceValidation.Key("triagem@example.com","<b@example.com>"));
        Assert.Throws<OrcamentoException>(()=>QuoteEmailSourceValidation.Key("triagem@example.com","bad\r\nid"));
    }
}
