using System.Net;
using System.Net.Mail;
using System.Text;

namespace MdwConteudos.Api.Modules.Orcamentos;

public interface IQuoteEmailSender
{
    void EnsureAvailable();
    Task SendAsync(string recipient, OrcamentoService.PreparedProposal proposal, CancellationToken ct);
}

public sealed class QuoteEmailSender(IConfiguration config, IHostEnvironment environment) : IQuoteEmailSender
{
    private string Setting(string key) => Environment.GetEnvironmentVariable(key) ?? config[key] ?? "";
    public static string Recipient(string? value)
    {
        var text = value?.Trim() ?? "";
        if (text.Length is 0 or > 254 || !text.Contains('@') || text.Any(c => char.IsControl(c)) || !MailAddress.TryCreate(text, out var address)
            || address.Address != text || text.Contains(',') || text.Contains(';'))
            throw new OrcamentoException(422, "Informe um único endereço de e-mail válido, sem nome de exibição.");
        return text;
    }
    public void EnsureAvailable()
    {
        if (environment.IsEnvironment("Homologacao")) throw new OrcamentoException(409, "Envio de e-mail bloqueado em homologação.");
        if (string.IsNullOrWhiteSpace(Setting("SMTP_SERVER")) || string.IsNullOrWhiteSpace(Setting("SMTP_USERNAME"))
            || string.IsNullOrWhiteSpace(Setting("SMTP_PASSWORD"))) throw new OrcamentoException(503, "Configuração SMTP incompleta no servidor.");
        if (!int.TryParse(Setting("SMTP_PORT"), out var port) || port is < 1 or > 65535)
            throw new OrcamentoException(503, "Configure SMTP_PORT com uma porta válida.");
        try { _ = Recipient(string.IsNullOrWhiteSpace(Setting("SMTP_SENDER")) ? Setting("SMTP_USERNAME") : Setting("SMTP_SENDER")); }
        catch (OrcamentoException) { throw new OrcamentoException(503, "Remetente SMTP inválido no servidor."); }
    }
    public static MailMessage Message(string from, string recipient, OrcamentoService.PreparedProposal proposal)
    {
        if (proposal.Superseded) throw new OrcamentoException(409, "Uma proposta superada não pode ser enviada.");
        var p = proposal.Document;
        // Subject is server-owned and header-safe; the unchanged body is the stored public projection.
        var message = new MailMessage(Recipient(from), Recipient(recipient))
        {
            Subject = $"Medware — Orçamento {p.Id} / revisão {p.Revision}",
            Body = proposal.Email, BodyEncoding = Encoding.UTF8, SubjectEncoding = Encoding.UTF8, IsBodyHtml = false
        };
        message.Attachments.Add(new Attachment(new MemoryStream(proposal.Pdf, writable: false), $"orcamento-{p.Id}-r{p.Revision}.pdf", "application/pdf"));
        return message;
    }
    public async Task SendAsync(string recipient, OrcamentoService.PreparedProposal proposal, CancellationToken ct)
    {
        EnsureAvailable();
        var from = string.IsNullOrWhiteSpace(Setting("SMTP_SENDER")) ? Setting("SMTP_USERNAME") : Setting("SMTP_SENDER");
        using var message = Message(from, recipient, proposal);
        using var smtp = new SmtpClient(Setting("SMTP_SERVER"), int.Parse(Setting("SMTP_PORT")))
        { Credentials = new NetworkCredential(Setting("SMTP_USERNAME"), Setting("SMTP_PASSWORD")), EnableSsl = true, Timeout = 20000 };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        await smtp.SendMailAsync(message, timeout.Token);
    }
}

public sealed record SendQuoteEmailRequest(string Version, int Revision, string Recipient, Guid RequestId);
