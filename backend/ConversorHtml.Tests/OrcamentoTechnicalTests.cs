using MdwConteudos.Api.Modules.Orcamentos;

namespace ConversorHtml.Tests;

public sealed class OrcamentoTechnicalTests
{
    public static List<QuoteTechnology> Technologies => [new(8, "HTML"), new(15, "C#")];
    public static QuoteFront Front(string technology = "html") => new("frente-1", "laudos", technology == "html" ? 8 : 15, "ecocardiograma", ["incluir-campos"], "Adicionar medidas circuladas em vermelho");
    public static QuoteTechnicalCatalog Catalog() => QuoteTechnicalCatalog.Initial with { Rules = QuoteTechnicalCatalog.Initial.Rules.Select(r => r.Id == "dependencias" ? r with { Active = true } : r).ToList() };
    [Fact]
    public void MultipleIssuesDoNotOverwriteEachOtherAndWarningsDoNotBlockTotals()
    {
        var item = OrcamentoTests.Item() with { Mode = "horas", Hours = new(1), BlockingPending = true, Pending = "Confirmar campos circulados", DiscountPercent = 10 };
        var total = QuoteCalculator.Calculate(OrcamentoTests.Draft(item), new(), new());
        Assert.Null(total.Net); Assert.False(total.Complete);
        Assert.Contains(total.Items[0].Issues, i => i.Code == "preco-hora"); Assert.Contains(total.Items[0].Issues, i => i.Code == "pendencia-tecnica");
        var priced = QuoteCalculator.Calculate(OrcamentoTests.Draft(item), new() { HourlyRate = 100 }, new());
        Assert.Contains(priced.Items[0].Issues, i => i.Code == "justificativa-desconto");
        var warning = QuoteCalculator.Calculate(OrcamentoTests.Draft(OrcamentoTests.Item() with { Pending = "Conferir impressão", BlockingPending = false }), new(), new());
        Assert.True(warning.Complete); Assert.Contains(warning.Items[0].Issues, i => i.Severity == "aviso");
    }
    [Fact]
    public void RulesMatchMixedTechnologiesAndRejectUnknownCodes()
    {
        var catalog = Catalog() with { Rules = [.. Catalog().Rules, new("html", "Regra HTML", true, "laudos", 8, null, [], "Validar impressão HTML", [])] };
        var fronts = new List<QuoteFront> { Front(), Front("csharp") with { Id = "frente-2" } };
        QuoteTechnicalRules.Validate(catalog, Technologies); QuoteTechnicalRules.ValidateFronts(fronts, catalog, Technologies, true);
        var contexts = QuoteTechnicalRules.Match("7", fronts, catalog, Technologies);
        Assert.Equal(2, contexts.Count); Assert.Contains(contexts[0].Rules, r => r.Id == "html"); Assert.DoesNotContain(contexts[1].Rules, r => r.Id == "html");
        Assert.All(contexts, c => Assert.Contains(c.Rules, r => r.Id == "dependencias"));
        Assert.Throws<OrcamentoException>(() => QuoteTechnicalRules.ValidateFronts([Front() with { TechnologyId = 999 }], catalog, Technologies, true));
        QuoteTechnicalRules.ValidateFronts([Front() with { TechnologyId = null, ProfileId = null, ServiceIds = [] }], catalog, Technologies, false);
        Assert.Throws<OrcamentoException>(() => QuoteTechnicalRules.ValidateFronts([Front() with { TechnologyId = null }], catalog, Technologies, true));
    }
    [Fact]
    public void ResolvingTechnicalIssueRequiresReasonAndCannotSpoofActor()
    {
        var key = Guid.NewGuid().ToString("N"); var old = OrcamentoTests.Draft(OrcamentoTests.Item() with { Key = key, BlockingPending = true, Pending = "Confirmar escopo" });
        var item = old.Items[0] with { BlockingPending = false };
        Assert.Throws<OrcamentoException>(() => QuoteTechnicalEditing.Prepare(old with { Items = [item] }, old, 42, DateTime.UtcNow));
        var saved = QuoteTechnicalEditing.Prepare(old with { Items = [item with { PendingResolution = new("Cliente confirmou somente os campos circulados", 999, "inventado") }] }, old, 42, DateTime.UtcNow);
        Assert.Equal(42, saved.Items[0].PendingResolution!.ActorId); Assert.NotEqual("inventado", saved.Items[0].PendingResolution!.ResolvedAtUtc);
        Assert.True(QuoteCalculator.Calculate(saved, new(), new()).Complete);
    }
    [Fact]
    public void MandatoryChecklistCannotBeForgedOrBypassedWithoutJustification()
    {
        var context = QuoteTechnicalRules.Match("1", [Front()], Catalog(), Technologies).Single();
        var original = OrcamentoTests.Draft(OrcamentoTests.Item() with { Key = Guid.NewGuid().ToString("N"), TechnicalContext = context });
        Assert.False(QuoteCalculator.Calculate(original, new(), new()).Complete);
        var answer = new QuoteCheckAnswer("dependencias:conferir-dependencias", "confirmado", "", 999, "inventado");
        Assert.Throws<OrcamentoException>(() => QuoteTechnicalEditing.Prepare(original with { Items = [original.Items[0] with { CheckAnswers = [answer] }] }, original, 5, DateTime.UtcNow));
        var saved = QuoteTechnicalEditing.Prepare(original with { Items = [original.Items[0] with { CheckAnswers = [answer with { Reason = "Fórmulas e referências verificadas pela equipe" }] }] }, original, 5, DateTime.UtcNow);
        Assert.Equal(5, saved.Items[0].CheckAnswers[0].ActorId); Assert.True(QuoteCalculator.Calculate(saved, new(), new()).Complete);
        var revised = QuoteTechnicalEditing.Prepare(saved with { Items = [saved.Items[0] with { Description = "Novo escopo com dependências diferentes" }] }, saved, 6, DateTime.UtcNow);
        Assert.Equal("pendente", revised.Items[0].CheckAnswers[0].Status); Assert.False(QuoteCalculator.Calculate(revised, new(), new()).Complete);
        Assert.Throws<OrcamentoException>(() => QuoteTechnicalEditing.Prepare(saved with { Items = [saved.Items[0] with { TechnicalContext = context with { Rules = [] } }] }, saved, 5, DateTime.UtcNow));
    }
    [Fact]
    public void ClassificationPhaseRejectsHoursAndTechnicalPhaseRequiresConfirmedFront()
    {
        var input = new QuoteAiInput("Título", "Escopo", "", [], []) { Phase = "classificacao", Briefing = "Apenas campos circulados", Catalog = Catalog(), Technologies = Technologies };
        var result = new QuoteAiSuggestion("Identificado", [], []) { Classification = new("Frente", [Front()], []) };
        Assert.NotNull(QuoteAiSuggestion.ValidateForInput(result, input).Classification);
        Assert.Throws<OrcamentoException>(() => QuoteAiSuggestion.ValidateForInput(result with { Items = [OrcamentoAiTests.Suggestion().Items[0]] }, input));
        var technical = input with { Phase = "analise", Contexts = QuoteTechnicalRules.Match("1", [Front()], Catalog(), Technologies) };
        Assert.Throws<OrcamentoException>(() => QuoteAiSuggestion.ValidateForInput(OrcamentoAiTests.Suggestion(), technical));
        Assert.Single(QuoteAiSuggestion.ValidateForInput(OrcamentoAiTests.Suggestion() with { Items = [OrcamentoAiTests.Suggestion().Items[0] with { FrontId = "frente-1" }] }, technical).Items);
    }
}
