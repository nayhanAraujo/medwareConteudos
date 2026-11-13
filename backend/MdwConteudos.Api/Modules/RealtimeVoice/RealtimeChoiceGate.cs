namespace MdwConteudos.Api.Modules.RealtimeVoice;

// A search and its human decision are one barrier. Queued tools cannot guess a candidate.
public sealed class RealtimeChoiceGate
{
    private readonly object _gate = new();
    private readonly HashSet<string> _searches = [];
    private long _epoch;
    private string? _token;
    public bool HasPending { get { lock (_gate) return _token is not null; } }
    public long Queue(string callId, string name)
    {
        lock (_gate) { if (name == "buscar_medidas") _searches.Add(callId); return _epoch; }
    }
    public bool CanExecute(long epoch, string name)
    {
        lock (_gate)
            return name == "consultar_rascunho" || (epoch == _epoch && _token is null && (name == "buscar_medidas" || _searches.Count == 0));
    }
    public bool RequiresDecision(long epoch) { lock (_gate) return _token is not null || epoch != _epoch; }
    public bool Offer(long epoch, string token)
    {
        lock (_gate)
        {
            if (_token is not null || epoch != _epoch) return false;
            _token = token; _epoch++; return true;
        }
    }
    public void EndSearch(string callId) { lock (_gate) _searches.Remove(callId); }
    public bool Complete(string token)
    {
        lock (_gate)
        {
            if (_token != token) return false;
            _token = null; _epoch++; return true;
        }
    }
}
