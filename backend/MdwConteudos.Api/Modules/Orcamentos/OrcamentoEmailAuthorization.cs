using System.Data.Common;
using Dapper;

namespace MdwConteudos.Api.Modules.Orcamentos;

public sealed partial class OrcamentoService
{
    public async Task<Guid> AuthorizeEmail(long id,AuthorizeQuoteEmailRequest request,CancellationToken ct)
    {
        var recipient=QuoteEmailSender.Recipient(request.Recipient);
        await using var c=await db.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);var actor=await Actor(c,tx,ct);var h=await ReadHead(c,id,tx,ct);
        if(h.Revision!=request.Revision||h.Status is not("aprovado" or "enviado"))throw new OrcamentoException(409,"Autorize apenas a revisão atual aprovada, antes do aceite.");
        var source=await RequireConfirmedEmailSource(c,h,tx,ct)??throw new OrcamentoException(409,"Esta autorização é exclusiva para solicitações importadas por e-mail.");
        if(source.Confirmation!.Recipient!=recipient)throw new OrcamentoException(409,"O destinatário deve corresponder à confirmação humana da origem.");
        QuoteCalculator.Ready(h.Snapshot,DateOnly.FromDateTime(Now));
        var gate=await AnalysisGateFor(c,h,tx,ct);if(gate.Processing||gate.AwaitingClassification||gate.AwaitingApplication)throw new OrcamentoException(409,"Resolva a análise pendente antes de autorizar envio.");
        if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ORCAMENTOPROPOSTA WHERE CODORCAMENTO=@id AND REVISAO=@revision",new{id,revision=h.Revision},tx,cancellationToken:ct))!=1)throw new OrcamentoException(409,"Prepare e confira a proposta antes de autorizar o bot.");
        await Bump(c,tx,h,request.Version,Now,ct);var authorization=Guid.NewGuid();
        await Event(c,tx,h,"email_autorizado",h.Status,actor,new(request.Version,"email_autorizado","Envio único autorizado explicitamente para execução pelo bot.",$"email:{authorization:D}",recipient),ct);
        await tx.CommitAsync(ct);return authorization;
    }
    private static async Task RequireEmailAuthorization(DbConnection c,DbTransaction tx,Head h,string reference,string recipient,CancellationToken ct)
    {
        var latest=await c.QuerySingleOrDefaultAsync<EmailAuthorizationRow>(new CommandDefinition("SELECT FIRST 1 REFERENCIA AS Reference,DESTINO AS Recipient,CODORCAMENTO AS Id,REVISAO AS Revision FROM ORCAMENTOEVENTO WHERE CODORCAMENTO=@id AND REVISAO=@revision AND ACAO='email_autorizado' ORDER BY CODORCAMENTOEVENTO DESC",new{id=h.Id,revision=h.Revision},tx,cancellationToken:ct));
        if(latest is null||latest.Reference!=reference||latest.Recipient!=recipient)throw new OrcamentoException(409,"Não existe autorização humana vigente para este envio. Uma autorização mais recente substitui as anteriores.");
        if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ORCAMENTOEVENTO WHERE CODORCAMENTO=@id AND REFERENCIA=@reference AND ACAO='email_revogado'",new{id=h.Id,reference},tx,cancellationToken:ct))>0)throw new OrcamentoException(409,"Esta autorização de envio foi revogada.");
    }
    public async Task RevokeEmail(long id,RevokeQuoteEmailRequest request,CancellationToken ct)
    {
        QuoteCalculator.CheckText(request.Reason,"Motivo",1000);if(string.IsNullOrWhiteSpace(request.Reason))throw new OrcamentoException(422,"Informe o motivo da revogação.");
        await using var c=await db.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);var actor=await Actor(c,tx,ct);var h=await ReadHead(c,id,tx,ct);
        var reference=$"email:{request.RequestId:D}";
        var recipient=await c.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT DESTINO FROM ORCAMENTOEVENTO WHERE CODORCAMENTO=@id AND REVISAO=@revision AND REFERENCIA=@reference AND ACAO='email_autorizado'",new{id,revision=h.Revision,reference},tx,cancellationToken:ct))??throw new OrcamentoException(404,"Autorização não encontrada nesta revisão.");
        await RequireEmailAuthorization(c,tx,h,reference,recipient,ct);
        if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ORCAMENTOEVENTO WHERE CODORCAMENTO=@id AND REFERENCIA=@reference AND ACAO='email_iniciado'",new{id,reference},tx,cancellationToken:ct))>0)throw new OrcamentoException(409,"O envio já foi iniciado; não é possível revogá-lo. Confira o recebimento.");
        await Bump(c,tx,h,request.Version,Now,ct);await Event(c,tx,h,"email_revogado",h.Status,actor,new(request.Version,"email_revogado",request.Reason,reference,recipient),ct);await tx.CommitAsync(ct);
    }
    public async Task SendConfirmedEmail(long id,DispatchQuoteEmailRequest request,IQuoteEmailSender sender,CancellationToken ct)
    {
        await using var c=await db.OpenConnectionAsync(ct);_=await Actor(c,null,ct);
        var recipient=await c.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT DESTINO FROM ORCAMENTOEVENTO WHERE CODORCAMENTO=@id AND REVISAO=@revision AND ACAO='email_autorizado' AND REFERENCIA=@reference",new{id,revision=request.Revision,reference=$"email:{request.RequestId:D}"},cancellationToken:ct))??throw new OrcamentoException(409,"Autorização de envio não encontrada.");
        await SendProposalEmail(id,new(request.Version,request.Revision,recipient,request.RequestId),sender,ct,requireAuthorization:true);
    }
    public async Task<object> PendingEmailDispatches(long? after,CancellationToken ct)
    {
        await using var c=await db.OpenConnectionAsync(ct);_=await Actor(c,null,ct);
        var rows=(await c.QueryAsync<EmailAuthorizationRow>(new CommandDefinition("""
            SELECT FIRST 51 e.CODORCAMENTO AS Id,e.REVISAO AS Revision,e.REFERENCIA AS Reference,e.DESTINO AS Recipient,o.VERSAO AS Version
            FROM ORCAMENTOEVENTO e JOIN ORCAMENTO o ON o.CODORCAMENTO=e.CODORCAMENTO AND o.REVISAO=e.REVISAO
            WHERE e.ACAO='email_autorizado' AND o.ARQUIVADO=0 AND o.STATUS IN ('aprovado','enviado')
            AND (@after IS NULL OR e.CODORCAMENTO>@after)
            AND NOT EXISTS(SELECT 1 FROM ORCAMENTOEVENTO n WHERE n.CODORCAMENTO=e.CODORCAMENTO AND n.REVISAO=e.REVISAO AND n.ACAO='email_autorizado' AND n.CODORCAMENTOEVENTO>e.CODORCAMENTOEVENTO)
            AND NOT EXISTS(SELECT 1 FROM ORCAMENTOEVENTO s WHERE s.CODORCAMENTO=e.CODORCAMENTO AND s.REFERENCIA=e.REFERENCIA AND s.ACAO IN ('email_iniciado','email_revogado'))
            ORDER BY e.CODORCAMENTO
            """,new{after},cancellationToken:ct))).ToList();
        return new {success=true,data=rows.Take(50).Select(r=>new{id=r.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),r.Revision,version=r.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),r.Recipient,requestId=r.Reference[6..]}),nextAfter=rows.Count>50?rows[49].Id.ToString(System.Globalization.CultureInfo.InvariantCulture):null};
    }
    private sealed class EmailAuthorizationRow {public long Id{get;set;}public int Revision{get;set;}public long Version{get;set;}public string Reference{get;set;}="";public string Recipient{get;set;}="";}
}
