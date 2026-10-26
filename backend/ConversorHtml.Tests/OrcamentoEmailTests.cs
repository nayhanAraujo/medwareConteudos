using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MdwConteudos.Api.Modules.Orcamentos;

namespace ConversorHtml.Tests;

public sealed class OrcamentoEmailTests
{
    [Theory]
    [InlineData("")][InlineData("sem-email")][InlineData("a@example.com,b@example.com")]
    [InlineData("Nome <a@example.com>")][InlineData("a@example.com\r\nBcc: b@example.com")]
    public void InvalidRecipientsCannotInjectHeadersOrMultipleAddresses(string value) => Assert.Throws<OrcamentoException>(() => QuoteEmailSender.Recipient(value));
    [Fact]
    public void PublicMessageContainsOnlyFrozenBodyAndPdf()
    {
        var projection=QuoteProposal.From("1",1,OrcamentoTests.Snapshot(),false);
        var pdf=projection.Pdf(); var proposal=new OrcamentoService.PreparedProposal(projection,projection.Email(),pdf,false);
        using var message=QuoteEmailSender.Message("sender@example.com","client@example.com",proposal);
        Assert.Single(message.To);Assert.Equal("client@example.com",message.To[0].Address);
        Assert.Empty(message.CC);Assert.Empty(message.Bcc);Assert.False(message.IsBodyHtml);
        Assert.Equal(projection.Email(),message.Body);Assert.Equal("utf-8",message.BodyEncoding!.WebName);
        var attachment=Assert.Single(message.Attachments);Assert.Equal("orcamento-1-r1.pdf",attachment.Name);
        using var output=new MemoryStream();attachment.ContentStream.CopyTo(output);Assert.Equal(pdf,output.ToArray());
        Assert.Throws<OrcamentoException>(()=>QuoteEmailSender.Message("sender@example.com","client@example.com",proposal with {Superseded=true}));
    }
    [Fact]
    public void HomologacaoAlwaysBlocksSmtpBeforeAnyConnection()
    {
        var sender=new QuoteEmailSender(new ConfigurationBuilder().Build(),new TestEnvironment());
        Assert.Equal(409,Assert.Throws<OrcamentoException>(sender.EnsureAvailable).Status);
    }
    private sealed class TestEnvironment:IHostEnvironment
    {
        public string EnvironmentName {get;set;}="Homologacao";
        public string ApplicationName {get;set;}="tests";
        public string ContentRootPath {get;set;}="";
        public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();
    }
}

public sealed partial class OrcamentoFirebirdTests
{
    [FirebirdPublicationFact]
    public async Task EmailRequiresCurrentPreparedApprovedProposalAndRecordsIdempotentOutcomes()
    {
        var sender=new FakeQuoteMail();
        var id=await Service.Create(new(1,OrcamentoTests.Draft(OrcamentoTests.Item())),default);
        var quote=await Service.Get(id,default);
        Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.SendProposalEmail(id,new(quote.Header.Version,1,"client@example.com",Guid.NewGuid()),sender,default))).Status);
        Assert.Equal(0,sender.Calls);
        await Act(id,"solicitar_aprovacao");Context(2);quote=await Act(id,"aprovar");
        await Service.PrepareProposal(id,quote.Header.Version,default);quote=await Service.Get(id,default);
        var request=new SendQuoteEmailRequest(quote.Header.Version,1,"client@example.com",Guid.NewGuid());
        await Service.SendProposalEmail(id,request,sender,default);
        await Service.SendProposalEmail(id,request,sender,default);Assert.Equal(1,sender.Calls);
        quote=await Service.Get(id,default);Assert.Equal("aprovado",quote.Header.Status);
        Assert.Contains(quote.Events,e=>e.Action=="email_enviado"&&e.Destination=="client@example.com");
        Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.SendProposalEmail(id,request with {Recipient="other@example.com"},sender,default))).Status);
        Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.SendProposalEmail(id,request with {RequestId=Guid.NewGuid()},sender,default))).Status);
        sender.Fail=true;request=new(quote.Header.Version,1,"other@example.com",Guid.NewGuid());
        Assert.Equal(502,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.SendProposalEmail(id,request,sender,default))).Status);
        Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.SendProposalEmail(id,request,sender,default))).Status);
        Assert.Equal(2,sender.Calls);quote=await Service.Get(id,default);
        Assert.Contains(quote.Events,e=>e.Action=="email_incerto");
        Assert.DoesNotContain(quote.Events,e=>e.Reason.Contains("TEST_SMTP_SECRET"));
        await Act(id,"nova_revisao","Novo escopo");quote=await Service.Get(id,default);
        Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.SendProposalEmail(id,new(quote.Header.Version,1,"client@example.com",Guid.NewGuid()),sender,default))).Status);
    }
    [FirebirdPublicationFact]
    public async Task InFlightEmailBlocksAnotherRequestEvenWithRefreshedVersion()
    {
        var id=await Service.Create(new(1,OrcamentoTests.Draft(OrcamentoTests.Item())),default);
        await Act(id,"solicitar_aprovacao");Context(2);var quote=await Act(id,"aprovar");
        await Service.PrepareProposal(id,quote.Header.Version,default);quote=await Service.Get(id,default);
        var sender=new FakeQuoteMail { Hold=new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var request=new SendQuoteEmailRequest(quote.Header.Version,1,"client@example.com",Guid.NewGuid());
        var first=Service.SendProposalEmail(id,request,sender,default);
        try
        {
            await sender.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            quote=await Service.Get(id,default);
            Assert.Equal(409,(await Assert.ThrowsAsync<OrcamentoException>(()=>Service.SendProposalEmail(id,request with {Version=quote.Header.Version,RequestId=Guid.NewGuid()},sender,default))).Status);
            Assert.Equal(1,sender.Calls);
        }
        finally { sender.Hold.SetResult(); await first; }
    }
    private sealed class FakeQuoteMail:IQuoteEmailSender
    {
        public int Calls;public bool Fail;
        public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? Hold;
        public void EnsureAvailable(){}
        public async Task SendAsync(string recipient,OrcamentoService.PreparedProposal proposal,CancellationToken ct)
        {Calls++;Started.TrySetResult();if(Hold is not null)await Hold.Task;if(Fail)throw new System.Net.Mail.SmtpException("TEST_SMTP_SECRET");}
    }
}
