using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MdwConteudos.Api.Modules.Auth;

namespace ConversorHtml.Tests;

[CollectionDefinition("RecoverySmtp", DisableParallelization = true)]
public sealed class RecoverySmtpCollection;

[Collection("RecoverySmtp")]
public sealed class RecoveryEmailSenderTests
{
    [Fact]
    public async Task DeliversRecoveryLinkToLocalStartTlsSmtpServer()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        // Schannel needs a temporary imported key rather than an ephemeral RSA key.
        using var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.UserKeySet);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var receive = ReceiveAsync(listener, certificate, timeout.Token);
        var settings = new Dictionary<string, string?> {
            ["SMTP_SERVER"] = "127.0.0.1", ["SMTP_PORT"] = ((IPEndPoint)listener.LocalEndpoint).Port.ToString(),
            ["SMTP_USERNAME"] = "sender@example.test", ["SMTP_PASSWORD"] = "test-only-password", ["SMTP_SENDER"] = "sender@example.test"
        };
        var previous = settings.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        var previousValidation = ServicePointManager.ServerCertificateValidationCallback;
        try
        {
            foreach (var setting in settings) Environment.SetEnvironmentVariable(setting.Key, setting.Value);
            // Only trust this test's exact ephemeral certificate, only during this isolated test.
            ServicePointManager.ServerCertificateValidationCallback = (_, cert, _, _) => cert?.GetCertHashString() == certificate.Thumbprint;
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["PasswordRecovery:FrontendUrl"] = "https://frontend.example.test"
            }).Build();
            var sender = new RecoveryEmailSender(config, new TestEnvironment());
            var token = new string('a', 64);
            try { await sender.SendAsync("recipient@example.test", token, timeout.Token); }
            catch { await receive; throw; }
            var message = await receive;
            var parts = message.Split("\r\n\r\n", 2);
            var body = parts[0].Contains("base64", StringComparison.OrdinalIgnoreCase)
                ? Encoding.UTF8.GetString(Convert.FromBase64String(parts[1])) : parts[1].Replace("=\r\n", "");
            Assert.Contains("https://frontend.example.test/reset-password#token=" + token, body);
            Assert.Contains("30 minutos", body);
            Assert.Contains("recipient@example.test", parts[0]);
        }
        finally
        {
            ServicePointManager.ServerCertificateValidationCallback = previousValidation;
            foreach (var setting in previous) Environment.SetEnvironmentVariable(setting.Key, setting.Value);
            timeout.Cancel();
        }
    }

    [Theory]
    [InlineData("Homologacao", "https://frontend.example.test")]
    [InlineData("Production", "http://frontend.example.test")]
    [InlineData("Development", "https://frontend.example.test?redirect=evil")]
    public async Task RejectsBlockedEnvironmentOrUntrustedLinkConfiguration(string environment, string url)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PasswordRecovery:FrontendUrl"] = url }).Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RecoveryEmailSender(config, new TestEnvironment { EnvironmentName = environment }).SendAsync("recipient@example.test", "token", default));
    }

    private static async Task<string> ReceiveAsync(TcpListener listener, X509Certificate2 certificate, CancellationToken ct)
    {
        using var client = await listener.AcceptTcpClientAsync(ct);
        Stream stream = client.GetStream();
        StreamReader Reader() => new(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
        StreamWriter Writer() => new(stream, Encoding.ASCII, 1024, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };
        using var initialReader = Reader();
        using var initialWriter = Writer();
        await initialWriter.WriteLineAsync("220 localhost SMTP test");
        Assert.StartsWith("EHLO", await initialReader.ReadLineAsync(ct));
        await initialWriter.WriteLineAsync("250-localhost\r\n250 STARTTLS");
        Assert.Equal("STARTTLS", await initialReader.ReadLineAsync(ct));
        await initialWriter.WriteLineAsync("220 Ready for TLS");
        using var tls = new SslStream(stream, leaveInnerStreamOpen: true);
        await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate, EnabledSslProtocols = SslProtocols.Tls12 }, ct);
        stream = tls;
        using var reader = Reader();
        using var writer = Writer();
        while (await reader.ReadLineAsync(ct) is { } command)
        {
            if (command.StartsWith("EHLO")) await writer.WriteLineAsync("250-localhost\r\n250 AUTH LOGIN");
            else if (command.StartsWith("AUTH")) await writer.WriteLineAsync("235 Authentication successful");
            else if (command == "DATA")
            {
                await writer.WriteLineAsync("354 Send message");
                var message = new StringBuilder();
                while (await reader.ReadLineAsync(ct) is { } line && line != ".") message.Append(line).Append("\r\n");
                await writer.WriteLineAsync("250 Accepted");
                return message.ToString();
            }
            else await writer.WriteLineAsync("250 OK");
        }
        throw new InvalidOperationException("SMTP session ended before DATA.");
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
