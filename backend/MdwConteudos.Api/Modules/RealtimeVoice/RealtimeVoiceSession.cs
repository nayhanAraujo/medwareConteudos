using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Domain.Models;
using Microsoft.AspNetCore.SignalR;
using MdwConteudos.Api.Modules.FormulasModelos;
using MdwConteudos.Api.Modules.StudioDrafts;

namespace MdwConteudos.Api.Modules.RealtimeVoice;

public sealed class RealtimeVoiceSession
{
    private readonly string _connection;
    private readonly ClaimsPrincipal _user;
    private readonly VoiceSession _stored;
    private readonly int? _model;
    private readonly RealtimeVoiceOptions _config;
    private readonly IVoiceSessionStore _store;
    private readonly IServiceScopeFactory _scopes;
    private readonly Func<string, object, Task> _emit;
    private readonly ILogger _log;
    private readonly Action<RealtimeVoiceSession>? _ended;
    private readonly IRealtimeVoiceTransport _socket;
    private readonly CancellationTokenSource _cts;
    private readonly CancellationToken _sessionToken;
    private readonly object _lifecycleGate = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<string> _audio = Channel.CreateBounded<string>(64);
    private readonly Channel<string> _control = Channel.CreateBounded<string>(64);
    private readonly Channel<Call> _tools = Channel.CreateBounded<Call>(16);
    private readonly Channel<(ClaimsPrincipal User, RealtimeConfirmation Request)> _confirmations = Channel.CreateBounded<(ClaimsPrincipal, RealtimeConfirmation)>(4);
    private readonly ConcurrentDictionary<string, byte> _confirmationIds = new();
    private readonly SemaphoreSlim _sendSignal = new(0);
    private readonly SemaphoreSlim _mutation = new(1);
    private readonly ConcurrentDictionary<string, byte> _calls = new();
    private readonly Dictionary<string, Batch> _batches = [];
    private readonly Dictionary<string, (long Generation, CancellationTokenSource Cts)> _groups = [];
    private readonly List<CancellationTokenSource> _operationTokens = [];
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly object _gate = new();
    private readonly RealtimeChoiceGate _choiceGate = new();
    private readonly RealtimeDraft _draft;
    private readonly Guid? _persistentId;
    private readonly bool _interactiveChoices;
    private long _persistentRevision;
    private RealtimeSelection? _selection;
    private string _turnGroup = Guid.NewGuid().ToString();
    private readonly Dictionary<string, string> _responseGroups = [];
    private string[] _keyterms = ["superfície corporal", "átrio esquerdo", "septo", "ventrículo", "normalidade", "aorta"];
    private string _expectedJson;
    private long _sequence = -1;
    private long _generation;
    private Task? _run;
    private Task? _stopTask;
    private int _resourcesDisposed;
    private bool _responseActive;
    private Proposal? _proposal;
    private string? _savedProposal;
    public Guid Id => _stored.Id;
    public bool IsStopped => _sessionToken.IsCancellationRequested;
    private sealed record Call(string Id, string Response, string Name, string Arguments, string Group, long Generation, CancellationToken Token, long Revision, string OperationGroup, long ChoiceEpoch);
    private sealed class Batch { public int Pending; public bool Closed; public bool Continued; public bool Cancelled; public bool AwaitingChoice; }
    private sealed record Proposal(string Id, long Revision, SalvarComposicaoRequest Composition);

    public RealtimeVoiceSession(string connection, ClaimsPrincipal user, VoiceSession stored, int? model, string key,
        RealtimeVoiceOptions config, IVoiceSessionStore store, IServiceScopeFactory scopes, Func<string, object, Task> emit,
        CancellationToken stopping, ILogger log, IRealtimeVoiceTransport? transport = null, Action<RealtimeVoiceSession>? ended = null, Guid? persistentId = null, bool interactiveChoices = false)
    {
        _connection = connection; _user = user; _stored = stored; _model = model; _config = config;
        _store = store; _scopes = scopes; _emit = emit; _log = log;
        _ended = ended;
        _persistentId = persistentId;
        _interactiveChoices = interactiveChoices || persistentId.HasValue;
        _persistentRevision = stored.ImportedRevision;
        _socket = transport ?? new XaiVoiceTransport(key);
        _draft = new RealtimeDraft(stored.CamposScriptJson, stored.ImportedRevision); _expectedJson = stored.CamposScriptJson;
        if (persistentId is null && stored.ImportedDocumentJson is null)
        {
            stored.CamposScriptJson = _expectedJson = _draft.Render();
            store.Update(stored);
        }
        _cts = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        _sessionToken = _cts.Token;
        _cts.CancelAfter(TimeSpan.FromMinutes(Math.Clamp(config.MaxSessionMinutes, 1, 60)));
    }

    public async Task StartAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_sessionToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            if (_persistentId is { } persistent)
            {
                await using var scope = _scopes.CreateAsyncScope();
                var view = await scope.ServiceProvider.GetRequiredService<StudioDraftService>().Get(persistent.ToString(), int.Parse(_stored.OwnerUserId), timeout.Token);
                _persistentRevision = view.Revision;
                var catalog = await scope.ServiceProvider.GetRequiredService<IFormulasModelosService>().ListVariaveisAsync(timeout.Token);
                var used = view.Document.Secoes.SelectMany(s => s.Itens).Select(i => i.CodVariavel).ToHashSet();
                _keyterms = catalog.OrderByDescending(v => used.Contains(v.CodVariavel)).Select(v => v.Nome).Concat(_keyterms)
                    .Where(s => !string.IsNullOrWhiteSpace(s) && s.Length <= 50).Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToArray();
            }
            await _socket.ConnectAsync(new Uri($"wss://api.x.ai/v1/realtime?model={Uri.EscapeDataString(_config.Model)}"), timeout.Token);
            lock (_lifecycleGate)
            {
                _sessionToken.ThrowIfCancellationRequested();
                _run = RunAsync();
            }
            await _ready.Task.WaitAsync(timeout.Token);
            if (_persistentId is { } current)
            {
                await using var scope = _scopes.CreateAsyncScope();
                await _emit("SessionReady", new { sessionId = Id, revision = _persistentRevision, draft = await scope.ServiceProvider.GetRequiredService<StudioDraftService>().Get(current.ToString(), int.Parse(_stored.OwnerUserId), timeout.Token) });
            }
            else await _emit("SessionReady", new { sessionId = Id, revision = _stored.ImportedDocumentJson is not null ? _stored.ImportedRevision : _draft.Revision, camposScriptJson = _stored.ImportedDocumentJson is not null ? _stored.CamposScriptJson : _draft.Render(), documentHash = StudioDraftService.Hash(_stored.ImportedDocumentJson??_stored.CamposScriptJson) });
        }
        catch (Exception ex)
        {
            _log.LogWarning("Realtime startup failed session={SessionId} connection={ConnectionId} type={ErrorType}", Id, _connection, ex.GetType().Name);
            await StopAsync();
            throw;
        }
    }

    public void Append(RealtimeAudio audio)
    {
        if (!_ready.Task.IsCompletedSuccessfully || IsStopped) throw new HubException("Sessão ainda não está pronta.");
        if (_stored.ExpiresAt <= DateTime.UtcNow) { RequestStop(); throw new HubException("Rascunho expirado."); }
        if (audio.Audio.Length > 3000) { RequestStop(); throw new HubException("Bloco de áudio inválido."); }
        byte[] bytes;
        try { bytes = Convert.FromBase64String(audio.Audio); } catch (FormatException) { RequestStop(); throw new HubException("Áudio inválido."); }
        // Exactly 40 ms of PCM16 mono at 24 kHz. Never silently drop frames.
        if (bytes.Length != 1920 || audio.Sequence != Interlocked.Read(ref _sequence) + 1)
        { RequestStop(); throw new HubException("Sequência de áudio interrompida. Inicie novamente."); }
        if (audio.Sequence > _clock.Elapsed.TotalMilliseconds / 40 + 25)
        { RequestStop(); throw new HubException("Taxa de áudio inválida. Inicie novamente."); }
        Interlocked.Exchange(ref _sequence, audio.Sequence);
        if (!_audio.Writer.TryWrite(JsonSerializer.Serialize(new { type = "input_audio_buffer.append", audio = audio.Audio })))
        { RequestStop(); throw new HubException("Fila de áudio cheia. Rascunho preservado; retome a gravação."); }
        _sendSignal.Release();
    }

    private void Send(object value)
    {
        if (!_control.Writer.TryWrite(JsonSerializer.Serialize(value))) throw new InvalidOperationException("Control queue saturated.");
        _sendSignal.Release();
    }

    private async Task RunAsync()
    {
        var tasks = new[] { ReadAsync(), WriteAsync(), WorkerAsync(), WorkerAsync(), WorkerAsync(), SaveWorkerAsync() };
        Exception? failure = null;
        try
        {
            var first = await Task.WhenAny(tasks);
            await first;
        }
        catch (OperationCanceledException ex) when (IsExpectedCancellation(ex)) { }
        catch (Exception ex)
        {
            failure = ex;
            _log.LogWarning("Realtime connection failed session={SessionId} type={ErrorType}", Id, ex.GetType().Name);
        }
        finally
        {
            try { RequestStop(); } catch (Exception ex) { failure ??= ex; }
            try { _socket.Abort(); } catch (Exception ex) { failure ??= ex; }
            try { await Task.WhenAll(tasks); } catch { /* Inspect each task below, including cancellation tokens. */ }
            foreach (var task in tasks)
            {
                try { await task; }
                catch (OperationCanceledException ex) when (IsExpectedCancellation(ex)) { }
                catch (Exception ex) { failure ??= ex; }
            }
            if (failure is null) _ready.TrySetCanceled(_sessionToken);
            else _ready.TrySetException(failure);
            try { await _emit("SessionClosed", new { sessionId = Id, message = "Sessão encerrada. O rascunho foi preservado. Retome a gravação.", code = Id.ToString() }); } catch { }
            var disposalFailure = DisposeResources();
            failure ??= disposalFailure;
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private async Task WriteAsync()
    {
        while (!IsStopped)
        {
            await _sendSignal.WaitAsync(_sessionToken);
            if (_control.Reader.TryRead(out var json) || _audio.Reader.TryRead(out json))
                await _socket.SendAsync(json, _sessionToken);
        }
    }

    private async Task ReadAsync()
    {
        var configured = false;
        while (!IsStopped)
        {
            using var doc = await _socket.ReceiveAsync(_sessionToken);
            if (doc is null) return;
            var e = doc.RootElement;
            var type = e.GetProperty("type").GetString();
            if (!configured && type is "session.created" or "conversation.created")
            {
                configured = true;
                Send(new { type = "session.update", session = new
                {
                    modalities = new[] { "text" },
                    instructions = "Você monta laudos em português brasileiro por ferramentas. Sempre consulte o rascunho para obter IDs. Preserve campos não mencionados. Use somente medidas do catálogo e esclareça ambiguidades. Reutilize grupoOperacao e substituirOperacao=true para corrigir a mesma intenção. Uma nova fala pode adicionar outra intenção; não desfaça tudo. Nunca confirme um salvamento: apenas prepare a proposta e aguarde o botão na interface. Não invente dados clínicos. Seja silencioso e conciso.",
                    turn_detection = new { type = "server_vad", silence_duration_ms = _config.SilenceDurationMs },
                    audio = new { input = new { format = new { type = "audio/pcm", rate = 24000 }, transcription = new { language_hint = "pt-BR", keyterms = _keyterms } } },
                    tools = _persistentId.HasValue || _stored.ImportedDocumentJson is not null ? StudioRealtimeAdapter.Tools : RealtimeTools.Definitions
                } });
            }
            else if (type == "session.updated")
            {
                try { ValidateCapabilities(e.GetProperty("session"), _config.SilenceDurationMs); }
                catch
                {
                    _log.LogWarning("xAI capability validation rejected session={SessionId}; expected text-only and server_vad intervalMs={SilenceMs}. Run the capability probe before enabling this feature.", Id, _config.SilenceDurationMs);
                    throw;
                }
                _ready.TrySetResult();
            }
            else if (type == "error")
            {
                // Never log provider message text: it can include input or credentials.
                var code = e.TryGetProperty("error", out var err) && err.TryGetProperty("code", out var c) ? c.GetString() : "unknown";
                _log.LogWarning("xAI error session={SessionId} code={ProviderCode}", Id, code);
                throw new InvalidOperationException("Provider rejected session/event.");
            }
            else if (type is "input_audio_buffer.speech_started" or "input_audio_buffer.speech_stopped")
            {
                if (type.EndsWith("started")) _turnGroup = Guid.NewGuid().ToString();
                await _emit("SpeechState", new { sessionId = Id, speaking = type.EndsWith("started") });
            }
            else if (type == "conversation.item.input_audio_transcription.completed")
                await _emit("Transcript", new { sessionId = Id, text = e.GetProperty("transcript").GetString() });
            else if (type == "response.created") { lock (_gate) { _responseActive = true; if (_persistentId.HasValue) _responseGroups[e.GetProperty("response").GetProperty("id").GetString()!] = _turnGroup; } }
            else if (type == "response.function_call_arguments.done") QueueCall(e);
            else if (type == "response.done")
            {
                var response = e.GetProperty("response");
                var id = response.GetProperty("id").GetString()!;
                lock (_gate)
                {
                    _responseActive = false;
                    if (_batches.TryGetValue(id, out var batch))
                    {
                        batch.Closed = true;
                        batch.Cancelled = response.TryGetProperty("status", out var status) && status.GetString() != "completed";
                    }
                    ContinueReadyBatches();
                }
            }
            else if (type is "response.output_audio.delta" or "response.audio.delta")
                throw new InvalidOperationException("Provider emitted audio despite required text-only mode.");
        }
    }

    public static void ValidateCapabilities(JsonElement session, int silenceMs)
    {
        if (!session.TryGetProperty("modalities", out var modes) || modes.ValueKind != JsonValueKind.Array
            || modes.GetArrayLength() != 1 || modes[0].GetString() != "text"
            || !session.TryGetProperty("turn_detection", out var vad) || vad.GetProperty("type").GetString() != "server_vad"
            || !vad.TryGetProperty("silence_duration_ms", out var silence) || silence.GetInt32() != silenceMs)
            throw new InvalidOperationException("xAI did not acknowledge text-only mode and the requested VAD interval. Keep feature disabled until capability validation.");
    }

    private void QueueCall(JsonElement e)
    {
        if (!_ready.Task.IsCompletedSuccessfully) throw new InvalidOperationException("Tool before session ready.");
        var id = e.GetProperty("call_id").GetString()!;
        var response = e.GetProperty("response_id").GetString()!;
        if (!_calls.TryAdd(id, 0)) return;
        if (_calls.Count > 1000) throw new InvalidOperationException("Session tool limit exceeded.");
        var arguments = e.GetProperty("arguments").GetString()!;
        if (arguments.Length > 16000) throw new InvalidOperationException("Tool arguments too large.");
        var group = id;
        using (var parsed = JsonDocument.Parse(arguments))
            if (parsed.RootElement.ValueKind == JsonValueKind.Object && parsed.RootElement.TryGetProperty("grupoOperacao", out var g) && g.ValueKind == JsonValueKind.String)
                group = g.GetString()!;
        lock (_gate)
        {
            if (_groups.TryGetValue(group, out var old)) { old.Cts.Cancel(); }
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_sessionToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            _operationTokens.Add(cts);
            var generation = ++_generation;
            _groups[group] = (generation, cts);
            if (!_batches.TryGetValue(response, out var batch)) _batches[response] = batch = new Batch();
            batch.Pending++;
            var name = e.GetProperty("name").GetString()!;
            var choiceEpoch = _choiceGate.Queue(id, name);
            if (!_tools.Writer.TryWrite(new Call(id, response, name, arguments, group, generation, cts.Token, Interlocked.Read(ref _persistentRevision), _persistentId.HasValue ? _responseGroups.GetValueOrDefault(response, _turnGroup) : group, choiceEpoch)))
            {
                _choiceGate.EndSearch(id);
                throw new InvalidOperationException("Tool queue saturated.");
            }
        }
    }

    private void ContinueReadyBatches()
    {
        if (_responseActive) return;
        // One continuation includes all finished tool batches; never continue per individual call.
        var ready = _batches.Values.Where(b => b.Closed && b.Pending == 0 && !b.Continued).ToArray();
        if (ready.Length == 0) return;
        foreach (var b in ready) b.Continued = true;
        if (!_choiceGate.HasPending && ready.Any(b => !b.Cancelled && !b.AwaitingChoice)) { _responseActive = true; Send(new { type = "response.create" }); }
    }

    private async Task WorkerAsync()
    {
        await foreach (var call in _tools.Reader.ReadAllAsync(_sessionToken))
        {
            object result;
            var started = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                call.Token.ThrowIfCancellationRequested();
                await _emit("ToolStarted", new { sessionId = Id, callId = call.Id, responseId = call.Response, name = call.Name });
                using var parsed = JsonDocument.Parse(call.Arguments);
                if (!_persistentId.HasValue && _stored.ImportedDocumentJson is null) RealtimeTools.Validate(call.Name, parsed.RootElement);
                await using var scope = _scopes.CreateAsyncScope();
                await RealtimeVoiceManager.AuthorizeAsync(scope.ServiceProvider, _user, false, call.Token);
                var service = scope.ServiceProvider.GetRequiredService<IFormulasModelosService>();
                if (_persistentId is { } persistent)
                {
                    var commands = scope.ServiceProvider.GetRequiredService<StudioDraftService>();
                    var commandId = Guid.ParseExact(StudioDraftService.Hash($"{Id}:{call.Id}")[..32], "N").ToString();
                    EnsureCurrent(call);
                    result = _choiceGate.CanExecute(call.ChoiceEpoch, call.Name)
                        ? await StudioRealtimeAdapter.Execute(commands, persistent.ToString(), int.Parse(_stored.OwnerUserId), call.Name,
                            parsed.RootElement, commandId, call.Revision, _selection, call.Token, call.OperationGroup)
                        : new { status = _choiceGate.RequiresDecision(call.ChoiceEpoch) ? "awaiting_user_choice" : "retry_after_search", message = "Aguarde a consulta terminar e, se houver sugestões, a escolha ou o cancelamento na interface. Não adivinhe uma opção." };
                    var payload = JsonSerializer.SerializeToElement(result, StudioDocumentEditor.Json);
                    if (payload.TryGetProperty("draft", out var updated))
                    {
                        Interlocked.Exchange(ref _persistentRevision, updated.GetProperty("revision").GetInt64());
                        await _emit("DraftUpdated", new { sessionId = Id, revision = _persistentRevision, draft = updated, commandId, callId = call.Id });
                    }
                    if (payload.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 1)
                    {
                        var current = await commands.Get(persistent.ToString(), int.Parse(_stored.OwnerUserId), call.Token);
                        var token = scope.ServiceProvider.GetRequiredService<StudioChoices>().Issue(persistent.ToString(), int.Parse(_stored.OwnerUserId), current.Revision, candidates.EnumerateArray().Select(x => x.GetProperty("codVariavel").GetInt32()));
                        var offered = _choiceGate.Offer(call.ChoiceEpoch, token);
                        result = new { status = "awaiting_user_choice", message = "Aguarde a seleção explícita na interface.", candidates, revision = current.Revision };
                        if (offered)
                            await _emit("ChoiceRequested", new { sessionId = Id, draftId = persistent.ToString(), revision = current.Revision, draft = current, candidates, sectionId = _selection?.SectionId, token });
                    }
                }
                else if (_interactiveChoices && !_choiceGate.CanExecute(call.ChoiceEpoch, call.Name))
                    result = new { status = _choiceGate.RequiresDecision(call.ChoiceEpoch) ? "awaiting_user_choice" : "retry_after_search", message = "Aguarde a escolha ou o cancelamento na interface. Não adivinhe uma medida." };
                else if (_stored.ImportedDocumentJson is not null && call.Name != "buscar_medidas")
                {
                    if (call.Name == "consultar_rascunho") result = new { status = "completed", revision = _stored.ImportedRevision, document = ImportedVoiceEditor.Document(_stored) };
                    else if (call.Name == "preparar_salvamento") result = new { status = "unavailable", message = "Modelo importado: baixe o JSON completo. A publicação perderia componentes." };
                    else
                    {
                        var catalog = await service.ListVariaveisAsync(call.Token);
                        EnsureCurrent(call);
                        var edit = ImportedVoiceEditor.Apply(_stored, new([new(call.Name, parsed.RootElement.Clone())], "Estrutura atualizada por voz.", []), call.Revision, catalog);
                        _expectedJson = edit.CamposScriptJson;
                        Interlocked.Exchange(ref _persistentRevision, _stored.ImportedRevision);
                        _store.Update(_stored);
                        result = new { status = "completed", revision = _stored.ImportedRevision, document = ImportedVoiceEditor.Document(_stored), warnings = edit.Warnings };
                        await _emit("DraftUpdated", new { sessionId = Id, revision = _stored.ImportedRevision, camposScriptJson = _expectedJson, documentHash = StudioDraftService.Hash(_stored.ImportedDocumentJson??_stored.CamposScriptJson), callId = call.Id });
                    }
                }
                else if (call.Name == "buscar_medidas")
                {
                    var search = parsed.RootElement.GetProperty("busca").GetString()!;
                    var catalog = await service.ListVariaveisAsync(call.Token);
                    EnsureCurrent(call);
                    var matches = catalog.Where(v => System.Globalization.CultureInfo.GetCultureInfo("pt-BR").CompareInfo.IndexOf($"{v.Nome} {v.Sigla} {v.Grupo}", search,
                            System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0)
                        .Take(30).ToArray();
                    result = new { status = "completed", medidas = matches };
                    if (_interactiveChoices && matches.Length > 1)
                    {
                        var token = scope.ServiceProvider.GetRequiredService<StudioChoices>().Issue(Id.ToString(), int.Parse(_stored.OwnerUserId), _stored.ImportedRevision, matches.Select(v=>v.CodVariavel));
                        var sections = _stored.ImportedDocumentJson is not null
                            ? ImportedVoiceEditor.Document(_stored).Secoes.Select(s=>new {id=s.Id,nome=s.Nome})
                            : System.Text.Json.Nodes.JsonNode.Parse(_stored.CamposScriptJson)!["camposScript"]!.AsArray().OfType<System.Text.Json.Nodes.JsonObject>()
                                .Where(f=>StudioJsonImport.Text(f,"tipo")=="etiqueta").Select(f=>new {id=f["id"]!.ToString(),nome=StudioJsonImport.Text(f,"descricao")});
                        if (_choiceGate.Offer(call.ChoiceEpoch, token))
                            await _emit("ChoiceRequested",new {sessionId=Id,draftId=Id.ToString(),revision=_stored.ImportedRevision,candidates=matches,sections=sections.ToArray(),token});
                        result = new {status="awaiting_user_choice",message="Aguarde a escolha explícita na interface."};
                    }
                }
                else
                {
                    IReadOnlyList<VariavelOpcao> catalog = call.Name is not ("consultar_rascunho" or "preparar_salvamento")
                        ? await service.ListVariaveisAsync(call.Token) : [];
                    await _mutation.WaitAsync(call.Token);
                    try
                    {
                        EnsureCurrent(call); EnsureUnchanged();
                        if (call.Name == "consultar_rascunho") result = new { status = "completed", revision = _draft.Revision, campos = _draft.Describe() };
                        else if (call.Name == "preparar_salvamento")
                        {
                            if (_model is not { } model) throw new InvalidOperationException("Selecione na interface um modelo de destino antes de iniciar a sessão.");
                            var composition = _draft.Composition(parsed.RootElement.GetProperty("nome").GetString()!);
                            await service.PreviewComposicaoAsync(model, composition, int.Parse(_stored.OwnerUserId), call.Token);
                            EnsureCurrent(call);
                            _proposal = new Proposal(Guid.NewGuid().ToString("N"), _draft.Revision, composition);
                            result = new { status = "awaiting_confirmation", proposalId = _proposal.Id, revision = _proposal.Revision, modeloId = model, nome = composition.Nome };
                            await _emit("SaveProposed", new { sessionId = Id, proposalId = _proposal.Id, revision = _proposal.Revision, modeloId = model, nome = composition.Nome });
                        }
                        else
                        {
                            lock (_gate)
                            {
                                EnsureCurrent(call);
                                result = new { status = "completed", edit = _draft.Apply(call.Name, parsed.RootElement, catalog), campos = _draft.Describe() };
                                _proposal = null;
                                _stored.CamposScriptJson = _expectedJson = _draft.Render();
                                _stored.ImportedRevision = _draft.Revision;
                                Interlocked.Exchange(ref _persistentRevision, _draft.Revision);
                                _store.Update(_stored);
                            }
                            await _emit("DraftUpdated", new { sessionId = Id, revision = _draft.Revision, camposScriptJson = _expectedJson, documentHash = StudioDraftService.Hash(_stored.CamposScriptJson), callId = call.Id });
                        }
                    }
                    finally { _mutation.Release(); }
                }
                EnsureCurrent(call);
                await _emit("ToolCompleted", new { sessionId = Id, callId = call.Id, name = call.Name, result });
            }
            catch (OperationCanceledException) { result = new { status = "cancelled", message = "Operação substituída ou cancelada." }; }
            catch (Exception ex)
            {
                result = ex is MeasureInUseException blocked ? new {status = "error", code = blocked.Code, message = blocked.Message, details = blocked.Details} :
                    (object)new { status = "error", message = ex is InvalidOperationException or HubException ? ex.Message : "Falha ao executar a ferramenta. Rascunho preservado." };
                await _emit("ToolFailed", new { sessionId = Id, callId = call.Id, result });
            }
            lock (_gate)
            {
                if (call.Token.IsCancellationRequested || _groups[call.Group].Generation != call.Generation)
                    result = new { status = "cancelled", message = "Operação substituída ou cancelada." };
                _log.LogInformation("Realtime tool session={SessionId} call={CallId} response={ResponseId} name={ToolName} status={Status} durationMs={DurationMs}",
                    Id, call.Id, call.Response, call.Name, JsonSerializer.SerializeToElement(result).GetProperty("status").GetString(), started.ElapsedMilliseconds);
                Send(new { type = "conversation.item.create", item = new { type = "function_call_output", call_id = call.Id, output = JsonSerializer.Serialize(result) } });
                if (JsonSerializer.SerializeToElement(result).GetProperty("status").GetString() == "awaiting_user_choice") _batches[call.Response].AwaitingChoice = true;
                _choiceGate.EndSearch(call.Id);
                _batches[call.Response].Pending--; ContinueReadyBatches();
            }
        }
    }

    public void CompleteChoice(string token, long? revision = null)
    {
        if (!_choiceGate.Complete(token) || !revision.HasValue) return;
        long current;
        do { current = Interlocked.Read(ref _persistentRevision); if (current >= revision.Value) return; }
        while (Interlocked.CompareExchange(ref _persistentRevision, revision.Value, current) != current);
    }

    private void EnsureCurrent(Call call)
    {
        call.Token.ThrowIfCancellationRequested();
        lock (_gate) if (_groups[call.Group].Generation != call.Generation) throw new OperationCanceledException();
    }
    public void SetContext(RealtimeSelection selection)
    {
        if (!_persistentId.HasValue) return;
        if ((selection.SectionId is not null && !Guid.TryParse(selection.SectionId, out _)) || (selection.ItemId is not null && !Guid.TryParse(selection.ItemId, out _))) throw new HubException("Seleção inválida.");
        _selection = selection; Interlocked.Exchange(ref _persistentRevision, selection.Revision);
    }
    private void EnsureUnchanged()
    {
        if (_stored.ExpiresAt <= DateTime.UtcNow || _store.Get(Id) is not { } stored || stored.CamposScriptJson != _expectedJson)
            throw new InvalidOperationException("O rascunho mudou fora desta sessão ou expirou. Pare e inicie novamente para preservar as alterações.");
    }

    public void QueueConfirmation(ClaimsPrincipal user, RealtimeConfirmation request)
    {
        if (!_confirmationIds.TryAdd(request.ProposalId, 0)) return;
        if (!_confirmations.Writer.TryWrite((user, request)))
        {
            _confirmationIds.TryRemove(request.ProposalId, out _);
            throw new HubException("Há um salvamento em andamento. Aguarde a confirmação.");
        }
    }

    private async Task SaveWorkerAsync()
    {
        await foreach (var item in _confirmations.Reader.ReadAllAsync(_sessionToken))
        {
            try { await ConfirmAsync(item.User, item.Request); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _confirmationIds.TryRemove(item.Request.ProposalId, out _);
                await _emit("SaveFailed", new { sessionId = Id, message = ex is HubException ? ex.Message : "Não foi possível salvar o modelo." });
            }
        }
    }

    public async Task ConfirmAsync(ClaimsPrincipal user, RealtimeConfirmation request)
    {
        await _mutation.WaitAsync(_sessionToken);
        try
        {
            EnsureUnchanged();
            if (_savedProposal == request.ProposalId) return;
            if (_proposal is not { } proposal || proposal.Id != request.ProposalId || proposal.Revision != request.Revision || _draft.Revision != request.Revision || _model is not { } model)
                throw new HubException("Proposta expirada. Solicite uma nova proposta de salvamento.");
            await using var scope = _scopes.CreateAsyncScope();
            var owner = await RealtimeVoiceManager.AuthorizeAsync(scope.ServiceProvider, user, true, _sessionToken);
            if (owner.ToString() != _stored.OwnerUserId) throw new HubException("Rascunho não autorizado.");
            await scope.ServiceProvider.GetRequiredService<IFormulasModelosService>().SaveComposicaoAsync(model, proposal.Composition, owner, _sessionToken);
            _savedProposal = proposal.Id; _proposal = null;
            await _emit("SaveCompleted", new { sessionId = Id, modeloId = model, revision = _draft.Revision });
        }
        catch (Exception ex) when (ex is not HubException)
        { _log.LogWarning("Realtime save failed session={SessionId} type={ErrorType}", Id, ex.GetType().Name); throw new HubException("Não foi possível salvar. Revise o modelo e solicite uma nova proposta."); }
        finally { _mutation.Release(); }
    }

    public Task StopAsync()
    {
        lock (_lifecycleGate) return _stopTask ??= StopCoreAsync();
    }

    private async Task StopCoreAsync()
    {
        Exception? failure = null;
        try
        {
            RequestStop();
            if (_run is null) _socket.Abort();
            else await _run;
        }
        catch (Exception ex) { failure = ex; }
        finally
        {
            var disposalFailure = DisposeResources();
            failure ??= disposalFailure;
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private bool IsExpectedCancellation(OperationCanceledException exception)
        => _sessionToken.IsCancellationRequested && exception.CancellationToken == _sessionToken;

    private void RequestStop()
    {
        lock (_lifecycleGate) if (_resourcesDisposed == 0) _cts.Cancel();
    }

    private Exception? DisposeResources()
    {
        lock (_lifecycleGate)
        {
            if (Interlocked.Exchange(ref _resourcesDisposed, 1) != 0) return null;
        }
        Exception? failure = null;
        void DisposeOne(Action action) { try { action(); } catch (Exception ex) { failure ??= ex; } }
        // Run's child tasks have finished before disposing resources they can still use.
        lock (_gate) foreach (var token in _operationTokens) DisposeOne(token.Dispose);
        DisposeOne(_socket.Dispose);
        DisposeOne(_cts.Dispose);
        if (_ended is not null) DisposeOne(() => _ended(this));
        return failure;
    }
}
