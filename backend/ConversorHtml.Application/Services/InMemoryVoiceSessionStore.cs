using System.Collections.Concurrent;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Domain.Models;

namespace ConversorHtml.Application.Services;

public class InMemoryVoiceSessionStore : IVoiceSessionStore
{
    private readonly ConcurrentDictionary<Guid, VoiceSession> _sessions = new();

    public VoiceSession Create(VoiceSession session)
    {
        _sessions[session.Id] = session;
        return session;
    }

    public VoiceSession? Get(Guid id)
    {
        if (_sessions.TryGetValue(id, out var session) && session.ExpiresAt > DateTime.UtcNow)
        {
            return session;
        }

        _sessions.TryRemove(id, out _);
        return null;
    }

    public VoiceSession Update(VoiceSession session)
    {
        session.UpdatedAt = DateTime.UtcNow;
        _sessions[session.Id] = session;
        return session;
    }

    public void RemoveExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in _sessions)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                _sessions.TryRemove(pair.Key, out _);
            }
        }
    }
}
