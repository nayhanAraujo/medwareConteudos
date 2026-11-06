namespace MdwConteudos.Api.Modules.StudioDrafts;

public sealed class StudioDraftOptions { public bool Enabled { get; set; } }
public sealed record StudioItem(string Id, int? CodVariavel, string? Descricao = null, bool ExibirGrafico = false, string? NormalidadeOverride = null);
public sealed record StudioSection(string Id, string Nome, int Coluna, List<StudioItem> Itens, int? CodSecao = null);
public sealed record StudioDocument(string Nome, List<StudioSection> Secoes, int? ModeloOrigem = null, long? VersaoOrigem = null);
public sealed record DraftView(string Id, long Revision, bool Archived, StudioDocument Document, DateTime UpdatedAt);
public sealed record DraftCommand(string CommandId, long ExpectedRevision, string Type, System.Text.Json.JsonElement Payload, string? Group = null, string Source = "manual");
public sealed record DraftCreate(string Nome, int? ModeloId = null, Guid? VoiceSessionId = null, Guid? PublicacaoId = null);
public sealed record PublicationRequest(long ExpectedRevision, int? ModeloId, string Nome);
public sealed record PublicationConfirmation(string ProposalId);
public sealed record ExportRequest(long Revision, string Format);
public sealed record DraftProblem(string Code, string Message, string? ItemId = null);
public sealed class StudioException(int status, string message) : Exception(message) { public int Status { get; } = status; }
