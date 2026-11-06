using System.Text.Json;
using MdwConteudos.Api.Modules.RealtimeVoice;
using MdwConteudos.Api.Modules.FormulasModelos;

namespace ConversorHtml.Tests;

public class RealtimeVoiceTests
{
    private static JsonElement Args(string json) => JsonSerializer.Deserialize<JsonElement>(json);
    private static readonly VariavelOpcao[] Catalog =
    [new(1, "Peso", "PESO", null, null, "kg"),
     new(2, "Superfície corporal", "SC", "<<VR_PESO>>", null, "m²", Dependencias: [1])];
    private static RealtimeDraft Draft()
    {
        var draft = new RealtimeDraft("{\"camposScript\":[]}");
        draft.Apply("criar_secao", Args("""{"grupoOperacao":"secao","nome":"CÂMARAS ESQUERDAS","coluna":1,"ordem":0}"""), Catalog);
        return draft;
    }

    [Fact]
    public void DependenciesAreInsertedBeforeCalculatedFieldAndUnicodeIsPreserved()
    {
        var draft = Draft();
        draft.Apply("incluir_medidas", Args("""{"grupoOperacao":"incluir","secaoId":1,"codVariaveis":[2]}"""), Catalog);
        using var json = JsonDocument.Parse(draft.Render());
        var fields = json.RootElement.GetProperty("camposScript");
        Assert.Equal("CÂMARAS ESQUERDAS", fields[0].GetProperty("descricao").GetString());
        Assert.Equal(1, fields[1].GetProperty("codVariavel").GetInt32());
        Assert.Equal("m²", fields[2].GetProperty("medida").GetString());
        Assert.Equal(2, draft.Composition("Laudo").Secoes![0].Variaveis!.Count);
    }

    [Fact]
    public void FailedEditDoesNotPartiallyMutateDraft()
    {
        var draft = Draft(); var previous = draft.Render(); var revision = draft.Revision;
        Assert.Throws<InvalidOperationException>(() => draft.Apply("incluir_medidas", Args("""{"grupoOperacao":"incluir","secaoId":1,"codVariaveis":[1,99]}"""), Catalog));
        Assert.Equal(previous, draft.Render()); Assert.Equal(revision, draft.Revision);
    }

    [Fact]
    public void CorrectionReplacesOnlyItsOwnOperation()
    {
        var draft = Draft();
        draft.Apply("criar_secao", Args("""{"grupoOperacao":"outra","nome":"Dados","coluna":2,"ordem":1}"""), Catalog);
        draft.Apply("criar_secao", Args("""{"grupoOperacao":"secao","substituirOperacao":true,"nome":"Átrio esquerdo","coluna":1,"ordem":0}"""), Catalog);
        using var json = JsonDocument.Parse(draft.Render());
        var fields = json.RootElement.GetProperty("camposScript");
        Assert.Equal(2, fields.GetArrayLength());
        Assert.Equal("Átrio esquerdo", fields[0].GetProperty("descricao").GetString());
        Assert.Equal("Dados", fields[1].GetProperty("descricao").GetString());
    }

    [Fact]
    public void UndoCannotOverwriteLaterEditOrOrphanFields()
    {
        var draft = Draft();
        draft.Apply("incluir_medidas", Args("""{"grupoOperacao":"incluir","secaoId":1,"codVariaveis":[1]}"""), Catalog);
        var previous = draft.Render();
        Assert.Throws<InvalidOperationException>(() => draft.Apply("desfazer_alteracao", Args("""{"grupoOperacao":"secao"}"""), Catalog));
        Assert.Equal(previous, draft.Render());
    }

    [Fact]
    public void DuplicateMeasuresAreRejected()
    {
        var draft = Draft(); var args = Args("""{"grupoOperacao":"incluir","secaoId":1,"codVariaveis":[1]}""");
        draft.Apply("incluir_medidas", args, Catalog);
        Assert.Throws<InvalidOperationException>(() => draft.Apply("incluir_medidas", args, Catalog));
    }

    [Theory]
    [InlineData("{\"modalities\":[\"audio\"],\"turn_detection\":{\"type\":\"server_vad\",\"silence_duration_ms\":400}}")]
    [InlineData("{\"modalities\":[\"text\"],\"turn_detection\":{\"type\":\"server_vad\"}}")]
    public void UnacknowledgedCapabilitiesBlockAudio(string json)
        => Assert.Throws<InvalidOperationException>(() => RealtimeVoiceSession.ValidateCapabilities(Args(json), 400));

    [Fact]
    public void AcknowledgedCapabilitiesAllowSession()
        => RealtimeVoiceSession.ValidateCapabilities(Args("""{"modalities":["text"],"turn_detection":{"type":"server_vad","silence_duration_ms":400}}"""), 400);

    [Fact]
    public void ModelCannotCallConfirmationOrSendArbitraryState()
    {
        Assert.Throws<InvalidOperationException>(() => RealtimeTools.Validate("ConfirmSave", Args("{}")));
        Assert.Throws<InvalidOperationException>(() => RealtimeTools.Validate("consultar_rascunho", Args("{\"sql\":\"DELETE\"}")));
    }
}
