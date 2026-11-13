using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Application.Dtos;
using ConversorHtml.Application.Services;
using ConversorHtml.Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MdwConteudos.Api.Controllers;
using MdwConteudos.Api.Modules.FormulasModelos;
using MdwConteudos.Api.Modules.StudioDrafts;

namespace ConversorHtml.Tests;

public class StudioJsonImportTests
{
    private static readonly VariavelOpcao[] Catalog = [new(1, "Peso", "PESO", null, "do banco", "kg"), new(2, "Calculada", "CALC", "<<VR_PESO>> * 2", null, Dependencias: [1])];
    private const string Minimal = """{"unknownRoot":{"version":"1"},"camposScript":[{"id":1,"tipo":"etiqueta","nome":"A","descricao":"Seção A","coluna":1,"ordem":200},{"id":2,"tipo":"etiqueta","nome":"B","descricao":"Seção B","coluna":2,"ordem":201},{"id":3,"tipo":"numero","nome":"PESO","descricao":"Peso original","medida":"kg","etiquetaPai":"A","coluna":1,"ordem":"1","funcao":"","referenciaNormalidade":[{"valorMax":"123"}],"custom":true},{"id":4,"nome":"HELPER","coluna":-1,"funcao":"const n = 1;"}]}""";
    private static StudioDocument Read(string json = Minimal) => StudioJsonImport.Parse(Encoding.UTF8.GetBytes(json), "Modelo.json", Catalog);
    private static DraftCommand Command(string type, object payload) => new(Guid.NewGuid().ToString(), 0, type, JsonSerializer.SerializeToElement(payload));
    private static bool Equivalent(string a, string b) => JsonNode.DeepEquals(JsonNode.Parse(a), JsonNode.Parse(b));
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void ReviewsExpireAfter15MinutesAndRespectThePerOwnerLimit()
    {
        var clock = new Clock(); var reviews = new StudioJsonReviews(clock); var id = reviews.Add(1, Read());
        for (var i = 1; i < 8; i++) reviews.Add(1, Read());
        Assert.Equal(429, Assert.Throws<StudioException>(() => reviews.Add(1, Read())).Status);
        clock.Now = clock.Now.AddMinutes(15);
        Assert.Equal(404, Assert.Throws<StudioException>(() => reviews.Get(id, 1)).Status);
        reviews.Add(1, Read());
    }

    [Fact]
    public void ReferenceModelPreservesAll52ElementsAndTheirProperties()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Studio", "ecocardiograma.json"));
        var doc = StudioJsonImport.Parse(bytes, "Ecodopplercardiograma.json", []);
        Assert.Equal(5, doc.Secoes.Count);
        Assert.Equal(42, doc.Secoes.Sum(s => s.Itens.Count));
        Assert.Equal(5, doc.ImportedJson!.Components.Count);
        Assert.Equal(52, StudioJsonImport.Elements(doc).Count());
        Assert.True(Equivalent(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'), StudioJsonImport.Export(doc)));
        foreach (var section in doc.Secoes)
            Assert.All(section.Itens, item => Assert.Equal(StudioJsonImport.Text(section.Imported!.Properties, "nome"), StudioJsonImport.Text(item.Imported!.Properties, "etiquetaPai")));
        var recovered = JsonSerializer.Deserialize<StudioDocument>(JsonSerializer.Serialize(doc, StudioDocumentEditor.Json), StudioDocumentEditor.Json)!;
        Assert.True(Equivalent(StudioJsonImport.Export(doc), StudioJsonImport.Export(recovered)));
    }

    [Fact]
    public void InterleavedColumnsUseParentTokensAndPreserveStringOrdersAndUnknownProperties()
    {
        var doc = Read();
        Assert.Single(doc.Secoes[0].Itens); Assert.Empty(doc.Secoes[1].Itens);
        Assert.Equal("1", doc.Secoes[0].Itens[0].Imported!.Properties["ordem"]!.GetValue<string>());
        Assert.True(Equivalent(Minimal, StudioJsonImport.Export(doc)));
        Assert.True(Equivalent(Minimal, StudioJsonImport.Export(StudioJsonImport.Parse(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Minimal)).ToArray(), "Bom.JSON", Catalog))));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"camposScript\":{}}")]
    [InlineData("{\"camposScript\":[null]}")]
    [InlineData("{\"camposScript\":[{\"id\":1},{\"id\":1}]}")]
    [InlineData("{\"camposScript\":[],\"camposScript\":[]}")]
    [InlineData("{\"camposScript\":[{\"tipo\":\"etiqueta\",\"nome\":\"A\"},{\"tipo\":\"etiqueta\",\"nome\":\"A\"}]}")]
    [InlineData("{\"camposScript\":[{\"tipo\":\"numero\",\"etiquetaPai\":\"NAO_EXISTE\"}]}")]
    public void InvalidStructureIsRejected(string json) => Assert.ThrowsAny<Exception>(() => Read(json));

    [Fact]
    public void RejectsWrongEncodingSizeAndExtension()
    {
        Assert.Throws<StudioException>(() => StudioJsonImport.Parse([0xff, 0xfe, 0x00], "x.json", Catalog));
        Assert.Throws<StudioException>(() => StudioJsonImport.Parse(new byte[StudioJsonImport.MaxBytes + 1], "x.json", Catalog));
        Assert.Throws<StudioException>(() => StudioJsonImport.Parse(Encoding.UTF8.GetBytes(Minimal), "x.txt", Catalog));
    }

    [Fact]
    public void RenameAndMoveChangeOnlyStructuralPropertiesAndUndoRestoresOriginal()
    {
        var before = Read(); var section = before.Secoes[0]; var item = section.Itens[0];
        var renamed = StudioDocumentEditor.Apply(before, Command("editSection", new {sectionId = section.Id, name = "Nome novo", column = 1, position = 0}), Catalog);
        Assert.Equal("A", StudioJsonImport.Text(renamed.Secoes[0].Imported!.Properties, "nome"));
        var moved = StudioDocumentEditor.Apply(renamed, Command("moveItem", new {itemId = item.Id, sectionId = before.Secoes[1].Id, position = 0}), Catalog);
        var raw = moved.Secoes[1].Itens[0].Imported!.Properties;
        Assert.Equal("B", StudioJsonImport.Text(raw, "etiquetaPai"));
        Assert.Equal(2, StudioJsonImport.Number(raw, "coluna"));
        Assert.Equal("0", raw["ordem"]!.GetValue<string>());
        foreach (var property in new[]{"id", "nome", "funcao", "referenciaNormalidade", "custom"})
            Assert.True(JsonNode.DeepEquals(item.Imported!.Properties[property], raw[property]));
        Assert.True(Equivalent(Minimal, StudioJsonImport.Export(StudioDocumentEditor.Reverse(renamed, before, renamed))));
        Assert.True(Equivalent(Minimal, StudioJsonImport.Export(before)));
        Assert.Throws<StudioException>(() => StudioDocumentEditor.Composition(moved));
        Assert.Throws<StudioException>(() => StudioDocumentEditor.Apply(moved, Command("editItem", new {itemId = item.Id, normality = "nova"}), Catalog));
    }

    [Fact]
    public void ExistingImportedDependencyIsNotReplacedByCurrentCatalog()
    {
        var before = Read();
        var updated = StudioDocumentEditor.Apply(before, Command("addMeasures", new {sectionId = before.Secoes[0].Id, ids = new[]{2}}), Catalog);
        Assert.Equal(2, updated.Secoes[0].Itens.Count);
        foreach (var property in before.Secoes[0].Itens[0].Imported!.Properties.Where(p => p.Key != "linha"))
            Assert.True(JsonNode.DeepEquals(property.Value, updated.Secoes[0].Itens[0].Imported!.Properties[property.Key]));
        using var exported = JsonDocument.Parse(StudioJsonImport.Export(updated));
        var calculated = Assert.Single(exported.RootElement.GetProperty("camposScript").EnumerateArray(),
            field => field.TryGetProperty("nome", out var name) && name.GetString() == "CALC");
        Assert.Equal("<<VR_PESO>> * 2", calculated.GetProperty("funcao").GetString());
    }

    [Fact]
    public void PreexistingProblemsAreWarningsButNewBrokenReferencesCannotBeIntroduced()
    {
        var doc = Read(Minimal.Replace("const n = 1;", "const n = <<VR_NAO_EXISTE>>; <<VR_PESO>>;"));
        Assert.Contains(StudioJsonImport.Problems(doc), p => p.Message.Contains("NAO_EXISTE"));
        var renamed = StudioDocumentEditor.Apply(doc, Command("rename", new {name = "Novo"}), Catalog);
        Assert.NotEmpty(StudioJsonImport.Export(renamed));
        Assert.Throws<MeasureInUseException>(() => StudioDocumentEditor.Apply(doc, Command("deleteItem", new {itemId = doc.Secoes[0].Itens[0].Id}), Catalog));
        Assert.Contains("const n", StudioJsonImport.Export(doc)); // Retained as data, never evaluated.
    }

    [Fact]
    public void ConventionalCommandsAreAtomicVersionedAndReversible()
    {
        var doc = Read(); var original = StudioJsonImport.Export(doc);
        var session = new VoiceSession { ImportedDocumentJson = JsonSerializer.Serialize(doc, StudioDocumentEditor.Json), CamposScriptJson = original };
        VoiceStructureCall Call(string name, object args) => new(name, JsonSerializer.SerializeToElement(args));
        var valid = Call("organizar_secao", new {secaoId = doc.Secoes[0].Id, nome = "Nova", coluna = 1, posicao = 0});
        Assert.Throws<StudioException>(() => ImportedVoiceEditor.Apply(session, new([valid, Call("executar_formula", new {})], "", []), 0, Catalog));
        Assert.Equal(0, session.ImportedRevision); Assert.True(Equivalent(original, session.CamposScriptJson));
        ImportedVoiceEditor.Apply(session, new([valid], "Renomeado", []), 0, Catalog);
        Assert.Equal(1, session.ImportedRevision);
        Assert.Throws<StudioException>(() => ImportedVoiceEditor.Apply(session, new([valid], "", []), 0, Catalog));
        ImportedVoiceEditor.Apply(session, new([Call("desfazer_alteracao", new {})], "", []), 1, Catalog);
        Assert.True(Equivalent(original, session.CamposScriptJson));
        ImportedVoiceEditor.Apply(session, new([Call("refazer_alteracao", new {})], "", []), 2, Catalog);
        Assert.Contains("Nova", session.CamposScriptJson);
    }

    [Fact]
    public async Task ReviewIsPrivateAndConfirmationDoesNotDuplicateSessionsOrPermitChangedPayload()
    {
        var reviews = new StudioJsonReviews(); var doc = Read(); var id = reviews.Add(1, doc);
        Assert.Throws<StudioException>(() => reviews.Get(id, 2));
        var proxy = System.Reflection.DispatchProxy.Create<IFormulasModelosService, RealtimeVoiceProtocolTests.ModelsProxy>();
        ((RealtimeVoiceProtocolTests.ModelsProxy)proxy).Query = _ => Task.FromResult<IReadOnlyList<VariavelOpcao>>(Catalog);
        var store = new InMemoryVoiceSessionStore();
        var controller = new StudioJsonImportController(reviews, proxy, null!, store, new ConfigurationBuilder().Build()) {
            ControllerContext = new ControllerContext {HttpContext = new DefaultHttpContext {User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "Bearer"))}}
        };
        var request = new JsonImportConfirmation("conventional", Guid.NewGuid().ToString(), "Modelo");
        var first = Assert.IsType<OkObjectResult>(await controller.Confirm(id, request, default));
        var again = Assert.IsType<OkObjectResult>(await controller.Confirm(id, request, default));
        Assert.Same(first.Value, again.Value);
        var exception = await Assert.ThrowsAsync<StudioException>(() => controller.Confirm(id, request with {Nome = "Outro"}, default));
        Assert.Equal(409, exception.Status);
    }

    [Fact]
    public async Task ConventionalExportUsesCanonicalJsonAndBlocksLossyFormats()
    {
        var doc = Read(); var store = new InMemoryVoiceSessionStore();
        var session = store.Create(new VoiceSession {Id = Guid.NewGuid(), OwnerUserId = "1", ImportedDocumentJson = JsonSerializer.Serialize(doc, StudioDocumentEditor.Json),
            CamposScriptJson = StudioJsonImport.Export(doc), ExpiresAt = DateTime.UtcNow.AddHours(1)});
        var controller = new VoiceSessionsController(store, null!, null!, null!, null!, new ConfigurationBuilder().Build(), NullLogger<VoiceSessionsController>.Instance) {
            ControllerContext = new ControllerContext {HttpContext = new DefaultHttpContext {User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "Bearer"))}}
        };
        Assert.IsType<BadRequestObjectResult>((await controller.Generate(session.Id, new VoiceGenerateRequestDto {Format = "html"}, default)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Generate(session.Id, new VoiceGenerateRequestDto {Format = "modoTexto"}, default)).Result);
        var result = Assert.IsType<OkObjectResult>((await controller.Generate(session.Id, new VoiceGenerateRequestDto {Format = "jsonStudio"}, default)).Result);
        var body = Assert.IsType<ConversionResponseDto>(result.Value);
        Assert.True(Equivalent(Minimal, body.Text!));
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "2")], "Bearer"));
        Assert.IsType<NotFoundObjectResult>((await controller.Generate(session.Id, new VoiceGenerateRequestDto {Format = "jsonStudio"}, default)).Result);
    }
}
