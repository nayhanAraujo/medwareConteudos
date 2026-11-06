using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace MdwConteudos.Api.Modules.RealtimeVoice;

public interface IRealtimeVoiceTransport : IDisposable
{
    Task ConnectAsync(Uri uri, CancellationToken ct);
    Task SendAsync(string json, CancellationToken ct);
    Task<JsonDocument?> ReceiveAsync(CancellationToken ct);
    void Abort();
}

public sealed class XaiVoiceTransport : IRealtimeVoiceTransport
{
    private readonly ClientWebSocket _socket = new();
    public XaiVoiceTransport(string key) => _socket.Options.SetRequestHeader("Authorization", $"Bearer {key}");
    public Task ConnectAsync(Uri uri, CancellationToken ct) => _socket.ConnectAsync(uri, ct);
    public Task SendAsync(string json, CancellationToken ct)
        => _socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, ct);
    public async Task<JsonDocument?> ReceiveAsync(CancellationToken ct)
    {
        var buffer = new byte[16384];
        using var stream = new MemoryStream();
        WebSocketReceiveResult frame;
        do
        {
            frame = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (frame.MessageType == WebSocketMessageType.Close) return null;
            if (frame.MessageType != WebSocketMessageType.Text || stream.Length + frame.Count > 1024 * 1024)
                throw new InvalidOperationException("Invalid provider frame.");
            stream.Write(buffer, 0, frame.Count);
        } while (!frame.EndOfMessage);
        return JsonDocument.Parse(stream.ToArray());
    }
    public void Abort() => _socket.Abort();
    public void Dispose() => _socket.Dispose();
}
