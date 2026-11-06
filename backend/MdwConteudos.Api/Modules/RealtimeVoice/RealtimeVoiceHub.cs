using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MdwConteudos.Api.Modules.RealtimeVoice;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class RealtimeVoiceHub(RealtimeVoiceManager sessions) : Hub
{
    public Task StartSession(RealtimeStart request) => sessions.StartAsync(Context.ConnectionId, Context.User!, request);
    public Task AppendAudio(RealtimeAudio request) => sessions.AppendAsync(Context.ConnectionId, request);
    public Task ConfirmSave(RealtimeConfirmation request) => sessions.ConfirmAsync(Context.ConnectionId, Context.User!, request);
    public Task StopSession() => sessions.StopAsync(Context.ConnectionId);
    public override async Task OnDisconnectedAsync(Exception? exception)
    { await sessions.StopAsync(Context.ConnectionId); await base.OnDisconnectedAsync(exception); }
}
