using ConversorHtml.Application;
using ConversorHtml.Application.Dtos;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Domain.Enums;
using ConversorHtml.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MdwConteudos.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/voice")]
public class VoiceSessionsController : ControllerBase
{
    private static readonly string[] AllowedImageTypes =
    [
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/bmp", "image/gif"
    ];

    private static readonly string[] AllowedAudioTypes =
    [
        "audio/webm", "audio/wav", "audio/x-wav", "audio/mpeg", "audio/mp3", "audio/ogg"
    ];

    private const long MaxImageSize = 10 * 1024 * 1024;
    private const long MaxAudioSize = 25 * 1024 * 1024;

    private readonly IVoiceSessionStore _store;
    private readonly IVoiceLaudoService _voiceService;
    private readonly ITranscriptionService _transcriptionService;
    private readonly ILaudosUxHtmlValidator _htmlValidator;
    private readonly ILaudosUxModoTextoValidator _modoTextoValidator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VoiceSessionsController> _logger;

    public VoiceSessionsController(
        IVoiceSessionStore store,
        IVoiceLaudoService voiceService,
        ITranscriptionService transcriptionService,
        ILaudosUxHtmlValidator htmlValidator,
        ILaudosUxModoTextoValidator modoTextoValidator,
        IConfiguration configuration,
        ILogger<VoiceSessionsController> logger)
    {
        _store = store;
        _voiceService = voiceService;
        _transcriptionService = transcriptionService;
        _htmlValidator = htmlValidator;
        _modoTextoValidator = modoTextoValidator;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("sessions")]
    [RequestSizeLimit(MaxImageSize)]
    public async Task<ActionResult<VoiceSessionResponseDto>> CreateSession(
        [FromForm] string? mode,
        IFormFile? image,
        CancellationToken cancellationToken)
    {
        _store.RemoveExpired();

        var sessionMode = Enum.TryParse<VoiceSessionMode>(mode, ignoreCase: true, out var parsed)
            ? parsed
            : VoiceSessionMode.FromScratch;

        if (sessionMode == VoiceSessionMode.FromImage && (image is null || image.Length == 0))
        {
            return BadRequest(new { message = "Modo FromImage requer upload de imagem." });
        }

        var ttlMinutes = _configuration.GetValue("Voice:SessionTtlMinutes", 120);
        var now = DateTime.UtcNow;
        var session = new VoiceSession
        {
            Id = Guid.NewGuid(),
            Mode = sessionMode,
            CamposScriptJson = "{\"camposScript\":[]}",
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now.AddMinutes(ttlMinutes)
        };

        if (sessionMode == VoiceSessionMode.FromImage && image is not null)
        {
            if (image.Length > MaxImageSize)
            {
                return BadRequest(new { message = "Imagem excede 10MB." });
            }

            if (!AllowedImageTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = $"Tipo de imagem não suportado: {image.ContentType}" });
            }

            session.SourceFileName = image.FileName;
            await using var stream = image.OpenReadStream();
            session.CamposScriptJson = await _voiceService.BootstrapFromImageAsync(stream, image.FileName, cancellationToken);
        }

        _store.Create(session);
        return Ok(MapSession(session));
    }

    [HttpGet("sessions/{id:guid}")]
    public ActionResult<VoiceSessionResponseDto> GetSession(Guid id)
    {
        _store.RemoveExpired();
        var session = _store.Get(id);
        if (session is null)
        {
            return NotFound(new { message = "Sessão não encontrada ou expirada." });
        }

        return Ok(MapSession(session));
    }

    [HttpPost("sessions/{id:guid}/utterance")]
    public async Task<ActionResult<VoiceUtteranceResponseDto>> ApplyUtterance(
        Guid id,
        [FromBody] VoiceUtteranceRequestDto request,
        CancellationToken cancellationToken)
    {
        _store.RemoveExpired();
        var session = _store.Get(id);
        if (session is null)
        {
            return NotFound(new { message = "Sessão não encontrada ou expirada." });
        }

        if (string.IsNullOrWhiteSpace(request.Transcript))
        {
            return BadRequest(new { message = "Transcript vazio." });
        }

        try
        {
            var intent = ResolveUtteranceIntent(request.Intent, session);
            var result = await _voiceService.ApplyUtteranceAsync(
                session,
                request.Transcript.Trim(),
                intent,
                cancellationToken);
            session.CamposScriptJson = result.CamposScriptJson;
            session.TranscriptHistory.Add(new VoiceTranscriptEntry
            {
                At = DateTime.UtcNow,
                Transcript = request.Transcript.Trim(),
                SttSource = request.SttSource,
                Summary = result.Summary
            });
            _store.Update(session);

            return Ok(new VoiceUtteranceResponseDto
            {
                CamposScriptJson = result.CamposScriptJson,
                Summary = result.Summary,
                Warnings = result.Warnings.ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao aplicar utterance na sessão {SessionId}", id);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    [HttpPost("sessions/{id:guid}/generate")]
    public async Task<ActionResult<ConversionResponseDto>> Generate(
        Guid id,
        [FromBody] VoiceGenerateRequestDto request,
        CancellationToken cancellationToken)
    {
        _store.RemoveExpired();
        var session = _store.Get(id);
        if (session is null)
        {
            return NotFound(new { message = "Sessão não encontrada ou expirada." });
        }

        var format = ConversionFormatParser.Parse(request.Format);

        try
        {
            var exported = await _voiceService.ExportAsync(session, format, cancellationToken);
            var validation = format == ConversionOutputFormat.ModoTexto
                ? _modoTextoValidator.Validate(exported.Content)
                : _htmlValidator.Validate(exported.Content);

            var response = new ConversionResponseDto
            {
                Format = format,
                SourceFileName = session.SourceFileName ?? "laudo-voz",
                ConvertedAt = DateTime.UtcNow,
                Provider = _voiceService.GetType().Name.Replace("VoiceLaudoService", "", StringComparison.Ordinal),
                Validation = new ValidationResponseDto
                {
                    IsValid = validation.IsValid,
                    Errors = validation.Errors,
                    Warnings = validation.Warnings
                }
            };

            if (format == ConversionOutputFormat.ModoTexto)
            {
                response.Text = exported.Content;
            }
            else
            {
                response.Html = exported.Content;
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao gerar laudo da sessão {SessionId}", id);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    [HttpPost("transcribe")]
    [RequestSizeLimit(MaxAudioSize)]
    public async Task<ActionResult<TranscribeResponseDto>> Transcribe(IFormFile? audio, CancellationToken cancellationToken)
    {
        if (audio is null || audio.Length == 0)
        {
            return BadRequest(new { message = "Áudio não enviado." });
        }

        if (audio.Length > MaxAudioSize)
        {
            return BadRequest(new { message = "Áudio excede 25MB." });
        }

        if (!AllowedAudioTypes.Contains(audio.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = $"Tipo de áudio não suportado: {audio.ContentType}" });
        }

        try
        {
            await using var stream = audio.OpenReadStream();
            var transcript = await _transcriptionService.TranscribeAsync(stream, audio.ContentType, cancellationToken);
            return Ok(new TranscribeResponseDto { Transcript = transcript });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }

    private static VoiceUtteranceIntent ResolveUtteranceIntent(VoiceUtteranceIntent requested, VoiceSession session)
    {
        // Do zero (sem imagem/bootstrap): sempre criar modelo, nunca editar vazio
        if (IsEmptyModel(session.CamposScriptJson))
        {
            return VoiceUtteranceIntent.Build;
        }

        if (session.Mode == VoiceSessionMode.FromScratch && IsEmptyModel(session.CamposScriptJson))
        {
            return VoiceUtteranceIntent.Build;
        }

        return requested;
    }

    private static bool IsEmptyModel(string camposScriptJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(camposScriptJson);
            if (!doc.RootElement.TryGetProperty("camposScript", out var arr) || arr.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return true;
            }

            return arr.GetArrayLength() == 0;
        }
        catch
        {
            return true;
        }
    }

    private static VoiceSessionResponseDto MapSession(VoiceSession session) => new()
    {
        Id = session.Id,
        Mode = session.Mode,
        CamposScriptJson = session.CamposScriptJson,
        SourceFileName = session.SourceFileName,
        CreatedAt = session.CreatedAt,
        UpdatedAt = session.UpdatedAt,
        TranscriptHistory = session.TranscriptHistory.Select(e => new VoiceTranscriptEntryDto
        {
            At = e.At,
            Transcript = e.Transcript,
            SttSource = e.SttSource,
            Summary = e.Summary
        }).ToList()
    };
}
