using System.Globalization;
using System.Security.Cryptography;
using Dapper;

namespace MdwConteudos.Api.Modules.Orcamentos;

public sealed partial class OrcamentoService
{
    public async Task<string> QueueAnalysis(long id, QuoteAiRequest request, OrcamentoAiOptions options, CancellationToken ct, bool classify = false)
    {
        if (!options.Enabled) throw new OrcamentoException(503, "Análise por IA desabilitada neste ambiente.");
        if (!request.ConfirmExternalProcessing) throw new OrcamentoException(422, "Confirme o envio do escopo e dos anexos selecionados ao Cursor.");
        if (request.AttachmentIds is null || request.AttachmentIds.Count > 5 || request.AttachmentIds.Distinct().Count() != request.AttachmentIds.Count) throw new OrcamentoException(422, "Selecione até cinco anexos distintos.");
        var fileIds = request.AttachmentIds.Select(s => Version(s)).ToArray();
        QuoteCalculator.CheckText(options.Model, "Modelo de IA", 80);
        if (classify) { QuoteCalculator.CheckText(request.Briefing, "Briefing", 4000); if (string.IsNullOrWhiteSpace(request.Briefing)) throw new OrcamentoException(422, "Informe o que deseja que a IA analise."); }
        await using var c = await db.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        var actor = await Actor(c, tx, ct); var h = await ReadHead(c, id, tx, ct);
        if (!OrcamentoWorkflow.Editable(h.Status)) throw new OrcamentoException(409, "Análise exige revisão editável.");
        await RequireConfirmedEmailSource(c, h, tx, ct);
        await Bump(c, tx, h, request.Version, Now, ct);
        if (await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ORCAMENTOANALISE WHERE CODORCAMENTO=@id AND STATUS IN ('pendente','executando')", new { id }, tx, cancellationToken: ct)) > 0)
            throw new OrcamentoException(409, "Já existe análise pendente ou em execução neste orçamento.");
        if (await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ORCAMENTOANALISE WHERE CODORCAMENTO=@id", new { id }, tx, cancellationToken: ct)) >= 50)
            throw new OrcamentoException(422, "Limite de 50 análises por orçamento atingido.");
        var sources = new List<QuoteAiSource>(); var images = new List<QuoteAiImage>(); long total = 0;
        foreach (var file in fileIds)
        {
            var row = await c.QuerySingleOrDefaultAsync<FileRow>(new CommandDefinition("SELECT CONTEUDO AS Content,NOME AS Name,MIME AS Mime FROM ORCAMENTOANEXO WHERE CODORCAMENTOANEXO=@file AND CODORCAMENTO=@id AND REVISAO=@revision AND TIPO='escopo'", new { file, id, revision = h.Revision }, tx, cancellationToken: ct))
                ?? throw new OrcamentoException(422, "Anexo deve ser material de escopo da revisão atual deste orçamento.");
            total += row.Content.Length;
            if (total > 8 * 1024 * 1024) throw new OrcamentoException(422, "Anexos da análise limitados a 8 MB no total.");
            var validated = QuoteFileValidation.Validate(row.Name, row.Content);
            var hash = Convert.ToHexString(SHA256.HashData(row.Content)).ToLowerInvariant();
            if (validated.Mime is "image/png" or "image/jpeg") images.Add(new(row.Name, validated.Mime, Convert.ToBase64String(row.Content)));
            else sources.Add(new(row.Name, QuoteAiExtraction.Text(row.Name, row.Content), hash));
        }
        var draft = h.Snapshot.Draft;
        var existing = string.Join("\n", draft.Items.Select(i => $"{i.Category}: {i.Description}"));
        if (draft.Request.Length + draft.Title.Length + existing.Length + sources.Sum(s => s.Text.Length) > 50_000) throw new OrcamentoException(422, "Contexto de análise limitado a 50 mil caracteres; selecione menos documentos.");
        if (string.IsNullOrWhiteSpace(draft.Request) && draft.Items.All(i => string.IsNullOrWhiteSpace(i.Description)) && sources.Count == 0 && images.Count == 0) throw new OrcamentoException(422, "Informe o escopo ou selecione documentos para analisar.");
        var input = new QuoteAiInput(draft.Title, draft.Request, existing, sources, images);
        if (classify)
        {
            var technical = await TechnicalCatalog(c, tx, ct);
            input = input with { Phase = "classificacao", Briefing = request.Briefing, CatalogVersion = technical.Version, Catalog = technical.Catalog, Technologies = await Technologies(c, tx, ct) };
        }
        CheckAiContextLimit(input);
        var job = await c.ExecuteScalarAsync<long>(new CommandDefinition("INSERT INTO ORCAMENTOANALISE(CODORCAMENTO,REVISAO,VERSAOORCAMENTO,STATUS,ENTRADA,MODELO,CODUSUARIO,DTHRCRIACAO,ETAPA,CONTEXTO) VALUES(@id,@revision,@version,'pendente',@input,@model,@actor,@now,@phase,@context) RETURNING CODORCAMENTOANALISE",
            new { id, revision = h.Revision, version = h.Version + 1, input = QuoteTextBlob.Json(input), model = options.Model, actor = actor.Id, now = Now, phase = input.Phase, context = QuoteTextBlob.Json(Metadata(input)) }, tx, cancellationToken: ct));
        await Event(c, tx, h, "analisar_ia", h.Status, actor, null, ct); await tx.CommitAsync(ct);
        return job.ToString(CultureInfo.InvariantCulture);
    }
    public sealed class AiRow
    {
        public long Id { get; set; } public long QuoteId { get; set; } public int Revision { get; set; } public long Version { get; set; }
        public string Status { get; set; } = ""; public string Input { get; set; } = ""; public string? Result { get; set; } public string? Error { get; set; }
        public string Model { get; set; } = ""; public int ActorId { get; set; } public DateTime Created { get; set; }
        public string Phase { get; set; } = "analise"; public long? Origin { get; set; } public string? Metadata { get; set; }
        public object View() => new { id = Id.ToString(CultureInfo.InvariantCulture), revision = Revision, version = Version.ToString(CultureInfo.InvariantCulture), status = Status, model = Model, error = Error,
            result = Result is null ? null : OrcamentoJson.Read<QuoteAiSuggestion>(Result), phase = Phase, origin = Origin?.ToString(CultureInfo.InvariantCulture), context = Metadata is null ? null : OrcamentoJson.Read<QuoteAiMetadata>(Metadata), actorId = ActorId, createdAtUtc = QuoteCalculator.Utc(Created) };
    }
    internal const string AiSql = "SELECT CODORCAMENTOANALISE AS Id,CODORCAMENTO AS QuoteId,REVISAO AS Revision,VERSAOORCAMENTO AS Version,STATUS AS Status,ENTRADA AS Input,RESULTADO AS Result,ERRO AS Error,MODELO AS Model,CODUSUARIO AS ActorId,DTHRCRIACAO AS Created,ETAPA AS Phase,CODANALISEORIGEM AS Origin,CONTEXTO AS Metadata FROM ORCAMENTOANALISE";
    public async Task<object> Analyses(long id, CancellationToken ct)
    {
        await using var c = await db.OpenConnectionAsync(ct); _ = await Actor(c, null, ct); _ = await ReadHead(c, id, ct: ct);
        var rows = await c.QueryAsync<AiRow>(new CommandDefinition(AiSql.Replace("SELECT ", "SELECT FIRST 10 ").Replace("ENTRADA AS Input,", "") + " WHERE CODORCAMENTO=@id ORDER BY CODORCAMENTOANALISE DESC", new { id }, cancellationToken: ct));
        return new { success = true, data = rows.Select(r => r.View()) };
    }
    public async Task CancelAnalysis(long id, long job, CancellationToken ct)
    {
        await using var c = await db.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct); var actor = await Actor(c, tx, ct); var h = await ReadHead(c, id, tx, ct);
        if (await c.ExecuteAsync(new CommandDefinition("UPDATE ORCAMENTOANALISE SET STATUS='cancelada',DTHRFIM=@now WHERE CODORCAMENTOANALISE=@job AND CODORCAMENTO=@id AND STATUS IN ('pendente','executando','concluida') AND NOT EXISTS(SELECT 1 FROM ORCAMENTOANALISE child WHERE child.CODANALISEORIGEM=@job)", new { id, job, now = Now }, tx, cancellationToken: ct)) != 1)
            throw new OrcamentoException(409, "Análise já finalizada ou inexistente.");
        await Event(c, tx, h, "cancelar_ia", h.Status, actor, null, ct); await tx.CommitAsync(ct);
    }
    private static async Task<QuoteAnalysisGate> AnalysisGateFor(System.Data.Common.DbConnection c, Head h, System.Data.Common.DbTransaction? tx, CancellationToken ct)
    {
        var rows = (await c.QueryAsync<AiRow>(new CommandDefinition("SELECT CODORCAMENTOANALISE AS Id,ETAPA AS Phase,STATUS AS Status,VERSAOORCAMENTO AS Version,CODANALISEORIGEM AS Origin FROM ORCAMENTOANALISE WHERE CODORCAMENTO=@id AND REVISAO=@revision", new { id = h.Id, revision = h.Revision }, tx, cancellationToken: ct))).ToList();
        var origins = rows.Where(r => r.Origin.HasValue).Select(r => r.Origin!.Value).ToHashSet();
        return new(rows.Any(r => r.Status is "pendente" or "executando"),
            rows.Any(r => r.Status == "concluida" && r.Version == h.Version && r.Phase == "classificacao" && !origins.Contains(r.Id)),
            rows.Any(r => r.Status == "concluida" && r.Version == h.Version && r.Phase == "analise"));
    }
    public async Task ApplyAnalysis(long id, long job, QuoteAiApply request, CancellationToken ct)
    {
        await using var c = await db.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct); var actor = await Actor(c, tx, ct); var h = await ReadHead(c, id, tx, ct);
        var analysis = await c.QuerySingleOrDefaultAsync<AiRow>(new CommandDefinition(AiSql + " WHERE CODORCAMENTOANALISE=@job AND CODORCAMENTO=@id", new { id, job }, tx, cancellationToken: ct)) ?? throw new OrcamentoException(404, "Análise não encontrada.");
        if (analysis.Phase != "analise" || analysis.Status != "concluida" || analysis.Result is null || analysis.Revision != h.Revision || analysis.Version != h.Version || !OrcamentoWorkflow.Editable(h.Status))
            throw new OrcamentoException(409, "Sugestão já utilizada, superada ou revisão não editável. Solicite uma nova análise.");
        var input = OrcamentoJson.Read<QuoteAiInput>(analysis.Input);
        var result = QuoteAiSuggestion.ValidateForInput(QuoteAiSuggestion.Parse(analysis.Result), input);
        if (request.Items is null || request.Items.Count == 0 || request.Items.Count > 100 || request.Items.Distinct().Count() != request.Items.Count || request.Items.Any(i => i < 0 || i >= result.Items.Count)) throw new OrcamentoException(422, "Selecione itens válidos da análise.");
        var snapshot = h.Snapshot; var draft = snapshot.Draft with { Items = [.. snapshot.Draft.Items, .. request.Items.Select(i => QuoteAiSuggestion.ToItem(result.Items[i]) with { Key = Guid.NewGuid().ToString("N"), TechnicalContext = input.Contexts.SingleOrDefault(c => c.Front.Id == result.Items[i].FrontId) })] };
        snapshot = snapshot with { Draft = draft, Calculation = QuoteCalculator.Calculate(draft, snapshot.Policy, snapshot.Client.Commercial) };
        await Bump(c, tx, h, request.Version, Now, ct);
        await c.ExecuteAsync(new CommandDefinition("UPDATE ORCAMENTOREVISAO SET DOCUMENTO=@json WHERE CODORCAMENTO=@id AND NUMERO=@revision", new { id, revision = h.Revision, json = QuoteTextBlob.Json(snapshot) }, tx, cancellationToken: ct));
        await WriteItems(c, tx, id, h.Revision, snapshot, ct);
        await c.ExecuteAsync(new CommandDefinition("UPDATE OR INSERT INTO ORCAMENTO_USUARIO(CODORCAMENTO,REVISAO,CODUSUARIO) VALUES(@id,@revision,@actor) MATCHING(CODORCAMENTO,REVISAO,CODUSUARIO)", new { id, revision = h.Revision, actor = actor.Id }, tx, cancellationToken: ct));
        if (await c.ExecuteAsync(new CommandDefinition("UPDATE ORCAMENTOANALISE SET STATUS='aplicada' WHERE CODORCAMENTOANALISE=@job AND STATUS='concluida'", new { job }, tx, cancellationToken: ct)) != 1) throw new OrcamentoException(409, "Análise alterada por outro usuário.");
        await Event(c, tx, h, "aplicar_ia", h.Status, actor, null, ct); await tx.CommitAsync(ct);
    }
    private static QuoteAiMetadata Metadata(QuoteAiInput input) => new(input.Briefing, input.CatalogVersion, input.Catalog is null ? null : input.Catalog with { Rules = [] }, input.Technologies, input.Contexts);
    private static void CheckAiContextLimit(QuoteAiInput input)
    {
        var taxonomy = input.Phase == "classificacao" && input.Catalog is not null ? OrcamentoJson.Write(input.Catalog with { Rules = [] }) + OrcamentoJson.Write(input.Technologies) : OrcamentoJson.Write(input.Contexts);
        if (input.Title.Length + input.Request.Length + input.ExistingScope.Length + (input.Briefing?.Length ?? 0) + input.Sources.Sum(s => s.Text.Length) + taxonomy.Length > 50_000)
            throw new OrcamentoException(422, "Briefing, documentos e regras excedem 50 mil caracteres. Revise o contexto; nada foi truncado.");
    }
    public async Task<string> ConfirmClassification(long id, long job, QuoteAiConfirmation request, OrcamentoAiOptions options, CancellationToken ct)
    {
        if (!options.Enabled) throw new OrcamentoException(503, "Análise por IA desabilitada neste ambiente.");
        if (!request.ConfirmExternalProcessing) throw new OrcamentoException(422, "Confirme a segunda chamada ao fornecedor.");
        await using var c = await db.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct); var actor = await Actor(c, tx, ct); var h = await ReadHead(c, id, tx, ct);
        var source = await c.QuerySingleOrDefaultAsync<AiRow>(new CommandDefinition(AiSql + " WHERE CODORCAMENTOANALISE=@job AND CODORCAMENTO=@id", new { id, job }, tx, cancellationToken: ct)) ?? throw new OrcamentoException(404, "Classificação não encontrada.");
        if (source.Phase != "classificacao" || source.Status != "concluida" || source.Revision != h.Revision || source.Version != h.Version || !OrcamentoWorkflow.Editable(h.Status)) throw new OrcamentoException(409, "Classificação superada ou já confirmada. Solicite nova classificação.");
        var input = OrcamentoJson.Read<QuoteAiInput>(source.Input);
        if (input.Catalog is null || input.CatalogVersion is null) throw new OrcamentoException(422, "Classificação sem catálogo.");
        QuoteTechnicalRules.ValidateFronts(request.Fronts, input.Catalog, input.Technologies, confirmed: true);
        var contexts = QuoteTechnicalRules.Match(input.CatalogVersion, request.Fronts, input.Catalog, input.Technologies);
        var generic = contexts.Any(context => !context.Rules.Any(rule => rule.TechnologyId.HasValue || rule.ProfileId is not null || rule.ServiceIds.Count > 0));
        if (generic && !request.AcceptGenericAnalysis) throw new OrcamentoException(422, "Há frentes sem regras específicas publicadas. Confirme explicitamente a análise genérica.");
        input = input with { Phase = "analise", Contexts = contexts, Catalog = null };
        CheckAiContextLimit(input); await Bump(c, tx, h, request.Version, Now, ct);
        if (await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ORCAMENTOANALISE WHERE CODORCAMENTO=@id AND STATUS IN ('pendente','executando')", new { id }, tx, cancellationToken: ct)) > 0) throw new OrcamentoException(409, "Já existe processamento ativo.");
        if (await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ORCAMENTOANALISE WHERE CODORCAMENTO=@id", new { id }, tx, cancellationToken: ct)) >= 50) throw new OrcamentoException(422, "Limite de análises atingido.");
        var child = await c.ExecuteScalarAsync<long>(new CommandDefinition("INSERT INTO ORCAMENTOANALISE(CODORCAMENTO,REVISAO,VERSAOORCAMENTO,STATUS,ENTRADA,MODELO,CODUSUARIO,DTHRCRIACAO,ETAPA,CODANALISEORIGEM,CONTEXTO) VALUES(@id,@revision,@version,'pendente',@input,@model,@actor,@now,'analise',@job,@context) RETURNING CODORCAMENTOANALISE",
            new { id, revision = h.Revision, version = h.Version + 1, input = QuoteTextBlob.Json(input), model = options.Model, actor = actor.Id, now = Now, job, context = QuoteTextBlob.Json(Metadata(input)) }, tx, cancellationToken: ct));
        await Event(c, tx, h, "confirmar_classificacao", h.Status, actor, null, ct); await tx.CommitAsync(ct); return child.ToString(CultureInfo.InvariantCulture);
    }
}
