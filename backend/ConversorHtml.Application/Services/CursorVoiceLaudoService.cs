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

    public CursorVoiceLaudoService(
        IOptions<CursorOptions> cursorOptions,
        IOptions<VoiceOptions> voiceOptions,
        ILogger<CursorVoiceLaudoService> logger)
    {
        _cursorOptions = cursorOptions;
        _voiceOptions = voiceOptions;
        _logger = logger;
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
        // Falhas do agente devem chegar ao controlador sem recriar o laudo com heurísticas.
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

        if (string.IsNullOrWhiteSpace(response.CamposScriptJson))
        {
            throw new InvalidOperationException("O agente não retornou um modelo válido.");
        }

        return new VoiceApplyResult(
            response.CamposScriptJson,
            response.Summary ?? (intent == VoiceUtteranceIntent.Build ? "Modelo criado pelo agente." : "Alterações aplicadas."),
            response.Warnings ?? []);
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
