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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Modules.FormulasModelos;
using MdwConteudos.Api.Modules.Permissions;
using MdwConteudos.Api.Modules.RealtimeVoice;

namespace ConversorHtml.Tests;

public class RealtimeVoiceProtocolTests
{
    [Fact]
    public async Task StopWhileWaitingForAudioControlAndToolsIsNormalAndIdempotent()
    {
        await using var fixture = new Fixture();
        await fixture.Start();
        var stops = Enumerable.Range(0, 8).Select(_ => Task.Run(() => fixture.Session.StopAsync())).ToArray();
        await Task.WhenAll(stops).WaitAsync(TimeSpan.FromSeconds(3));
        var stop = fixture.Session.StopAsync();
        Assert.Same(stop, fixture.Session.StopAsync());
        await stop;
        Assert.True(fixture.Session.IsStopped);
        Assert.Equal(1, fixture.Wire.Disposals);
        Assert.Equal(1, fixture.EndedCount);
        Assert.Single(fixture.Events.Where(e => e.Name == "SessionClosed"));
    }

    [Fact]
    public async Task StopDuringAudioWriteCancelsWriterWithoutEscapingCancellation()
    {
        await using var fixture = new Fixture();
        fixture.Wire.BlockAudio = true;
        await fixture.Start();
        fixture.Session.Append(new RealtimeAudio(fixture.Stored.Id, 0, Convert.ToBase64String(new byte[1920])));
        await fixture.Wire.AudioWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await fixture.Session.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, fixture.Wire.Disposals);
        Assert.Equal(1, fixture.EndedCount);
    }

    [Fact]
    public async Task StartupWaitsForConfigurationWriteBeforeAcceptingAudio()
    {
        await using var fixture = new Fixture();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Wire.SessionUpdateGate = gate.Task;
        var starting = fixture.Start();
        try
        {
            await fixture.Wire.SessionUpdateStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(starting.IsCompleted);
            Assert.DoesNotContain(fixture.Events, e => e.Name == "SessionReady");
            Assert.Throws<Microsoft.AspNetCore.SignalR.HubException>(() => fixture.Session.Append(
                new RealtimeAudio(fixture.Stored.Id, 0, Convert.ToBase64String(new byte[1920]))));
        }
        finally { gate.TrySetResult(); }
        await starting.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Contains(fixture.Wire.Sent, e => e.GetProperty("type").GetString() == "session.update");
        Assert.Single(fixture.Events, e => e.Name == "SessionReady");
        fixture.Session.Append(new RealtimeAudio(fixture.Stored.Id, 0, Convert.ToBase64String(new byte[1920])));
        await fixture.Wait(() => fixture.Wire.Sent.Any(e => e.GetProperty("type").GetString() == "input_audio_buffer.append"));
    }

    [Fact]
    public async Task ConfigurationWriteAloneDoesNotAcceptAudioBeforeProviderAcknowledgement()
    {
        await using var fixture = new Fixture();
        fixture.Wire.AutoAcknowledge = false;
        var starting = fixture.Start();
        try
        {
            await fixture.Wire.SessionUpdateWritten.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(starting.IsCompleted);
            Assert.DoesNotContain(fixture.Events, e => e.Name == "SessionReady");
            Assert.Throws<Microsoft.AspNetCore.SignalR.HubException>(() => fixture.Session.Append(
                new RealtimeAudio(fixture.Stored.Id, 0, Convert.ToBase64String(new byte[1920]))));
        }
        finally { fixture.Wire.Acknowledge(); }
        await starting.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Single(fixture.Events, e => e.Name == "SessionReady");
    }

    [Fact]
    public async Task ProviderAcknowledgementAloneDoesNotAcceptAudioBeforeSendCompletes()
    {
        await using var fixture = new Fixture();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Wire.SessionUpdateCompletionGate = completion.Task;
        var starting = fixture.Start();
        try
        {
            await fixture.Wire.SessionUpdatedProcessed.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(starting.IsCompleted);
            Assert.DoesNotContain(fixture.Events, e => e.Name == "SessionReady");
            Assert.Throws<Microsoft.AspNetCore.SignalR.HubException>(() => fixture.Session.Append(
                new RealtimeAudio(fixture.Stored.Id, 0, Convert.ToBase64String(new byte[1920]))));
        }
        finally { completion.TrySetResult(); }
        await starting.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Single(fixture.Events, e => e.Name == "SessionReady");
    }

    [Fact]
    public async Task StartupPumpsDoNotDependOnTheCallersSynchronizationContext()
    {
        await using var fixture = new Fixture();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Wire.SessionUpdateGate = gate.Task;
        var caller = SynchronizationContext.Current;
        var paused = new PausedContext();
        Task starting;
        try
        {
            SynchronizationContext.SetSynchronizationContext(paused);
            starting = fixture.Start();
        }
        finally { SynchronizationContext.SetSynchronizationContext(caller); }
        try
        {
            await fixture.Wire.SessionUpdateStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(starting.IsCompleted);
        }
        finally { gate.TrySetResult(); }
        await starting.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(0, paused.Posts);
        Assert.Single(fixture.Events, e => e.Name == "SessionReady");
    }

    [Fact]
    public async Task StopDuringConfigurationWriteCancelsStartupAndDisposesOnlyOnce()
    {
        await using var fixture = new Fixture();
        fixture.Wire.SessionUpdateGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task;
        var starting = fixture.Start();
        await fixture.Wire.SessionUpdateStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await fixture.Session.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.DoesNotContain(fixture.Events, e => e.Name == "SessionReady");
        Assert.Equal(1, fixture.Wire.Disposals);
        Assert.Equal(1, fixture.EndedCount);
    }

    [Fact]
    public async Task FailedConfigurationSendCannotPublishReadyEvenIfProviderAlreadyAcknowledged()
    {
        var fixture = new Fixture();
        try
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Wire.SessionUpdateCompletionGate = completion.Task;
            fixture.Wire.FailConfigurationSend = true;
            var starting = fixture.Start();
            try
            {
                await fixture.Wire.SessionUpdatedProcessed.Task.WaitAsync(TimeSpan.FromSeconds(3));
                Assert.Throws<Microsoft.AspNetCore.SignalR.HubException>(() => fixture.Session.Append(
                    new RealtimeAudio(fixture.Stored.Id, 0, Convert.ToBase64String(new byte[1920]))));
            }
            finally { completion.TrySetResult(); }
            var failure = await Assert.ThrowsAsync<IOException>(() => starting.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.Equal("Simulated configuration send failure", failure.Message);
            Assert.DoesNotContain(fixture.Events, e => e.Name == "SessionReady");
            Assert.Equal(1, fixture.Wire.Disposals);
            Assert.Equal(1, fixture.EndedCount);
            await Assert.ThrowsAsync<IOException>(() => fixture.Session.StopAsync());
        }
        finally { await fixture.DisposeProviderAsync(); }
    }

    private sealed class PausedContext : SynchronizationContext
    {
        public int Posts;
        // Intentionally do not drain callbacks: session I/O must not require this context.
        public override void Post(SendOrPostCallback callback, object? state) => Interlocked.Increment(ref Posts);
    }

    [Fact]
    public async Task StopDuringToolCancelsAndDisposesItsToken()
    {
        await using var fixture = new Fixture();
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Service.Query = async token =>
        {
            entered.TrySetResult(token);
            await Task.Delay(Timeout.Infinite, token);
            return [];
        };
        await fixture.Start();
        fixture.Call("slow", "buscar_medidas", "{\"busca\":\"peso\"}");
        var token = await fixture.WaitForToolEntry("slow", entered.Task);
        Assert.Equal(3, fixture.WorkersAtReady);
        Assert.Equal(new[] { "received", "enqueueing", "started", "authorized" }, fixture.Log.StagesFor("slow"));
        await fixture.Session.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(token.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => { _ = token.WaitHandle; });
        Assert.Equal(1, fixture.Wire.Disposals);
        Assert.Equal(1, fixture.EndedCount);
    }

    [Fact]
    public async Task AllToolConsumersAreWaitingBeforeReadyAndCanRunConcurrentCallsImmediately()
    {
        await using var fixture = new Fixture();
        var entries = Channel.CreateUnbounded<CancellationToken>();
        fixture.Service.Query = async token =>
        {
            entries.Writer.TryWrite(token);
            await Task.Delay(Timeout.Infinite, token);
            return [];
        };
        // Queue the calls from the readiness callback itself, with no scheduling delay.
        fixture.OnReady = () =>
        {
            Assert.Equal(3, fixture.WorkersAtReady);
            foreach (var id in new[] { "first", "second", "third" })
                fixture.Call(id, "buscar_medidas", "{\"busca\":\"peso\"}");
        };
        await fixture.Start();
        var tokens = new List<CancellationToken>();
        for (var i = 0; i < 3; i++) tokens.Add(await entries.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)));
        fixture.Session.Append(new RealtimeAudio(fixture.Stored.Id, 0, Convert.ToBase64String(new byte[1920])));
        await fixture.Wire.AudioWritten.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await fixture.Session.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));
        foreach (var token in tokens)
        {
            Assert.True(token.IsCancellationRequested);
            Assert.Throws<ObjectDisposedException>(() => { _ = token.WaitHandle; });
        }
        Assert.Equal(1, fixture.Wire.Disposals);
        Assert.Equal(1, fixture.EndedCount);
    }

    [Fact]
    public async Task ToolEntryWaitReportsPermissionFailureInsteadOfTimingOut()
    {
        await using var fixture = new Fixture();
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Service.Query = token => { entered.TrySetResult(token); return Task.FromResult<IReadOnlyList<VariavelOpcao>>([]); };
        await fixture.Start();
        fixture.Permissions.Allowed = false;
        fixture.Call("denied", "buscar_medidas", "{\"busca\":\"peso\"}");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.WaitForToolEntry("denied", entered.Task));
        Assert.Contains("ToolFailed", error.Message);
        Assert.Contains("started", error.Message);
        Assert.DoesNotContain("authorized", fixture.Log.StagesFor("denied"));
        Assert.False(entered.Task.IsCompleted);
    }

    [Fact]
    public async Task ToolEntryWaitReportsSessionClosureInsteadOfTimingOut()
    {
        await using var fixture = new Fixture();
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        await fixture.Start();
        var waiting = fixture.WaitForToolEntry("not-received", entered.Task);
        await fixture.Session.StopAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => waiting);
        Assert.Contains("SessionClosed", error.Message);
        Assert.False(entered.Task.IsCompleted);
    }

    [Fact]
    public async Task UnexpectedCancellationRemainsVisibleAndStillCleansUp()
    {
        var fixture = new Fixture();
        try
        {
            await fixture.Start();
            using var unrelated = new CancellationTokenSource();
            unrelated.Cancel();
            fixture.Wire.FailRead(new OperationCanceledException(unrelated.Token));
            await fixture.Wait(() => fixture.Wire.Disposals == 1);
            var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Session.StopAsync());
            Assert.Equal(unrelated.Token, error.CancellationToken);
            Assert.Equal(1, fixture.EndedCount);
        }
        finally { await fixture.DisposeProviderAsync(); }
    }

    [Fact]
    public async Task ProviderFailureIsPreservedEvenWhenTransportDisposalFails()
    {
        var fixture = new Fixture();
        try
        {
            await fixture.Start();
            fixture.Wire.ThrowOnDispose = true;
            fixture.Wire.FailRead(new IOException("Simulated provider failure"));
            await fixture.Wait(() => fixture.EndedCount == 1);
            var error = await Assert.ThrowsAsync<IOException>(() => fixture.Session.StopAsync());
            Assert.Equal("Simulated provider failure", error.Message);
            await Assert.ThrowsAsync<IOException>(() => fixture.Session.StopAsync());
            Assert.Equal(1, fixture.Wire.Disposals);
            Assert.Equal(1, fixture.EndedCount);
        }
        finally { await fixture.DisposeProviderAsync(); }
    }

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
        public int Disposals;
        public bool BlockAudio;
        public bool ThrowOnDispose;
        private Exception? _readFailure;
        public Task? SessionUpdateGate;
        public Task? SessionUpdateCompletionGate;
        public bool AutoAcknowledge = true;
        public bool FailConfigurationSend;
        private bool _acknowledgementReturned;
        public readonly TaskCompletionSource SessionUpdateStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource SessionUpdateWritten = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource SessionUpdatedProcessed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource AudioWriteStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource AudioWritten = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Outputs => Sent.Count(e => e.GetProperty("type").GetString() == "conversation.item.create");
        public int Continuations => Sent.Count(e => e.GetProperty("type").GetString() == "response.create");
        public void Push(object e) => _incoming.Writer.TryWrite(JsonSerializer.Serialize(e));
        public Task ConnectAsync(Uri uri, CancellationToken ct) => Task.CompletedTask;
        public async Task SendAsync(string json, CancellationToken ct)
        {
            var message = JsonSerializer.Deserialize<JsonElement>(json);
            var type = message.GetProperty("type").GetString();
            if (type == "session.update")
            {
                SessionUpdateStarted.TrySetResult();
                if (SessionUpdateGate is { } gate) await gate.WaitAsync(ct);
            }
            if (BlockAudio && type == "input_audio_buffer.append")
            { AudioWriteStarted.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); }
            Sent.Enqueue(message);
            if (type == "input_audio_buffer.append") AudioWritten.TrySetResult();
            // A provider can acknowledge configuration only after the writer sends it.
            // Prequeuing this reply lets Start return before the writer has started.
            if (type == "session.update")
            {
                SessionUpdateWritten.TrySetResult();
                if (AutoAcknowledge) Acknowledge();
                if (SessionUpdateCompletionGate is { } completion) await completion.WaitAsync(ct);
                if (FailConfigurationSend) throw new IOException("Simulated configuration send failure");
            }
        }
        public void Acknowledge() => Push(new { type = "session.updated", session = new { modalities = new[] { "text" }, turn_detection = new { type = "server_vad", silence_duration_ms = 400 } } });
        public void FailRead(Exception exception) { _readFailure = exception; Push(new { type = "test.failure" }); }
        public async Task<JsonDocument?> ReceiveAsync(CancellationToken ct)
        {
            // A subsequent receive proves the session reader finished handling the ack;
            // merely dequeuing it would leave a race with the assertions in the test.
            if (_acknowledgementReturned) SessionUpdatedProcessed.TrySetResult();
            var message = await _incoming.Reader.ReadAsync(ct);
            if (_readFailure is { } failure) throw failure;
            var document = JsonDocument.Parse(message);
            if (document.RootElement.GetProperty("type").GetString() == "session.updated") _acknowledgementReturned = true;
            return document;
        }
        public void Abort() { }
        public void Dispose() { Interlocked.Increment(ref Disposals); if (ThrowOnDispose) throw new InvalidOperationException("Simulated dispose failure"); }
    }
    private sealed class DispatchLog : ILogger
    {
        private readonly ConcurrentQueue<(string Call, string Stage)> _stages = new();
        public int Workers;
        public string[] StagesFor(string call) => _stages.Where(s => s.Call == call).Select(s => s.Stage).ToArray();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> values) return;
            var fields = values.ToDictionary(v => v.Key, v => v.Value);
            if (fields.TryGetValue("WorkerCount", out var workers) && workers is int count) Interlocked.Exchange(ref Workers, count);
            if (fields.TryGetValue("CallId", out var call) && fields.TryGetValue("Stage", out var stage))
                _stages.Enqueue((call!.ToString()!, stage!.ToString()!));
        }
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
        public int EndedCount;
        public readonly DispatchLog Log = new();
        public int WorkersAtReady;
        public Action? OnReady;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _terminals = new();
        private readonly TaskCompletionSource<string> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
                _provider.GetRequiredService<IServiceScopeFactory>(), Emit,
                default, Log, Wire, _ => Interlocked.Increment(ref EndedCount));
        }
        private Task Emit(string name, object payload)
        {
            var json = JsonSerializer.SerializeToElement(payload);
            Events.Enqueue((name, json));
            if (name == "SessionReady")
            {
                WorkersAtReady = Volatile.Read(ref Log.Workers);
                OnReady?.Invoke();
            }
            if (name is "ToolFailed" or "ToolCompleted") Terminal(json.GetProperty("callId").GetString()!).TrySetResult(name);
            if (name == "SessionClosed") _closed.TrySetResult(name);
            return Task.CompletedTask;
        }
        private TaskCompletionSource<string> Terminal(string call)
            => _terminals.GetOrAdd(call, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));

        public async Task<CancellationToken> WaitForToolEntry(string call, Task<CancellationToken> entry)
        {
            var terminal = Terminal(call).Task;
            try
            {
                var finished = await Task.WhenAny(entry, terminal, _closed.Task).WaitAsync(TimeSpan.FromSeconds(3));
                if (entry.IsCompleted) return await entry;
                var outcome = finished == terminal ? await terminal : await _closed.Task;
                throw new InvalidOperationException($"Tool {call}: {outcome} before service entry; stages=[{string.Join(",", Log.StagesFor(call))}].");
            }
            catch (TimeoutException ex)
            {
                throw new TimeoutException($"Tool {call}: service entry was not signalled; stages=[{string.Join(",", Log.StagesFor(call))}]; workersAtReady={WorkersAtReady}.", ex);
            }
        }
        public Task Start()
        {
            Wire.Push(new { type = "session.created" });
            return Session.StartAsync();
        }
        public void Call(string id, string name, string args) => Wire.Push(new { type = "response.function_call_arguments.done", call_id = id, response_id = "r1", name, arguments = args });
        public void Done() => Wire.Push(new { type = "response.done", response = new { id = "r1", status = "completed" } });
        public async Task Wait(Func<bool> predicate)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            while (!predicate()) await Task.Delay(5, timeout.Token);
        }
        public ValueTask DisposeProviderAsync() => _provider.DisposeAsync();
        public async ValueTask DisposeAsync()
        {
            try { await Session.StopAsync(); }
            finally { await _provider.DisposeAsync(); }
        }
    }
}
