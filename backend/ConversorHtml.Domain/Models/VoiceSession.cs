using ConversorHtml.Domain.Enums;

namespace ConversorHtml.Domain.Models;

public class VoiceSession
{
    public Guid Id { get; set; }
    public string OwnerUserId { get; init; } = string.Empty;
    public VoiceSessionMode Mode { get; set; }
    public string CamposScriptJson { get; set; } = "{\"camposScript\":[]}";
    public List<VoiceTranscriptEntry> TranscriptHistory { get; set; } = [];
    public string? SourceFileName { get; set; }
    public string? ImageBase64 { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class VoiceTranscriptEntry
{
    public DateTime At { get; set; }
    public string Transcript { get; set; } = string.Empty;
    public VoiceSttSource SttSource { get; set; }
    public string Summary { get; set; } = string.Empty;
}
