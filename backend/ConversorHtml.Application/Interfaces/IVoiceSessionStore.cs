using ConversorHtml.Domain.Models;

namespace ConversorHtml.Application.Interfaces;

public interface IVoiceSessionStore
{
    VoiceSession Create(VoiceSession session);
    VoiceSession? Get(Guid id);
    VoiceSession Update(VoiceSession session);
    void RemoveExpired();
}
