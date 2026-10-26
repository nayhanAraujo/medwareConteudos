using Dapper;

namespace MdwConteudos.Api.Modules.Orcamentos;

public sealed partial class OrcamentoService
{
    public async Task SendProposalEmail(long id, SendQuoteEmailRequest request, IQuoteEmailSender sender, CancellationToken ct, bool requireAuthorization = false)
    {
        var recipient = QuoteEmailSender.Recipient(request.Recipient);
        if (request.RequestId == Guid.Empty) throw new OrcamentoException(422, "Identificador de envio obrigatório.");
        sender.EnsureAvailable();
        var reference = $"email:{request.RequestId:D}";
        Head h; (int Id, string Name) actor; PreparedProposal proposal;
        await using (var c = await db.OpenConnectionAsync(ct))
        await using (var tx = await c.BeginTransactionAsync(ct))
        {
            actor = await Actor(c, tx, ct); h = await ReadHead(c, id, tx, ct);
            var previous = (await c.QueryAsync<EmailEvent>(new CommandDefinition("SELECT ACAO AS Action,DESTINO AS Destination,REVISAO AS Revision FROM ORCAMENTOEVENTO WHERE CODORCAMENTO=@id AND REFERENCIA=@reference AND ACAO IN ('email_iniciado','email_enviado','email_incerto')", new { id, reference }, tx, cancellationToken: ct))).ToList();
            if (previous.Count > 0)
            {
                if (previous.Any(e => e.Destination != recipient || e.Revision != request.Revision)) throw new OrcamentoException(409, "Este identificador já pertence a outro envio.");
                if (previous.Any(e => e.Action == "email_enviado")) return;
                throw new OrcamentoException(409, "Este envio já foi iniciado. Confira o histórico; não há repetição automática. Em caso de timeout, verifique o recebimento antes de reenviar.");
            }
            if (request.Revision != h.Revision || h.Status is not ("aprovado" or "enviado"))
                throw new OrcamentoException(409, "Envie somente a revisão atual aprovada ou já enviada, antes do aceite do cliente.");
            var source = await RequireConfirmedEmailSource(c, h, tx, ct);
            if (source is not null && source.Confirmation!.Recipient != recipient) throw new OrcamentoException(409, "O destinatário precisa corresponder à confirmação humana da origem do e-mail.");
            if (requireAuthorization) await RequireEmailAuthorization(c, tx, h, reference, recipient, ct);
            else if (source is not null && await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ORCAMENTOEVENTO e WHERE e.CODORCAMENTO=@id AND e.REVISAO=@revision AND e.ACAO='email_autorizado' AND NOT EXISTS(SELECT 1 FROM ORCAMENTOEVENTO n WHERE n.CODORCAMENTO=e.CODORCAMENTO AND n.REVISAO=e.REVISAO AND n.ACAO='email_autorizado' AND n.CODORCAMENTOEVENTO>e.CODORCAMENTOEVENTO) AND NOT EXISTS(SELECT 1 FROM ORCAMENTOEVENTO s WHERE s.CODORCAMENTO=e.CODORCAMENTO AND s.REFERENCIA=e.REFERENCIA AND s.ACAO IN ('email_iniciado','email_revogado'))", new { id, revision = h.Revision }, tx, cancellationToken: ct)) > 0)
                throw new OrcamentoException(409, "Existe uma autorização pendente para o bot. Revogue-a antes de enviar manualmente para evitar duplicidade.");
            QuoteCalculator.Ready(h.Snapshot, DateOnly.FromDateTime(Now));
            var gate = await AnalysisGateFor(c, h, tx, ct);
            if (gate.Processing || gate.AwaitingClassification || gate.AwaitingApplication) throw new OrcamentoException(409, "Resolva a análise pendente antes de enviar a proposta.");
            if (await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ORCAMENTOEVENTO e WHERE e.CODORCAMENTO=@id AND e.ACAO='email_iniciado' AND e.DTHRCRIACAO>@since AND NOT EXISTS(SELECT 1 FROM ORCAMENTOEVENTO f WHERE f.CODORCAMENTO=e.CODORCAMENTO AND f.REFERENCIA=e.REFERENCIA AND f.ACAO IN ('email_enviado','email_incerto'))", new { id, since = Now.AddMinutes(-1) }, tx, cancellationToken: ct)) > 0)
                throw new OrcamentoException(409, "Já existe um envio em andamento neste orçamento. Aguarde e atualize o histórico.");
            var row = await c.QuerySingleOrDefaultAsync<ProposalRow>(new CommandDefinition("SELECT DOCUMENTO AS Document,EMAIL AS Email,PDF AS Pdf FROM ORCAMENTOPROPOSTA WHERE CODORCAMENTO=@id AND REVISAO=@revision", new { id, revision = h.Revision }, tx, cancellationToken: ct))
                ?? throw new OrcamentoException(409, "Prepare a proposta aprovada antes de enviar.");
            proposal = new(OrcamentoJson.Read<QuoteProposal>(row.Document), row.Email, row.Pdf, false);
            await Bump(c, tx, h, request.Version, Now, ct);
            await Event(c, tx, h, "email_iniciado", h.Status, actor, new(request.Version, "email_iniciado", "Envio SMTP solicitado explicitamente; PDF aprovado anexado.", reference, recipient), ct);
            await tx.CommitAsync(ct);
        }
        // SMTP cannot participate in the DB transaction. Persist the attempt BEFORE sending,
        // never retry automatically, and finish audit even if the browser disconnects.
        var accepted = false;
        try { await sender.SendAsync(recipient, proposal, CancellationToken.None); accepted = true; }
        catch (Exception ex) when (ex is System.Net.Mail.SmtpException or OperationCanceledException or InvalidOperationException or OrcamentoException) { }
        using var finishTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using (var c = await db.OpenConnectionAsync(finishTimeout.Token))
        await using (var tx = await c.BeginTransactionAsync(finishTimeout.Token))
        {
            var action = accepted ? "email_enviado" : "email_incerto";
            await Event(c, tx, h, action, h.Status, actor, new(request.Version, action,
                accepted ? "Mensagem aceita pelo servidor SMTP; isso não confirma leitura nem entrega na caixa postal. O marco comercial de envio é registrado separadamente."
                    : "Não foi possível confirmar o envio. Verifique o recebimento antes de reenviar; não houve tentativa automática adicional.", reference, recipient), finishTimeout.Token);
            await tx.CommitAsync(finishTimeout.Token);
        }
        if (!accepted) throw new OrcamentoException(502, "Não foi possível confirmar o envio SMTP. Confira o histórico e o recebimento antes de tentar um novo envio.");
    }
    private sealed class EmailEvent { public string Action { get; set; } = ""; public string Destination { get; set; } = ""; public int Revision { get; set; } }
}
