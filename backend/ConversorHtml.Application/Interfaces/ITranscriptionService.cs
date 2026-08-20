namespace ConversorHtml.Application.Interfaces;

public interface ITranscriptionService
{
    Task<string> TranscribeAsync(Stream audioStream, string mimeType, CancellationToken cancellationToken = default);
}
