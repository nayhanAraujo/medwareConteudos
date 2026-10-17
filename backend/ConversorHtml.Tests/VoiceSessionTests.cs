using ConversorHtml.Application.Services;
using ConversorHtml.Application.Dtos;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Domain.Enums;
using ConversorHtml.Domain.Models;
using MdwConteudos.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
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

    [Theory]
    [InlineData(VoiceUtteranceIntent.Build)]
    [InlineData(VoiceUtteranceIntent.Edit)]
    public async Task UnrecognizedEdit_PreservesExistingModel(VoiceUtteranceIntent intent)
    {
        var service = new MockVoiceLaudoService();
        var session = EmptySession();
        var initial = await service.ApplyUtteranceAsync(session, "altura em cm peso em kg");
        session.CamposScriptJson = initial.CamposScriptJson;

        var result = await service.ApplyUtteranceAsync(session, "troque a ordem do peso e altura", intent);

        Assert.Equal(initial.CamposScriptJson, result.CamposScriptJson);
        Assert.NotEmpty(result.Warnings);
    }

    [Theory]
    [InlineData("{\"camposScript\": []}")]
    [InlineData("{\"camposScript\": null}")]
    [InlineData("{\"camposScript\": {}}")]
    [InlineData("[]")]
    [InlineData("invalid json")]
    public async Task InvalidAgentResponse_DoesNotOverwriteStoredModel(string response)
    {
        var (controller, session, _) = ControllerWithModel(response);
        var previous = session.CamposScriptJson;
        var result = await controller.ApplyUtterance(session.Id, new VoiceUtteranceRequestDto { Transcript = "adicione o ventrículo direito" }, default);

        Assert.Equal(502, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        Assert.Equal(previous, session.CamposScriptJson);
        Assert.Empty(session.TranscriptHistory);
        var retrieved = Assert.IsType<VoiceSessionResponseDto>(Assert.IsType<OkObjectResult>(controller.GetSession(session.Id).Result).Value);
        Assert.Equal(previous, retrieved.CamposScriptJson);
    }

    [Fact]
    public async Task BuildOnExistingSession_IsSentAsEdit()
    {
        const string updated = "{\"camposScript\":[{\"nome\":\"PESO\"},{\"nome\":\"ALTURA\"}]}";
        var (controller, session, service) = ControllerWithModel(updated);
        var result = await controller.ApplyUtterance(session.Id, new VoiceUtteranceRequestDto { Transcript = "troque a ordem", Intent = VoiceUtteranceIntent.Build }, default);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(VoiceUtteranceIntent.Edit, service.ReceivedIntent);
        Assert.Equal(updated, session.CamposScriptJson);
    }

    private static (VoiceSessionsController, VoiceSession, StubVoiceService) ControllerWithModel(string response)
    {
        var store = new InMemoryVoiceSessionStore();
        var session = new VoiceSession
        {
            Id = Guid.NewGuid(), OwnerUserId = "1", ExpiresAt = DateTime.UtcNow.AddHours(1),
            CamposScriptJson = "{\"camposScript\":[{\"nome\":\"ALTURA\"},{\"nome\":\"PESO\"}]}"
        };
        store.Create(session);
        var service = new StubVoiceService(response);
        var controller = new VoiceSessionsController(store, service, null!, null!, null!,
            new ConfigurationBuilder().Build(), NullLogger<VoiceSessionsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "Bearer"))
                }
            }
        };
        return (controller, session, service);
    }

    private sealed class StubVoiceService(string response) : IVoiceLaudoService
    {
        public VoiceUtteranceIntent ReceivedIntent { get; private set; }
        public Task<string> BootstrapFromImageAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<VoiceExportResult> ExportAsync(VoiceSession session, ConversionOutputFormat format, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<VoiceApplyResult> ApplyUtteranceAsync(VoiceSession session, string transcript, VoiceUtteranceIntent intent = VoiceUtteranceIntent.Build, CancellationToken cancellationToken = default)
        {
            ReceivedIntent = intent;
            return Task.FromResult(new VoiceApplyResult(response, "Alterações aplicadas.", []));
        }
    }
}
