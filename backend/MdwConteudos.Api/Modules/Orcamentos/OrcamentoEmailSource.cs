using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Dapper;

namespace MdwConteudos.Api.Modules.Orcamentos;

public sealed record QuoteEmailOrigin(string Mailbox, string MessageId, string InternetMessageId, string? ConversationId, string From, string? ReplyTo, string Subject, DateTimeOffset ReceivedAt);
public sealed record ImportQuoteEmailRequest(int ClientId, string Title, string Scope, QuoteEmailOrigin Origin);
public sealed record FindQuoteEmailRequest(string Mailbox, string InternetMessageId);
public sealed record ConfirmQuoteEmailRequest(string Version, int ClientId, string Recipient, bool ReviewedScope, string Reason);
public sealed record AuthorizeQuoteEmailRequest(string Version, int Revision, string Recipient);
public sealed record DispatchQuoteEmailRequest(string Version, int Revision, Guid RequestId);
public sealed record RevokeQuoteEmailRequest(string Version, Guid RequestId, string Reason);
public sealed record QuoteEmailConfirmation(int Revision, int ClientId, string Recipient, string ScopeHash, int ActorId, string ActorName, string ConfirmedAtUtc);
public sealed record QuoteEmailSourceDocument(QuoteEmailOrigin Origin, string ImportHash, int ImportedBy, QuoteEmailConfirmation? Confirmation = null);
public sealed record QuoteEmailSourceView(QuoteEmailOrigin Origin, int ImportedBy, QuoteEmailConfirmation? Confirmation, bool Confirmed);
public sealed record ImportQuoteEmailResult(string Id, bool Created);

public static class QuoteEmailSourceValidation
{
    public static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(OrcamentoJson.Write(value)))).ToLowerInvariant();
    private static string Identifier(string? value, string label, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max || value.Any(char.IsControl)) throw new OrcamentoException(422, $"{label}: identificador obrigatório e sem caracteres de controle (até {max}).");
        return value.Trim();
    }
    public static string Key(string mailbox, string internetMessageId) => Hash(new[] { QuoteEmailSender.Recipient(mailbox).ToLowerInvariant(), Identifier(internetMessageId, "InternetMessageId", 998) });
    public static ImportQuoteEmailRequest Normalize(ImportQuoteEmailRequest request)
    {
        if (request.Origin is null) throw new OrcamentoException(422, "Origem da mensagem obrigatória.");
        QuoteCalculator.CheckText(request.Title,"Título",200); QuoteCalculator.CheckText(request.Scope,"Escopo",4000);
        if (string.IsNullOrWhiteSpace(request.Title)||string.IsNullOrWhiteSpace(request.Scope)) throw new OrcamentoException(422,"Informe o título e o escopo extraído para revisão humana.");
        var o=request.Origin;
        QuoteCalculator.CheckText(o.Subject,"Assunto",500);
        if(o.ReceivedAt==default)throw new OrcamentoException(422,"Informe a data de recebimento com fuso horário.");
        return request with {Title=request.Title.Trim(),Scope=request.Scope.Trim(),Origin=o with {
            Mailbox=QuoteEmailSender.Recipient(o.Mailbox).ToLowerInvariant(), MessageId=Identifier(o.MessageId,"MessageId",2048),
            InternetMessageId=Identifier(o.InternetMessageId,"InternetMessageId",998),
            ConversationId=string.IsNullOrWhiteSpace(o.ConversationId)?null:Identifier(o.ConversationId,"ConversationId",2048),
            From=QuoteEmailSender.Recipient(o.From),ReplyTo=string.IsNullOrWhiteSpace(o.ReplyTo)?null:QuoteEmailSender.Recipient(o.ReplyTo),ReceivedAt=o.ReceivedAt.ToUniversalTime()
        }};
    }
}

public sealed partial class OrcamentoService
{
    private static async Task<QuoteEmailSourceDocument?> ReadEmailSource(DbConnection c,long id,DbTransaction? tx,CancellationToken ct)
    {
        var json=await c.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT DOCUMENTO FROM ORCAMENTOEMAIL WHERE CODORCAMENTO=@id",new{id},tx,cancellationToken:ct));
        return json is null?null:OrcamentoJson.Read<QuoteEmailSourceDocument>(json);
    }
    private static async Task<string> ScopeHash(DbConnection c,Head h,DbTransaction? tx,CancellationToken ct)
    {
        var hashes=(await c.QueryAsync<string>(new CommandDefinition("SELECT SHA256 FROM ORCAMENTOANEXO WHERE CODORCAMENTO=@id AND REVISAO=@revision AND TIPO='escopo' ORDER BY CODORCAMENTOANEXO",new{id=h.Id,revision=h.Revision},tx,cancellationToken:ct))).ToList();
        return QuoteEmailSourceValidation.Hash(new {h.ClientId,h.Snapshot.Draft.Title,h.Snapshot.Draft.Request,Attachments=hashes});
    }
    private static async Task<QuoteEmailSourceView?> EmailSourceFor(DbConnection c,Head h,DbTransaction? tx,CancellationToken ct)
    {
        var source=await ReadEmailSource(c,h.Id,tx,ct);if(source is null)return null;
        var confirmation=source.Confirmation;
        return new(source.Origin,source.ImportedBy,confirmation,confirmation is not null && confirmation.Revision==h.Revision && confirmation.ClientId==h.ClientId && confirmation.ScopeHash==await ScopeHash(c,h,tx,ct));
    }
    private static async Task<QuoteEmailSourceView?> RequireConfirmedEmailSource(DbConnection c,Head h,DbTransaction tx,CancellationToken ct)
    {
        var source=await EmailSourceFor(c,h,tx,ct);
        if(source is {Confirmed:false})throw new OrcamentoException(409,"Confirme o cliente, o escopo e o destinatário da origem do e-mail nesta revisão antes de avançar.");
        return source;
    }
    public async Task<ImportQuoteEmailResult> ImportEmail(ImportQuoteEmailRequest input,CancellationToken ct)
    {
        var request=QuoteEmailSourceValidation.Normalize(input);
        var key=QuoteEmailSourceValidation.Key(request.Origin.Mailbox,request.Origin.InternetMessageId);
        // MessageId/ConversationId can change after a move; InternetMessageId + mailbox is the dedup identity.
        var hash=QuoteEmailSourceValidation.Hash(new{request.ClientId,request.Title,request.Scope,request.Origin.Mailbox,request.Origin.InternetMessageId,request.Origin.From,request.Origin.ReplyTo,request.Origin.Subject,request.Origin.ReceivedAt});
        await using var c=await db.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);var actor=await Actor(c,tx,ct);
        var existing=await c.QuerySingleOrDefaultAsync<EmailImportRow>(new CommandDefinition("SELECT CODORCAMENTO AS Id,DOCUMENTO AS Document FROM ORCAMENTOEMAIL WHERE CHAVEORIGEM=@key",new{key},tx,cancellationToken:ct));
        if(existing is not null)
        {
            if(OrcamentoJson.Read<QuoteEmailSourceDocument>(existing.Document).ImportedBy!=actor.Id)throw new OrcamentoException(403,"Esta mensagem já foi importada por outra conta. Solicite revisão à gestão.");
            if(OrcamentoJson.Read<QuoteEmailSourceDocument>(existing.Document).ImportHash!=hash)throw new OrcamentoException(409,$"Esta mensagem já está vinculada ao orçamento #{existing.Id}. A nova importação tem dados diferentes; revise o registro existente.");
            return new(existing.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),false);
        }
        var id=await CreateCore(c,tx,new(request.ClientId,new QuoteDraft{Title=request.Title,Request=request.Scope}),actor,ct);
        await c.ExecuteAsync(new CommandDefinition("INSERT INTO ORCAMENTOEMAIL(CODORCAMENTO,CHAVEORIGEM,DOCUMENTO,DTHRCRIACAO) VALUES(@id,@key,@document,@now)",new{id,key,document=QuoteTextBlob.Json(new QuoteEmailSourceDocument(request.Origin,hash,actor.Id)),now=Now},tx,cancellationToken:ct));
        var h=await ReadHead(c,id,tx,ct);await Event(c,tx,h,"importar_email",h.Status,actor,null,ct);await tx.CommitAsync(ct);
        return new(id.ToString(System.Globalization.CultureInfo.InvariantCulture),true);
    }
    public async Task<string> FindEmail(FindQuoteEmailRequest request,CancellationToken ct)
    {
        var key=QuoteEmailSourceValidation.Key(request.Mailbox,request.InternetMessageId);
        await using var c=await db.OpenConnectionAsync(ct);var actor=await Actor(c,null,ct);
        var id=await c.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("SELECT CODORCAMENTO FROM ORCAMENTOEMAIL WHERE CHAVEORIGEM=@key",new{key},cancellationToken:ct));
        if(id is null)throw new OrcamentoException(404,"Mensagem ainda não importada.");
        if((await ReadEmailSource(c,id.Value,null,ct))!.ImportedBy!=actor.Id)throw new OrcamentoException(403,"Mensagem importada por outra conta.");
        return id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    public async Task ConfirmEmailSource(long id,ConfirmQuoteEmailRequest request,CancellationToken ct)
    {
        if(!request.ReviewedScope)throw new OrcamentoException(422,"Confirme a revisão humana do escopo recebido.");
        QuoteCalculator.CheckText(request.Reason,"Motivo",1000);if(string.IsNullOrWhiteSpace(request.Reason))throw new OrcamentoException(422,"Registre o motivo da confirmação do cliente e destinatário.");
        var recipient=QuoteEmailSender.Recipient(request.Recipient);
        await using var c=await db.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);var actor=await Actor(c,tx,ct);var h=await ReadHead(c,id,tx,ct);
        if(!OrcamentoWorkflow.Editable(h.Status))throw new OrcamentoException(409,"Confirme ou altere a origem apenas em uma revisão editável.");
        var source=await ReadEmailSource(c,id,tx,ct)??throw new OrcamentoException(404,"Orçamento sem origem por e-mail.");
        // Different permission from import; bot accounts must not receive confirmar_email.
        var client=await Client(c,request.ClientId,tx,ct);await Bump(c,tx,h,request.Version,Now,ct);
        var previous=h.Snapshot;var snapshot=previous with {Client=client,Calculation=QuoteCalculator.Calculate(previous.Draft,previous.Policy,client.Commercial)};
        await c.ExecuteAsync(new CommandDefinition("UPDATE ORCAMENTO SET CODCLIENTE=@client WHERE CODORCAMENTO=@id",new{id,client=client.Id},tx,cancellationToken:ct));
        await c.ExecuteAsync(new CommandDefinition("UPDATE ORCAMENTOREVISAO SET DOCUMENTO=@json WHERE CODORCAMENTO=@id AND NUMERO=@revision",new{id,revision=h.Revision,json=QuoteTextBlob.Json(snapshot)},tx,cancellationToken:ct));
        h.ClientId=client.Id;h.Document=OrcamentoJson.Write(snapshot);
        var confirmation=new QuoteEmailConfirmation(h.Revision,client.Id,recipient,await ScopeHash(c,h,tx,ct),actor.Id,actor.Name,QuoteCalculator.Utc(Now));
        await c.ExecuteAsync(new CommandDefinition("UPDATE ORCAMENTOEMAIL SET DOCUMENTO=@json WHERE CODORCAMENTO=@id",new{id,json=QuoteTextBlob.Json(source with {Confirmation=confirmation})},tx,cancellationToken:ct));
        await c.ExecuteAsync(new CommandDefinition("UPDATE OR INSERT INTO ORCAMENTO_USUARIO(CODORCAMENTO,REVISAO,CODUSUARIO) VALUES(@id,@revision,@actor) MATCHING(CODORCAMENTO,REVISAO,CODUSUARIO)",new{id,revision=h.Revision,actor=actor.Id},tx,cancellationToken:ct));
        await WriteItems(c,tx,id,h.Revision,snapshot,ct);await Event(c,tx,h,"confirmar_email",h.Status,actor,new(request.Version,"confirmar_email",request.Reason,"",recipient),ct);await tx.CommitAsync(ct);
    }
    public async Task<object> EmailImportSummary(long id,CancellationToken ct)
    {
        await using var c=await db.OpenConnectionAsync(ct);var actor=await Actor(c,null,ct);var h=await ReadHead(c,id,ct:ct);var source=await ReadEmailSource(c,id,null,ct);
        if(source is null||source.ImportedBy!=actor.Id)throw new OrcamentoException(403,"Consulte somente solicitações importadas por esta conta.");
        return new{id=h.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),version=h.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),revision=h.Revision,status=h.Status,clientId=h.ClientId,confirmed=(await EmailSourceFor(c,h,null,ct))!.Confirmed};
    }
    private sealed class EmailImportRow {public long Id{get;set;}public string Document{get;set;}="";}
}
