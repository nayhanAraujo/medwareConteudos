namespace MdwConteudos.Api.Modules.RealtimeVoice;

public sealed class RealtimeVoiceOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "grok-voice-latest";
    public int SilenceDurationMs { get; set; } = 400;
    public int MaxSessions { get; set; } = 20;
    public int MaxSessionMinutes { get; set; } = 20;
}

public sealed record RealtimeStart(Guid SessionId, int? ModeloId = null);
public sealed record RealtimeAudio(Guid SessionId, long Sequence, string Audio);
public sealed record RealtimeConfirmation(Guid SessionId, string ProposalId, long Revision);
