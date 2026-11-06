using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Application.Services;
using ConversorHtml.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Modules.FormulasModelos;
using MdwConteudos.Api.Modules.Permissions;
using MdwConteudos.Api.Modules.RealtimeVoice;

namespace ConversorHtml.Tests;

public class RealtimeVoiceProtocolTests
{
    [Fact]
    public async Task AnotherUsersDraftIsRejectedBeforeOpeningProviderConnection()
    {
        await using var fixture = new Fixture();
        var store = new InMemoryVoiceSessionStore();
        var other = store.Create(new VoiceSession { Id = Guid.NewGuid(), OwnerUserId = "2", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        var manager = new RealtimeVoiceManager(Options.Create(new RealtimeVoiceOptions { Enabled = true, ApiKey = "test-only" }), store,
            fixture.Scopes, null!, null!, NullLoggerFactory.Instance);
        await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() => manager.StartAsync("connection", fixture.User, new RealtimeStart(other.Id)));
        Assert.False(manager.IsActive(other.Id));
    }

    [Fact]
    public async Task SlowParallelToolsDoNotBlockAudioAndContinueOnlyAfterAllOutputs()
    {
        await using var fixture = new Fixture();
        var query = new TaskCompletionSource<IReadOnlyList<VariavelOpcao>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Service.Query = _ => query.Task;
        await fixture.Start();
        fixture.Call("a", "buscar_medidas", "{\"busca\":\"peso\"}");
        fixture.Call("b", "consultar_rascunho", "{}");
        fixture.Done();
        await fixture.Wait(() => fixture.Wire.Outputs >= 1);
        fixture.Session.Append(new RealtimeAudio(fixture.Stored.Id, 0, Convert.ToBase64String(new byte[1920])));
        await fixture.Wait(() => fixture.Wire.Sent.Any(e => e.GetProperty("type").GetString() == "input_audio_buffer.append"));
        Assert.Equal(0, fixture.Wire.Continuations);
        query.SetResult([]);
        await fixture.Wait(() => fixture.Wire.Continuations == 1);
        Assert.Equal(2, fixture.Wire.Outputs);
        fixture.Call("a", "buscar_medidas", "{\"busca\":\"peso\"}");
        await Task.Delay(30);
        Assert.Equal(2, fixture.Wire.Outputs);
    }

    [Fact]
    public async Task SaveProposalRequiresSeparateConfirmationAndIsIdempotent()
    {
        await using var fixture = new Fixture();
        await fixture.Start();
        fixture.Call("save", "preparar_salvamento", "{\"nome\":\"Meu laudo\"}"); fixture.Done();
        await fixture.Wait(() => fixture.Events.Any(e => e.Name == "SaveProposed"));
        Assert.Equal(0, fixture.Service.Saves);
        var proposal = fixture.Events.First(e => e.Name == "SaveProposed").Payload;
        var confirmation = new RealtimeConfirmation(fixture.Stored.Id, proposal.GetProperty("proposalId").GetString()!, proposal.GetProperty("revision").GetInt64());
        await fixture.Session.ConfirmAsync(fixture.User, confirmation);
        await fixture.Session.ConfirmAsync(fixture.User, confirmation);
        Assert.Equal(1, fixture.Service.Saves);
    }

    [Fact]
    public async Task RevokedPermissionPreventsMutation()
    {
        await using var fixture = new Fixture(); await fixture.Start();
        fixture.Permissions.Allowed = false;
        var original = fixture.Stored.CamposScriptJson;
        fixture.Call("create", "criar_secao", "{\"grupoOperacao\":\"x\",\"nome\":\"Outro\",\"coluna\":1,\"ordem\":2,\"substituirOperacao\":false}");
        fixture.Done();
        await fixture.Wait(() => fixture.Wire.Outputs == 1);
        Assert.Equal(original, fixture.Stored.CamposScriptJson);
        Assert.Contains(fixture.Events, e => e.Name == "ToolFailed");
    }

    public class PermissionsProxy : DispatchProxy
    {
        public bool Allowed = true;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "HasPermissionAsync" ? Task.FromResult(Allowed) : throw new NotSupportedException();
    }
    public class ModelsProxy : DispatchProxy
    {
        public Func<CancellationToken, Task<IReadOnlyList<VariavelOpcao>>> Query = _ => Task.FromResult<IReadOnlyList<VariavelOpcao>>([]);
        public int Saves;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "ListVariaveisAsync") return Query((CancellationToken)args![0]!);
            if (method.Name == "PreviewComposicaoAsync") return Task.FromResult("[Dados]\nPeso (PESO): 0 kg");
            if (method.Name == "SaveComposicaoAsync") { Saves++; return Task.FromResult(new ModeloDetalhe(1, "Laudo", [])); }
            throw new NotSupportedException(method.Name);
        }
    }
    private sealed class Wire : IRealtimeVoiceTransport
    {
        private readonly Channel<string> _incoming = Channel.CreateUnbounded<string>();
        public readonly ConcurrentQueue<JsonElement> Sent = new();
        public int Outputs => Sent.Count(e => e.GetProperty("type").GetString() == "conversation.item.create");
        public int Continuations => Sent.Count(e => e.GetProperty("type").GetString() == "response.create");
        public void Push(object e) => _incoming.Writer.TryWrite(JsonSerializer.Serialize(e));
        public Task ConnectAsync(Uri uri, CancellationToken ct) => Task.CompletedTask;
        public Task SendAsync(string json, CancellationToken ct) { Sent.Enqueue(JsonSerializer.Deserialize<JsonElement>(json)); return Task.CompletedTask; }
        public async Task<JsonDocument?> ReceiveAsync(CancellationToken ct) => JsonDocument.Parse(await _incoming.Reader.ReadAsync(ct));
        public void Abort() { }
        public void Dispose() { }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public readonly Wire Wire = new();
        public readonly ConcurrentQueue<(string Name, JsonElement Payload)> Events = new();
        public readonly ModelsProxy Service;
        public readonly PermissionsProxy Permissions;
        public readonly RealtimeVoiceSession Session;
        public readonly ClaimsPrincipal User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "Bearer"));
        public readonly VoiceSession Stored;
        private readonly ServiceProvider _provider;
        public IServiceScopeFactory Scopes => _provider.GetRequiredService<IServiceScopeFactory>();
        public Fixture()
        {
            var models = DispatchProxy.Create<IFormulasModelosService, ModelsProxy>(); Service = (ModelsProxy)models;
            var permissions = DispatchProxy.Create<IPermissionService, PermissionsProxy>(); Permissions = (PermissionsProxy)permissions;
            var services = new ServiceCollection().AddSingleton(models).AddSingleton(permissions);
            _provider = services.BuildServiceProvider();
            IVoiceSessionStore store = new InMemoryVoiceSessionStore();
            Stored = store.Create(new VoiceSession { Id = Guid.NewGuid(), OwnerUserId = "1", ExpiresAt = DateTime.UtcNow.AddHours(1),
                CamposScriptJson = """{"camposScript":[{"id":1,"tipo":"etiqueta","nome":"DADOS","descricao":"Dados","coluna":1,"ordem":0},{"id":2,"tipo":"numero","nome":"PESO","codVariavel":1,"etiquetaPai":"DADOS","ordem":0}]}""" });
            Session = new RealtimeVoiceSession("connection", User, Stored, 1, "test-only", new RealtimeVoiceOptions(), store,
                _provider.GetRequiredService<IServiceScopeFactory>(), (name, payload) => { Events.Enqueue((name, JsonSerializer.SerializeToElement(payload))); return Task.CompletedTask; },
                default, NullLogger.Instance, Wire);
        }
        public async Task Start()
        {
            Wire.Push(new { type = "session.created" });
            Wire.Push(new { type = "session.updated", session = new { modalities = new[] { "text" }, turn_detection = new { type = "server_vad", silence_duration_ms = 400 } } });
            await Session.StartAsync();
        }
        public void Call(string id, string name, string args) => Wire.Push(new { type = "response.function_call_arguments.done", call_id = id, response_id = "r1", name, arguments = args });
        public void Done() => Wire.Push(new { type = "response.done", response = new { id = "r1", status = "completed" } });
        public async Task Wait(Func<bool> predicate)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            while (!predicate()) await Task.Delay(5, timeout.Token);
        }
        public async ValueTask DisposeAsync() { await Session.StopAsync(); await _provider.DisposeAsync(); }
    }
}
