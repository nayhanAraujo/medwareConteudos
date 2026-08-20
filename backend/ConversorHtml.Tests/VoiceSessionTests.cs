using ConversorHtml.Application.Services;
using ConversorHtml.Domain.Enums;
using ConversorHtml.Domain.Models;
using Xunit;

namespace ConversorHtml.Tests;

public class VoiceSessionTests
{
    private const string UserLongTranscript =
        "Eu quero um modelo de laudo que tem a sessão com os dados do paciente nessa sessão vai ter os campos altura peso e superfície corporal quero uma outra sessão com as câmaras esquerdas que vão ter a medida via seda do V em milímetro anel aust em milímetro a menor óptica indexada e a horta seios de valsava em milímetro com a normalidade 28.5 a 35.9";

    private static VoiceSession EmptySession() => new()
    {
        Id = Guid.NewGuid(),
        Mode = VoiceSessionMode.FromScratch,
        CamposScriptJson = "{\"camposScript\":[]}",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddHours(2)
    };

    [Fact]
    public async Task MockBuildFromSpeech_NaturalDescription_GeneratesFields()
    {
        var service = new MockVoiceLaudoService();
        var session = EmptySession();
        var result = await service.ApplyUtteranceAsync(
            session,
            "dados gerais altura em cm peso em kg anel aórtico em mm homem 19 a 23 mulher 17 a 21",
            VoiceUtteranceIntent.Build);

        Assert.Contains("Modelo criado", result.Summary);
        Assert.Contains("ALTURA", result.CamposScriptJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PESO", result.CamposScriptJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MockBuildFromSpeech_UserLongTranscript_GeneratesMultipleFields()
    {
        var service = new MockVoiceLaudoService();
        var session = EmptySession();
        var result = await service.ApplyUtteranceAsync(session, UserLongTranscript, VoiceUtteranceIntent.Build);

        Assert.Contains("Modelo criado", result.Summary);
        Assert.Contains("ALTURA", result.CamposScriptJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PESO", result.CamposScriptJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VSVE", result.CamposScriptJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ANEL_AORTICO", result.CamposScriptJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("28.5", result.CamposScriptJson);
    }

    [Fact]
    public async Task MockApplyUtterance_AddField_UpdatesCamposScript()
    {
        var service = new MockVoiceLaudoService();
        var session = EmptySession();

        var result = await service.ApplyUtteranceAsync(session, "adicionar altura em cm", VoiceUtteranceIntent.Edit);

        Assert.Contains("Altura", result.CamposScriptJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ALTURA", result.CamposScriptJson, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(result.Summary));
    }

    [Fact]
    public async Task MockApplyUtterance_RemoveField_RemovesFromJson()
    {
        var service = new MockVoiceLaudoService();
        var session = EmptySession();
        var add = await service.ApplyUtteranceAsync(session, "adicionar peso em kg", VoiceUtteranceIntent.Edit);
        session.CamposScriptJson = add.CamposScriptJson;

        var remove = await service.ApplyUtteranceAsync(session, "remover peso", VoiceUtteranceIntent.Edit);

        Assert.DoesNotContain("PESO", remove.CamposScriptJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Removido", remove.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MockBootstrapFromImage_ReturnsSampleModel()
    {
        var service = new MockVoiceLaudoService();
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var json = await service.BootstrapFromImageAsync(stream, "eco.png");

        Assert.Contains("camposScript", json);
        Assert.Contains("DADOS_GERAIS", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MockExport_Html_PassesValidator()
    {
        var service = new MockVoiceLaudoService();
        var session = EmptySession();
        var add = await service.ApplyUtteranceAsync(session, "adicionar anel aórtico em mm", VoiceUtteranceIntent.Edit);
        session.CamposScriptJson = add.CamposScriptJson;

        var exported = await service.ExportAsync(session, ConversionOutputFormat.Html);
        var validation = new LaudosUxHtmlValidator().Validate(exported.Content);

        Assert.True(validation.IsValid, string.Join(" | ", validation.Errors));
    }

    [Fact]
    public async Task MockExport_ModoTexto_PassesValidator()
    {
        var service = new MockVoiceLaudoService();
        var session = EmptySession();
        var add = await service.ApplyUtteranceAsync(session, "adicionar via de saída do VE em mm homem 10 a 21 mulher 10 a 19", VoiceUtteranceIntent.Edit);
        session.CamposScriptJson = add.CamposScriptJson;

        var exported = await service.ExportAsync(session, ConversionOutputFormat.ModoTexto);
        var validation = new LaudosUxModoTextoValidator().Validate(exported.Content);

        Assert.True(validation.IsValid, string.Join(" | ", validation.Errors));
    }

    [Fact]
    public async Task MockBuildFromSpeech_ExportsValidModoTexto()
    {
        var service = new MockVoiceLaudoService();
        var session = EmptySession();
        var build = await service.ApplyUtteranceAsync(session, UserLongTranscript, VoiceUtteranceIntent.Build);
        session.CamposScriptJson = build.CamposScriptJson;

        var exported = await service.ExportAsync(session, ConversionOutputFormat.ModoTexto);
        var validation = new LaudosUxModoTextoValidator().Validate(exported.Content);

        Assert.True(validation.IsValid, string.Join(" | ", validation.Errors));
        Assert.Contains("[", exported.Content);
    }
}
