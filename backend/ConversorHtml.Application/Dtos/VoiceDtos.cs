using ConversorHtml.Domain.Enums;

namespace ConversorHtml.Application.Dtos;

public class CreateVoiceSessionRequestDto
{
    public VoiceSessionMode Mode { get; set; } = VoiceSessionMode.FromScratch;
}

public class VoiceTranscriptEntryDto
{
    public DateTime At { get; set; }
    public string Transcript { get; set; } = string.Empty;
    public VoiceSttSource SttSource { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public class VoiceSessionResponseDto
{
    public Guid Id { get; set; }
    public VoiceSessionMode Mode { get; set; }
    public string CamposScriptJson { get; set; } = string.Empty;
    public List<VoiceTranscriptEntryDto> TranscriptHistory { get; set; } = [];
    public string? SourceFileName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class VoiceUtteranceRequestDto
{
    public string Transcript { get; set; } = string.Empty;
    public VoiceSttSource SttSource { get; set; } = VoiceSttSource.Browser;
    public VoiceUtteranceIntent Intent { get; set; } = VoiceUtteranceIntent.Build;
}

public class VoiceUtteranceResponseDto
{
    public string CamposScriptJson { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = [];
}

public class VoiceGenerateRequestDto
{
    public string? Format { get; set; }
}

public class TranscribeResponseDto
{
    public string Transcript { get; set; } = string.Empty;
}
