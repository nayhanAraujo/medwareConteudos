using ConversorHtml.Application.Configuration;
using ConversorHtml.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConversorHtml.Application.Services;

/// <summary>
/// STT via OpenAI Whisper API (opcional). Requer Voice:OpenAiApiKey ou OPENAI_API_KEY.
/// </summary>
public class WhisperTranscriptionService : ITranscriptionService
{
    private readonly VoiceOptions _options;
    private readonly ILogger<WhisperTranscriptionService> _logger;
    private readonly HttpClient _httpClient;

    public WhisperTranscriptionService(
        IOptions<VoiceOptions> options,
        ILogger<WhisperTranscriptionService> logger,
        HttpClient httpClient)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<string> TranscribeAsync(Stream audioStream, string mimeType, CancellationToken cancellationToken = default)
    {
        var apiKey = _options.OpenAiApiKey?.Trim()
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")?.Trim()
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key não configurada para transcrição. Defina Voice:OpenAiApiKey ou OPENAI_API_KEY.");
        }

        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(audioStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
        form.Add(streamContent, "file", $"audio{GuessExtension(mimeType)}");
        form.Add(new StringContent(_options.WhisperModel), "model");
        form.Add(new StringContent("pt"), "language");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions")
        {
            Content = form
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Whisper falhou: {Body}", body);
            throw new InvalidOperationException($"Transcrição falhou: {body}");
        }

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("text").GetString()?.Trim()
               ?? throw new InvalidOperationException("Whisper retornou texto vazio.");
    }

    private static string GuessExtension(string mimeType) => mimeType.ToLowerInvariant() switch
    {
        "audio/wav" or "audio/x-wav" => ".wav",
        "audio/mpeg" or "audio/mp3" => ".mp3",
        "audio/ogg" => ".ogg",
        _ => ".webm"
    };
}
