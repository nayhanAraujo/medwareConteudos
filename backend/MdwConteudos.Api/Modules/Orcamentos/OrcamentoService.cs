using System.Data.Common;
using System.Globalization;
using System.Security.Claims;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Orcamentos;

public sealed partial class OrcamentoService(IFirebirdConnectionFactory db, IHttpContextAccessor http, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static long Version(string value, bool zero = false) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && (zero ? n >= 0 : n > 0)
        ? n : throw new OrcamentoException(422, "Versão inválida. Atualize o registro.");
    private sealed class Head
    {
        public long Id { get; set; } public int ClientId { get; set; } public int Revision { get; set; }
        public string Status { get; set; } = ""; public int Archived { get; set; } public long Version { get; set; }
        public DateTime Updated { get; set; } public string Document { get; set; } = "";
        public QuoteSnapshot Snapshot => OrcamentoJson.Read<QuoteSnapshot>(Document);
        public QuoteListItem Item() => new(Id.ToString(CultureInfo.InvariantCulture), ClientId, Snapshot.Client.Name, Snapshot.Draft.Title, Revision, Status, Archived != 0,
            Snapshot.Calculation.Net, Version.ToString(CultureInfo.InvariantCulture), QuoteCalculator.Utc(Updated));
    }
    private const string HeadSql = "SELECT o.CODORCAMENTO AS Id,o.CODCLIENTE AS ClientId,o.REVISAO AS Revision,o.STATUS AS Status,o.ARQUIVADO AS Archived,o.VERSAO AS Version,o.DTHRULTMODIFICACAO AS Updated,r.DOCUMENTO AS Document FROM ORCAMENTO o JOIN ORCAMENTOREVISAO r ON r.CODORCAMENTO=o.CODORCAMENTO AND r.NUMERO=o.REVISAO";
    private static async Task<Head> ReadHead(DbConnection c, long id, DbTransaction? tx = null, CancellationToken ct = default) =>
        await c.QuerySingleOrDefaultAsync<Head>(new CommandDefinition(HeadSql + " WHERE o.CODORCAMENTO=@id", new { id }, tx, cancellationToken: ct)) ?? throw new OrcamentoException(404, "Orçamento não encontrado.");
    private async Task<(int Id, string Name)> Actor(DbConnection c, DbTransaction? tx, CancellationToken ct)
    {
        if (!int.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) throw new OrcamentoException(401, "Faça login para continuar.");
        var name = await c.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT NOME FROM USUARIO WHERE CODUSUARIO=@id AND STATUS=-1", new { id }, tx, cancellationToken: ct));
        return name is null ? throw new OrcamentoException(403, "Usuário inativo ou inexistente.") : (id, name);
    }
    private static async Task Bump(DbConnection c, DbTransaction tx, Head h, string version, DateTime now, CancellationToken ct)
    {
        if (h.Archived != 0) throw new OrcamentoException(409, "Orçamento arquivado. O histórico é somente leitura.");
        if (h.Version != Version(version)) throw new OrcamentoException(409, "Versão desatualizada. Atualize o orçamento.");
        if (await c.ExecuteAsync(new CommandDefinition("UPDATE ORCAMENTO SET VERSAO=VERSAO+1,DTHRULTMODIFICACAO=@now WHERE CODORCAMENTO=@id AND VERSAO=@version", new { id = h.Id, version = Version(version), now }, tx, cancellationToken: ct)) != 1)
            throw new OrcamentoException(409, "O orçamento foi alterado por outra pessoa. Atualize antes de continuar.");
    }
    private async Task Audit(DbConnection c, DbTransaction tx, string type, string id, string action, CancellationToken ct)
    {
        // Only identifiers/decision codes enter the central audit, never quote text, files or chat.
        await c.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AUDITORIA_EVENTO(OCCURRED_UTC,MODULE,ACTION,RESULT,ENTITY_TYPE,ENTITY_ID,SUMMARY,ACTOR_ID,ACTOR_NAME,ACTOR_KIND,ORIGIN,CORRELATION_ID,OPERATION_ID,REQUEST_METHOD,REQUEST_PATH,IP_ADDRESS)
            VALUES(@now,'orcamentos',@action,'sucesso',@type,@id,@summary,
              CAST(RDB$GET_CONTEXT('USER_SESSION','AUD_ACTOR_ID') AS INTEGER),RDB$GET_CONTEXT('USER_SESSION','AUD_ACTOR_NAME'),'usuario','web',
              RDB$GET_CONTEXT('USER_SESSION','AUD_CORRELATION'),RDB$GET_CONTEXT('USER_SESSION','AUD_OPERATION'),RDB$GET_CONTEXT('USER_SESSION','AUD_METHOD'),
              RDB$GET_CONTEXT('USER_SESSION','AUD_PATH'),RDB$GET_CONTEXT('USER_SESSION','AUD_IP'))
            """, new { now = Now, action, type, id, summary = $"Orçamento {id}: {action}" }, tx, cancellationToken: ct));
    }
    private async Task Event(DbConnection c, DbTransaction tx, Head h, string action, string next, (int Id, string Name) actor, QuoteCommand? command, CancellationToken ct)
    {
        await c.ExecuteAsync(new CommandDefinition("""
            INSERT INTO ORCAMENTOEVENTO(CODORCAMENTO,REVISAO,ACAO,STATUSANTERIOR,STATUSPOSTERIOR,CODUSUARIO,NOMEAUTOR,MOTIVO,REFERENCIA,DESTINO,DTHRCRIACAO)
            VALUES(@id,@revision,@action,@before,@next,@actor,@name,@reason,@reference,@destination,@now)
            """, new { id = h.Id, revision = h.Revision, action, before = h.Status, next, actor = actor.Id, name = actor.Name, reason = command?.Reason ?? "", reference = command?.Reference ?? "", destination = command?.Destination ?? "", now = Now }, tx, cancellationToken: ct));
        await Audit(c, tx, "ORCAMENTO", h.Id.ToString(CultureInfo.InvariantCulture), action, ct);
    }
    private static async Task WriteItems(DbConnection c, DbTransaction tx, long id, int revision, QuoteSnapshot snapshot, CancellationToken ct)
    {
        await c.ExecuteAsync(new CommandDefinition("DELETE FROM ORCAMENTOITEM WHERE CODORCAMENTO=@id AND REVISAO=@revision", new { id, revision }, tx, cancellationToken: ct));
        for (var i = 0; i < snapshot.Draft.Items.Count; i++)
            await c.ExecuteAsync(new CommandDefinition("INSERT INTO ORCAMENTOITEM(CODORCAMENTO,REVISAO,POSICAO,CATEGORIA,DOCUMENTO,VALORBRUTO,DESCONTO,VALORFINAL) VALUES(@id,@revision,@position,@category,@json,@gross,@discount,@net)",
                new { id, revision, position = i + 1, category = snapshot.Draft.Items[i].Category, json = QuoteTextBlob.Json(snapshot.Draft.Items[i]), gross = snapshot.Calculation.Items[i].Gross, discount = snapshot.Calculation.Items[i].Discount, net = snapshot.Calculation.Items[i].Net }, tx, cancellationToken: ct));
    }
    public async Task<object> List(string? status, long? before, bool archived, CancellationToken ct)
    {
        if (status is not null && !new[] { "rascunho", "analise", "em_aprovacao", "devolvido", "aprovado", "enviado", "aceito", "pago", "encaminhado", "cancelado", "recusado" }.Contains(status)) throw new OrcamentoException(422, "Estado inválido.");
        await using var c = await db.OpenConnectionAsync(ct);
        var sql = HeadSql.Replace("SELECT o.CODORCAMENTO", "SELECT FIRST 51 o.CODORCAMENTO") + " WHERE o.ARQUIVADO=@archive AND (@status IS NULL OR o.STATUS=@status) AND (@before IS NULL OR o.CODORCAMENTO<@before) ORDER BY o.CODORCAMENTO DESC";
        var rows = (await c.QueryAsync<Head>(new CommandDefinition(sql, new { archive = archived ? 1 : 0, status, before }, cancellationToken: ct))).ToList();
        return new { success = true, data = rows.Take(50).Select(h => h.Item()), nextBefore = rows.Count > 50 ? rows[49].Id.ToString(CultureInfo.InvariantCulture) : null };
    }
    public async Task<QuoteDetail> Get(long id, CancellationToken ct)
    {
        await using var c = await db.OpenConnectionAsync(ct);
        // Consistent detail, including state and its revision/history, under concurrent edits.
        await using var tx = await c.BeginTransactionAsync(System.Data.IsolationLevel.Snapshot, ct);
        var h = await ReadHead(c, id, tx, ct);
        var events = await c.QueryAsync<EventRow>(new CommandDefinition("SELECT CODORCAMENTOEVENTO AS Id,REVISAO AS Revision,ACAO AS Action,STATUSANTERIOR AS BeforeStatus,STATUSPOSTERIOR AS AfterStatus,CODUSUARIO AS ActorId,NOMEAUTOR AS ActorName,MOTIVO AS Reason,REFERENCIA AS Reference,DESTINO AS Destination,DTHRCRIACAO AS Created FROM ORCAMENTOEVENTO WHERE CODORCAMENTO=@id ORDER BY CODORCAMENTOEVENTO DESC", new { id }, tx, cancellationToken: ct));
        var attachments = await c.QueryAsync<AttachmentRow>(new CommandDefinition("SELECT CODORCAMENTOANEXO AS Id,REVISAO AS Revision,NOME AS Name,MIME AS Mime,TAMANHO AS Size,SHA256 AS Sha256,TIPO AS Kind,CODUSUARIO AS ActorId,DTHRCRIACAO AS Created FROM ORCAMENTOANEXO WHERE CODORCAMENTO=@id ORDER BY CODORCAMENTOANEXO DESC", new { id }, tx, cancellationToken: ct));
        var revisions = await c.QueryAsync<RevisionRow>(new CommandDefinition("SELECT NUMERO AS Number,STATUS AS Status,DOCUMENTO AS Document,CODUSUARIO AS CreatedBy,NOMEAUTOR AS CreatedByName,DTHRCRIACAO AS Created FROM ORCAMENTOREVISAO WHERE CODORCAMENTO=@id ORDER BY NUMERO DESC", new { id }, tx, cancellationToken: ct));
        var editors = (await c.QueryAsync<int>(new CommandDefinition("SELECT CODUSUARIO FROM ORCAMENTO_USUARIO WHERE CODORCAMENTO=@id AND REVISAO=@revision", new { id, revision = h.Revision }, tx, cancellationToken: ct))).ToList();
        var analysisGate = await AnalysisGateFor(c, h, tx, ct);
        var emailSource = await EmailSourceFor(c, h, tx, ct);
        await tx.CommitAsync(ct);
        return new(h.Item(), h.Snapshot, events.Select(e => e.View()).ToList(), attachments.Select(a => a.View()).ToList(), revisions.Select(r => r.View()).ToList(), editors) { AnalysisGate = analysisGate, EmailSource = emailSource };
    }
    private sealed class EventRow
    {
        public long Id { get; set; } public int Revision { get; set; } public string Action { get; set; } = ""; public string BeforeStatus { get; set; } = ""; public string AfterStatus { get; set; } = ""; public int ActorId { get; set; } public string ActorName { get; set; } = ""; public string Reason { get; set; } = ""; public string Reference { get; set; } = ""; public string Destination { get; set; } = ""; public DateTime Created { get; set; }
        public QuoteEvent View() => new(Id.ToString(CultureInfo.InvariantCulture), Revision, Action, BeforeStatus, AfterStatus, ActorId, ActorName, Reason, Reference, Destination, QuoteCalculator.Utc(Created));
    }
    private sealed class RevisionRow
    {
        public int Number { get; set; } public string Status { get; set; } = ""; public string Document { get; set; } = ""; public int CreatedBy { get; set; } public string CreatedByName { get; set; } = ""; public DateTime Created { get; set; }
        public QuoteRevision View() => new(Number, Status, OrcamentoJson.Read<QuoteSnapshot>(Document), CreatedBy, CreatedByName, QuoteCalculator.Utc(Created));
    }
    private sealed class AttachmentRow
    {
        public long Id { get; set; } public int Revision { get; set; } public string Name { get; set; } = ""; public string Mime { get; set; } = ""; public long Size { get; set; } public string Sha256 { get; set; } = ""; public string Kind { get; set; } = ""; public int ActorId { get; set; } public DateTime Created { get; set; }
        public QuoteAttachment View() => new(Id.ToString(CultureInfo.InvariantCulture), Revision, Name, Mime, Size, Sha256, Kind, ActorId, QuoteCalculator.Utc(Created));
    }
    public async Task<List<object>> Clients(CancellationToken ct)
    {
        await using var c = await db.OpenConnectionAsync(ct);
        return (await c.QueryAsync<ClientRow>(new CommandDefinition("SELECT CODCLIENTE AS Id,NOME AS Name FROM CLIENTES ORDER BY NOME", cancellationToken: ct))).Select(r => (object)new { r.Id, r.Name }).ToList();
    }
    private sealed class ClientRow { public int Id { get; set; } public string Name { get; set; } = ""; }
    private static async Task<QuoteClient> Client(DbConnection c, int id, DbTransaction? tx, CancellationToken ct)
    {
        var name = await c.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT NOME FROM CLIENTES WHERE CODCLIENTE=@id", new { id }, tx, cancellationToken: ct)) ?? throw new OrcamentoException(404, "Cliente não encontrado.");
        var document = await c.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT DOCUMENTO FROM CLIENTECOMERCIAL WHERE CODCLIENTE=@id", new { id }, tx, cancellationToken: ct));
        return new(id, name, document is null ? new() : OrcamentoJson.Read<CommercialClient>(document));
    }
    private static async Task<(string Version, QuotePolicy Policy)> Policy(DbConnection c, DbTransaction? tx, CancellationToken ct)
    {
        var row = await c.QuerySingleAsync<PolicyRow>(new CommandDefinition("SELECT p.CODORCAMENTOPOLITICA AS Id,p.DOCUMENTO AS Document FROM ORCAMENTOPOLITICA p JOIN ORCAMENTOCONFIG s ON s.VERSAO=p.CODORCAMENTOPOLITICA AND s.CODORCAMENTOCONFIG=1", transaction: tx, cancellationToken: ct));
        return (row.Id.ToString(CultureInfo.InvariantCulture), OrcamentoJson.Read<QuotePolicy>(row.Document));
    }
    private sealed class PolicyRow { public long Id { get; set; } public string Document { get; set; } = ""; }
    public async Task<object> GetPolicy(CancellationToken ct) { await using var c = await db.OpenConnectionAsync(ct); var p = await Policy(c, null, ct); return new { version = p.Version, policy = p.Policy }; }
    public async Task<object> GetClient(int id, CancellationToken ct)
    {
        await using var c = await db.OpenConnectionAsync(ct); var client = await Client(c, id, null, ct);
        var version = await c.ExecuteScalarAsync<long?>(new CommandDefinition("SELECT VERSAO FROM CLIENTECOMERCIAL WHERE CODCLIENTE=@id", new { id }, cancellationToken: ct)) ?? 0;
        return new { version = version.ToString(CultureInfo.InvariantCulture), client };
    }
    public async Task SavePolicy(SavePolicyRequest request, CancellationToken ct)
    {
        QuoteCalculator.ValidatePolicy(request.Policy); QuoteCalculator.CheckText(request.Reason, "Motivo", 1000);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new OrcamentoException(422, "Justifique a alteração da política.");
        await using var c = await db.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct); var actor = await Actor(c, tx, ct);
        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition("INSERT INTO ORCAMENTOPOLITICA(DOCUMENTO,CODUSUARIO,MOTIVO,DTHRCRIACAO) VALUES(@json,@actor,@reason,@now) RETURNING CODORCAMENTOPOLITICA", new { json = QuoteTextBlob.Json(request.Policy), actor = actor.Id, reason = request.Reason, now = Now }, tx, cancellationToken: ct));
        if (await c.ExecuteAsync(new CommandDefinition("UPDATE ORCAMENTOCONFIG SET VERSAO=@id WHERE CODORCAMENTOCONFIG=1 AND VERSAO=@version", new { id, version = Version(request.Version) }, tx, cancellationToken: ct)) != 1) throw new OrcamentoException(409, "A política foi alterada. Atualize antes de salvar.");
        await Audit(c, tx, "ORCAMENTOPOLITICA", id.ToString(CultureInfo.InvariantCulture), "editar", ct); await tx.CommitAsync(ct);
    }
    public async Task SaveClient(int id, SaveClientRequest request, CancellationToken ct)
    {
        var d = request.Commercial;
        foreach (var text in new[] { d.FiscalId, d.Contact, d.Email }) QuoteCalculator.CheckText(text, "Dados comerciais", 254);
        if (d.MonthlyFee is < 0 or > 1000000000) throw new OrcamentoException(422, "Mensalidade inválida.");
        if (d.Email.Length > 0 && !System.Net.Mail.MailAddress.TryCreate(d.Email, out _)) throw new OrcamentoException(422, "E-mail inválido.");
        await using var c = await db.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct); _ = await Actor(c, tx, ct); _ = await Client(c, id, tx, ct);
        var version = Version(request.Version, true); var json = QuoteTextBlob.Json(d);
        if (version == 0) await c.ExecuteAsync(new CommandDefinition("INSERT INTO CLIENTECOMERCIAL(CODCLIENTE,VERSAO,DOCUMENTO) VALUES(@id,1,@json)", new { id, json }, tx, cancellationToken: ct));
        else if (await c.ExecuteAsync(new CommandDefinition("UPDATE CLIENTECOMERCIAL SET VERSAO=VERSAO+1,DOCUMENTO=@json WHERE CODCLIENTE=@id AND VERSAO=@version", new { id, json, version }, tx, cancellationToken: ct)) != 1) throw new OrcamentoException(409, "Dados comerciais alterados. Atualize antes de salvar.");
        await Audit(c, tx, "CLIENTECOMERCIAL", id.ToString(CultureInfo.InvariantCulture), "editar", ct); await tx.CommitAsync(ct);
    }
    public async Task<long> Create(CreateQuoteRequest request, CancellationToken ct)
    {
        await using var c = await db.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct); var actor = await Actor(c, tx, ct);
        var id = await CreateCore(c, tx, request, actor, ct); await tx.CommitAsync(ct); return id;
    }
    private async Task<long> CreateCore(DbConnection c, DbTransaction tx, CreateQuoteRequest request, (int Id, string Name) actor, CancellationToken ct)
    {
        var client = await Client(c, request.ClientId, tx, ct); var policy = await Policy(c, tx, ct);
        var draft = QuoteTechnicalEditing.Prepare(request.Draft, null, actor.Id, Now);
        draft = draft with { PaymentTerms = string.IsNullOrWhiteSpace(draft.PaymentTerms) ? policy.Policy.PaymentTerms : draft.PaymentTerms,
            ValidUntil = draft.ValidUntil ?? (policy.Policy.ValidityDays.HasValue ? DateOnly.FromDateTime(Now).AddDays(policy.Policy.ValidityDays.Value) : null) };
        var snapshot = new QuoteSnapshot(draft, client, policy.Policy, policy.Version, QuoteCalculator.Calculate(draft, policy.Policy, client.Commercial));
        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition("INSERT INTO ORCAMENTO(CODCLIENTE,REVISAO,STATUS,ARQUIVADO,VERSAO,DTHRULTMODIFICACAO) VALUES(@client,1,'rascunho',0,1,@now) RETURNING CODORCAMENTO", new { client = client.Id, now = Now }, tx, cancellationToken: ct));
        await c.ExecuteAsync(new CommandDefinition("INSERT INTO ORCAMENTOREVISAO(CODORCAMENTO,NUMERO,STATUS,DOCUMENTO,CODUSUARIO,NOMEAUTOR,DTHRCRIACAO) VALUES(@id,1,'rascunho',@json,@actor,@name,@now)", new { id, json = QuoteTextBlob.Json(snapshot), actor = actor.Id, name = actor.Name, now = Now }, tx, cancellationToken: ct));
        await c.ExecuteAsync(new CommandDefinition("INSERT INTO ORCAMENTO_USUARIO(CODORCAMENTO,REVISAO,CODUSUARIO) VALUES(@id,1,@actor)", new { id, actor = actor.Id }, tx, cancellationToken: ct));
        await WriteItems(c, tx, id, 1, snapshot, ct);
        await Event(c, tx, new() { Id = id, Revision = 1, Status = "" }, "criar", "rascunho", actor, null, ct); return id;
    }
    public async Task Save(long id, SaveQuoteRequest request, CancellationToken ct)
    {
        await using var c = await db.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct); var actor = await Actor(c, tx, ct); var h = await ReadHead(c, id, tx, ct);
        if (!OrcamentoWorkflow.Editable(h.Status)) throw new OrcamentoException(409, "Esta revisão é somente leitura. Crie uma nova revisão para alterar o conteúdo comercial.");
        if (h.Version != Version(request.Version)) throw new OrcamentoException(409, "Versão desatualizada. Atualize o orçamento.");
        var previous = h.Snapshot; var policy = request.RefreshPolicy ? await Policy(c, tx, ct) : (previous.PolicyVersion, previous.Policy);
        var client = request.RefreshClient ? await Client(c, h.ClientId, tx, ct) : previous.Client;
        var prepared = QuoteTechnicalEditing.Prepare(request.Draft, previous.Draft, actor.Id, Now);
        var snapshot = new QuoteSnapshot(prepared, client, policy.Item2, policy.Item1, QuoteCalculator.Calculate(prepared, policy.Item2, client.Commercial));
        if (OrcamentoJson.Write(snapshot) == h.Document) { await tx.CommitAsync(ct); return; }
        await Bump(c, tx, h, request.Version, Now, ct);
        await c.ExecuteAsync(new CommandDefinition("UPDATE ORCAMENTOREVISAO SET DOCUMENTO=@json WHERE CODORCAMENTO=@id AND NUMERO=@revision", new { id, revision = h.Revision, json = QuoteTextBlob.Json(snapshot) }, tx, cancellationToken: ct));
        await c.ExecuteAsync(new CommandDefinition("UPDATE OR INSERT INTO ORCAMENTO_USUARIO(CODORCAMENTO,REVISAO,CODUSUARIO) VALUES(@id,@revision,@actor) MATCHING(CODORCAMENTO,REVISAO,CODUSUARIO)", new { id, revision = h.Revision, actor = actor.Id }, tx, cancellationToken: ct));
        await WriteItems(c, tx, id, h.Revision, snapshot, ct); await Event(c, tx, h, "editar", h.Status, actor, null, ct); await tx.CommitAsync(ct);
    }
    public async Task Command(long id, QuoteCommand command, CancellationToken ct)
    {
        await using var c = await db.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct); var actor = await Actor(c, tx, ct); var h = await ReadHead(c, id, tx, ct);
        var isEditor = await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ORCAMENTO_USUARIO WHERE CODORCAMENTO=@id AND REVISAO=@revision AND CODUSUARIO=@actor", new { id, revision = h.Revision, actor = actor.Id }, tx, cancellationToken: ct)) > 0;
        var next = OrcamentoWorkflow.Next(h.Status, command, isEditor, h.Snapshot, DateOnly.FromDateTime(Now));
        if (command.Action is "solicitar_aprovacao" or "aprovar" or "enviar")
        {
            await RequireConfirmedEmailSource(c, h, tx, ct);
            var analysisGate = await AnalysisGateFor(c, h, tx, ct);
            if (analysisGate.Processing || analysisGate.AwaitingClassification || analysisGate.AwaitingApplication)
                throw new OrcamentoException(409, "Conclua ou cancele a análise por IA pendente antes de avançar a aprovação ou o envio.");
        }
        await Bump(c, tx, h, command.Version, Now, ct);
        if (command.Action == "nova_revisao")
        {
            h.Revision++;
            await c.ExecuteAsync(new CommandDefinition("INSERT INTO ORCAMENTOREVISAO(CODORCAMENTO,NUMERO,STATUS,DOCUMENTO,CODUSUARIO,NOMEAUTOR,DTHRCRIACAO) VALUES(@id,@revision,'rascunho',@json,@actor,@name,@now)", new { id, revision = h.Revision, json = new QuoteTextBlob(h.Document), actor = actor.Id, name = actor.Name, now = Now }, tx, cancellationToken: ct));
            // Retain all prior contributors: copying a revision must not enable self-approval laundering.
            await c.ExecuteAsync(new CommandDefinition("INSERT INTO ORCAMENTO_USUARIO(CODORCAMENTO,REVISAO,CODUSUARIO) SELECT CODORCAMENTO,@revision,CODUSUARIO FROM ORCAMENTO_USUARIO WHERE CODORCAMENTO=@id AND REVISAO=@previous", new { id, revision = h.Revision, previous = h.Revision - 1 }, tx, cancellationToken: ct));
            await c.ExecuteAsync(new CommandDefinition("UPDATE OR INSERT INTO ORCAMENTO_USUARIO(CODORCAMENTO,REVISAO,CODUSUARIO) VALUES(@id,@revision,@actor) MATCHING(CODORCAMENTO,REVISAO,CODUSUARIO)", new { id, revision = h.Revision, actor = actor.Id }, tx, cancellationToken: ct));
            await WriteItems(c, tx, id, h.Revision, h.Snapshot, ct);
        }
        else await c.ExecuteAsync(new CommandDefinition("UPDATE ORCAMENTOREVISAO SET STATUS=@next WHERE CODORCAMENTO=@id AND NUMERO=@revision", new { id, revision = h.Revision, next }, tx, cancellationToken: ct));
        await c.ExecuteAsync(new CommandDefinition("UPDATE ORCAMENTO SET STATUS=@next,REVISAO=@revision,ARQUIVADO=@archive WHERE CODORCAMENTO=@id", new { id, next, revision = h.Revision, archive = command.Action == "arquivar" ? 1 : 0 }, tx, cancellationToken: ct));
        await Event(c, tx, h, command.Action, next, actor, command, ct); await tx.CommitAsync(ct);
    }
}
