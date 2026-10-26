using System.Text.Json;
using System.Text.Json.Serialization;

namespace MdwConteudos.Api.Modules.Orcamentos;

public sealed class OrcamentoAiOptions
{
    public bool Enabled { get; set; }
    public string NodePath { get; set; } = "node";
    public string BridgePath { get; set; } = "quote-ai-bridge/analyze.mjs";
    public string Model { get; set; } = "composer-2.5";
    public int TimeoutSeconds { get; set; } = 180;
}
public sealed record QuoteAiRequest(string Version, List<string> AttachmentIds, bool ConfirmExternalProcessing, string? Briefing = null);
public sealed record QuoteAiConfirmation(string Version, List<QuoteFront> Fronts, bool ConfirmExternalProcessing, bool AcceptGenericAnalysis);
public sealed record QuoteAiMetadata(string? Briefing, string? CatalogVersion, QuoteTechnicalCatalog? Catalog, List<QuoteTechnology> Technologies, List<QuoteTechnicalContext> Contexts);
public sealed record QuoteAiApply(string Version, List<int> Items);
public sealed record QuoteAiImage(string Name, string MimeType, string Data);
public sealed record QuoteAiInput(string Title, string Request, string ExistingScope, List<QuoteAiSource> Sources, List<QuoteAiImage> Images)
{
    public string Phase { get; init; } = "analise";
    public string? Briefing { get; init; }
    public string? CatalogVersion { get; init; }
    public QuoteTechnicalCatalog? Catalog { get; init; }
    public List<QuoteTechnology> Technologies { get; init; } = [];
    public List<QuoteTechnicalContext> Contexts { get; init; } = [];
}
public sealed record QuoteAiSource(string Name, string Text, string Sha256);
public sealed record QuoteAiItem(string Category, string Description, decimal Quantity, string Complexity, string Reason, QuoteHours Hours, string Dependencies, string Pending, bool BlockingPending)
{
    public string? FrontId { get; init; }
}
public sealed record QuoteAiSuggestion(string Summary, List<QuoteAiItem> Items, List<string> Warnings)
{
    public QuoteClassification? Classification { get; init; }
    public static QuoteAiSuggestion Parse(string json)
    {
        if (json.Length > 100_000) throw new OrcamentoException(422, "Resposta de IA excedeu o limite.");
        QuoteAiSuggestion result;
        try { result = JsonSerializer.Deserialize<QuoteAiSuggestion>(json, new JsonSerializerOptions(OrcamentoJson.Options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })!; }
        catch (JsonException) { throw new OrcamentoException(422, "A IA retornou uma estrutura inválida. Nenhum item foi aplicado."); }
        if (result is null || result.Items is null || result.Warnings is null || result.Items.Count > 100 || result.Items.Count == 0 && result.Classification is null || result.Warnings.Count > 30) throw new OrcamentoException(422, "Resposta de IA incompleta.");
        QuoteCalculator.CheckText(result.Summary, "Resumo da IA");
        foreach (var warning in result.Warnings) QuoteCalculator.CheckText(warning, "Aviso da IA", 2000);
        foreach (var item in result.Items)
        {
            if (item is null || item.Hours is null) throw new OrcamentoException(422, "Item da IA inválido.");
            QuoteCalculator.CheckText(item.Reason, "Justificativa da IA");
            if (string.IsNullOrWhiteSpace(item.Description) || string.IsNullOrWhiteSpace(item.Reason)) throw new OrcamentoException(422, "Item da IA sem descrição ou justificativa.");
            QuoteCalculator.Calculate(new() { Items = [ToItem(item)] }, new() { HourlyRate = 1 }, new());
        }
        return result;
    }
    public static QuoteAiSuggestion ValidateForInput(QuoteAiSuggestion result, QuoteAiInput input)
    {
        result = Parse(OrcamentoJson.Write(result));
        if (input.Phase == "classificacao")
        {
            if (result.Items.Count != 0 || result.Classification is null || input.Catalog is null) throw new OrcamentoException(422, "A primeira fase deve retornar somente classificação, sem estimativas de horas.");
            _ = QuoteClassification.Parse(OrcamentoJson.Write(result.Classification), input.Catalog, input.Technologies);
        }
        else
        {
            if (result.Classification is not null || result.Items.Count == 0 || input.Contexts.Count > 0 && result.Items.Any(i => !input.Contexts.Any(c => c.Front.Id == i.FrontId))) throw new OrcamentoException(422, "Resultado técnico sem vínculo com as frentes confirmadas.");
        }
        return result;
    }
    public static QuoteItem ToItem(QuoteAiItem item) => new() { Category = item.Category, Description = item.Description, Quantity = item.Quantity, Complexity = item.Complexity,
        ComplexityReason = item.Reason, Hours = item.Hours, Dependencies = item.Dependencies, Pending = item.Pending, BlockingPending = item.BlockingPending, Mode = "horas" };
}
public interface IQuoteAiProvider
{
    Task<QuoteAiSuggestion> Analyze(QuoteAiInput input, CancellationToken ct);
}
