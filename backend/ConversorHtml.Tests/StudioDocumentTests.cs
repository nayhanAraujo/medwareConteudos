using System.Text.Json;
using MdwConteudos.Api.Modules.FormulasModelos;
using MdwConteudos.Api.Modules.StudioDrafts;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace ConversorHtml.Tests;

public class StudioDocumentTests
{
    private static readonly VariavelOpcao[] Catalog = [new(1, "Peso", "PESO", null, null, "kg"), new(2, "Superfície corporal", "SC", "formula_do_banco", "normalidade_do_banco", "m²", Dependencias: [1])];
    private static StudioDocument Empty() => new("Laudo", []);
    private static DraftCommand Command(string type, object payload) => new(Guid.NewGuid().ToString(), 0, type, JsonSerializer.SerializeToElement(payload));
    private static StudioDocument WithSection() => StudioDocumentEditor.Apply(Empty(), Command("createSection", new {name = "Dados", column = 1}), Catalog);

    [Fact]
    public void CatalogDependenciesAreOrderedAndMetadataIsNotFrozenInDocument()
    {
        var doc = WithSection();
        doc = StudioDocumentEditor.Apply(doc, Command("addMeasures", new {sectionId = doc.Secoes[0].Id, ids = new[]{2}}), Catalog);
        Assert.Equal(new int?[]{1,2}, doc.Secoes[0].Itens.Select(i => i.CodVariavel));
        var json = JsonSerializer.Serialize(doc, StudioDocumentEditor.Json);
        Assert.DoesNotContain("formula_do_banco", json); Assert.DoesNotContain("normalidade_do_banco", json);
    }

    [Fact]
    public void FailedCommandCannotMutateOriginalDocument()
    {
        var doc = WithSection(); var before = JsonSerializer.Serialize(doc);
        Assert.Throws<StudioException>(() => StudioDocumentEditor.Apply(doc, Command("addMeasures", new {sectionId = doc.Secoes[0].Id, ids = new[]{1,999}}), Catalog));
        Assert.Equal(before, JsonSerializer.Serialize(doc));
    }

    [Fact]
    public void UndoAndRedoPreserveUnrelatedLaterEdits()
    {
        var before = WithSection();
        var after = StudioDocumentEditor.Apply(before, Command("addMeasures", new {sectionId = before.Secoes[0].Id, ids = new[]{1}}), Catalog);
        var current = StudioDocumentEditor.Apply(after, Command("createSection", new {name = "Outro", column = 2}), Catalog);
        var reversed = StudioDocumentEditor.Reverse(current, before, after);
        Assert.Equal(2, reversed.Secoes.Count); Assert.Empty(reversed.Secoes[0].Itens);
        var redone = StudioDocumentEditor.Reverse(reversed, after, before);
        Assert.Single(redone.Secoes[0].Itens); Assert.Equal("Outro", redone.Secoes[1].Nome);
    }

    [Fact]
    public void UndoCannotRemoveSectionThatReceivedLaterItems()
    {
        var before = Empty(); var after = WithSection();
        var current = StudioDocumentEditor.Apply(after, Command("addMeasures", new {sectionId = after.Secoes[0].Id, ids = new[]{1}}), Catalog);
        Assert.Equal(409, Assert.Throws<StudioException>(() => StudioDocumentEditor.Reverse(current, before, after)).Status);
    }

    [Fact]
    public void UnlinkedImageFieldsArePreservedButBlockExportValidation()
    {
        var section = new StudioSection(Guid.NewGuid().ToString(), "Câmaras", 1, [new(Guid.NewGuid().ToString(), null, "Medida extraída")]);
        var doc = StudioDocumentEditor.Apply(Empty(), Command("importSections", new {sections = new[]{section}}), Catalog);
        Assert.Single(doc.Secoes[0].Itens);
        Assert.Contains(StudioDocumentEditor.Validate(doc, Catalog), p => p.Code == "unlinked");
    }

    [Fact]
    public void NormalityAndChartOverridesRemainSpecificToDraftAndPublication()
    {
        var doc = WithSection(); doc = StudioDocumentEditor.Apply(doc, Command("addMeasures", new {sectionId = doc.Secoes[0].Id, ids = new[]{1}}), Catalog);
        doc = StudioDocumentEditor.Apply(doc, Command("editItem", new {itemId = doc.Secoes[0].Itens[0].Id, normality = "M: 1 a 2", chart = true}), Catalog);
        var composition = StudioDocumentEditor.Composition(doc);
        Assert.Equal("M: 1 a 2", composition.Secoes![0].Variaveis![0].NormalidadeOverride);
        Assert.True(composition.Secoes[0].Variaveis![0].ExibirGrafico);
        Assert.Null(Catalog[0].Normalidade);
    }

    [Fact]
    public void ChoiceTicketCannotBeUsedForAnotherOwnerDraftOrCandidate()
    {
        var choices = new StudioChoices(new EphemeralDataProtectionProvider());
        var id = Guid.NewGuid().ToString(); var token = choices.Issue(id, 1, 5, [1,2]);
        Assert.Equal(5, choices.Validate(token, id, 1, 2));
        Assert.Throws<StudioException>(() => choices.Validate(token, id, 2, 2));
        Assert.Throws<StudioException>(() => choices.Validate(token, Guid.NewGuid().ToString(), 1, 2));
        Assert.Throws<StudioException>(() => choices.Validate(token, id, 1, 999));
    }
}
