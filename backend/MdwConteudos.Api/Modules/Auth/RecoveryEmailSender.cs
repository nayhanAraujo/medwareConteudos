using System.Net;
using System.Net.Mail;
using System.Text;

namespace MdwConteudos.Api.Modules.Auth;

public sealed class RecoveryEmailSender(IConfiguration config, IHostEnvironment environment) : IRecoveryEmailSender
{
    public async Task SendAsync(string email, string token, CancellationToken ct)
    {
        if (environment.IsEnvironment("Homologacao")) throw new InvalidOperationException("Envio bloqueado em homologação.");
        var frontend = config["PasswordRecovery:FrontendUrl"];
        if (!Uri.TryCreate(frontend, UriKind.Absolute, out var url) ||
            (url.Scheme != "https" && !(environment.IsDevelopment() && url.Scheme == "http" && url.IsLoopback)) ||
            !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment) || !string.IsNullOrEmpty(url.UserInfo))
            throw new InvalidOperationException("Configure PasswordRecovery:FrontendUrl com a URL pública HTTPS.");
        string Setting(string key) => Environment.GetEnvironmentVariable(key) ?? config[key] ?? "";
        var host = Setting("SMTP_SERVER");
        var user = Setting("SMTP_USERNAME");
        var pass = Setting("SMTP_PASSWORD");
        var from = Setting("SMTP_SENDER");
        if (string.IsNullOrWhiteSpace(from)) from = user;
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            throw new InvalidOperationException("Configuração SMTP incompleta.");
        var link = frontend!.TrimEnd('/') + "/reset-password#token=" + Uri.EscapeDataString(token);
        using var message = new MailMessage(from, email)
        {
            Subject = "MDW — Recuperação de senha", BodyEncoding = Encoding.UTF8, SubjectEncoding = Encoding.UTF8,
            Body = $"Recebemos uma solicitação para redefinir sua senha.\n\nAcesse: {link}\n\nO link é válido por 30 minutos e pode ser usado uma única vez. Se você não solicitou a recuperação, ignore esta mensagem."
        };
        using var smtp = new SmtpClient(host, int.TryParse(Setting("SMTP_PORT"), out var port) ? port : 587)
        { Credentials = new NetworkCredential(user, pass), EnableSsl = true, Timeout = 15000 };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await smtp.SendMailAsync(message, timeout.Token);
    }
}
