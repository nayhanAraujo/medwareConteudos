using System.Text.Json;
using System.Text.Json.Serialization;

namespace MdwConteudos.Api.Modules.Orcamentos;

public sealed record QuoteNamedCode(string Id, string Name, bool Active = true);
public sealed record QuoteTechnology(int Id, string Name);
public sealed record QuoteRuleCheck(string Id, string Label, bool Required);
public sealed record QuoteTechnicalRule(string Id, string Name, bool Active, string? Category, int? TechnologyId, string? ProfileId, List<string> ServiceIds, string Instructions, List<QuoteRuleCheck> Checks);
public sealed record QuoteTechnicalCatalog(List<QuoteNamedCode> Profiles, List<QuoteNamedCode> Services, List<QuoteTechnicalRule> Rules)
{
    public static QuoteTechnicalCatalog Initial => new(
        [new("geral", "Geral"), new("ecocardiograma", "Ecocardiograma")],
        [new("layout", "Ajuste de layout"), new("incluir-campos", "Inclusão de campos"), new("remover-campos", "Remoção de campos"), new("normalidades", "Atualização de normalidades")],
        [new("impressao", "Conferência de layout e impressão", false, "laudos", null, null, ["layout"], "Conferir impacto na visualização e impressão, sem pressupor reconstrução completa do modelo.", [new("validar-impressao", "Validar impacto na impressão", true)]),
         new("dependencias", "Dependências de campos", false, "laudos", null, null, ["incluir-campos", "remover-campos"], "Identificar fórmulas, textos e validações que dependem dos campos alterados.", [new("conferir-dependencias", "Conferir dependências dos campos", true)]),
         new("referencia-normalidades", "Referência para normalidades", false, "laudos", null, "ecocardiograma", ["normalidades"], "Solicitar a referência e sua versão fornecidas pelo cliente/equipe. Não inventar limites clínicos ou diretrizes.", [new("confirmar-referencia", "Confirmar referência e versão para atualização das normalidades", true)])]);
}
public sealed record QuoteCatalogPublication(string Version, string Reason, QuoteTechnicalCatalog Catalog);
public sealed record QuoteFront(string Id, string Category, int? TechnologyId, string? ProfileId, List<string> ServiceIds, string Description);
public sealed record QuoteClassification(string Summary, List<QuoteFront> Fronts, List<string> Warnings)
{
    public static QuoteClassification Parse(string json, QuoteTechnicalCatalog catalog, List<QuoteTechnology> technologies)
    {
        QuoteClassification? result;
        try { result = JsonSerializer.Deserialize<QuoteClassification>(json, new JsonSerializerOptions(OrcamentoJson.Options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow }); }
        catch (JsonException) { throw new OrcamentoException(422, "Classificação da IA inválida."); }
        if (json.Length > 100_000 || result is null || result.Fronts is null || result.Fronts.Count is < 1 or > 30 || result.Warnings is null || result.Warnings.Count > 30) throw new OrcamentoException(422, "Classificação incompleta.");
        QuoteCalculator.CheckText(result.Summary, "Resumo"); foreach (var warning in result.Warnings) QuoteCalculator.CheckText(warning, "Aviso", 2000);
        QuoteTechnicalRules.ValidateFronts(result.Fronts, catalog, technologies, confirmed: false);
        return result;
    }
}
public sealed record QuoteTechnicalContext(string CatalogVersion, QuoteFront Front, string? TechnologyName, string ProfileName, List<QuoteTechnicalRule> Rules);
public sealed record QuoteCheckAnswer(string Id, string Status = "pendente", string Reason = "", int? ActorId = null, string? ResolvedAtUtc = null);
public sealed record QuotePendingResolution(string Reason, int? ActorId = null, string? ResolvedAtUtc = null);
public sealed record QuoteIssue(string Code, string Severity, int? ItemIndex, string Message, string Effect, string Field, string Action, int Step);

public static class QuoteTechnicalRules
{
    public static void Code(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 60 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) throw new OrcamentoException(422, "Código técnico inválido; use letras, números e hífen.");
    }
    public static void Validate(QuoteTechnicalCatalog catalog, List<QuoteTechnology> technologies)
    {
        if (catalog is null || catalog.Profiles is null || catalog.Services is null || catalog.Rules is null || catalog.Profiles.Count > 100 || catalog.Services.Count > 100 || catalog.Rules.Count > 100) throw new OrcamentoException(422, "Catálogo técnico inválido.");
        foreach (var entries in new[] { catalog.Profiles, catalog.Services })
        {
            if (entries.Any(x => x is null) || entries.Select(x => x.Id).Distinct().Count() != entries.Count) throw new OrcamentoException(422, "Códigos técnicos duplicados.");
            foreach (var entry in entries) { Code(entry.Id); QuoteCalculator.CheckText(entry.Name, "Nome técnico", 200); if (string.IsNullOrWhiteSpace(entry.Name)) throw new OrcamentoException(422, "Informe o nome técnico."); }
        }
        if (catalog.Rules.Any(r => r is null) || catalog.Rules.Select(r => r.Id).Distinct().Count() != catalog.Rules.Count) throw new OrcamentoException(422, "Regras técnicas duplicadas.");
        foreach (var rule in catalog.Rules)
        {
            Code(rule.Id); QuoteCalculator.CheckText(rule.Name, "Regra", 200); QuoteCalculator.CheckText(rule.Instructions, "Orientações", 4000);
            if (string.IsNullOrWhiteSpace(rule.Name) || string.IsNullOrWhiteSpace(rule.Instructions) || rule.ServiceIds is null || rule.Checks is null || rule.Checks.Count > 30 || rule.Checks.Any(c => c is null) || rule.Checks.Select(c => c.Id).Distinct().Count() != rule.Checks.Count
                || rule.Category is not null && !QuoteCalculator.Categories.Contains(rule.Category) || rule.TechnologyId.HasValue && !technologies.Any(t => t.Id == rule.TechnologyId)
                || rule.ProfileId is not null && !catalog.Profiles.Any(p => p.Id == rule.ProfileId) || rule.ServiceIds.Any(id => !catalog.Services.Any(s => s.Id == id))) throw new OrcamentoException(422, "Aplicabilidade ou checklist da regra inválido.");
            foreach (var check in rule.Checks) { Code(check.Id); QuoteCalculator.CheckText(check.Label, "Verificação", 500); if (string.IsNullOrWhiteSpace(check.Label)) throw new OrcamentoException(422, "Informe a verificação."); }
        }
    }
    public static void ValidateFronts(List<QuoteFront> fronts, QuoteTechnicalCatalog catalog, List<QuoteTechnology> technologies, bool confirmed)
    {
        if (fronts is null || fronts.Count is < 1 or > 30 || fronts.Any(f => f is null) || fronts.Select(f => f.Id).Distinct().Count() != fronts.Count) throw new OrcamentoException(422, "Informe de 1 a 30 frentes distintas.");
        foreach (var front in fronts)
        {
            Code(front.Id); QuoteCalculator.CheckText(front.Description, "Descrição da frente", 2000);
            if (!QuoteCalculator.Categories.Contains(front.Category) || front.ServiceIds is null || front.ServiceIds.Distinct().Count() != front.ServiceIds.Count
                || front.TechnologyId.HasValue && !technologies.Any(t => t.Id == front.TechnologyId) || front.ProfileId is not null && !catalog.Profiles.Any(p => p.Active && p.Id == front.ProfileId)
                || front.ServiceIds.Any(id => !catalog.Services.Any(s => s.Active && s.Id == id))) throw new OrcamentoException(422, "Classificação contém códigos inexistentes ou inativos.");
            if (confirmed && (string.IsNullOrWhiteSpace(front.Description) || front.Category == "laudos" && (!front.TechnologyId.HasValue || front.ProfileId is null || front.ServiceIds.Count == 0))) throw new OrcamentoException(422, "Confirme tecnologia, perfil e serviços das frentes de laudo.");
        }
    }
    public static List<QuoteTechnicalContext> Match(string version, List<QuoteFront> fronts, QuoteTechnicalCatalog catalog, List<QuoteTechnology> technologies) => fronts.Select(front => new QuoteTechnicalContext(version, front,
        technologies.SingleOrDefault(t => t.Id == front.TechnologyId)?.Name, catalog.Profiles.SingleOrDefault(p => p.Id == front.ProfileId)?.Name ?? "Não informado",
        catalog.Rules.Where(r => r.Active && (r.Category is null || r.Category == front.Category) && (!r.TechnologyId.HasValue || r.TechnologyId == front.TechnologyId)
            && (r.ProfileId is null || r.ProfileId == front.ProfileId) && (r.ServiceIds.Count == 0 || r.ServiceIds.Intersect(front.ServiceIds).Any())).OrderBy(r => r.Id, StringComparer.Ordinal).ToList())).ToList();
}
