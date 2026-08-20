using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ConversorHtml.Application.Configuration;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConversorHtml.Application.Services;

public class CursorComposerImageToHtmlConverter : IImageConversionConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CursorOptions _options;
    private readonly ILogger<CursorComposerImageToHtmlConverter> _logger;

    public CursorComposerImageToHtmlConverter(
        IOptions<CursorOptions> options,
        ILogger<CursorComposerImageToHtmlConverter> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> ConvertAsync(
        Stream imageStream,
        string fileName,
        ConversionOutputFormat format,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = Environment.GetEnvironmentVariable("CURSOR_API_KEY")?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Cursor API key não configurada. Defina Cursor:ApiKey via User Secrets ou variável CURSOR_API_KEY.");
        }

        var bridgePath = ResolveBridgeScriptPath();
        if (!File.Exists(bridgePath))
        {
            throw new FileNotFoundException(
                $"Bridge Node não encontrado em '{bridgePath}'. Execute npm install em backend/agent-bridge.",
                bridgePath);
        }

        var sdkPath = Path.Combine(Path.GetDirectoryName(bridgePath)!, "node_modules", "@cursor", "sdk");
        if (!Directory.Exists(sdkPath))
        {
            throw new FileNotFoundException(
                $"Pacote @cursor/sdk não encontrado em '{sdkPath}'. Execute npm install em backend/agent-bridge.",
                sdkPath);
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        var tempImagePath = Path.Combine(Path.GetTempPath(), $"conversor-{Guid.NewGuid():N}{extension}");
        var mimeType = GuessMimeType(extension);

        try
        {
            await using (var fs = File.Create(tempImagePath))
            {
                await imageStream.CopyToAsync(fs, cancellationToken);
            }

            var repoRoot = ResolveRepoRoot(bridgePath);
            var timeout = TimeSpan.FromSeconds(Math.Max(30, _options.TimeoutSeconds));

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            var formatArg = format == ConversionOutputFormat.ModoTexto ? "modoTexto" : "html";

            var (exitCode, stdout, stderr) = await RunNodeBridgeAsync(
                bridgePath,
                tempImagePath,
                mimeType,
                formatArg,
                repoRoot,
                apiKey,
                cts.Token);

            if (exitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                _logger.LogError("Bridge Node falhou (exit {ExitCode}): {Detail}", exitCode, detail);

                if (exitCode == 3 || detail.Contains("AUTH_ERROR", StringComparison.OrdinalIgnoreCase)
                    || detail.Contains("Invalid User API Key", StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException(
                        "Chave da API Cursor inválida. Gere uma User API Key em https://cursor.com/dashboard/integrations " +
                        "e configure com: dotnet user-secrets set \"Cursor:ApiKey\" \"<nova-chave>\". " +
                        "Reinicie a API em ambiente Development.");
                }

                throw new InvalidOperationException(
                    $"Falha na conversão via Composer 2.5 (exit {exitCode}): {Truncate(detail, 500)}");
            }

            if (string.IsNullOrWhiteSpace(stdout))
            {
                throw new InvalidOperationException("Bridge Node retornou saída vazia.");
            }

            BridgeResponse? response;
            try
            {
                response = JsonSerializer.Deserialize<BridgeResponse>(stdout, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Bridge Node retornou JSON inválido: {Truncate(stdout, 300)}", ex);
            }

            if (format == ConversionOutputFormat.ModoTexto)
            {
                if (string.IsNullOrWhiteSpace(response?.Text))
                {
                    throw new InvalidOperationException("Resposta do agente sem TXT modo texto.");
                }

                _logger.LogInformation(
                    "Conversão Cursor (modo texto) concluída. RunId={RunId}, Model={Model}, TextLength={Length}",
                    response.RunId,
                    response.Model ?? _options.Model,
                    response.Text.Length);

                return response.Text;
            }

            if (string.IsNullOrWhiteSpace(response?.Html))
            {
                throw new InvalidOperationException("Resposta do agente sem HTML.");
            }

            _logger.LogInformation(
                "Conversão Cursor concluída. RunId={RunId}, Model={Model}, HtmlLength={Length}",
                response.RunId,
                response.Model ?? _options.Model,
                response.Html.Length);

            return response.Html;
        }
        finally
        {
            try
            {
                if (File.Exists(tempImagePath))
                {
                    File.Delete(tempImagePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Não foi possível remover arquivo temporário {Path}", tempImagePath);
            }
        }
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunNodeBridgeAsync(
        string bridgePath,
        string imagePath,
        string mimeType,
        string formatArg,
        string repoRoot,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"\"{bridgePath}\" \"{imagePath}\" \"{mimeType}\" \"{formatArg}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(bridgePath) ?? repoRoot
        };

        startInfo.Environment["CURSOR_OUTPUT_FORMAT"] = formatArg;
        startInfo.Environment["CURSOR_API_KEY"] = apiKey;
        startInfo.Environment["CURSOR_MODEL"] = _options.Model;
        startInfo.Environment["CURSOR_USE_FAST"] = _options.UseFast ? "true" : "false";
        startInfo.Environment["CURSOR_REPO_ROOT"] = repoRoot;

        using var process = new Process { StartInfo = startInfo };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) stderr.AppendLine(e.Data);
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Não foi possível iniciar o processo Node. Verifique se o Node.js está no PATH.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignore kill failures
            }

            throw new TimeoutException(
                $"Conversão via Composer excedeu o timeout de {_options.TimeoutSeconds}s.");
        }

        return (process.ExitCode, stdout.ToString().Trim(), stderr.ToString().Trim());
    }

    private string ResolveBridgeScriptPath()
    {
        var configured = _options.BridgeScriptPath;
        if (Path.IsPathRooted(configured) && File.Exists(configured))
        {
            return configured;
        }

        // Prefer relative to ContentRoot when running from MdwConteudos.Api (bridge lives in backend/)
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", configured)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", configured)),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configured)),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", configured)),
            Path.GetFullPath(configured)
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", configured));
    }

    private string ResolveRepoRoot(string bridgePath)
    {
        if (!string.IsNullOrWhiteSpace(_options.RepoRoot) && Directory.Exists(_options.RepoRoot))
        {
            return Path.GetFullPath(_options.RepoRoot);
        }

        // bridge is backend/agent-bridge/convert.mjs → agent cwd is backend/ (manual_scripts)
        var agentBridgeDir = Path.GetDirectoryName(bridgePath)!;
        return Path.GetFullPath(Path.Combine(agentBridgeDir, ".."));
    }

    private static string GuessMimeType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        _ => "image/png"
    };

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "...";

    private sealed class BridgeResponse
    {
        public string? Format { get; set; }
        public string Html { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? RunId { get; set; }
        public string? Model { get; set; }
    }
}
