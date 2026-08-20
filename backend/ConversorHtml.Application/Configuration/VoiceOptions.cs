namespace ConversorHtml.Application.Configuration;

public class VoiceOptions
{
    public const string SectionName = "Voice";

    public int SessionTtlMinutes { get; set; } = 120;
    public string Provider { get; set; } = "Cursor";
    public string TranscriptionProvider { get; set; } = "Browser";
    public string VoiceBridgeScriptPath { get; set; } = "agent-bridge/voice.mjs";
    public string? OpenAiApiKey { get; set; }
    public string WhisperModel { get; set; } = "whisper-1";
}
