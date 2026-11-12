using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;
using ConversorHtml.Application.Interfaces;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.FormulasModelos;

namespace MdwConteudos.Api.Modules.StudioDrafts;

public sealed class StudioDraftService(IFirebirdConnectionFactory db, FormulasModelosService models, IVoiceSessionStore voice, IOptions<StudioDraftOptions> options)
{
    public static readonly JsonSerializerOptions Json = StudioDocumentEditor.Json;
    public static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    public void Enabled() { if (!options.Value.Enabled) throw new StudioException(503, "Editor integrado desabilitado. Habilite StudioDrafts somente após aplicar sua migração."); }
    public async Task<bool> Available(CancellationToken ct)
    {
        if (!options.Value.Enabled) return false;
        await using var c = await db.OpenConnectionAsync(ct);
        return await c.ExecuteScalarAsync<int>(Q("SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME IN ('STUDIORASCUNHO','STUDIOREVISAO','STUDIOMODELOESTADO','STUDIOPUBLICACAO')", null, null, ct)) == 4;
    }
    private static CommandDefinition Q(string sql, object? args, FbTransaction? tx, CancellationToken ct) => new(sql, args, tx, cancellationToken: ct);
    public sealed class DraftRow
    {
        public string Id { get; set; } = ""; public int Owner { get; set; } public long Revision { get; set; }
        public int Archived { get; set; } public string Document { get; set; } = ""; public DateTime UpdatedAt { get; set; }
        public DraftView View() => new(Id.Trim(), Revision, Archived == 1, JsonSerializer.Deserialize<StudioDocument>(Document, Json)!, DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc));
    }
    public sealed class RevisionRow
    {
        public int Number { get; set; } public string CommandId { get; set; } = ""; public string Sha { get; set; } = "";
        public string Type { get; set; } = ""; public string Source { get; set; } = ""; public string Group { get; set; } = "";
        public string Description { get; set; } = ""; public string Document { get; set; } = ""; public string Details { get; set; } = "{}";
        public DateTime CreatedAt { get; set; }
    }
    private const string RowSql = "SELECT CODRASCUNHO AS Id,CODUSUARIO AS Owner,REVISAO AS Revision,ARQUIVADO AS Archived,DOCUMENTO AS Document,DTHRULTMODIFICACAO AS UpdatedAt FROM STUDIORASCUNHO";
    private const string HistorySql = "SELECT NUMERO AS Number,CODCOMANDO AS CommandId,SHA256 AS Sha,TIPO AS Type,ORIGEM AS Source,GRUPOOPERACAO AS \"Group\",DESCRICAO AS Description,DOCUMENTO AS Document,DETALHES AS Details,DTHRCRIACAO AS CreatedAt FROM STUDIOREVISAO";
    private async Task<DraftRow> Owned(FbConnection c, FbTransaction? tx, string id, int owner, CancellationToken ct, bool locked = false)
        => await c.QuerySingleOrDefaultAsync<DraftRow>(Q(RowSql + " WHERE CODRASCUNHO=@id AND CODUSUARIO=@owner" + (locked ? " WITH LOCK" : ""), new { id, owner }, tx, ct))
           ?? throw new StudioException(404, "Rascunho não encontrado.");

    public async Task<DraftView> Create(int owner, DraftCreate request, CancellationToken ct, StudioDocument? importedDocument = null, string? importedId = null)
    {
        Enabled();
        var doc = importedDocument is null ? new StudioDocument(StudioDocumentEditor.Title(request.Nome), []) : StudioDocumentEditor.Copy(importedDocument);
        await using var c = await db.OpenConnectionAsync(ct);
        if (importedId is not null && await c.ExecuteScalarAsync<int>(Q("SELECT COUNT(*) FROM STUDIORASCUNHO WHERE CODRASCUNHO=@importedId AND CODUSUARIO=@owner", new { importedId, owner }, null, ct)) > 0)
            return (await Owned(c, null, importedId, owner, ct)).View();
        if (request.PublicacaoId is { } published)
        {
            var snapshotJson = await c.ExecuteScalarAsync<string?>(Q("SELECT DOCUMENTO FROM STUDIOPUBLICACAO WHERE CODPUBLICACAO=@published AND CODUSUARIO=@owner AND ESTADO='publicada'", new { published = published.ToString(), owner }, null, ct)) ?? throw new StudioException(404, "Publicação não encontrada.");
            var snapshot = JsonSerializer.Deserialize<StudioDocument>(snapshotJson, Json)!;
            doc = snapshot with { Nome = doc.Nome, ModeloOrigem = null, VersaoOrigem = null, Secoes = snapshot.Secoes.Select(s => s with { CodSecao = null }).ToList() };
        }
        if (request.ModeloId is { } modelId)
        {
            var model = await models.GetModeloAsync(modelId, owner, ct) ?? throw new StudioException(404, "Modelo não encontrado.");
            var version = await c.ExecuteScalarAsync<long>(Q("SELECT VERSAO FROM STUDIOMODELOESTADO WHERE CODMODELO=@modelId", new { modelId }, null, ct));
            var overrides = await c.ExecuteScalarAsync<string?>(Q("SELECT AJUSTES FROM STUDIOMODELOESTADO WHERE CODMODELO=@modelId", new { modelId }, null, ct));
            var custom = JsonSerializer.Deserialize<Dictionary<int, string?>>(overrides ?? "{}", Json)!;
            doc = new(model.Nome, model.Secoes.OrderBy(s => s.X).ThenBy(s => s.Y).Select(s => new StudioSection(StudioDocumentEditor.Token(), s.Nome,
                s.X >= 6 ? 2 : 1, s.Variaveis.OrderBy(v => v.Ordem).Select(v => new StudioItem(StudioDocumentEditor.Token(), v.CodVariavel,
                    ExibirGrafico: v.ExibirGrafico, NormalidadeOverride: custom.GetValueOrDefault(v.CodVariavel))).ToList(), s.CodSecao)).ToList(), modelId, version);
        }
        if (request.VoiceSessionId is { } sessionId)
        {
            var session = voice.Get(sessionId);
            if (session is null || session.OwnerUserId != owner.ToString()) throw new StudioException(404, "Sessão de voz não encontrada.");
            doc = session.ImportedDocumentJson is { } imported ? JsonSerializer.Deserialize<StudioDocument>(imported, Json)!
                : FromCampos(session.CamposScriptJson, doc.Nome, await models.ListVariaveisAsync(ct));
        }
        StudioDocumentEditor.Shape(doc);
        var id = importedId ?? StudioDocumentEditor.Token(); var now = DateTime.UtcNow; var json = JsonSerializer.Serialize(doc, Json);
        using var tx = c.BeginTransaction();
        await c.ExecuteAsync(Q("INSERT INTO STUDIORASCUNHO(CODRASCUNHO,CODUSUARIO,NOME,DOCUMENTO,DTHRCRIACAO,DTHRULTMODIFICACAO) VALUES(@id,@owner,@nome,@json,@now,@now)", new { id, owner, nome = doc.Nome, json, now }, tx, ct));
        await Record(c, tx, id, 0, new(StudioDocumentEditor.Token(), 0, "create", JsonSerializer.SerializeToElement(new { })), Hash(doc), doc, "Rascunho criado", new { }, ct);
        tx.Commit(); return new(id, 0, false, doc, now);
    }

    public async Task<IReadOnlyList<DraftView>> List(int owner, bool archived, CancellationToken ct)
    {
        Enabled(); await using var c = await db.OpenConnectionAsync(ct);
        return (await c.QueryAsync<DraftRow>(Q(RowSql + " WHERE CODUSUARIO=@owner AND ARQUIVADO=@archived ORDER BY DTHRULTMODIFICACAO DESC ROWS 100", new { owner, archived = archived ? 1 : 0 }, null, ct))).Select(x => x.View()).ToArray();
    }
    public async Task<DraftView> Get(string id, int owner, CancellationToken ct)
    { Enabled(); await using var c = await db.OpenConnectionAsync(ct); return (await Owned(c, null, id, owner, ct)).View(); }
    public async Task<object> Publications(int owner, CancellationToken ct)
    {
        Enabled(); await using var c = await db.OpenConnectionAsync(ct);
        return await c.QueryAsync<PublicationSummary>(Q("SELECT CODPUBLICACAO AS Id,NOME AS Nome,CODMODELO AS ModeloId,DTHRCRIACAO AS CriadaEm FROM STUDIOPUBLICACAO WHERE CODUSUARIO=@owner AND ESTADO='publicada' ORDER BY DTHRCRIACAO DESC ROWS 100", new { owner }, null, ct));
    }
    public sealed record PublicationSummary(string Id, string Nome, int? ModeloId, DateTime CriadaEm);
    public async Task DeletePublication(string id, int owner, CancellationToken ct)
    {
        Enabled(); await using var c = await db.OpenConnectionAsync(ct);
        if (await c.ExecuteAsync(Q("DELETE FROM STUDIOPUBLICACAO WHERE CODPUBLICACAO=@id AND CODUSUARIO=@owner AND ESTADO='publicada'", new { id, owner }, null, ct)) != 1)
            throw new StudioException(404, "Publicação não encontrada.");
    }
    public async Task<IReadOnlyList<RevisionRow>> History(string id, int owner, CancellationToken ct, bool includeDocument = true)
    {
        Enabled(); await using var c = await db.OpenConnectionAsync(ct); await Owned(c, null, id, owner, ct);
        return (await c.QueryAsync<RevisionRow>(Q((includeDocument ? HistorySql : HistorySql.Replace("DOCUMENTO AS Document,", "")) + " WHERE CODRASCUNHO=@id ORDER BY NUMERO", new { id }, null, ct))).ToArray();
    }
    public async Task<RevisionRow> Revision(string id, int owner, int number, CancellationToken ct)
    {
        Enabled(); await using var c = await db.OpenConnectionAsync(ct); await Owned(c, null, id, owner, ct);
        return await c.QuerySingleOrDefaultAsync<RevisionRow>(Q(HistorySql + " WHERE CODRASCUNHO=@id AND NUMERO=@number", new { id, number }, null, ct)) ?? throw new StudioException(404, "Revisão não encontrada.");
    }
    public async Task<DraftView> Command(string id, int owner, DraftCommand command, CancellationToken ct)
    {
        Enabled(); if (!Guid.TryParse(command.CommandId, out _) || command.Source is not ("manual" or "voz" or "sistema") || command.Group?.Length > 100) throw new StudioException(400, "Identificador ou origem inválida.");
        IReadOnlyList<VariavelOpcao> catalog = command.Type is "rename" or "createSection" or "editSection" or "archive" or "unarchive"
            ? [] : await models.ListVariaveisAsync(ct);
        await using var c = await db.OpenConnectionAsync(ct);
        using var tx = c.BeginTransaction();
        var row = await Owned(c, tx, id, owner, ct, true);
        var sha = Hash(command);
        var previous = await c.QuerySingleOrDefaultAsync<RevisionRow>(Q(HistorySql + " WHERE CODRASCUNHO=@id AND CODCOMANDO=@commandId", new { id, commandId = command.CommandId }, tx, ct));
        if (previous is not null)
        {
            if (previous.Sha.Trim() != sha) throw new StudioException(409, "Identificador já utilizado para outro comando.");
            tx.Commit(); return row.View() with { Revision = previous.Number, Document = JsonSerializer.Deserialize<StudioDocument>(previous.Document, Json)! };
        }
        if (row.Revision != command.ExpectedRevision) throw new StudioException(409, "Rascunho alterado em outra sessão. Recarregue antes de editar.");
        if (row.Archived == 1 && command.Type is not "unarchive") throw new StudioException(409, "Rascunho arquivado.");
        var doc = row.View().Document;
        object details = new { };
        if (command.Type is "undo" or "redo" or "restore")
        {
            var history = (await c.QueryAsync<RevisionRow>(Q(HistorySql + " WHERE CODRASCUNHO=@id ORDER BY NUMERO", new { id }, tx, ct))).ToArray();
            if (command.Type == "restore")
            {
                var number = command.Payload.GetProperty("revision").GetInt32();
                doc = JsonSerializer.Deserialize<StudioDocument>(history.FirstOrDefault(h => h.Number == number)?.Document ?? throw new StudioException(404, "Versão não encontrada."), Json)!;
            }
            else
            {
                var undone = new HashSet<int>();
                foreach (var h in history.Where(h => h.Type is "undo" or "redo"))
                    foreach (var n in JsonSerializer.Deserialize<int[]>(h.Details, Json)!) { if (h.Type == "undo") undone.Add(n); else undone.Remove(n); }
                var eligible = history.Where(h => h.Number > 0 && h.Type is not ("undo" or "redo" or "archive" or "unarchive" or "published")).ToArray();
                RevisionRow[] targets;
                if (command.Type == "undo")
                {
                    var group = command.Payload.TryGetProperty("group", out var g) ? g.GetString() : eligible.LastOrDefault(h => !undone.Contains(h.Number))?.Group.Trim();
                    targets = eligible.Where(h => h.Group.Trim() == group && !undone.Contains(h.Number)).OrderByDescending(h => h.Number).ToArray();
                }
                else targets = eligible.Where(h => undone.Contains(h.Number)).GroupBy(h => h.Group.Trim()).OrderByDescending(g => g.Max(h => h.Number)).FirstOrDefault()?.OrderBy(h => h.Number).ToArray() ?? [];
                if (targets.Length == 0) throw new StudioException(409, "Não há operação compatível para desfazer/refazer.");
                foreach (var target in targets)
                {
                    var before = JsonSerializer.Deserialize<StudioDocument>(history.Single(h => h.Number == target.Number - 1).Document, Json)!;
                    var after = JsonSerializer.Deserialize<StudioDocument>(target.Document, Json)!;
                    doc = command.Type == "undo" ? StudioDocumentEditor.Reverse(doc, before, after) : StudioDocumentEditor.Reverse(doc, after, before);
                }
                details = targets.Select(t => t.Number).ToArray();
                if (doc.ImportedJson is not null) StudioJsonImport.EnsureSafeChange(row.View().Document, doc);
                else if (StudioDocumentEditor.Validate(doc, catalog).Any(p => p.Code is "cycle" or "duplicate" or "dependency")) throw new StudioException(409, "A reversão invalidaria dependências do rascunho.");
            }
        }
        else if (command.Type is not ("archive" or "unarchive")) doc = StudioDocumentEditor.Apply(doc, command, catalog);
        StudioDocumentEditor.Shape(doc);
        var revision = row.Revision + 1; var now = DateTime.UtcNow;
        var archived = command.Type == "archive" ? 1 : command.Type == "unarchive" ? 0 : row.Archived;
        var json = JsonSerializer.Serialize(doc, Json);
        await c.ExecuteAsync(Q("UPDATE STUDIORASCUNHO SET REVISAO=@revision,NOME=@nome,DOCUMENTO=@json,ARQUIVADO=@archived,DTHRULTMODIFICACAO=@now WHERE CODRASCUNHO=@id", new { revision, nome = doc.Nome, json, archived, now, id }, tx, ct));
        await Record(c, tx, id, revision, command, sha, doc, Description(command.Type), details, ct);
        tx.Commit(); return new(id, revision, archived == 1, doc, now);
    }
    private static Task Record(FbConnection c, FbTransaction tx, string id, long number, DraftCommand command, string sha, StudioDocument doc, string description, object details, CancellationToken ct)
        => c.ExecuteAsync(Q("INSERT INTO STUDIOREVISAO(CODRASCUNHO,NUMERO,CODCOMANDO,SHA256,TIPO,ORIGEM,GRUPOOPERACAO,DESCRICAO,DOCUMENTO,DETALHES,DTHRCRIACAO) VALUES(@id,@number,@commandId,@sha,@type,@source,@grp,@description,@document,@details,@now)",
            new { id, number, commandId = command.CommandId, sha, type = command.Type, source = command.Source, grp = command.Group ?? command.CommandId, description, document = JsonSerializer.Serialize(doc, Json), details = JsonSerializer.Serialize(details, Json), now = DateTime.UtcNow }, tx, ct));
    private static string Description(string type) => type switch { "createSection" => "Seção criada", "editSection" => "Seção reorganizada", "addMeasures" => "Medidas e dependências incluídas", "moveItem" => "Medida movida", "editItem" => "Medida atualizada", "undo" => "Operação desfeita", "redo" => "Operação refeita", "importSections" => "Importação revisada aplicada", "restore" => "Versão restaurada", _ => "Rascunho atualizado" };

    public async Task Delete(string id, int owner, long revision, CancellationToken ct)
    {
        Enabled(); await using var c = await db.OpenConnectionAsync(ct); using var tx = c.BeginTransaction();
        var row = await Owned(c, tx, id, owner, ct, true);
        if (row.Revision != revision) throw new StudioException(409, "Revisão mudou. Refaça a confirmação de exclusão.");
        await c.ExecuteAsync(Q("DELETE FROM STUDIOPUBLICACAO WHERE CODRASCUNHO=@id AND ESTADO='pendente'", new { id }, tx, ct));
        await c.ExecuteAsync(Q("DELETE FROM STUDIOREVISAO WHERE CODRASCUNHO=@id", new { id }, tx, ct));
        await c.ExecuteAsync(Q("DELETE FROM STUDIORASCUNHO WHERE CODRASCUNHO=@id AND CODUSUARIO=@owner", new { id, owner }, tx, ct));
        tx.Commit();
    }

    public async Task<object> Validate(string id, int owner, CancellationToken ct)
    {
        var draft = await Get(id, owner, ct);
        if (draft.Document.ImportedJson is not null) return new { revision = draft.Revision, metadataHash = Hash(draft.Document), problems = StudioDocumentEditor.Validate(draft.Document, []) };
        var catalog = await models.ListVariaveisAsync(ct);
        return new { revision = draft.Revision, metadataHash = Hash(catalog), problems = StudioDocumentEditor.Validate(draft.Document, catalog) };
    }
    public async Task<(GeneratedModel File, long Revision, string MetadataHash)> Export(string id, int owner, long revision, string format, CancellationToken ct)
    {
        var draft = await Get(id, owner, ct);
        if (draft.Revision != revision) throw new StudioException(409, "Preview desatualizado. Gere novamente.");
        if (draft.Document.ImportedJson is not null)
        {
            if (format != "json") throw new StudioException(400, "Este modelo importado só pode ser exportado como JSON completo.");
            return (new GeneratedModel("modelo-importado.json", StudioJsonImport.Export(draft.Document), "application/json; charset=utf-8"), revision, Hash(draft.Document));
        }
        var catalog = await models.ListVariaveisAsync(ct); var errors = StudioDocumentEditor.Validate(draft.Document, catalog);
        if (errors.Count > 0) throw new StudioException(400, string.Join(" ", errors.Select(e => e.Message)));
        var file = await models.RenderCompositionAsync(StudioDocumentEditor.Composition(draft.Document), format, ct);
        var latest = await models.ListVariaveisAsync(ct);
        if (Hash(catalog) != Hash(latest)) throw new StudioException(409, "Catálogo alterado durante a geração. Gere novamente.");
        return (file, revision, Hash(latest));
    }

    public async Task<object> Prepare(string id, int owner, PublicationRequest request, CancellationToken ct)
    {
        var draft = await Get(id, owner, ct);
        if (draft.Document.ImportedJson is not null) throw new StudioException(400, "Este modelo importado pode ser recuperado como rascunho e baixado em JSON. A publicação perderia componentes.");
        if (draft.Revision != request.ExpectedRevision || draft.Archived) throw new StudioException(409, "Revisão indisponível para publicação.");
        var doc = draft.Document with { Nome = StudioDocumentEditor.Title(request.Nome) };
        var catalog = await models.ListVariaveisAsync(ct); var errors = StudioDocumentEditor.Validate(doc, catalog);
        if (errors.Count > 0) throw new StudioException(400, string.Join(" ", errors.Select(e => e.Message)));
        await using var c = await db.OpenConnectionAsync(ct);
        long? version = null;
        if (request.ModeloId is { } modelId)
        {
            if (await models.GetModeloAsync(modelId, owner, ct) is null) throw new StudioException(404, "Modelo não encontrado.");
            version = await c.ExecuteScalarAsync<long>(Q("SELECT VERSAO FROM STUDIOMODELOESTADO WHERE CODMODELO=@modelId", new { modelId }, null, ct));
            if (doc.ModeloOrigem == modelId && doc.VersaoOrigem != version) throw new StudioException(409, "Modelo original alterado. Reabra-o ou salve como cópia.");
        }
        var proposal = StudioDocumentEditor.Token(); var metadata = Hash(catalog);
        await c.ExecuteAsync(Q("INSERT INTO STUDIOPUBLICACAO(CODPUBLICACAO,CODUSUARIO,CODRASCUNHO,REVISAO,CODMODELO,VERSAOMODELO,NOME,ESTADO,DOCUMENTO,HASHCATALOGO,DTHRCRIACAO) VALUES(@proposal,@owner,@id,@revision,@modelId,@version,@name,'pendente',@document,@metadata,@now)",
            new { proposal, owner, id, revision = draft.Revision, modelId = request.ModeloId, version, name = doc.Nome, document = JsonSerializer.Serialize(doc, Json), metadata, now = DateTime.UtcNow }, null, ct));
        return new { proposalId = proposal, revision = draft.Revision, modeloId = request.ModeloId, nome = doc.Nome, sections = doc.Secoes.Count, items = doc.Secoes.Sum(s => s.Itens.Count) };
    }
    private sealed class Publication
    {
        public string Id { get; set; } = ""; public string? DraftId { get; set; } public long Revision { get; set; }
        public int? ModelId { get; set; } public long? Version { get; set; } public string State { get; set; } = "";
        public string Document { get; set; } = ""; public string Metadata { get; set; } = "";
    }
    public async Task<object> Confirm(string id, int owner, string proposalId, CancellationToken ct)
    {
        Enabled(); var catalog = await models.ListVariaveisAsync(ct);
        await using var c = await db.OpenConnectionAsync(ct); using var tx = c.BeginTransaction();
        var row = await Owned(c, tx, id, owner, ct, true);
        var p = await c.QuerySingleOrDefaultAsync<Publication>(Q("SELECT CODPUBLICACAO AS Id,CODRASCUNHO AS DraftId,REVISAO AS Revision,CODMODELO AS ModelId,VERSAOMODELO AS Version,ESTADO AS State,DOCUMENTO AS Document,HASHCATALOGO AS Metadata FROM STUDIOPUBLICACAO WHERE CODPUBLICACAO=@proposalId AND CODUSUARIO=@owner AND CODRASCUNHO=@id WITH LOCK", new { proposalId, owner, id }, tx, ct)) ?? throw new StudioException(404, "Proposta não encontrada.");
        if (p.State == "publicada") { tx.Commit(); return new { modeloId = p.ModelId, published = true }; }
        if (row.Revision != p.Revision || row.Archived == 1 || Hash(catalog) != p.Metadata.Trim()) throw new StudioException(409, "Rascunho ou catálogo mudou. Prepare uma nova publicação.");
        var doc = JsonSerializer.Deserialize<StudioDocument>(p.Document, Json)!;
        var modelId = p.ModelId;
        if (modelId.HasValue)
        {
            var exists = await c.ExecuteScalarAsync<int>(Q("SELECT COUNT(*) FROM MODELOMODOTEXTO WHERE CODMODELO=@modelId AND CODUSUARIO=@owner", new { modelId, owner }, tx, ct));
            if (exists == 0) throw new StudioException(404, "Modelo de destino indisponível.");
            var current = await c.ExecuteScalarAsync<long>(Q("SELECT VERSAO FROM STUDIOMODELOESTADO WHERE CODMODELO=@modelId WITH LOCK", new { modelId }, tx, ct));
            if (current != p.Version) throw new StudioException(409, "Modelo alterado após a proposta.");
        }
        else modelId = await c.ExecuteScalarAsync<int>(Q("INSERT INTO MODELOMODOTEXTO(NOME,CODUSUARIO,DTHRULTMODIFICACAO) VALUES(@name,@owner,@now) RETURNING CODMODELO", new { name = doc.Nome, owner, now = DateTime.UtcNow }, tx, ct));
        await models.WriteCompositionAsync(c, tx, modelId.Value, StudioDocumentEditor.Composition(doc, p.ModelId is null || doc.ModeloOrigem != p.ModelId), owner, ct);
        var version = await c.ExecuteScalarAsync<long>(Q("SELECT VERSAO FROM STUDIOMODELOESTADO WHERE CODMODELO=@modelId", new { modelId }, tx, ct));
        var savedSections = (await c.QueryAsync<(int CodSecao, string Nome)>(Q("SELECT CODSECAO,NOME FROM SECAOMODOTEXTO WHERE CODMODELO=@modelId", new { modelId }, tx, ct))).ToDictionary(s => s.Nome, s => s.CodSecao, StringComparer.OrdinalIgnoreCase);
        doc = doc with { ModeloOrigem = modelId, VersaoOrigem = version, Secoes = doc.Secoes.Select(s => s with { CodSecao = savedSections[s.Nome] }).ToList() };
        var newRevision = row.Revision + 1;
        await c.ExecuteAsync(Q("UPDATE STUDIORASCUNHO SET DOCUMENTO=@json,REVISAO=@newRevision,DTHRULTMODIFICACAO=@now WHERE CODRASCUNHO=@id", new { json = JsonSerializer.Serialize(doc, Json), newRevision, now = DateTime.UtcNow, id }, tx, ct));
        await Record(c, tx, id, newRevision, new(proposalId, row.Revision, "published", JsonSerializer.SerializeToElement(new { }), Source: "sistema"), Hash(proposalId), doc, "Modelo publicado com confirmação", new { }, ct);
        await c.ExecuteAsync(Q("UPDATE STUDIOPUBLICACAO SET ESTADO='publicada',CODMODELO=@modelId WHERE CODPUBLICACAO=@proposalId", new { modelId, proposalId }, tx, ct));
        tx.Commit(); return new { modeloId = modelId, published = true };
    }

    public async Task<bool> PublicationCreatesModel(string id, int owner, string proposalId, CancellationToken ct)
    {
        Enabled(); await using var c = await db.OpenConnectionAsync(ct); await Owned(c, null, id, owner, ct);
        var p = await c.QuerySingleOrDefaultAsync<Publication>(Q("SELECT CODMODELO AS ModelId FROM STUDIOPUBLICACAO WHERE CODPUBLICACAO=@proposalId AND CODUSUARIO=@owner AND CODRASCUNHO=@id", new { proposalId, owner, id }, null, ct)) ?? throw new StudioException(404, "Proposta não encontrada.");
        return p.ModelId is null;
    }

    public async Task<IReadOnlyList<VariavelOpcao>> Search(string term, CancellationToken ct)
    {
        Enabled(); var catalog = await models.ListVariaveisAsync(ct);
        await using var c = await db.OpenConnectionAsync(ct);
        var alternatives = (await c.QueryAsync<(int CodVariavel, string Alternativa)>(Q("SELECT CODVARIAVEL,ALTERNATIVA FROM VARIAVEISALTERNATIVAS", null, null, ct))).ToLookup(x => x.CodVariavel, x => x.Alternativa);
        var compare = System.Globalization.CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
        return catalog.Where(v => compare.IndexOf($"{v.Nome} {v.Sigla} {v.Abreviacao} {v.Grupo} {string.Join(' ', alternatives[v.CodVariavel])}", term,
            System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0).Take(50).ToArray();
    }

    public static StudioDocument FromCampos(string json, string name, IReadOnlyList<VariavelOpcao> catalog)
    {
        var root = JsonNode.Parse(json); var doc = new StudioDocument(name, []); StudioSection? current = null;
        foreach (var node in root?["camposScript"]?.AsArray() ?? [])
        {
            if (node is null) continue;
            if (node["tipo"]?.GetValue<string>() == "etiqueta") { current = new(StudioDocumentEditor.Token(), node["descricao"]?.GetValue<string>() ?? "Seção", node["coluna"]?.GetValue<int>() == 2 ? 2 : 1, []); doc.Secoes.Add(current); continue; }
            if (current is null) { current = new(StudioDocumentEditor.Token(), "Dados gerais", 1, []); doc.Secoes.Add(current); }
            var token = node["nome"]?.GetValue<string>(); var label = node["descricao"]?.GetValue<string>();
            var candidates = catalog.Where(v => string.Equals(v.Codigo ?? v.Sigla, token, StringComparison.OrdinalIgnoreCase) || string.Equals(v.Nome, label, StringComparison.OrdinalIgnoreCase)).ToArray();
            current.Itens.Add(new(StudioDocumentEditor.Token(), candidates.Length == 1 ? candidates[0].CodVariavel : null, label));
        }
        return doc;
    }
}
