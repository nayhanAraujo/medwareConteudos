using ConversorHtml.Application.Configuration;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Domain.Enums;
using ConversorHtml.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConversorHtml.Application.Services;

public class CursorVoiceLaudoService : IVoiceLaudoService
{
    private readonly IOptions<CursorOptions> _cursorOptions;
    private readonly IOptions<VoiceOptions> _voiceOptions;
    private readonly ILogger<CursorVoiceLaudoService> _logger;
    private readonly MockVoiceLaudoService _mockFallback = new();

    public CursorVoiceLaudoService(
        IOptions<CursorOptions> cursorOptions,
        IOptions<VoiceOptions> voiceOptions,
        ILogger<CursorVoiceLaudoService> logger)
    {
        _cursorOptions = cursorOptions;
        _voiceOptions = voiceOptions;
        _logger = logger;
    }

    private bool HasCursorApiKey()
    {
        var key = _cursorOptions.Value.ApiKey?.Trim();
        if (!string.IsNullOrWhiteSpace(key)) return true;
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CURSOR_API_KEY"));
    }

    public async Task<string> BootstrapFromImageAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".png";

        var tempImagePath = Path.Combine(Path.GetTempPath(), $"voice-bootstrap-{Guid.NewGuid():N}{extension}");
        var mimeType = GuessMimeType(extension);

        try
        {
            await using (var fs = File.Create(tempImagePath))
            {
                await imageStream.CopyToAsync(fs, cancellationToken);
            }

            var response = await VoiceBridgeRunner.RunJsonAsync<BootstrapResponse>(
                _cursorOptions,
                _voiceOptions,
                _logger,
                "bootstrap",
                new { imagePath = tempImagePath, mimeType, fileName },
                cancellationToken);

            if (string.IsNullOrWhiteSpace(response.CamposScriptJson))
            {
                throw new InvalidOperationException("Bootstrap não retornou camposScript.");
            }

            return response.CamposScriptJson;
        }
        finally
        {
            try
            {
                if (File.Exists(tempImagePath)) File.Delete(tempImagePath);
            }
            catch
            {
                // ignore
            }
        }
    }

    public async Task<VoiceApplyResult> ApplyUtteranceAsync(
        VoiceSession session,
        string transcript,
        VoiceUtteranceIntent intent = VoiceUtteranceIntent.Build,
        CancellationToken cancellationToken = default)
    {
        if (!HasCursorApiKey())
        {
            _logger.LogWarning("CURSOR_API_KEY ausente — gerando modelo com heurística Mock até a chave ser configurada.");
            return await _mockFallback.ApplyUtteranceAsync(session, transcript, intent, cancellationToken);
        }

        try
        {
            var response = await VoiceBridgeRunner.RunJsonAsync<ApplyResponse>(
                _cursorOptions,
                _voiceOptions,
                _logger,
                "apply",
                new
                {
                    transcript,
                    camposScriptJson = session.CamposScriptJson,
                    mode = session.Mode.ToString(),
                    sourceFileName = session.SourceFileName,
                    intent = intent.ToString().ToLowerInvariant()
                },
                cancellationToken);

            var json = response.CamposScriptJson ?? session.CamposScriptJson;
            var isEmpty = string.IsNullOrWhiteSpace(json) || json.Contains("\"camposScript\":[]") || json.Contains("\"camposScript\": []");
            if (intent == VoiceUtteranceIntent.Build && isEmpty)
            {
                _logger.LogWarning("Agente Cursor não retornou campos — usando Mock para montar o modelo.");
                return await _mockFallback.ApplyUtteranceAsync(session, transcript, intent, cancellationToken);
            }

            return new VoiceApplyResult(
                json,
                response.Summary ?? (intent == VoiceUtteranceIntent.Build ? "Modelo criado pelo agente." : "Alterações aplicadas."),
                response.Warnings ?? []);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha no agente Cursor — gerando modelo com Mock.");
            return await _mockFallback.ApplyUtteranceAsync(session, transcript, intent, cancellationToken);
        }
    }

    public async Task<VoiceExportResult> ExportAsync(VoiceSession session, ConversionOutputFormat format, CancellationToken cancellationToken = default)
    {
        var formatArg = format == ConversionOutputFormat.ModoTexto ? "modoTexto" : "html";
        var response = await VoiceBridgeRunner.RunJsonAsync<ExportResponse>(
            _cursorOptions,
            _voiceOptions,
            _logger,
            "export",
            new { camposScriptJson = session.CamposScriptJson, format = formatArg },
            cancellationToken);

        if (string.IsNullOrWhiteSpace(response.Content))
        {
            throw new InvalidOperationException("Exportação não retornou conteúdo.");
        }

        return new VoiceExportResult(response.Content, format);
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

    private sealed class BootstrapResponse
    {
        public string? CamposScriptJson { get; set; }
    }

    private sealed class ApplyResponse
    {
        public string? CamposScriptJson { get; set; }
        public string? Summary { get; set; }
        public List<string>? Warnings { get; set; }
    }

    private sealed class ExportResponse
    {
        public string? Content { get; set; }
        public string? Format { get; set; }
    }
}
