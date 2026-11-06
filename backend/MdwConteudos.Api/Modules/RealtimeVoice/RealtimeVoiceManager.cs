using System.Collections.Concurrent;
using System.Security.Claims;
using ConversorHtml.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Modules.FormulasModelos;
using MdwConteudos.Api.Modules.Permissions;

namespace MdwConteudos.Api.Modules.RealtimeVoice;

public sealed class RealtimeVoiceManager(
    IOptions<RealtimeVoiceOptions> options, IVoiceSessionStore store, IServiceScopeFactory scopes,
    IHubContext<RealtimeVoiceHub> hub, IHostApplicationLifetime lifetime, ILoggerFactory logs)
{
    private readonly ConcurrentDictionary<string, RealtimeVoiceSession> _connections = new();
    private readonly ConcurrentDictionary<Guid, string> _drafts = new();
    private readonly SemaphoreSlim _startGate = new(1);
    public bool IsActive(Guid id) => _drafts.ContainsKey(id);

    public static async Task<int> AuthorizeAsync(IServiceProvider services, ClaimsPrincipal user, bool save, CancellationToken ct)
    {
        if (user.Identity?.IsAuthenticated != true || !int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            throw new HubException("Faça login novamente.");
        var permissions = services.GetRequiredService<IPermissionService>();
        foreach (var (domain, action) in save
                     ? new[] { ("studio", "visualizar"), ("studio", "voz"), (PermissionDomains.Formulas, PermissionActions.Editar) }
                     : new[] { ("studio", "visualizar"), ("studio", "voz") })
            if (!await permissions.HasPermissionAsync(id, user.FindFirstValue(ClaimTypes.Role) ?? "", domain, action, ct))
                throw new HubException("Usuário sem permissão para esta ação.");
        return id;
    }

    public async Task StartAsync(string connection, ClaimsPrincipal user, RealtimeStart request)
    {
        var config = options.Value;
        if (!config.Enabled) throw new HubException("Voz em tempo real desabilitada no servidor.");
        var key = string.IsNullOrWhiteSpace(config.ApiKey) ? Environment.GetEnvironmentVariable("XAI_API_KEY") : config.ApiKey;
        if (string.IsNullOrWhiteSpace(key)) throw new HubException("A integração xAI não está configurada no servidor.");
        await using var scope = scopes.CreateAsyncScope();
        var owner = await AuthorizeAsync(scope.ServiceProvider, user, false, default);
        var draft = store.Get(request.SessionId);
        if (request.RascunhoId is { } persistentId)
        {
            if (draft is not null) throw new HubException("Identificador de sessão já utilizado. Conecte novamente com um novo identificador.");
            await scope.ServiceProvider.GetRequiredService<MdwConteudos.Api.Modules.StudioDrafts.StudioDraftService>().Get(persistentId.ToString(), owner, default);
            draft = store.Create(new ConversorHtml.Domain.Models.VoiceSession { Id = request.SessionId, OwnerUserId = owner.ToString(), CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(config.MaxSessionMinutes), CamposScriptJson = "{\"camposScript\":[]}" });
        }
        if (draft is null || draft.OwnerUserId != owner.ToString()) throw new HubException("Rascunho não encontrado ou expirado.");
        if (request.ModeloId is { } model && await scope.ServiceProvider.GetRequiredService<IFormulasModelosService>().GetModeloAsync(model, owner, default) is null)
            throw new HubException("Modelo de destino não encontrado.");
        RealtimeVoiceSession session;
        await _startGate.WaitAsync();
        try
        {
            if (_connections.ContainsKey(connection) || _drafts.ContainsKey(request.RascunhoId ?? draft.Id)) throw new HubException("O rascunho já está em uma sessão de tempo real.");
            if (_connections.Count >= Math.Clamp(config.MaxSessions, 1, 100)) throw new HubException("Limite de sessões atingido. Tente novamente mais tarde.");
            session = new RealtimeVoiceSession(connection, user, draft, request.ModeloId, key, config, store, scopes,
                (name, payload) => hub.Clients.Client(connection).SendAsync(name, payload), lifetime.ApplicationStopping,
                logs.CreateLogger<RealtimeVoiceSession>(), ended: ended =>
                {
                    _connections.TryRemove(new KeyValuePair<string, RealtimeVoiceSession>(connection, ended));
                    _drafts.TryRemove(new KeyValuePair<Guid, string>(request.RascunhoId ?? ended.Id, connection));
                }, persistentId: request.RascunhoId);
            _connections[connection] = session; _drafts[request.RascunhoId ?? draft.Id] = connection;
        }
        finally { _startGate.Release(); }
        try { await session.StartAsync(); }
        catch { await StopAsync(connection); throw new HubException("Não foi possível iniciar a sessão xAI. Consulte o código de sessão nos logs."); }
    }

    public Task AppendAsync(string connection, RealtimeAudio request)
    {
        var session = Get(connection, request.SessionId);
        session.Append(request);
        return Task.CompletedTask;
    }
    public Task SetContextAsync(string connection, RealtimeSelection selection)
    {
        if (!_connections.TryGetValue(connection, out var session)) throw new HubException("Sessão encerrada.");
        session.SetContext(selection); return Task.CompletedTask;
    }

    public Task ConfirmAsync(string connection, ClaimsPrincipal user, RealtimeConfirmation request)
    {
        // Enqueue only: a database transaction must never block AppendAudio hub invocations.
        Get(connection, request.SessionId).QueueConfirmation(user, request);
        return Task.CompletedTask;
    }

    private RealtimeVoiceSession Get(string connection, Guid id)
    {
        if (!_connections.TryGetValue(connection, out var s) || s.Id != id || s.IsStopped)
            throw new HubException("Sessão encerrada. O rascunho foi preservado; inicie novamente.");
        return s;
    }

    public async Task StopAsync(string connection)
    {
        if (_connections.TryGetValue(connection, out var session))
        {
            await session.StopAsync();
            if (_connections.TryRemove(new KeyValuePair<string, RealtimeVoiceSession>(connection, session))) _drafts.TryRemove(session.Id, out _);
        }
    }
}
