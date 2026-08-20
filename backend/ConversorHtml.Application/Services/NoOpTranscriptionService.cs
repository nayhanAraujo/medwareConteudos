using ConversorHtml.Application.Interfaces;

namespace ConversorHtml.Application.Services;

/// <summary>
/// Placeholder quando STT no servidor não está configurado — use Web Speech no browser.
/// </summary>
public class NoOpTranscriptionService : ITranscriptionService
{
    public Task<string> TranscribeAsync(Stream audioStream, string mimeType, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "Transcrição no servidor não configurada. Use o microfone do navegador ou configure Voice:TranscriptionProvider=Whisper.");
}
